# Provider HANDBACK — Wave-2 refusal-reason diagnostics (+ a triage analysis of your repro)

**Status:** DONE 2026-07-18, **committed `4a8b5d605`** (local, stacked on the pushed Wave-2 head),
**exe RESTAGED** — the staged `SwgClient_r.exe` carries the diagnostics now; re-run the smoke.
**From:** swg-client-v2 (provider) · **To:** Utinni (consumer) · **Contract:** unchanged, **v18 / 133**
**Responds to:** `2026-07-18-utinni-goalB-wave2-add-diagnostics-REQUEST.md`

## 1. What landed

Every fail-closed branch of `wsAddObject` / `wsAddNodeAt` / `wsRemoveNode` now emits ONE
`[editor.ws]` line into `SwgClient_report.log` naming the branch + the offending value, plus OK
lines on success. Permanent, as you asked. The vocabulary:

- `wsAddObject REFUSED (args | container-not-found | container-not-live | origin |
  template-fetch | pob-crc-extract | pob-open | pob-into-container | id-mint |
  createObject)` — `id-mint` prints the live floor/ceiling band; `createObject` prints the CEC
  code + name, with `-1 [unknown; -1=template-instantiate]` meaning the template's own
  `createObject()` failed (that path never sets the CEC).
- `wsAddObject OK: id=… cells=… template=…`
- `wsAddNodeAt REFUSED (args | id-band | id-present | buildout-id | live-object |
  container-missing)`, `…DATA re-added but child spawn refused: CEC=…`, `…OK`.
- `wsRemoveNode MISS | OCCUPIED (with the offending subtree id) | OK (subtree size)`.

No contract change, no version bump, no `.h/.inc` drift (sha256s from the Wave-2 HANDBACK still
govern). Gate: serial Release/Win32, 0 unresolved, boot-smoked, restaged.

## 2. Triage analysis of the 11:41 repro — read before re-running

We walked your exact inputs (`shared_armoire_s01.iff`, pos (0, 20.3, 0), container 0) through
`85877bae4` line by line. What it eliminates:

- **Your origin-guard reading is correct** — `magnitudeSquared` includes Y: 412 > 100 passes, in
  both our pre-check and `createObject`'s own guard. Not the branch.
- **`id-mint` is implausible as a total refusal.** A mid-band live id makes the allocator SKIP
  past it, not fail; refusing outright needs every one of the ~6.8M candidates above the naboo max
  occupied. The new `id-mint` line prints the band if we're wrong.
- **`createObject` for the armoire has no reachable refusal branch**: `objectAlreadyExists` is
  allocator-pre-checked, origin passes, no pob → no `mismatchedPobCrc`, and a template-instantiate
  failure logs the engine's own "unable to load template" WARNING — which your log grep shows
  never fired. That also argues **our `template-fetch` branch** (same `ObjectTemplateList::fetch`
  API) didn't fire either.

Which leaves a candidate on YOUR side of the boundary that fits every observation at once —
**the call may never be reaching the provider.** If the v18 rebind's slot for
`worldSnapshot::wsAddObject` is null (stale `.h/.inc` resync, name-key mismatch, or a resolve
short of 133/133), your facade's graceful-degradation path returns `id=0` with zero provider-side
evidence — exactly the silent x3 you logged. The new diagnostics make this decisive:

> **After this restage, a click that produces NO `[editor.ws]` line at all in
> `SwgClient_report.log` means the call never arrived** — check your injector's resolve count
> (must be 133/133 on v18) and that the `wsAddObject` slot is non-null before suspecting a
> pre-check. Any `[editor.ws] REFUSED` line names the real branch; timestamps line up with your
> consumer-side log as planned.

## 3. The hybrid-session flags you asked for (server login + editor scene on top)

Two real behavioral differences exist; **neither explains an armoire refusal**:

1. **POB-crc verification always runs on a server session.** `instantiateObject`'s crc check is
   gated `!Game::getSinglePlayer() || !worldSnapshotIgnorePobChanges` — connected to a server,
   `getSinglePlayer()` is false, so the ignore-flag cannot bypass it. Affects POB adds only (a
   `.pob` whose crc drifted from the snapshot value refuses with `mismatchedPobCrc`); irrelevant
   to furniture.
2. **`NetworkIdManager` holds server-streamed objects** in a live session. The allocator treats a
   mid-band live id as occupied and skips past (correct by design); `createObject` would refuse a
   collision the allocator missed — but the allocator checks the same map first. Cost, not
   refusal.

One more environment note while you're in that hybrid: server-streamed objects are NOT
client-cached, so `wsRemoveNode` on a POB the server has populated (vendors, NPCs inside) will
correctly return `-1` occupied — expect that in smoke and don't read it as a false positive.

## 4. Next

Re-run the mutation smoke on the restaged exe; send the paired logs (your consumer line + the
`[editor.ws]` line, or its absence) and we close the branch same-day. `4a8b5d605` is local —
Kenny pushes with his next batch; your smoke runs against the staged exe either way.
