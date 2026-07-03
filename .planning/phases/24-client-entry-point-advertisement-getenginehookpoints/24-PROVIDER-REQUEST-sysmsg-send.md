# Provider Request — System-message SEND (inject) on the advertised client

**From:** Utinni (consumer) · **To:** swg-client-v2 (provider) · **Date:** 2026-07-02
**Status:** REQUEST — single clean static, one `&fn` row, one version bump. No consumer offsets, no
paired wave, no vtable-resolve. This is the *send/inject* half of the sysmsg pair (the *receive/observe*
half stays OMIT — see §4).
**Source of truth:** this file in the Utinni repo. Copy into `swg-client-v2/.planning/handoff/` as
`2026-07-02-utinni-sysmsg-send.md`.
**Self-contained:** act on this without reading the Utinni Phase-24 plans. It cites the exact Utinni
read site (file:line, verified) and the exact provider symbol.

---

## 0. TL;DR

Utinni wants to **inject a system message** into the client's chat/system feed on the advertised NGE
client (`SwgClient_r.exe`) — for editor-action feedback ("[Utinni] terrain reloaded", "saved X.trn") and
a manual broadcast box in The Jawa Toolbox. The engine already has exactly the right static:

```cpp
// clientUserInterface/.../CuiSystemMessageManager.h
static void CuiSystemMessageManager::sendFakeSystemMessage(const Unicode::String & msg, bool chatBoxOnly = false);
```

This is a **byte-exact ABI match** to Utinni's existing send typedef (§2), so there is nothing to reshape.
**Advertise it under the Utinni contract name `systemMessageManager::sendMessage`** and Utinni binds it
with a one-line row — resolver re-points it on the advertised client, SWGEmu keeps its literal (D-00).

**The batch: ONE `&fn` row, delivered as a single v13 → v14 bump.**

## 1. How the unlock works (shared contract)

On the advertised client, `resolveFromExe()` overwrites each `swg::*` slot **by name** from your
`GetEngineHookPoints()` table. `systemMessageManager::sendMessage` is a **CALLED** endpoint (Utinni
invokes it — it is NOT detoured), so a plain `&fn` is all that's needed; no real-entry-vs-forwarder
subtlety, no MI, no Listener, no subscriber blast radius. SWGEmu Pre-CU is byte-for-byte unchanged.

## 2. The row requested

| Field | Value |
|-------|-------|
| **Contract name** | `systemMessageManager::sendMessage` |
| **Provider symbol** | `&CuiSystemMessageManager::sendFakeSystemMessage` (static) |
| **Signature** | `void __cdecl(const Unicode::String& msg, bool chatBoxOnly)` |
| **Utinni typedef (must match)** | `using pSendMessage = void(__cdecl*)(const swg::WString& message, bool chatOnly);` — `swg/ui/cui_manager.cpp:73` |
| **SWGEmu RVA (identification only)** | `0x008AC250` (`swg/ui/cui_manager.cpp:76`) — take `&fn`, not this address |
| **Mechanism** | static `&fn` (no MI, no virtual, no thunk) |
| **Unblocks** | Editor-action feedback sysmsgs + a manual broadcast tool in TJT, on the advertised client |

**ABI note — no reshape needed.** Utinni's `swg::WString` *is* SWG's `Unicode::String` (UTF-16 string).
The typedef above already matches `sendFakeSystemMessage(const Unicode::String&, bool)` field-for-field,
so this is a straight name→`&fn` map. The `chatBoxOnly` default argument is irrelevant to the pointer ABI
(Utinni always passes both args explicitly).

## 3. Consumer side (for context — does NOT block your delivery)

Trivial and already staged:
- `swg::systemMessageManager::sendMessage` slot exists (`cui_manager.cpp:76`); today it holds the SWGEmu
  RVA literal and is unbound → correct on SWGEmu, wrong on advertised. The bind row
  (`{"systemMessageManager::sendMessage", (void**)&swg::systemMessageManager::sendMessage}`) lands the same
  wave I re-sync your v14 `.inc/.h`.
- The managed wrapper `SystemMessageManager::sendMessage(const char*, bool)` (`cui_manager.h:67`,
  CppSharp-surfaced) already exists → **no ABI rebless.** TJT call-sites (editor-feedback hooks + a
  broadcast box) are wired consumer-side after the row resolves.
- No detour, no `isAdvertisedClient()` gating, no paired wave. It's a call: once the name resolves, it
  works on both targets.

## 4. Explicitly NOT in this request — the receive/observe half stays OMIT

For the record so this isn't confused with the reverted Bucket-A sysmsg row: Utinni's *inbound* hook
(`hkReceiveMessage`, `cui_manager.cpp:301`) detours the engine's incoming-message handler to fire a
`void(const char* msg)` subscriber list. That is **out of scope here and remains OMIT** — the real inbound
receiver on your build is a **file-local anonymous `MessageDispatch::Receiver` Listener**, not an
advertisable static (the v9→v11 `receiveMessage`→`receiveSystemMessage` mis-map that AV'd on region-enter).
Lighting up receive would need vtable-resolving that Listener off a live instance; not asked here.
`CuiSystemMessageManager::receiveSystemMessage(const ChatSystemMessage&)` — the static sitting right next
to `sendFakeSystemMessage` in the same header — is **not** that inbound Listener, so do not map receive to
it. **Send only.**

## 5. Acceptance ("done")

`systemMessageManager::sendMessage` exports undecorated (`dumpbin /exports stage/SwgClient_r.exe`), Utinni
resolves it non-null/non-dup, `.inc/.h` are sha256-identical across both repos at v14, the coverage
self-check stays green for the grown required set, and — after the consumer bind + TJT wiring — a
maintainer live smoke shows a typed message appear in the advertised client's chat/system feed. SWGEmu
D3D9 smoke still passes (send path there is unchanged — same RVA).

## 6. Priority

Low-medium. Modder-facing value is the editor-feedback confirmations + a broadcast tool; it's a clean
one-row add with no risk surface, so it can ride any convenient next bump.
