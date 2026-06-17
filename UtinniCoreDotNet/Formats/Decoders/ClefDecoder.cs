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
// ClientEffect (.iff / FORM CLEF) detection. Understood by reading swg-client-v2
// .../clientGame/.../ClientEffectTemplate.cpp (SOE/Bootprint, All Rights Reserved): the root chunk is
// a FORM whose sub-type is CLEF. Only the on-disk layout was studied — no code, comments, identifier
// names, or test fixtures copied from any reference source. Implementation original to Utinni under MIT.

using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Formats.Decoders
{
    /// <summary>
    /// Cheap structural sniff for the <c>decode-iff</c> dispatcher (mirrors
    /// <see cref="TgenDecoder.LooksLikeTerrain"/>): the root chunk is a container FORM whose sub-type is
    /// <c>CLEF</c>. Plan 02 may inline the same check in the decode-iff branch.
    /// </summary>
    public static class ClefDecoder
    {
        private const string RootClef = "CLEF";

        /// <summary>True when the root chunk is a container FORM whose sub-type id is <c>CLEF</c>.</summary>
        public static bool LooksLikeClientEffect(IffChunk root)
        {
            return (root as IffContainerChunk)?.SubTypeId == RootClef;
        }
    }
}
