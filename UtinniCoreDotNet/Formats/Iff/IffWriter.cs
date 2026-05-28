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
using System.IO;

namespace UtinniCoreDotNet.Formats.Iff
{
    /// <summary>
    /// EA-IFF-85 serializer — the write mirror of <see cref="IffReader"/>.
    ///
    /// <para><b>Framing (mirrors the reader inverse — RESEARCH § Code Examples WriteBe32):</b></para>
    /// <list type="bullet">
    ///   <item>leaf chunk: <c>BE_u32(tag4) · BE_u32(payloadLen) · payload</c> — NO trailing pad byte
    ///         (SWG no-pad quirk; <see cref="IffReader"/> detects-but-does-not-require it).</item>
    ///   <item>container chunk: <c>BE_u32(tag4) · BE_u32(innerLen) · BE_u32(subType4) · child0 · child1 · ...</c>
    ///         where <c>innerLen = 4 + Σ child serialized sizes</c>; children are serialized first
    ///         (bottom-up roll-up per D-07).</item>
    /// </list>
    ///
    /// <para><b>Checked long arithmetic (08-REVIEWS MEDIUM-7):</b> the container roll-up accumulates
    /// child sizes as <c>long</c> in a <c>checked</c> block; before writing the BE_u32 header the
    /// total is validated against <c>int.MaxValue</c> AND the 64 MB <see cref="MaxChunkSize"/> cap
    /// (T-04-DoS, reuse of <see cref="IffReader"/>'s cap value). Overflow throws
    /// <see cref="IffParseException"/>(<see cref="IffParseError.NestedChunkOverflow"/>) — never silently
    /// wraps to a truncated/negative u32.</para>
    ///
    /// <para><b>Hybrid DOM (D-07):</b> a clean node (<c>!IsDirty</c> AND a captured slice present)
    /// re-emits its captured verbatim source-byte slice byte-for-byte; a dirty node reserializes
    /// fresh. Ancestor invalidation in <see cref="MutableIffNode"/> guarantees a clean ancestor
    /// cannot re-emit a stale slice over a dirty subtree.</para>
    /// </summary>
    public static class IffWriter
    {
        // T-04-DoS: absolute cap per chunk payload (64 MB). Reuses IffReader's value.
        internal const int MaxChunkSize = 64 * 1024 * 1024;

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Serializes the given mutable document to a fresh byte array using EA-IFF-85 framing
        /// with the SWG no-pad quirk.
        /// </summary>
        public static byte[] Write(MutableIffDocument doc)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (doc.Root == null) throw new ArgumentException("MutableIffDocument has no root.", "doc");
            using (var ms = new MemoryStream())
            {
                Write(doc, ms);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Serializes the given mutable document into the supplied output stream using EA-IFF-85
        /// framing with the SWG no-pad quirk.
        /// </summary>
        public static void Write(MutableIffDocument doc, Stream output)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (doc.Root == null) throw new ArgumentException("MutableIffDocument has no root.", "doc");
            if (output == null) throw new ArgumentNullException("output");
            WriteNode(doc.Root, output);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Core recursive writer
        // ─────────────────────────────────────────────────────────────────────

        private static void WriteNode(MutableIffNode node, Stream output)
        {
            // Hybrid-DOM verbatim re-emit: clean node with a captured slice writes its slice
            // byte-for-byte. Ancestor invalidation guarantees a dirty descendant has already
            // cleared this node's captured slice, so this branch is taken only when truly clean.
            if (!node.IsDirty)
            {
                byte[] captured = node.GetCapturedSliceInternal();
                if (captured != null)
                {
                    output.Write(captured, 0, captured.Length);
                    return;
                }
            }

            if (node.Kind == MutableIffNodeKind.Leaf)
            {
                WriteLeafFresh(node, output);
            }
            else
            {
                WriteContainerFresh(node, output);
            }
        }

        private static void WriteLeafFresh(MutableIffNode node, Stream output)
        {
            byte[] payload = node.GetPayloadInternal();
            int payloadLen = payload.Length;
            // Security V5 / T-04-DoS: refuse to serialize a payload above the 64 MB cap.
            if (payloadLen < 0 || payloadLen > MaxChunkSize)
            {
                throw new IffParseException(
                    IffParseError.ChunkLengthExceedsCap,
                    "Leaf TypeID='" + node.TypeId + "' payload length " + payloadLen
                    + " exceeds the 64 MB cap (" + MaxChunkSize + " bytes). T-04-DoS.");
            }
            WriteFourCc(output, node.TypeId);
            WriteBe32(output, (uint)payloadLen);
            if (payloadLen > 0)
            {
                output.Write(payload, 0, payloadLen);
            }
            // NO trailing pad byte (SWG no-pad quirk).
        }

        private static void WriteContainerFresh(MutableIffNode node, Stream output)
        {
            // First, serialize the children bytes into a buffer so we can roll up innerLen.
            // Build the inner payload = sub-type (4) + concatenated child bytes.
            //
            // Use a long accumulator under `checked` to refuse silent wrap-around on the child-size
            // sum or the +4 (sub-type) addition. Pre-write the validated u32 header.
            byte[] subTypeBytes = FourCcToBytes(node.SubTypeId);

            // Serialize children into a child buffer. We have to write them somewhere before
            // we know the total length — write into a MemoryStream and then copy.
            using (var childBuf = new MemoryStream())
            {
                foreach (var child in node.Children)
                {
                    WriteNode(child, childBuf);
                }

                long childTotal = childBuf.Length;
                long innerLen;
                checked
                {
                    innerLen = 4L + childTotal;
                }

                // Pre-write validation: refuse overflow before emitting the header.
                if (innerLen > int.MaxValue || innerLen > MaxChunkSize || innerLen < 0)
                {
                    throw new IffParseException(
                        IffParseError.NestedChunkOverflow,
                        "Container TypeID='" + node.TypeId + "' sub-type='" + node.SubTypeId
                        + "' would serialize with innerLen=" + innerLen
                        + " which exceeds int.MaxValue (" + int.MaxValue
                        + ") and/or the 64 MB cap (" + MaxChunkSize + " bytes). T-04-DoS / checked-arithmetic overflow.");
                }

                WriteFourCc(output, node.TypeId);
                WriteBe32(output, (uint)innerLen);
                output.Write(subTypeBytes, 0, 4);
                // Copy the serialized children bytes directly out.
                byte[] childBytes = childBuf.ToArray();
                output.Write(childBytes, 0, childBytes.Length);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Big-endian primitives (inverse of IffReader.ReadInt32Be)
        // ─────────────────────────────────────────────────────────────────────

        private static void WriteBe32(Stream s, uint v)
        {
            s.WriteByte((byte)((v >> 24) & 0xFF));
            s.WriteByte((byte)((v >> 16) & 0xFF));
            s.WriteByte((byte)((v >> 8) & 0xFF));
            s.WriteByte((byte)(v & 0xFF));
        }

        private static void WriteFourCc(Stream s, string fourCc)
        {
            byte[] bytes = FourCcToBytes(fourCc);
            s.Write(bytes, 0, 4);
        }

        private static byte[] FourCcToBytes(string fourCc)
        {
            // FourCCs are ASCII; the parser refuses non-printable so we know the input is safe.
            // We do NOT call Encoding.ASCII to avoid a marginal substitution surface — straight char-to-byte.
            if (fourCc == null) throw new ArgumentNullException("fourCc");
            if (fourCc.Length != 4) throw new ArgumentException("FourCC must be exactly 4 characters.", "fourCc");
            var b = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                char c = fourCc[i];
                if (c < 0x20 || c > 0x7E)
                    throw new ArgumentException("FourCC must contain only printable ASCII.", "fourCc");
                b[i] = (byte)c;
            }
            return b;
        }
    }
}
