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
using UtinniCoreDotNet.Formats.Decoders;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.UI
{
    /// <summary>
    /// Pure decision policy for the TRE Browser → Object Template Editor hand-off (Phase 11, D-10
    /// analog). Extracted to the framework (no WinForms / no TheJawaToolboxDotNet dependency) so the
    /// visibility + content gates are unit-testable, mirroring <see cref="DatatableHandoffPolicy"/> and
    /// the <c>SingletonFormClosePolicy</c> precedent.
    ///
    /// <para><b>Why this exists:</b> object templates compiled into a TRE are <c>object/**/*.iff</c>
    /// carriers (a <c>FORM &lt;type&gt;</c> with a DERV derivation form or a digit-tagged version form),
    /// NOT a single source extension. So the cheap visibility gate offers the item for any <c>.iff</c>,
    /// and the click-time content gate (<see cref="IsObjectTemplatePayload"/>) refuses a non-object-template
    /// <c>.iff</c> cleanly instead of letting the typed parse throw — the OT analog of
    /// <see cref="DatatableHandoffPolicy.IsDtiiPayload"/>.</para>
    /// </summary>
    public static class OtHandoffPolicy
    {
        /// <summary>
        /// Whether the TRE Browser should SHOW "Open in Object Template Editor" for a leaf entry. True
        /// when the payload is resolvable (not enumerate-only) AND the entry is an <c>.iff</c> (the cheap
        /// path-based gate; the actual object-template structure is re-checked on click via
        /// <see cref="IsObjectTemplatePayload"/>, since the payload is not resolved at context-menu-open
        /// time). Object templates have no single source extension (every type is an <c>.iff</c>), so the
        /// extension gate is just <c>.iff</c>; the content sniff does the real work on click.
        /// </summary>
        public static bool ShouldOfferObjectTemplateEditor(string logicalPath, bool enumerateOnly)
        {
            if (enumerateOnly) return false;
            if (string.IsNullOrEmpty(logicalPath)) return false;

            string ext = Path.GetExtension(logicalPath);
            return string.Equals(ext, ".iff", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Click-time content gate: parses the resolved payload as IFF and runs the Phase-7
        /// <see cref="ObjectTemplateDecoder.LooksLikeObjectTemplate"/> structural sniff (a FORM whose
        /// children include a DERV derivation form OR a digit-tagged version form with a 4-byte leading
        /// count chunk). Lets the click handler verify an <c>.iff</c> is genuinely an object template and
        /// surface a clean message otherwise, instead of letting the typed parse throw. Never throws —
        /// any parse failure returns false (treat as "not an object template").
        /// </summary>
        public static bool IsObjectTemplatePayload(byte[] payload)
        {
            if (payload == null || payload.Length < 12) return false;
            try
            {
                IffDocument doc;
                using (var ms = new MemoryStream(payload, writable: false))
                {
                    doc = IffReader.Read(ms);
                }
                return doc != null && doc.Root != null && ObjectTemplateDecoder.LooksLikeObjectTemplate(doc.Root);
            }
            catch (IffParseException)
            {
                return false;
            }
            catch (DecoderException)
            {
                return false;
            }
        }
    }
}
