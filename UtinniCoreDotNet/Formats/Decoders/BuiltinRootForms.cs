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
// Implementation original to Utinni under MIT.
//
// SINGLE SOURCE OF TRUTH for the D-05 altitude rule: which root-FORM sub-types Utinni already owns with
// a BUILT-IN typed decoder. A user-definable IFF chunk template (Phase 23) must NEVER engage on a file
// whose root FORM is built-in-owned -- the built-in wins wholesale (D-05). Both DecodeIffCommand's typed
// dispatch AND TemplateResolver's altitude gate consult THIS predicate, so the two provably cannot drift
// into a divergent built-in list.

using System;
using System.Collections.Generic;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Formats.Decoders
{
    /// <summary>
    /// The canonical "does a built-in typed decoder own this file's root FORM?" predicate (D-05 altitude).
    ///
    /// <para>The set is sourced from the literal root-FORM sub-types <c>DecodeIffCommand</c> dispatches on
    /// (the <c>(doc.Root as IffContainerChunk)?.SubTypeId == "XXXX"</c> branches and the per-type decoders)
    /// PLUS the structural sniffs (<see cref="TgenDecoder.LooksLikeTerrain"/>,
    /// <see cref="ObjectTemplateDecoder.LooksLikeObjectTemplate"/>) that recognize a built-in by shape
    /// rather than by a fixed sub-type. A <c>.stf</c> string table is NOT IFF-wrapped, so it never reaches
    /// an IFF tree and is intentionally absent from this set.</para>
    ///
    /// <para><b>Why single-source:</b> if the template resolver carried its own copy of the built-in list,
    /// adding a future built-in decoder (a new Wave-2 editor) would silently let a template shadow it. By
    /// routing both the CLI dispatch and the resolver gate through this one predicate, a new built-in only
    /// has to be registered HERE.</para>
    /// </summary>
    public static class BuiltinRootForms
    {
        // Root-FORM sub-types that map DIRECTLY to a built-in typed decoder (the literal sub-type branches
        // in DecodeIffCommand). Each entry is the 4-char FORM sub-type as it appears in the IFF tree.
        //   DTII  -> DataTableDecoder (DataTableDecoder.RootSubType)
        //   PEFT  -> Particle effect codec
        //   CLEF  -> ClientEffect codec
        //   MESH/SKMG/SKTM/KFAT/CKAT -> AppearanceSummary
        //   SSHT/CSHD -> shader structure summary
        // (TGEN/PTAT and the object-template shape are recognized by the structural sniffs below, not by a
        //  literal-set membership check, because they admit wrapper/shape variants.)
        private static readonly HashSet<string> DirectSubTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            DataTableDecoder.RootSubType, // "DTII"
            "PEFT",
            "CLEF",
            "MESH", "SKMG", "SKTM", "KFAT", "CKAT",
            "SSHT", "CSHD"
        };

        /// <summary>
        /// True when <paramref name="root"/> is owned by a built-in typed decoder (D-05). The template
        /// resolver returns NO template for ANY leaf in such a file; the CLI routes it to the built-in.
        /// Returns false for a null root, a non-container root, or a FORM sub-type with no built-in.
        /// </summary>
        public static bool HasBuiltinDecoder(IffChunk root)
        {
            var container = root as IffContainerChunk;
            if (container == null) return false;

            string subType = container.SubTypeId;
            if (subType != null && DirectSubTypes.Contains(subType)) return true;

            // Shape-recognized built-ins (terrain TGEN/PTAT wrapper; object-template DERV / versioned-count).
            if (TgenDecoder.LooksLikeTerrain(root)) return true;
            if (ObjectTemplateDecoder.LooksLikeObjectTemplate(root)) return true;

            return false;
        }
    }
}
