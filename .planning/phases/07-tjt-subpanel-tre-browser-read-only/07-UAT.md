---
status: complete
phase: 07-tjt-subpanel-tre-browser-read-only
source: [07-00-SUMMARY.md, 07-01-SUMMARY.md, 07-02-SUMMARY.md, 07-03-SUMMARY.md, 07-04a-SUMMARY.md, 07-04b-SUMMARY.md]
started: 2026-05-27T19:25:00Z
updated: 2026-05-27T19:40:00Z
---

## Current Test

[testing complete]

## Tests

### 1. TRE Browser opens + tree auto-populates
expected: TRE Browser opens from the TJT menu as a resizable window; the left tree auto-populates with the client's virtual paths (~125k) with no manual path entry.
result: pass

### 2. Lazy expand + debounced filter
expected: Expanding a directory loads its children lazily; typing in the filter box debounces and narrows the tree (whole-node bold), with the flat-ListView fallback when matches exceed the cap.
result: pass

### 3. Detail pane — metadata + IFF chunk tree + hex
expected: Selecting a readable IFF entry shows the metadata header (path/size/archive/CRC/compression), a type/version banner with the thin accent rule, the universal IFF chunk tree (TAG · size · real @offset), and a raw-bytes hex view.
result: pass

### 4. Non-readable states never crash
expected: A v6000 encrypted/enumerate-only entry shows the "Extract with TreeFileExtractor" info panel; a readable non-IFF entry shows raw bytes (not the encrypted copy); a corrupt entry shows "Could not decode this file" — and the browser stays usable for other files in every case.
result: pass

### 5. Per-type structured views (3-section splitter layout)
expected: Datatable shows a column-per-column grid with typed cells (row-capped); string table shows id/name/text; object template shows declared base + local fields; mesh/skeleton/anim shows a count grid; shader (.sht) and UI page (.gui) show their summary. The detail pane is three resizable sections (tree / table / raw bytes) with two splitters and overflow scrollbars; expanding raw bytes does not clobber the tree.
result: pass

### 6. Unrecognized type degrades + decode-iff CLI parity
expected: An unrecognized type hides the structured section while the chunk tree + raw bytes still render. Running `utinni-cli decode-iff <file>` produces JSON matching what the panel shows for the same file (datatable/STF/object-template/mesh/shader/ui-page).
result: pass

## Summary

total: 6
passed: 6
issues: 0
pending: 0
skipped: 0

## Gaps

[none yet]
