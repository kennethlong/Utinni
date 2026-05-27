/**
 * MIT License
 *
 * Copyright (c) 2020 Philip Klatt
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
**/
// Format understood by reading swg-client-v2/src/engine/shared/library/sharedFile/src/shared/{TreeFile,Iff}.{h,cpp}
// (SOE/Bootprint, All Rights Reserved) and the EA-IFF-85 public standard. No code,
// comments, identifier names, or test fixtures copied from any reference source.
// Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace UtinniCoreDotNet.Formats.Iff
{
    /// <summary>
    /// Pure-C# read-only IFF chunk reader conforming to EA-IFF-85.
    ///
    /// <para><b>Iter-4 HIGH-1 algorithm (each error-kind is reachable via a distinct code path):</b></para>
    /// <list type="number">
    ///   <item>NegativeLength — chunk length field is negative.</item>
    ///   <item>ChunkLengthExceedsCap — chunk length exceeds 64 MB (T-04-DoS).</item>
    ///   <item>NestedChunkOverflow — nested chunk's declared end exceeds parent's end.
    ///         NOT applied to the top-level chunk (top-level FORM IS the file).</item>
    ///   <item>Streaming-read EOF — <c>BinaryReader.ReadBytes(n)</c> returns fewer than n bytes
    ///         → throws <see cref="IffParseError.Truncated"/>.</item>
    ///   <item>Pad byte after an odd-length chunk — DETECTED, not assumed (07-04a real-asset
    ///         reversal of the REVIEWS MEDIUM-11 STRICT rule): a trailing 0x00 is consumed as the
    ///         EA-IFF-85 pad; a printable TypeID or the parent boundary means SWG wrote no pad
    ///         (real datatables omit it). A missing pad is NO LONGER a Truncated error.</item>
    /// </list>
    ///
    /// <para><b>Container set (REVIEWS MEDIUM-14 — EA-IFF-85):</b>
    /// { "FORM", "LIST", "CAT " } only. PROP is a leaf.</para>
    ///
    /// <para><b>Parser purity:</b> This class contains NO JSON serialisation references,
    /// no console output calls, and no file write calls.
    /// All JSON output lives in <c>Utinni.Cli.Commands.InspectIffCommand</c>.</para>
    ///
    /// <para><b>React Flow portability note:</b>
    /// Both <c>tree</c> and <c>flat</c> views in the CLI output derive from the same parser pass
    /// — they are alternative projections of one data structure, not two parses. The flat view is
    /// included so future React Flow tooling can lift the CLI output without re-shaping it.
    /// Do NOT drop the flat view in 'cleanup' refactors — it is a stable part of the
    /// schemaVersion: 1 contract.</para>
    /// </summary>
    public static class IffReader
    {
        // T-04-DoS: absolute cap per chunk payload (64 MB).
        private const int MaxChunkSize = 64 * 1024 * 1024;

        /// <summary>
        /// Container TypeID set per EA-IFF-85.
        /// REVIEWS MEDIUM-14: PROP is NOT included — PROP is a leaf chunk.
        /// The trailing space in "CAT " is required (EA-IFF-85 §3.1).
        /// </summary>
        private static readonly HashSet<string> ContainerTypeIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "FORM",
            "LIST",
            "CAT "
        };

        // ─────────────────────────────────────────────────────────────────────
        // Public factory methods
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Reads an IFF document from <paramref name="path"/> and returns the parsed result.
        /// </summary>
        public static IffDocument Read(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Read(fs);
            }
        }

        /// <summary>
        /// Reads an IFF document from <paramref name="input"/> and returns the parsed result.
        /// This is the testability seam — unit tests pass a <see cref="MemoryStream"/>.
        /// </summary>
        public static IffDocument Read(Stream input)
        {
            // WR-02: no endianness guard here. The IFF parser reads all multi-byte
            // integers via ReadInt32Be (manual shift-and-OR over byte[4]), which is
            // host-endianness-independent — every read is explicitly big-endian by
            // construction. The analogous guard in TreFile.Open IS load-bearing
            // because that parser uses BinaryReader.ReadInt32() (little-endian) for
            // header fields and would silently mis-parse on a big-endian host.

            long streamLength = input.Length;
            var preorderAccumulator = new List<IffChunk>();

            using (var br = new BinaryReader(input, Encoding.ASCII, leaveOpen: true))
            {
                // iter-4 HIGH-1: top-level FORM IS the file; parentEnd is stream.Length.
                // No explicit file-bound check on the top-level chunk.
                IffChunk root = ReadChunk(br, parentEnd: streamLength, parentIdPrefix: "", ordinalWithinParent: 0,
                    preorderAccumulator: preorderAccumulator, isTopLevel: true);

                return new IffDocument(root, preorderAccumulator.AsReadOnly());
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Core recursive reader
        // ─────────────────────────────────────────────────────────────────────

        private static IffChunk ReadChunk(
            BinaryReader br,
            long parentEnd,
            string parentIdPrefix,
            int ordinalWithinParent,
            List<IffChunk> preorderAccumulator,
            bool isTopLevel)
        {
            long chunkStart = br.BaseStream.Position;

            // ── TypeID (4 bytes) ─────────────────────────────────────────────
            byte[] typeIdBytes = br.ReadBytes(4);
            if (typeIdBytes.Length < 4)
            {
                throw new IffParseException(IffParseError.Truncated,
                    "Unexpected end of stream reading chunk TypeID at position " + chunkStart + ".");
            }

            // Validate: all four bytes must be printable ASCII (0x20–0x7E).
            foreach (byte b in typeIdBytes)
            {
                if (b < 0x20 || b > 0x7E)
                {
                    throw new IffParseException(IffParseError.MalformedFourCc,
                        "Chunk TypeID at offset " + chunkStart + " contains non-printable-ASCII byte 0x"
                        + b.ToString("X2", CultureInfo.InvariantCulture) + ".");
                }
            }

            string typeId = Encoding.ASCII.GetString(typeIdBytes);

            // ── Length field (4 bytes, big-endian signed) ────────────────────
            int length = ReadInt32Be(br);

            // ── Bounds checks: FIXED ORDER per iter-4 HIGH-1 ────────────────

            // 1. NegativeLength
            if (length < 0)
            {
                throw new IffParseException(IffParseError.NegativeLength,
                    "Chunk TypeID='" + typeId + "' at offset " + chunkStart + " has negative length " + length + ".");
            }

            // 2. ChunkLengthExceedsCap (T-04-DoS)
            if (length > MaxChunkSize)
            {
                throw new IffParseException(IffParseError.ChunkLengthExceedsCap,
                    "Chunk TypeID='" + typeId + "' at offset " + chunkStart + " declares length " + length
                    + " which exceeds the 64 MB cap (" + MaxChunkSize + " bytes). T-04-DoS.");
            }

            // 3. NestedChunkOverflow — ONLY for nested chunks (iter-4 HIGH-1).
            //    Top-level FORM IS the file; if its declared length exceeds stream.Length - 8,
            //    the streaming read of its (or its children's) payload will hit EOF → Truncated.
            if (!isTopLevel)
            {
                long declaredEnd = br.BaseStream.Position + length;
                if (declaredEnd > parentEnd)
                {
                    throw new IffParseException(IffParseError.NestedChunkOverflow,
                        "Chunk TypeID='" + typeId + "' at offset " + chunkStart
                        + " declares length " + length
                        + "; declared end " + declaredEnd + " exceeds parent end " + parentEnd + ".");
                }
            }

            // ── Build stable id ───────────────────────────────────────────────
            string idTrimmed = typeId.TrimEnd();

            // ── Dispatch: container vs leaf ──────────────────────────────────
            if (ContainerTypeIds.Contains(typeId))
            {
                return ReadContainerChunk(br, parentEnd, parentIdPrefix, ordinalWithinParent,
                    preorderAccumulator, chunkStart, typeId, idTrimmed, length);
            }
            else
            {
                return ReadLeafChunk(br, parentEnd, parentIdPrefix, ordinalWithinParent,
                    preorderAccumulator, chunkStart, typeId, idTrimmed, length);
            }
        }

        private static IffContainerChunk ReadContainerChunk(
            BinaryReader br,
            long parentEnd,
            string parentIdPrefix,
            int ordinalWithinParent,
            List<IffChunk> preorderAccumulator,
            long chunkStart,
            string typeId,
            string idTrimmed,
            int length)
        {
            // Container path: read 4-char sub-type ID, then child chunks.
            byte[] subTypeBytes = br.ReadBytes(4);
            if (subTypeBytes.Length < 4)
            {
                throw new IffParseException(IffParseError.Truncated,
                    "Unexpected end of stream reading sub-type of container chunk TypeID='"
                    + typeId + "' at offset " + chunkStart + ".");
            }

            foreach (byte b in subTypeBytes)
            {
                if (b < 0x20 || b > 0x7E)
                {
                    throw new IffParseException(IffParseError.MalformedFourCc,
                        "Container sub-type at offset " + (br.BaseStream.Position - 4)
                        + " contains non-printable-ASCII byte 0x" + b.ToString("X2", CultureInfo.InvariantCulture) + ".");
                }
            }

            string subTypeId = Encoding.ASCII.GetString(subTypeBytes);
            string subTypeTrimmed = subTypeId.TrimEnd();

            // Build stable id for this container.
            string thisId = parentIdPrefix + idTrimmed + ":" + subTypeTrimmed + "/"
                + ordinalWithinParent.ToString(CultureInfo.InvariantCulture);

            // chunkEnd = chunkStart + 8 (header) + length (payload).
            // After reading TypeID (4) + Length (4) = 8 bytes, then SubType (4) = 12 bytes total.
            long chunkEnd = chunkStart + 8 + length;

            // Reserve a placeholder slot in the preorder accumulator for this container.
            // We insert null now and replace it after construction so children (added during
            // recursive calls below) appear AFTER this container in the list — correct preorder.
            int containerSlot = preorderAccumulator.Count;
            preorderAccumulator.Add(null); // placeholder; replaced below

            // Now read children while inside the container's range.
            var children = new List<IffChunk>();
            int childOrdinal = 0;
            string childIdPrefix = thisId + "/";

            while (br.BaseStream.Position < chunkEnd)
            {
                IffChunk child = ReadChunk(br, parentEnd: chunkEnd, parentIdPrefix: childIdPrefix,
                    ordinalWithinParent: childOrdinal, preorderAccumulator: preorderAccumulator,
                    isTopLevel: false);
                children.Add(child);
                childOrdinal++;
            }

            var container = new IffContainerChunk(typeId, length, thisId, chunkStart, subTypeId, children.AsReadOnly());

            // Patch the placeholder with the constructed container.
            preorderAccumulator[containerSlot] = container;

            return container;
        }

        private static IffLeafChunk ReadLeafChunk(
            BinaryReader br,
            long parentEnd,
            string parentIdPrefix,
            int ordinalWithinParent,
            List<IffChunk> preorderAccumulator,
            long chunkStart,
            string typeId,
            string idTrimmed,
            int length)
        {
            string thisId = parentIdPrefix + idTrimmed + ":" + idTrimmed + "/"
                + ordinalWithinParent.ToString(CultureInfo.InvariantCulture);

            byte[] payload = br.ReadBytes(length);

            // iter-4 HIGH-1: streaming-read EOF check.
            // BinaryReader.ReadBytes(n) short-reads at EOF — check the returned count.
            if (payload.Length != length)
            {
                throw new IffParseException(IffParseError.Truncated,
                    "Stream truncated mid-leaf-payload. TypeID='" + typeId + "', Declared=" + length
                    + ", Read=" + payload.Length + ".");
            }

            // Pad-byte handling (07-04a real-asset reversal of the REVIEWS MEDIUM-11 STRICT rule).
            // EA-IFF-85 pads an odd-length chunk to an even boundary with a single 0x00 byte that is
            // NOT counted in the chunk length. REAL SWG datatables, however, are written WITHOUT that
            // pad — the next chunk's printable-ASCII TypeID (or the parent/stream boundary) follows
            // immediately (see the memory note on SWG IFF no-padding). So DETECT the pad rather than
            // assume it: consume a trailing byte only when it is actually 0x00. A non-0x00 byte is the
            // next chunk's TypeID (leave it), and reaching the parent boundary means there is nothing
            // to pad. This reads BOTH padded and unpadded SWG IFF correctly; genuine mid-payload
            // truncation is still caught by the ReadBytes short-read check above.
            if (length % 2 == 1 && br.BaseStream.Position < parentEnd)
            {
                long beforePad = br.BaseStream.Position;
                int next = br.BaseStream.ReadByte();
                if (next != 0x00)
                {
                    // Not a pad byte (the next chunk's TypeID, or an unexpected EOF) — restore so the
                    // next ReadChunk sees it.
                    br.BaseStream.Position = beforePad;
                }
                // else: consumed the single 0x00 pad byte.
            }

            var leaf = new IffLeafChunk(typeId, length, thisId, chunkStart, payload);
            preorderAccumulator.Add(leaf);
            return leaf;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Reads a 32-bit big-endian signed integer from <paramref name="br"/>.
        /// Throws <see cref="IffParseException"/>(<see cref="IffParseError.Truncated"/>)
        /// if fewer than 4 bytes are available.
        /// </summary>
        private static int ReadInt32Be(BinaryReader br)
        {
            byte[] bytes = br.ReadBytes(4);
            if (bytes.Length != 4)
            {
                throw new IffParseException(IffParseError.Truncated,
                    "Unexpected end of stream reading int32 (big-endian). Read " + bytes.Length + " bytes, expected 4.");
            }

            // Assemble big-endian: MSB first.
            return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
        }
    }
}
