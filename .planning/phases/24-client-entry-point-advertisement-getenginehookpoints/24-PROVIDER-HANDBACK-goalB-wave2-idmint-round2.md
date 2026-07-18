# Provider HANDBACK — id-mint round 2: every proposed mechanism REFUTED by measurement; discriminator staged

**Status:** DISCRIMINATOR ROUND (not yet a fix — read §1 for why that's the honest state),
**committed `d7dba07a6`**, **exe RESTAGED**. One click on the new exe names the mechanism.
**From:** swg-client-v2 (provider) · **To:** Utinni (consumer) · **Contract:** unchanged, v18 / 133
**Responds to:** `2026-07-18-utinni-goalB-wave2-idmint-fix-REQUEST.md`

## 1. Where the investigation stands — all three candidate mechanisms are dead

Your paired logs cleanly convicted the `id-mint` branch. We then measured every static input the
allocator consumes, and the results kill BOTH your mechanisms and our own follow-on hypothesis:

- **(a) seed explosion via buildout ids — impossible, twice over.** First: the v2 loader inserts
  every objid into `ms_buildoutObjects` unconditionally while the reader add is conditional, so a
  buildout id can never be in the reader without being in the set — the seed walk skips it either
  way. Second, and decisively: **naboo and tatooine have NO per-area buildout object tables in the
  client TREs at all** (we enumerated all 190 tres — only `areas_*.iff` lists plus kashyyyk/simple
  per-area tables exist). `loadOneBuildoutArea` opens nothing on your repro scenes; the buildout
  set is EMPTY there.
- **(a′) our variant — authored `.ws` ids above the ceiling dragging the seed out of the band:**
  we extracted and parsed **every copy of `snapshot/naboo.ws` (17) and `snapshot/tatooine.ws`
  (18)** in the install. Max ids: naboo 9,895,365 (`data_other_00.tre`; retail copies 9,895,360 —
  matches your parse), tatooine 9,995,370. **Zero ids ≥ 0x1000000 in any copy of either file.**
- **(b) `getObjectById` non-null for arbitrary ids:** it is a plain hash-map `find` returning 0 on
  miss (NetworkIdManager.cpp:72-80). No sentinel, no default.

So the runtime state at your clicks contradicts every measured input: seed should be ~9.9M, the
first candidate should be free, and the add should mint. When measurement and model disagree, the
model is wrong somewhere no static read can see — the next probe must be runtime.

## 2. What the restaged exe adds (`d7dba07a6`)

`wsAllocateIdRange` itself now logs on ANY refusal — before the existing `wsAddObject REFUSED
(id-mint)` line — one state dump plus per-collision attribution:

```
[editor.ws] wsAllocateIdRange REFUSED: seed=… cells=… band=[floor..ceiling) walked=… inSetSkips=… maxOutOfBand=… collisions=… first[0]=<id>(r|b|n) …
```

- `seed` is the number your logs have never shown. **seed ≥ ceiling with collisions=0** → the walk
  itself produced an out-of-band seed (and `maxOutOfBand`/`walked` say from what).
- **sane seed + millions of collisions** → a predicate is false-positive at scale, and `first[i]`'s
  tag says which: `r` = reader map, `b` = buildout set, `n` = NetworkIdManager.
- A separate line fires whenever authored ids at/above the ceiling exist (`excluded from seeding`)
  — that's the defensive hardening we kept regardless: out-of-band authored ids can no longer drag
  the seed out of the band on any future data.

Success paths are unchanged (still log `wsAddObject OK`).

## 3. Two findings from the sweep worth your notes

1. **Your Wave-1 "authored-only" smoke interpretation needs a caveat:** on naboo the buildout set
   is empty (no per-area tables exist client-side), so the 5449 count was NOT buildout filtering in
   action — there was nothing to filter. The filter is still correct and still load-bearing for
   scenes that HAVE client buildout (kashyyyk, simple) and for server-streamed... nothing else; but
   don't cite naboo counts as evidence of it.
2. On these scenes buildout content exists only server-side (streamed like any server object), so
   on a server session those objects live in `NetworkIdManager` under SERVER ids — which is exactly
   what the allocator's `n` predicate and the remove shim's occupancy guard treat correctly.

## 4. Next

One click of `wsAddObject` on the restaged exe (either scene), send the `wsAllocateIdRange
REFUSED` line — it names the mechanism outright, and the actual fix lands same-day against
evidence instead of a fourth hypothesis. `d7dba07a6` is stacked local→pushed per Kenny's cadence.
