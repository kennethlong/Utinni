# TRE uncompressed record with CompressedSize=0 — repack raw-slice path (latent)

**Status:** OPEN (latent; related to the fixed read-path bug)
**Opened:** 2026-05-30 (while fixing the `appearance/abyssin_m.sat` "stream ends too soon" open bug)
**Severity:** latent — only reachable via Save▾ ▸ "Repack into source .tre" on an archive containing an
uncompressed record written with `CompressedSize=0` (the patch-archive convention, e.g. `patch_11_00.tre`).

## Background

The read paths (`TreFile.GetRecordData`, `TrePayloadResolver.TryResolve`) were fixed to read
`UncompressedSize` bytes for an uncompressed record when `CompressedSize=0` (SWG's "not compressed"
marker). See commit fixing the `abyssin_m.sat` open bug + `TreUncompressedZeroCompressedSizeTests`.

## The remaining latent bug

`TreFile.GetRecordCompressedBytes(int)` (used by `TreWriter` repack to copy untouched records byte-for-byte)
still early-returns `new byte[0]` when `rec.CompressedSize == 0`:

```csharp
if (rec.CompressedSize == 0) return new byte[0];   // wrong for an uncompressed record with real bytes
```

For an uncompressed record with `CompressedSize=0` but `UncompressedSize>0`, the real on-disk slice is
`UncompressedSize` bytes at `rec.Offset`. So a repack that includes such a record would copy ZERO bytes for
it → the repacked archive drops that record's content → corruption (and the new TOC's offsets shift).

## Fix sketch (needs TreWriter coordination + tests)

1. In `GetRecordCompressedBytes`, compute the on-disk slice length as
   `rec.Compressor == 0 ? rec.UncompressedSize : rec.CompressedSize` (mirror the read-path fix).
2. Verify `TreWriter.Repack` writes a CONSISTENT TOC for the copied slice — i.e. when it copies N
   uncompressed bytes, the new record's `CompressedSize`/`UncompressedSize`/`Compressor` fields must agree
   (simplest: normalize an uncompressed record to `CompressedSize = UncompressedSize`, `Compressor = 0`,
   which both old and new readers handle). Do NOT copy the original `CompressedSize=0` verbatim alongside N
   real bytes.
3. Add a repack regression: build an archive with a `CompressedSize=0` uncompressed record (reuse
   `TreFileFixtures.BuildUncompressedRecordWithZeroCompressedSize`), edit a DIFFERENT record, repack, reopen,
   and assert the zero-compressed-size record's payload survives byte-for-byte.

## Why deferred

The user-reported bug was the read/open path (now fixed + tested). The repack path is gated and rarer;
fixing it correctly needs TreWriter analysis + its own acceptance tests, so it is tracked separately to keep
the open-bug fix focused and well-tested.
