---
status: complete
phase: 14-headless-mcp-server-utinni-mcp-the-centerpiece
source: [14-01-SUMMARY.md, 14-02-SUMMARY.md, 14-03-SUMMARY.md, 14-03a-SUMMARY.md, 14-04-SUMMARY.md]
started: 2026-06-07T12:07:21Z
updated: 2026-06-07T12:30:00Z
---

## Current Test
<!-- OVERWRITE each test - shows where we are -->

[testing complete]

## Tests

### 1. Cold Start Smoke Test
expected: Build Utinni.Mcp fresh and launch the exe. Without --root / UTINNI_MCP_ROOT it refuses to start (fail-closed access-control pin). With --root <path> it boots clean — logs on stderr only, stdout clean for stdio MCP. No startup crash.
result: pass
note: "Verified by Claude. Clean Release build (0 warn). No --root → InvalidOperationException 'refuses to start: no client root configured' on stderr, stdout empty. With --root D:\\Code\\Utinni → EXIT=0, StdioServerTransport reading messages, all logs stderr, stdout completely clean, graceful EOF shutdown."

### 2. MCP Handshake — Exactly 11 Tools
expected: Connect a real MCP client (Claude Code/Desktop wired to Utinni.Mcp, or the RoundTripTests harness). Handshake completes; ListTools returns EXACTLY 11 named tools — read_tre, inspect_iff, decode_iff, list_world_objects, get_template_schema, save_datatable, save_iff, save_stringtable, save_object_template, repack_tre, roundtrip_check.
result: pass
note: "Verified by Claude via hand-driven raw stdio JSON-RPC (independent of project test code). initialize negotiated protocolVersion 2024-11-05, serverInfo=Utinni.Mcp; tools/list returned exactly 11 tools, all expected names. Annotations correct: reads readOnlyHint:true, save_*/repack_tre destructiveHint:true, roundtrip_check verify-only."

### 3. Read a TRE Archive
expected: Call read_tre on a real (supported, non-encrypted) .tre archive under the root. Returns the parse-tre JSON envelope (record listing) verbatim — same shape as utinni-cli parse-tre. Encrypted/v6000 archives enumerate-only as expected.
result: pass
note: "Verified by Claude. read_tre on sample_v0006.tre (root via temp dir, --cli-path to built utinni-cli) → isError=false, command=parse-tre, structuredContent.result keys=header/records/source."

### 4. Decode Multiple IFF Formats
expected: Call decode_iff on .tab (datatable), .stf (stringtable), and an object-template .iff under the root. Each returns the typed decode with the correct format/type discriminator. inspect_iff on the same file returns the raw chunk tree instead.
result: pass
note: "Verified by Claude. decode_iff: sample.tab→type=datatable, sample.stf→type=stringtable, sample_ot.iff→type=objecttemplate (each isError=false, correct discriminator). inspect_iff sample_ot.iff→command=inspect-iff raw tree (flat/tree/source), distinct from typed decode. Bonus: a missing-file call returned a clean in-band FileNotFound error envelope (taxonomy works)."

### 5. Edit → Save → Read-Back (the centerpiece)
expected: Call save_datatable to edit one cell (recordIndex + columnId "0" + value), then decode_iff the SAME loose path. The read-back shows the edited value AND the file hash changed — i.e. the edit genuinely persisted (a no-op re-serialize would fail this). save exits clean (IsError=false).
result: pass
note: "Verified by Claude. sample.tab row0 col0 (anInt) 7→42 via save_datatable (isError=false, written=true, validated=true, bytesEqualUntouched=true). File hash changed b7c850→9fb3f2. Clean tool-path decode_iff read-back returned row0=[42,...]; only the targeted cell changed (rest byte-identical). NOTE: server processes JSON-RPC requests concurrently — a real McpClient awaits each call; a fire-all pipe can race read-back ahead of save."

### 6. Verify-Fail Does Not Write
expected: Call save_datatable with a bad/out-of-range edit (e.g. recordIndex=999). The tool returns an in-band error (IsError=true), the target file on disk is byte-unchanged, and NO partial write happened (fail-closed verify-before-commit).
result: pass
note: "Verified by Claude. save_datatable recordIndex=999 → isError=true, error.kind=UsageError '--mutate-cell (999,0) is out of range (rows=1, cols=8)'. File hash identical before/after (9fb3f2…) — byte-unchanged, fail-closed."

### 7. repack_tre Off By Default
expected: Call repack_tre WITHOUT dry_run=false. It returns a plan-only dry-run notice — no archive rewrite, no backup claim, no CLI spawn. Only an explicit dry_run=false performs the destructive repack (and that routes through a backup). repack_tre is never reachable from any save_* tool.
result: pass
note: "Verified by Claude on sample_v0006.tre (isolated temp copy). Default call (no dry_run) → 'Dry run (no write performed)' notice, tre hash unchanged, zero backup files. dry_run=false → real repack (isError=false, written=true, validated=true), timestamped backupPath .bak created, record count preserved 2→2. save_* tools never expose a repack path (tool surface + annotations)."

### 8. Path-Escape Boundary
expected: Call any tool with a path that escapes the pinned root (e.g. ../../outside.tab, ..\\..\\x, subdir/../../x). The host rejects it as a hard error with ZERO CLI spawns and writes nothing outside the root. Reads and writes both honor the boundary.
result: pass
note: "Verified by Claude. decode_iff '../../sample.tab', save_datatable '../../escape.tab', repack_tre 'subdir/../../escape.tre' all → isError=true; parent dir (outside root) unchanged, zero escape.* files leaked. (Zero-CLI-spawn-on-escape additionally proven by unit suite McpBoundaryPathEscapeTests.)"

## Summary

total: 8
passed: 8
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps

[none yet]
