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
    /// Leaf chunk — every IFF chunk that is NOT a FORM, LIST, or CAT  container.
    ///
    /// <para>REVIEWS MEDIUM-14: PROP is classified as a leaf per EA-IFF-85 §"Property Chunks".
    /// While a PROP chunk's payload contains structured property records, the chunk itself
    /// does not contain arbitrary nested IFF chunks — it is opaque at the chunk-graph level.</para>
    ///
    /// <para><b>Immutability:</b> <see cref="Data"/> is a private copy made at construction time,
    /// so callers cannot mutate the parsed document.</para>
    /// </summary>
    public sealed class IffLeafChunk : IffChunk
    {
        /// <summary>
        /// Opaque payload bytes. A copy is made on construction so callers cannot mutate
        /// the document across read boundaries.
        /// </summary>
        public byte[] Data { get; }

        /// <summary>Constructs an IffLeafChunk with all fields supplied by the reader.</summary>
        public IffLeafChunk(string typeId, int lengthBytes, string id, byte[] data)
            : base(typeId, lengthBytes, id)
        {
            // Copy on construction — callers cannot mutate across read boundaries.
            if (data == null || data.Length == 0)
            {
                Data = new byte[0];
            }
            else
            {
                Data = new byte[data.Length];
                System.Array.Copy(data, Data, data.Length);
            }
        }
    }
}
