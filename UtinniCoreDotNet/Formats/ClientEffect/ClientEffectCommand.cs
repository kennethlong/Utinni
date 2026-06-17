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
// CLEF command-chunk read view. Field tables understood by reading swg-client-v2
// .../clientGame/.../ClientEffectTemplate.cpp (SOE/Bootprint, All Rights Reserved): payload scalars
// are LITTLE-endian; CPAP/FFBK read string THEN scalars in that exact order. Only the on-disk layout
// was studied — no code, comments, identifier names, or test fixtures copied from any reference
// source. Implementation original to Utinni under MIT.

using System.Text;
using UtinniCoreDotNet.Formats.Decoders;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Formats.ClientEffect
{
    /// <summary>
    /// Per-command typed read view over one flat command-leaf chunk of a recognized-version CLEF.
    ///
    /// <para>Carries the command <see cref="Tag"/> (CPAP/PSND/CLGT/CAMS/FFBK or the raw 4cc), a
    /// back-reference to its <see cref="Leaf"/>, a <see cref="StableId"/> set by the caller from
    /// <see cref="MutableIffDocument.DeriveStableId"/> — the SAME ordinal-path id terrain surfaces as
    /// <c>node.StableIdPath</c> (REVIEWS #1; this view does NOT invent its own index/path scheme), and
    /// an <see cref="IsRaw"/> flag (true for unknown tags, truncated/undecodable known tags, AND
    /// residual-byte leaves). Typed accessors expose exactly the fields the file version defines.</para>
    ///
    /// <para><b>Consume-all-bytes guard (REVIEWS #7):</b> a typed decode reads every field in the SOE
    /// order then asserts the cursor consumed ALL payload bytes (<c>Remaining == 0</c>); a command
    /// with residual/trailing bytes raw-falls-back rather than silently dropping slack.</para>
    /// </summary>
    public sealed class ClientEffectCommand
    {
        // The on-disk string encoding for CLEF C-strings.
        private static readonly Encoding StringEncoding = Encoding.ASCII;

        private ClientEffectCommand(string tag, MutableIffNode leaf, bool isRaw, string version)
        {
            Tag = tag;
            Leaf = leaf;
            IsRaw = isRaw;
            Version = version;
        }

        /// <summary>The 4-char command tag (CPAP/PSND/CLGT/CAMS/FFBK, or the raw 4cc for an unknown tag).</summary>
        public string Tag { get; }

        /// <summary>The captured command-leaf node — the authoritative bytes for byte-exact re-emit.</summary>
        public MutableIffNode Leaf { get; }

        /// <summary>
        /// True when this command could not be typed (unknown tag, truncated/undecodable known tag, or
        /// residual trailing bytes) and is preserved verbatim — typed accessors are not populated. The
        /// leaf is never edited, so it re-emits its captured slice byte-exact.
        /// </summary>
        public bool IsRaw { get; }

        /// <summary>The owning CLEF version tag (e.g. "0003") — drives the CPAP version-variant decode.</summary>
        public string Version { get; }

        /// <summary>
        /// The ordinal-path stable id for this command leaf, set by the model from
        /// <see cref="MutableIffDocument.DeriveStableId"/> (REVIEWS #1). The CLI/editor address a
        /// command without a live reference via this id.
        /// </summary>
        public string StableId { get; internal set; }

        // ── typed accessors (populated only when !IsRaw) ────────────────────────

        /// <summary>CPAP appearanceTemplateName / PSND soundTemplateName / FFBK forceFeedbackFile.</summary>
        public string StringValue { get; private set; }

        /// <summary>CPAP/CLGT/CAMS timeInSeconds-family floats + FFBK range, keyed by field name.</summary>
        public float[] Floats { get; private set; }

        /// <summary>CLGT r,g,b channels (3 entries) when Tag == CLGT; otherwise null.</summary>
        public byte[] Bytes { get; private set; }

        /// <summary>CPAP softParticleTerminate (v0002+) — null below v0002.</summary>
        public bool? SoftParticleTerminate { get; private set; }

        /// <summary>FFBK iterations — null for non-FFBK commands.</summary>
        public int? Int32Value { get; private set; }

        // ── factory + decode ────────────────────────────────────────────────────

        /// <summary>Builds a raw (verbatim-preserved) command view for the given leaf.</summary>
        public static ClientEffectCommand Raw(MutableIffNode leaf, string version)
        {
            return new ClientEffectCommand(leaf != null ? leaf.TypeId : "????", leaf, true, version);
        }

        /// <summary>
        /// Decodes a command leaf for a recognized-version CLEF. Reads the typed fields in the exact
        /// SOE order (string THEN scalars for CPAP/FFBK) and asserts the cursor consumed ALL bytes
        /// (REVIEWS #7); ANY shortfall, an unknown tag, or trailing bytes throws
        /// <see cref="DecoderException"/> — which the model catches PER COMMAND to degrade to a raw
        /// view (REVIEWS #4). It never over-reads (the cursor is bounds-checked).
        /// </summary>
        public static ClientEffectCommand Decode(MutableIffNode leaf, string version)
        {
            if (leaf == null) throw new System.ArgumentNullException("leaf");

            string tag = leaf.TypeId;
            byte[] payload = leaf.GetPayloadCopy();
            var cursor = new IffPayloadCursor(payload);

            var cmd = new ClientEffectCommand(tag, leaf, false, version);

            switch (tag)
            {
                case "CPAP":
                    cmd.DecodeCpap(cursor, version);
                    break;
                case "PSND":
                    cmd.StringValue = cursor.ReadCString(StringEncoding);
                    break;
                case "CLGT":
                    cmd.DecodeClgt(cursor);
                    break;
                case "CAMS":
                    cmd.DecodeCams(cursor);
                    break;
                case "FFBK":
                    cmd.DecodeFfbk(cursor);
                    break;
                default:
                    // Unknown command tag — not typeable. Surface as a DecoderException so the model's
                    // per-command catch degrades it to a raw view (REVIEWS #4 / #13).
                    throw new DecoderException(DecoderError.UnexpectedForm,
                        "Unknown CLEF command tag '" + tag + "'.");
            }

            // Consume-all-bytes guard (REVIEWS #7): a typed decode MUST land exactly at end-of-payload.
            // Residual/trailing bytes degrade to raw rather than silently surviving.
            if (cursor.Remaining != 0)
            {
                throw new DecoderException(DecoderError.Truncated,
                    "Command '" + tag + "' left " + cursor.Remaining
                    + " residual byte(s) after a typed decode; raw-falling-back (REVIEWS #7).");
            }

            return cmd;
        }

        private void DecodeCpap(IffPayloadCursor cursor, string version)
        {
            int versionNum = NumericVersion(version);
            StringValue = cursor.ReadCString(StringEncoding); // appearanceTemplateName
            float timeInSeconds = cursor.ReadFloatLe();
            if (versionNum >= 2)
            {
                SoftParticleTerminate = cursor.ReadInt8() != 0;
            }
            if (versionNum >= 3)
            {
                float minScale = cursor.ReadFloatLe();
                float maxScale = cursor.ReadFloatLe();
                float minRate = cursor.ReadFloatLe();
                float maxRate = cursor.ReadFloatLe();
                Floats = new[] { timeInSeconds, minScale, maxScale, minRate, maxRate };
            }
            else
            {
                Floats = new[] { timeInSeconds };
            }
        }

        private void DecodeClgt(IffPayloadCursor cursor)
        {
            byte r = cursor.ReadInt8();
            byte g = cursor.ReadInt8();
            byte b = cursor.ReadInt8();
            float timeInSeconds = cursor.ReadFloatLe();
            float constantAtt = cursor.ReadFloatLe();
            float linearAtt = cursor.ReadFloatLe();
            float quadraticAtt = cursor.ReadFloatLe();
            float range = cursor.ReadFloatLe();
            Bytes = new[] { r, g, b };
            Floats = new[] { timeInSeconds, constantAtt, linearAtt, quadraticAtt, range };
        }

        private void DecodeCams(IffPayloadCursor cursor)
        {
            float magnitude = cursor.ReadFloatLe();
            float frequency = cursor.ReadFloatLe();
            float time = cursor.ReadFloatLe();
            float falloff = cursor.ReadFloatLe();
            Floats = new[] { magnitude, frequency, time, falloff };
        }

        private void DecodeFfbk(IffPayloadCursor cursor)
        {
            StringValue = cursor.ReadCString(StringEncoding); // forceFeedbackFile
            Int32Value = cursor.ReadInt32Le();                // iterations
            float range = cursor.ReadFloatLe();
            Floats = new[] { range };
        }

        private static int NumericVersion(string version)
        {
            int n;
            if (version != null && int.TryParse(version, out n)) return n;
            return 0;
        }
    }
}
