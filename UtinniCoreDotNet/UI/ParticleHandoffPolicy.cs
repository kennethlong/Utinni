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
    /// Pure decision policy for the TRE Browser → Particle Editor hand-off (15-06, PROD-W2-PRT).
    /// Extracted to the framework (no WinForms / no TheJawaToolboxDotNet dependency) so the visibility +
    /// content gates are unit-testable, mirroring <see cref="OtHandoffPolicy"/>,
    /// <see cref="DatatableHandoffPolicy"/> and the <c>SingletonFormClosePolicy</c> precedent.
    ///
    /// <para><b>Why this exists:</b> unlike object templates (every type is an <c>.iff</c>), a particle
    /// effect HAS a single canonical source extension — <c>.prt</c> — carrying a <c>FORM PEFT</c> root.
    /// So the cheap visibility gate offers the item for any resolvable <c>.prt</c>, and the click-time
    /// content gate (<see cref="IsParticlePayload"/>) confirms the bytes actually sniff as
    /// <c>FORM PEFT</c> before opening — hiding the menu item (or surfacing a clean message) instead of
    /// letting the typed parse throw. The sniff NEVER throws (defensive — returns false on
    /// malformed/truncated input).</para>
    /// </summary>
    public static class ParticleHandoffPolicy
    {
        /// <summary>
        /// Whether the TRE Browser should SHOW "Open in Particle Editor" for a leaf entry. True when the
        /// payload is resolvable (not enumerate-only) AND the entry is a <c>.prt</c> (the cheap
        /// path-based gate; the actual <c>FORM PEFT</c> structure is re-checked on click via
        /// <see cref="IsParticlePayload"/>, since the payload is not resolved at context-menu-open time).
        /// </summary>
        public static bool ShouldOfferParticleEditor(string logicalPath, bool enumerateOnly)
        {
            if (enumerateOnly) return false;
            if (string.IsNullOrEmpty(logicalPath)) return false;

            string ext = Path.GetExtension(logicalPath);
            return string.Equals(ext, ".prt", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Click-time content gate: a cheap, defensive byte-level sniff for the <c>FORM PEFT</c> root
        /// (bytes[0..4] == "FORM", bytes[8..12] == "PEFT"). Mirrors
        /// <see cref="DatatableHandoffPolicy.IsDtiiPayload"/> — it does NOT run the full typed codec
        /// (the editor's open path does that, degrading to raw-preserve on unknown versions); it only
        /// confirms the entry is genuinely a particle effect so the click handler can surface a clean
        /// message otherwise. Never throws — any short/null/garbage input returns false.
        /// </summary>
        public static bool IsParticlePayload(byte[] payload)
        {
            if (payload == null || payload.Length < 12) return false;
            try
            {
                // Top-level chunk must be a FORM whose type tag (bytes[8..12]) is "PEFT".
                if (payload[0] != (byte)'F' || payload[1] != (byte)'O' || payload[2] != (byte)'R' || payload[3] != (byte)'M')
                {
                    return false;
                }
                return payload[8] == (byte)'P' && payload[9] == (byte)'E' && payload[10] == (byte)'F' && payload[11] == (byte)'T';
            }
            catch (Exception)
            {
                // Defensive: the sniff must never throw (it gates a context-menu click).
                return false;
            }
        }
    }
}
