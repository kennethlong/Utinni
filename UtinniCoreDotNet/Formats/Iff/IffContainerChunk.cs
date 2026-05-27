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

using System.Collections.Generic;

namespace UtinniCoreDotNet.Formats.Iff
{
    /// <summary>
    /// Container chunk (FORM, LIST, or CAT  per EA-IFF-85).
    ///
    /// <para>REVIEWS MEDIUM-14: PROP is NOT a container — it is classified as
    /// <see cref="IffLeafChunk"/>. The container set is exactly { "FORM", "LIST", "CAT " }.</para>
    ///
    /// <para>Container payload: a 4-char sub-type tag followed by a sequence of zero or more
    /// child chunks. Children are parsed recursively by <see cref="IffReader"/>.</para>
    /// </summary>
    public sealed class IffContainerChunk : IffChunk
    {
        /// <summary>
        /// 4-character sub-type identifier read from the first 4 bytes of the container's payload.
        /// For example, a <c>FORM WSNP</c> chunk has <c>TypeId == "FORM"</c> and
        /// <c>SubTypeId == "WSNP"</c>.
        /// </summary>
        public string SubTypeId { get; }

        /// <summary>Ordered list of direct child chunks, in the order they appear on disk.</summary>
        public IReadOnlyList<IffChunk> Children { get; }

        /// <summary>Constructs an IffContainerChunk with all fields supplied by the reader.</summary>
        public IffContainerChunk(string typeId, int lengthBytes, string id, long offsetBytes, string subTypeId, IReadOnlyList<IffChunk> children)
            : base(typeId, lengthBytes, id, offsetBytes)
        {
            SubTypeId = subTypeId;
            Children = children;
        }
    }
}
