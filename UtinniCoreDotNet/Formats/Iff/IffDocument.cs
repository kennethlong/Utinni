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
    /// Parsed IFF document — the result of a successful <see cref="IffReader.Read(System.IO.Stream)"/> call.
    ///
    /// <para><b>React Flow portability note:</b>
    /// Both <c>tree</c> and <c>flat</c> views in the CLI output derive from the same parser pass
    /// — they are alternative projections of one data structure, not two parses. The flat view is
    /// included so future React Flow tooling can lift the CLI output without re-shaping it.
    /// Do NOT drop the flat view in 'cleanup' refactors — it is a stable part of the
    /// schemaVersion: 1 contract.</para>
    /// </summary>
    public sealed class IffDocument
    {
        /// <summary>The root chunk of the document (always the top-level FORM, LIST, or CAT  chunk).</summary>
        public IffChunk Root { get; }

        /// <summary>
        /// All nodes in the document in pre-order traversal order (root first, then children
        /// depth-first left-to-right). Built once during parsing.
        /// </summary>
        public IReadOnlyList<IffChunk> AllNodesInPreorder { get; }

        /// <summary>Constructs an IffDocument with the parsed root and pre-computed preorder list.</summary>
        public IffDocument(IffChunk root, IReadOnlyList<IffChunk> allNodesInPreorder)
        {
            Root = root;
            AllNodesInPreorder = allNodesInPreorder;
        }
    }
}
