# Provider Request — System-message SEND (inject) on the advertised client

**From:** Utinni (consumer) · **To:** swg-client-v2 (provider) · **Date:** 2026-07-02, **rev. 2 2026-07-03**
**Status:** REQUEST rev. 2 — the v14 direct-`&fn` row **CRASHED live smoke** (WRITE-AV); replace it with
an **extern "C" utf8 shim** (the `utinni_replayClientEffect` precedent). One shim, one v15 bump.
**Source of truth:** this file in the Utinni repo. Copy into `swg-client-v2/.planning/handoff/` as
`2026-07-03-utinni-sysmsg-send-rev2.md` (supersedes `2026-07-02-utinni-sysmsg-send.md`).
**Self-contained:** act on this without reading the Utinni Phase-24 plans.

---

## 0. TL;DR — what rev. 1 got wrong (consumer's error, not yours)

You delivered rev. 1 exactly as asked: v14 advertised `systemMessageManager::sendMessage` →
`&CuiSystemMessageManager::sendFakeSystemMessage(const Unicode::String&, bool)`. The row resolves,
the target is correct — and the first live call **crashed the client** (0xC0000005 WRITE-AV,
faulting rva `0xDBA770`, consumer sendMessage frames on the stack).

**Root cause:** rev. 1 claimed "byte-exact ABI match" against the consumer's typedef. That was only true
of the *signature*. The parameter is a **C++ string object passed by reference**, and the two sides model
it with different layouts:

| Side | `Unicode::String` layout (x86) |
|------|-------------------------------|
| Consumer `swg::WString` (models the 2002 SWGEmu exe) | 3 pointers: `begin` @0, `end` @4, `allocEnd` @8 (12 bytes) |
| Your v145 build (modern MSVC `std::basic_string<unsigned short>`) | SSO union (ptr/16-byte buf) @0, `_Mysize` @16, `_Myres` @20 (24 bytes) |

Your engine read `_Mysize` from past the end of the consumer's 12-byte object → garbage size → wild
write. **General rule both sides now adopt: only primitives and pointers cross the advertised ABI
boundary. Any C++ class-type parameter (strings above all) needs a provider-side `extern "C"` shim** —
exactly what Bucket B-2's `utinni_replayClientEffect(char const*)` already does.

Consumer-side the v14 wrapper is hard-blocked on the advertised client (Utinni `bc4005b`) until the shim
lands, so nothing can re-trigger the crash meanwhile.

## 1. The rev. 2 row — REPLACE, one shim, v14 → v15

**Remove** the v14 row `systemMessageManager::sendMessage` (it is unsafe for any consumer to call — a
loaded footgun if it stays advertised) and **add**:

| Field | Value |
|-------|-------|
| **Contract name** | `systemMessageManager::sendMessageUtf8` |
| **Provider symbol** | a new `extern "C"` shim, e.g. `utinni_sendFakeSystemMessage` |
| **Shim signature** | `void __cdecl utinni_sendFakeSystemMessage(const char* utf8Msg, bool chatBoxOnly)` |
| **Shim body** | widen `utf8Msg` → `Unicode::String` **on your side** (your CRT, your layout), then call `CuiSystemMessageManager::sendFakeSystemMessage(msg, chatBoxOnly)` |
| **Mechanism** | constant `&fn` of the shim (plain static, C linkage, no C++ objects in the signature) |
| **Unblocks** | Editor-action feedback sysmsgs + the TJT broadcast box (already wired consumer-side, currently blocked) |

Notes:
- **Name-REPLACE, not re-point.** A renamed row means any version-skewed pairing (old consumer / new exe
  or vice versa) *misses by name and degrades* instead of mis-calling across a changed ABI under the same
  name. Net count stays 120; bump `ENGINE_HOOKPOINTS_VERSION` 14 → 15; re-sync `engine_hookpoints.{h,inc}`
  byte-identical + sha256-verify as usual.
- **Widening:** the consumer passes UTF-8 (in practice ASCII editor text). `Unicode::narrowToWide` (or a
  simple per-byte widen for ASCII) is fine — your call, it's your string now.
- **Threading:** consumer calls it game-thread-only (main-loop marshaled), never per-frame. Same contract
  as `utinni_replayClientEffect`.

## 2. Consumer side (context — does not block you)

- New null-initialized slot with the char* typedef, bound to `systemMessageManager::sendMessageUtf8`.
  Starts null → the standard advertised-only pattern; a plain null-check guards it (no skew guard needed).
- `SystemMessageManager::sendMessage(const char*, bool)` wrapper branches: advertised → the utf8 shim
  (null-checked); SWGEmu → the existing WString literal (that path models the 2002 exe correctly and is
  smoke-green for years — unchanged, D-00).
- TJT wiring (save-feedback + broadcast box, UtinniPlugins `e93567f`) is already in place and lights up
  as soon as the shim resolves.

## 3. OPTIONAL, same pattern, only if convenient (not required for this wave)

`cuiChatWindow::writeToAllTabs` / `writeToCurrentTab` (advertised since v3) have the SAME class-type
parameter (`const Unicode::String&`) and are now consumer-blocked on advertised for the same reason
(latent — nothing calls them there yet). If/when the chat editor needs them, the fix is identical:
`extern "C" utinni_chatWriteToAllTabsUtf8(void* pChatWindow, const char* utf8)` (+ current-tab variant).
**Do not deliver these now unless trivially cheap** — flagging so the pattern is on your radar, and so
you can audit any OTHER advertised row whose parameters are C++ objects (std::string, Unicode::String,
std::vector, …) — each such row is unsafe to CALL from the consumer until shimmed. Rows the consumer only
DETOURS (reads args your code constructed) are unaffected.

## 4. Acceptance ("done")

`systemMessageManager::sendMessageUtf8` in the v15 table (v14 `sendMessage` gone), the shim exports/
resolves non-null, `.inc/.h` sha256-identical at v15, coverage self-check green, and the maintainer live
smoke shows: typed broadcast text appears in the advertised client's chat feed, and a `.trn` save shows
"[Utinni] saved … — terrain reloaded". SWGEmu D3D9 smoke unchanged.

## 5. Priority

Same as rev. 1 (low-medium) — but the consumer UI for it is already shipped and visibly dark, so sooner
is nicer. It's ~10 lines on your side.
