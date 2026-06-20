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

using System;
using System.IO;

namespace UtinniCoreDotNet.UI
{
    /// <summary>
    /// Pure decision policy for the TRE Browser → Effects (ClientEffect) Editor hand-off (22-04,
    /// PROD-W2-CFX-01). Extracted to the framework (no WinForms / no TheJawaToolboxDotNet dependency) so the
    /// visibility + content gates are unit-testable, mirroring <see cref="ParticleHandoffPolicy"/>,
    /// <see cref="OtHandoffPolicy"/> and <see cref="DatatableHandoffPolicy"/>.
    ///
    /// <para><b>Why this exists:</b> a client effect HAS a single canonical source extension — <c>.cef</c> —
    /// carrying a <c>FORM CLEF</c> root. So the cheap visibility gate offers the item for any resolvable
    /// <c>.cef</c>, and the optional content sniff (<see cref="IsClientEffectPayload"/>) confirms the bytes
    /// actually look like <c>FORM CLEF</c>. The editor's own open path is the authoritative reader and
    /// DEGRADES (raw/hex) on unknown versions or truncation rather than throwing, so the click hand-off does
    /// not require the sniff to pass — but the sniff is provided (and never throws) for callers that want a
    /// clean pre-open gate, matching the Particle precedent.</para>
    /// </summary>
    public static class EffectHandoffPolicy
    {
        /// <summary>
        /// Whether the TRE Browser should SHOW "Open in Effects Editor" for a leaf entry. True when the
        /// payload is resolvable (not enumerate-only) AND the entry is a <c>.cef</c> (the cheap path-based
        /// gate; the FORM CLEF structure is verified — and degraded, never hard-failed — by the editor on
        /// click, since the payload is not resolved at context-menu-open time).
        /// </summary>
        public static bool ShouldOfferEffectsEditor(string logicalPath, bool enumerateOnly)
        {
            if (enumerateOnly) return false;
            if (string.IsNullOrEmpty(logicalPath)) return false;

            string ext = Path.GetExtension(logicalPath);
            return string.Equals(ext, ".cef", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Content sniff: a cheap, defensive byte-level check for the <c>FORM CLEF</c> root (bytes[0..4] ==
        /// "FORM", bytes[8..12] == "CLEF"). Mirrors <see cref="ParticleHandoffPolicy.IsParticlePayload"/> —
        /// it does NOT run the full typed codec (the editor's open path does that, degrading to raw-preserve
        /// on unknown versions); it only confirms the entry is genuinely a client effect. Never throws — any
        /// short/null/garbage input returns false.
        /// </summary>
        public static bool IsClientEffectPayload(byte[] payload)
        {
            if (payload == null || payload.Length < 12) return false;
            try
            {
                // Top-level chunk must be a FORM whose type tag (bytes[8..12]) is "CLEF".
                if (payload[0] != (byte)'F' || payload[1] != (byte)'O' || payload[2] != (byte)'R' || payload[3] != (byte)'M')
                {
                    return false;
                }
                return payload[8] == (byte)'C' && payload[9] == (byte)'L' && payload[10] == (byte)'E' && payload[11] == (byte)'F';
            }
            catch (Exception)
            {
                // Defensive: the sniff must never throw (it gates a context-menu click).
                return false;
            }
        }
    }
}
