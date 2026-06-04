---
phase: 13
slug: wrap-revived-compilers-as-cli-verbs-close-ot-tier-2
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-06-03
---

# Phase 13 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (managed: Utinni.Cli.Tests + UtinniCoreDotNet.Tests, net472) + native MSBuild build-gate (tools/Utinni.Tools.sln) + golden-fixture + Get-FileHash byte-compare harness |
| **Config file** | none — existing test projects (Utinni.Cli.Tests, UtinniCoreDotNet.Tests) |
| **Quick run command** | `dotnet test Utinni.Cli.Tests --no-build` |
| **Full suite command** | VS2026 MSBuild (Debug\|x86) then `dotnet test --no-build` (per feedback_dotnet_build_msbuild_resources) |
| **Estimated runtime** | ~{N} seconds (to be measured Wave 0) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test Utinni.Cli.Tests --no-build`
- **After every plan wave:** Run full managed + native suite
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** {N} seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 13-01-01 | 01 | 1 | AUTH-06 | — | Native leaf-lib links libxml2 prebuilt without unresolved externals | native build | `MSBuild tools/Utinni.Tools.sln /t:sharedXml /p:Configuration=Debug;Platform=Win32` builds green (sharedXml.lib produced) | ✅ | ⬜ pending |
| 13-01-02 | 01 | 1 | AUTH-06 | T-13-01 | Perforce source-stubbed to no-op; no live-network dependency in headless build | native build | `MSBuild tools/Utinni.Tools.sln /p:Configuration=Debug;Platform=Win32` builds green (DataTableTool/ArmorExporterTool/CoreWeaponExporterTool .exe produced) | ✅ | ⬜ pending |
| 13-01-03 | 01 | 1 | AUTH-06 | — | Dependency manifest covers the new exes + Perforce-stub (CI hard-gate auto-extends) | grep gate | `grep -v '^#' tools/DEPENDENCY-MANIFEST.md \| grep -c -E "DataTableTool\|ArmorExporterTool\|CoreWeaponExporterTool\|sharedXml\|Perforce-stub"` >= 5 | ✅ | ⬜ pending |
| 13-02-01 | 02 | 1 | AUTH-04 | T-13-13 | UseShellExecute=false + arg array; banner (date/abs-path) stripped from output | unit (tdd) | `dotnet test --no-build --filter NativeToolRunner` (exit-code 0->0/non-zero->2/missing-exe->3; envelope keys; NormalizeBanner strips date/abs-path) | ✅ W0 | ⬜ pending |
| 13-02-02 | 02 | 1 | AUTH-04 | — | .rsp recipe preserves on-disk order; no pre-sort (byte-exact engine input) | unit (tdd) | `dotnet test --no-build --filter RspSynthesizer` (order preserved; @u marker on uncompressed; disk-first; no pre-sort) | ✅ W0 | ⬜ pending |
| 13-03-01 | 03 | 1 | AUTH-05 | T-13-0x | Loose-override path-containment rejection; atomic Flush(true) | unit (tdd) | `dotnet test --no-build --filter Save` (4-format save + envelope keys; path-containment rejection; .tre-redirect) | ✅ W0 | ⬜ pending |
| 13-03-02 | 03 | 1 | AUTH-05 | — | Backup-before-repack; V6000 encrypted archive refused (exit 2) | unit (tdd) | `dotnet test --no-build --filter RepackTre` (backup taken; repack round-trips; V6000 refused exit 2; `utinni-cli --help` lists both verbs) | ✅ W0 | ⬜ pending |
| 13-04-01 | 04 | 2 | AUTH-03 | — | Produced .iff banner-normalized; native-error->exit 2, missing-exe->exit 3 | golden (tdd) | `dotnet test --no-build --filter CompileTemplate` (.iff matches golden post-banner-normalize; managed OT reader decodes typed-correct; missing-exe->3; native error->2) | ✅ W0 | ⬜ pending |
| 13-04-02 | 04 | 2 | AUTH-04 | — | Uncompressed synth-.rsp round-trip SHA256 byte-exact; native-error->exit 2 | byte-exact (tdd) | `dotnet test --no-build --filter BuildTre` (uncompressed synth-.rsp SHA256-byte-exact via Get-FileHash compare; compressed structural-compare; native-error->2) | ✅ W0 | ⬜ pending |
| 13-05-01 | 05 | 3 | AUTH-02 | T-13-14 | Schema banner-normalized (no __DATE__/abs-path); sorted-key stable | golden (tdd) | `dotnet test --no-build --filter CompileDefinition` (schema JSON from minimal .tdf fixture matches golden; ListType!=LIST_NONE param present+typed; banner-normalized stable) | ✅ W0 | ⬜ pending |
| 13-05-02 | 05 | 3 | AUTH-06 | T-13-13 | DataTableTool wrapped via arg-array; D-04 native-authoritative on managed mismatch | golden (tdd) | `dotnet test --no-build --filter CompileDatatable` (native .iff matches golden; D-03 managed-oracle cross-check records divergence, native authoritative) | ✅ W0 | ⬜ pending |
| 13-05-03 | 05 | 3 | AUTH-06 | T-13-03 / T-13-15 | Input path validated (reject shell-meta/`..`) before exporter system(); staged TemplateCompiler.exe resolution | golden (tdd) | `dotnet test --no-build --filter Export` (both exporters run headless emitting .tpf+.iff matching goldens; no Perforce FATAL; injection-path rejected pre-invocation) | ✅ W0 | ⬜ pending |
| 13-06-01 | 06 | 4 | RESID-01 | T-13-16 | Absent/malformed schema degrades to no-match without throwing on the editor open path | unit (tdd) | `dotnet test --no-build --filter ObjectTemplateSchema` (loads committed common-class schema; ListType!=LIST_NONE classified structured; absent/malformed -> graceful no-throw) | ✅ W0 | ⬜ pending |
| 13-06-02 | 06 | 4 | RESID-01 | T-13-17 | Display-only — no edit to codec Encode path; byte-exact round-trip preserved | build + regression | VS2026 MSBuild UtinniCoreDotNet+TJT Debug+Release\|x86 green; `dotnet test --no-build` full framework xUnit green (schema tests + no OT-suite regression) | ✅ | ⬜ pending |
| 13-06-03 | 06 | 4 | RESID-01 | — | Maintainer-in-the-loop Tier-4 visual residual (CON-TT-03; FlaUI deliberately skipped) | human-verify (Nyquist-exempt) | `<human-check>` — maintainer confirms typed list/struct display + typed-label+hex tail against a live client | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*
*(Filled by the planner per-task and the Nyquist auditor during execution.)*

**Nyquist continuity:** every auto/tdd task carries an `<automated>` command (or a native MSBuild/Get-FileHash gate). The lone `checkpoint:human-verify` task (13-06-03) is the documented CON-TT-03 maintainer-in-the-loop residual and is Nyquist-exempt. No run of 3 consecutive auto/tdd tasks lacks an automated verify.

---

## Wave 0 Requirements

- [x] Golden fixtures for the new BUILD/SAVE/schema verbs in `Utinni.Cli.Tests` — `.tpf` (13-04-01), minimal `.tdf` + schema golden (13-05-01), `.tab`/spreadsheet + native-.iff golden (13-05-02), datatable `.iff` + `tools.cfg` (13-05-03)
- [x] Real pre-CU `.tre` extraction fixtures (cross-check oracle inputs, D-05) — synth-`.rsp` round-trip corpus (13-02-02 / 13-04-02)
- [x] `.rsp`-synthesis round-trip harness (D-06) — RspSynthesizer (13-02-02) feeding build-tre byte-exact ladder (13-04-02)
- [x] Committed per-class schema artifact under `.planning/phases/.../schema/` — emitted from the minimal `.tdf` (13-05-01) and derived from the generated `Shared*ObjectTemplate` classes for the common classes (13-06-01)

*Wave-0 fixtures are authored inside their owning Wave 1-4 tasks (no separate Wave-0-only plan); `wave_0_complete` flips true once those fixtures land in the first run of each.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| OT editor typed list/struct param display (slots/attributes/hair) renders typed; rare tail = typed-label+hex | RESID-01 | WinForms host visual; FlaUI deliberately skipped (CON-TT-03 — documented Tier-4 maintainer-in-the-loop residual) | 13-06-03 `<how-to-verify>`: build TJT+UtinniCore (VS2026 MSBuild Debug\|x86), open a draft-schematic / hair template, confirm structured typed rows (not a Consolas hex blob), confirm rare tail shows typed-label+hex, confirm scalar save still round-trips |

*All BUILD/SAVE/schema verbs are headless CLI + golden/Get-FileHash — automatable (no live SWG needed). Only the RESID-01 typed-display visual is manual.*

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies (13-06-03 is the Nyquist-exempt CON-TT-03 human-verify residual)
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (`.tpf`/`.tdf`/`.tab` golden assets + schema artifact authored in-task)
- [x] No watch-mode flags
- [ ] Feedback latency < {N}s (measure Wave 0)
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved (Nyquist map complete; lone human-verify task exempt per CON-TT-03)
</content>
