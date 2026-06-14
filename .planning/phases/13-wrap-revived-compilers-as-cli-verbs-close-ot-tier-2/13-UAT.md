---
status: complete
phase: 13-wrap-revived-compilers-as-cli-verbs-close-ot-tier-2
source: [13-01-SUMMARY.md, 13-02-SUMMARY.md, 13-03-SUMMARY.md, 13-04-SUMMARY.md, 13-05-SUMMARY.md, 13-06-SUMMARY.md]
started: 2026-06-14T19:30:00Z
updated: 2026-06-14T19:34:00Z
---

## Current Test

[testing complete]

## Tests

### 1. AUTH-02..06 — eight new utinni-cli verbs registered + wired
expected: compile-template, build-tre, compile-definition, compile-datatable, export-armor, export-weapon, save, repack-tre are all real registered verbs dispatching to native runners / framework writers.
result: pass
note: auto-verified — Program.cs:58-65 registers all 8 (SaveOptions, RepackTreOptions, CompileTemplateOptions, BuildTreOptions, CompileDefinitionOptions, CompileDatatableOptions, ExportArmorOptions, ExportWeaponOptions). Verb + error-path goldens ship; success-chain goldens for compile-template/export are documented A1 gate-findings (no canonical SOE .tpf/.tdf assets) — retiring when real assets arrive, not a coverage hole.

### 2. AUTH-05 — `save` verb writes structured envelope, loose-override default
expected: `save` writes an edited IFF/datatable/.stf/OT asset to the loose-override tier and emits {written, path, bytesWritten, backupPath, validated}; .tre is refused and routed to a separate repack-tre verb.
result: pass
note: auto-verified — SaveCommand.cs 4-format sniff over framework writers, LooseOverridePath.Resolve default, exact 5-key envelope; SaveCommandTests asserts all 5 keys; RepackTreCommand populates backupPath + refuses V6000.

### 3. AUTH-04 — `build-tre` produces byte-exact archive (uncompressed)
expected: build-tre wraps the revived TreeFileBuilder via a synthesized .rsp and reproduces a byte-exact .tre for the uncompressed path.
result: pass
note: auto-verified — BuildTreCommand + RspSynthesizer (TreFile.Records order + CompressionKind→@u); BuildTre_UncompressedSynthRsp_ByteExact confirmed (compressed = structural fallback, D-06).

### 4. AUTH-02 — `compile-definition` emits param→type schema; artifact committed + embedded
expected: compile-definition parses a .tdf into a per-class param→type schema (TdfSchemaExtractor); object-template-common.schema.json is committed and embedded for the editor to consume.
result: pass
note: auto-verified — CompileDefinitionCommand (TdfSchemaExtractor, --skip-native for tests); object-template-common.schema.json present in UtinniCoreDotNet/Formats/ObjectTemplate/ + EmbeddedResource; ObjectTemplateSchema(.cs)/SchemaLoader.cs ship (LoadCommon graceful no-throw).

### 5. RESID-01 — OT editor renders list/struct params typed, not raw hex
expected: List/struct params (draft-schematic slots/attributes, creature/hair) render as structured typed rows / typed LABELs (not a Consolas hex blob); exotic multi-chunk param degrades to typed-LABEL+hex (not bare hex); scalar edit+save still byte-exact.
result: pass
note: maintainer-confirmed 2026-06-14 — closes the 13-VERIFICATION.md human_needed Tier-4 visual checkpoint (13-06-03). Code path: FormObjectTemplateEditor.cs:94 (LoadCommon) + typed render :531-540/:610-616.

## Summary

total: 5
passed: 5
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps

[none yet]
