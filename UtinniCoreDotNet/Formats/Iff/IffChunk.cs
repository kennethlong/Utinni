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

namespace UtinniCoreDotNet.Formats.Iff
{
    /// <summary>
    /// Abstract base class for all IFF chunk nodes.
    ///
    /// <para>Read-only IFF chunk node. Phase 4 (CLI inspection) per DEC-C4 carve-out;
    /// Phase 8 owns read+write for editor use.</para>
    ///
    /// <para>Concrete sub-types:</para>
    /// <list type="bullet">
    ///   <item><see cref="IffContainerChunk"/> — for FORM, LIST, CAT  (EA-IFF-85 containers).</item>
    ///   <item><see cref="IffLeafChunk"/> — for everything else, INCLUDING PROP per EA-IFF-85
    ///         (REVIEWS MEDIUM-14: PROP is not a container).</item>
    /// </list>
    /// </summary>
    public abstract class IffChunk
    {
        /// <summary>
        /// 4-character ASCII TypeID (space-padded at the right if shorter, e.g. "CAT " with trailing
        /// space). Preserved verbatim from the on-disk representation.
        /// </summary>
        public string TypeId { get; }

        /// <summary>
        /// Declared payload length in bytes, as read from the on-disk big-endian length field.
        /// Does NOT include the 8-byte chunk header or the optional word-alignment pad byte.
        /// </summary>
        public int LengthBytes { get; }

        /// <summary>
        /// Stable path-based identifier for this chunk node, unique within its document.
        /// Format: <c>FORM:WSNP/0</c> for the root; nested nodes extend the path, e.g.
        /// <c>FORM:WSNP/0/FORM:OBJS/3</c>. The root is always ordinal 0.
        /// </summary>
        public string Id { get; }

        /// <summary>Protected constructor for sub-types.</summary>
        protected IffChunk(string typeId, int lengthBytes, string id)
        {
            TypeId = typeId;
            LengthBytes = lengthBytes;
            Id = id;
        }
    }
}
