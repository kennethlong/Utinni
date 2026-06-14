# Utinni ↔ swg-blender-plugin file-format boundary contract

**Status:** Authoritative (Utinni-owned, D-05). **Requirement:** ECO-01 (Phase 16).
**Reference implementation (read-only, no runtime dependency):** `D:/Code/swg-blender-plugin`
(`swg_pipeline/rsp_builder.py`, `swg_pipeline/export_manifest.py`, `swg_pipeline/export_bundle.py`).

This document is the single source of truth for the file-format / search-path seam between
**Utinni** (reads / opens / previews) and **swg-blender-plugin** (writes / authors). Utinni owns
format + injection; Blender owns DCC authoring. Neither repo imports the other (see §4). The
`swg-blender-plugin` repo carries only a pointer note back to this file.

The four surfaces below are the D-06 contract: (1) the `.rsp` search-path contract + the
`swg_export_manifest.json` schema, (2) the `.iff`/`.tre` format-version matrix, (3) the bundle /
directory layout, and (4) the ownership / anti-coupling rules.

---

## 1. `.rsp` search-path contract

A Blender export bundle ships `data_*.rsp` manifests that tell the SWG client where each loose
asset lives. The format mirrors the engine's `TreeFileRspBuilder` (swg-blender-plugin
`rsp_builder.py` `format_rsp_line`).

### 1.1 Line format

Each `.rsp` line is:

```
{treefile_path} @ {explicit_path}
```

- **LHS `treefile_path`** — the logical, *relative* TreeFile asset path the client resolves
  (forward slashes), e.g. `appearance/mesh/frn_all_bed_sm_s1_l0.msh`.
- **`@` separator** — a literal ` @ ` (space, at-sign, space).
- **RHS `explicit_path`** — an **ABSOLUTE, forward-slashed filesystem path** to the asset on disk.
  `rsp_builder.py` writes `str(abspath).replace("\\", "/")` from a `Path.resolve()`, so the RHS of a
  conforming bundle's `.rsp` is **normally absolute**, not relative.

Because the RHS is normally absolute, `utinni-cli validate-bundle` **ALLOWS an absolute RHS whose
canonical path is CONTAINED under the bundle root** (it is existence-checked like any contained
asset), and **rejects only refs that ESCAPE the bundle root** (a different drive / UNC, or a `..`
traversal). An escaping ref is recorded as a `rejectedRefs` finding and is **never** probed on disk.

### 1.2 Suffix → bucket → `.rsp` filename rules

`rsp_builder.py` classifies each asset by filename suffix into a bucket, and each bucket has a fixed
`.rsp` filename. The bucket rules (first suffix match wins, else the catch-all `other`):

| Suffix  | Bucket          | `.rsp` filename                     |
|---------|-----------------|-------------------------------------|
| `.mp3`  | `music`         | `data_uncompressed_music.rsp`       |
| `.wav`  | `sample`        | `data_uncompressed_sample.rsp`      |
| `.dds`  | `texture`       | `data_compressed_texture.rsp`       |
| `.ans`  | `animation`     | `data_compressed_animation.rsp`     |
| `.mgn`  | `mesh_skeletal` | `data_compressed_mesh_skeletal.rsp` |
| `.msh`  | `mesh_static`   | `data_compressed_mesh_static.rsp`   |
| (other) | `other`         | `data_compressed_other.rsp`         |

These seven filenames are the exact literal set `validate-bundle` knows about
(`ValidateBundleCommand.BucketFilenames`); an automated doc↔verb parity test asserts every one of
them appears in this document (C-17), so the two can never silently drift.

### 1.3 Priority / search ordering

Within a bucket map, **earlier search roots win on duplicate logical paths** (`build_rsp_maps`:
`if rel not in bucket_map`). The client loads loose overrides ahead of packed TREs when the
loose search path's priority is higher.

### 1.4 `client_search_paths.cfg` dialects

The bundle's `client_search_paths.cfg` fragment registers the loose search path under
`[SharedFile]`. Two dialects (`rsp_builder.py` `client_search_path_snippet`):

- **Legacy:** `searchPath{priority}=...` (e.g. `searchPath0=...`).
- **SWGSource / multi-SKU:** `searchPath_{sku:02d}_{priority}=...` (e.g. `searchPath_00_12=...`).

The Phase-7 validation bundle writes the SWGSource form at **priority 12, sku 0**
(`searchPath_00_12=`), which beats the retail `searchTree_00_7/8` TRE priorities.

> **Live-render caveat (not a Phase-16 gate):** the loose `searchPath` is currently **DISABLED**
> in the maintainer's client environment (see `project_swg_client_loose_overrides`), so a freshly
> exported bundle's *visible* in-client render is best-effort. Re-enabling it rides on RESID-03; it
> is **not** a Phase-16 success criterion. The boundary itself (Blender writes a conforming bundle,
> Utinni reads/validates it) is what this contract locks.

### 1.5 `validate-bundle` exit / valid semantics (CDX-NEW-9)

`utinni-cli validate-bundle` emits a JSON envelope. **A structurally-valid bundle (parseable
manifest / `.rsp` / `.cfg`) exits 0 even when it has missing or rejected refs** — those are
*findings* carried in the envelope, not parse failures. Therefore:

- **Agents MUST read the envelope `valid` and `hasRejectedRefs` fields — not the exit code alone.**
  `valid` is `true` only when there are zero `rejectedRefs`, zero `missingAssets`, and zero
  `bucketMismatches`. `hasRejectedRefs` is `true` when any ref escaped the bundle root.
- **Exit 2 (`ParseError`)** means the manifest JSON or a `.rsp`/`.cfg` line was structurally
  unparseable.
- **Exit 3 (`FileNotFound`)** means the manifest path itself does not exist.

The envelope `result` carries: `valid`, `hasRejectedRefs`, `assetsChecked`, `missingAssets`,
`rejectedRefs`, `rspFilesValidated`, `bucketMismatches`, `bundleRoot`.

---

## 1b. `swg_export_manifest.json` schema (CDX-NEW-10 / R3-5)

The export manifest is the authoritative bundle index. The schema below is sourced **verbatim** from
the real exporter — `export_manifest.py` `build_export_manifest` (top-level) and `export_bundle.py`
`BundleResult.as_dict` (the `assets` block). Do not assume fields the exporter never emits.

### Top-level fields

| Field              | Required | Notes                                                        |
|--------------------|----------|--------------------------------------------------------------|
| `pipeline_version` | yes      | Tooling version stamp (e.g. `"3.009"`).                       |
| `exported_at`      | yes      | UTC ISO-8601 timestamp.                                       |
| `bundle_type`      | yes      | Allowed values: `static`, `skeletal`.                        |
| `output_dir`       | yes      | Absolute bundle root the exporter wrote.                     |
| `author`           | yes      | May be empty string.                                         |
| `notes`            | yes      | May be empty string.                                         |
| `assets`           | yes      | The per-asset index (see below). `validate-bundle` requires it. |
| `rsp_files`        | optional | Top-level array of relative `.rsp` paths.                    |
| `tre_files`        | optional | Present only after a `--pack-tre` step.                      |
| `packed_at`        | optional | Present only after packing.                                  |
| *extra keys*       | optional | e.g. `client_test_notes`. **Unknown top-level keys are tolerated/ignored** (forward-compatible) — they are NOT a `ParseError`. |

### `assets` object (`BundleResult.as_dict`)

| Field        | Notes                                                          |
|--------------|---------------------------------------------------------------|
| `output_dir` | Bundle root (string).                                         |
| `mesh`       | Relative path to the `.msh`/`.mgn`.                           |
| `shaders`    | Relative-path array.                                          |
| `textures`   | Relative-path array.                                          |
| `manifest`   | Path to this manifest.                                        |
| `rsp_files`  | Relative-path array of the `.rsp` files.                      |
| `client_cfg` | Path to `client_search_paths.cfg` — **nested INSIDE `assets`**, NOT a top-level field (R3-5). May be a string path. |

`validate-bundle` gathers asset refs from the `assets` object (incl. `client_cfg`) AND the
top-level `rsp_files`, then existence-checks each through the shared containment gate (§4 / the
`IsContainedUnderRoot` predicate in `LooseOverridePath.cs`).

---

## 2. Format-version matrix (`.iff` / `.tre`)

Mirrors `UtinniCoreDotNet/Formats/Tre/TreVersion.cs` exactly (the authoritative reader). The version
tag is the raw 4-char on-disk value after the `EERT` token.

| TRE version | Utinni support     | Notes                                                            |
|-------------|--------------------|-----------------------------------------------------------------|
| `0004`      | READABLE           | SWGEmu Pre-CU; size-first 24-byte record stride.                |
| `0005`      | READABLE           | SWGEmu Pre-CU; size-first 24-byte stride (fixture-validated).   |
| `0006`      | READABLE           | SWGEmu Pre-CU; size-first 24-byte stride (fixture-validated).   |
| `5000`      | **READABLE**       | SWGEmu Pre-CU; size-first 24-byte stride, zlib-compressed TOC/name blocks. **NOT encrypted.** |
| `6000`      | enumerate-only     | Restoration; crc-first 32-byte stride, zlib-framed TOC/name blocks; **payloads encrypted** (enumerate TOC/names only, D-07). |
| (any other) | UnsupportedVersion | `TreVersions.Parse` throws.                                      |

> **Correctness pins (T-16-03):** do NOT describe `5000` as "encrypted" — it is fully readable. Do
> NOT list `COT2000` as a TRE version — `COT2000` is a master-index concept (`CotMasterIndex`), not
> a `TreVersion`. (These correct the stale `project_tre_version_support_gap` assumption; the shipped
> `TreVersion.cs` is the source of truth.)

`swg-blender-plugin` writes within the **Pre-CU `0004`/`0005`/`0006` family** Utinni reads fully.

> **Cross-validation finding CV-1 (TOC field order):** the `retail_mini_0005.tre` golden in
> `swg-blender-plugin/tests/golden/` is a **synthetic reader-unit fixture** (authored in
> `tests/test_tre_versions.py`), not a real `TreeFileBuilder` export. Its v0005 TOC entries are laid
> out **crc-first** (`crc, length, offset, comp, clen, fnoff`, per `tre_reader.py`
> `TOC_ENTRY_FMT="<Iiiiii"`), whereas Utinni reads the `0004`/`0005`/`0006` family **size-first**
> (`uncompressedSize, offset, compressor, compressedSize, checksum, nameOffset`, validated against
> the live SWGEmu client). The two field orders **disagree**, so `utinni-cli parse-tre` over that
> synthetic fixture reads the second TOC int (length=36) into the compressor slot and rejects it as
> `UnknownCompressor` (exit 2). This is a documented boundary finding, NOT a reader bug: real Blender
> `.tre` output comes from the SOE `TreeFileBuilder`, which writes the engine's size-first layout
> Utinni reads. Anyone hand-authoring a v0005 `.tre` for cross-tool use MUST use the engine's
> size-first TOC order. The cross-validation that **passes** is the `.msh` path (Blender writes,
> Utinni `decode-iff` reads it count-only — §4).

### IFF assets

Utinni reads IFF assets (`.iff`, `.msh`, `.mgn`, `.skt`, `.sht`, `.prt`, datatable/`.tab`, `.stf`)
**structurally** — chunk trees and count-only appearance summaries — never as geometry. See §5.

---

## 3. Directory / bundle layout

A Blender export bundle (`export_bundle.py`) has this on-disk shape:

```
<bundle_root>/
  appearance/
    mesh/        # .msh (static), .mgn (skeletal)
    skeleton/    # .skt
    animation/   # .ans
  shader/        # .sht
  texture/       # .dds
  rsp/
    data_compressed_mesh_static.rsp
    data_compressed_mesh_skeletal.rsp
    ... (only the buckets that have assets)
  client_search_paths.cfg
  swg_export_manifest.json
```

`validate-bundle` accepts either the `<bundle_root>` directory or the `swg_export_manifest.json`
path; the bundle root is the directory containing the manifest.

---

## 4. Ownership / anti-coupling rules

- **Utinni READS; Blender WRITES.** Utinni opens / previews / validates what Blender exports.
- **Neither repo imports the other.** There is **no runtime dependency** in either direction. The
  contract is a *file-format seam*, not a process coupling. `swg-blender-plugin` is referenced only
  as a read-only spec (its `.py` pipeline files document the format); Utinni vendors only pinned
  copies of its golden bytes for cross-validation tests.
- **Utinni adds NO geometry codec.** Per **DEC-A3** (Utinni is not a Maya / 3ds Max replacement;
  DCC tools own mesh / animation / texture authoring), Utinni does not decode or author
  mesh / skeleton / animation / texture geometry. It reads structure (counts, chunk trees) only.
  See also `project_swg_toolchain_crosswalk` (Utinni owns format + injection; Blender owns DCC).
- **Containment is enforced once.** Every asset ref `validate-bundle` checks — relative or absolute —
  routes through the single shared `IsContainedUnderRoot(canonicalRoot, canonicalCandidate)`
  predicate in `UtinniCoreDotNet.PathContainment/LooseOverridePath.cs` (the same gate
  `LooseOverridePath.Resolve` uses), so the absolute-ref path cannot drift from the shipped
  algorithm. Containment is **lexical** (canonical-path prefix); symlinks/junctions are not followed
  by the text validator.

---

## 5. Open / preview reachability (D-07)

The boundary is served entirely by **existing readers + one thin text verb** — no new codec:

| Verb               | Reads                          | Output                                            |
|--------------------|--------------------------------|---------------------------------------------------|
| `parse-tre`        | `.tre` archive                 | header + record listing (enumerate; payloads per §2). |
| `decode-iff`       | `.msh`/`.mgn`/`.skt`/`.sht`/`.prt`/`.tab`/`.stf` | typed summary — appearance is **count-only** (no geometry, DEC-A3). |
| `inspect-iff`      | any IFF                        | full chunk tree.                                  |
| `validate-bundle`  | manifest + `.rsp` + `.cfg`     | the TEXT contract envelope (§1.5), with bundle-root containment that allows contained absolutes. |

`decode-iff` over a Blender-written `.msh` is the working cross-validation proof (D-08 / SC4): it
emits a MESH appearance summary with non-zero vertex/shader counts and **no** geometry payload.
