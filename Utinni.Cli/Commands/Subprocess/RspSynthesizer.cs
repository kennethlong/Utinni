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

using System;
using System.IO;
using System.Text;
using UtinniCoreDotNet.Formats.Tre;
using UtinniCoreDotNet.Saving;

namespace Utinni.Cli.Commands.Subprocess
{
    /// <summary>
    /// Plan 13-02: synthesizes a TreeFileBuilder <c>.rsp</c> response file from a real <c>.tre</c>
    /// via the Phase-7 reader — the engine that makes AUTH-04 <c>build-tre</c> byte-exact a TESTABLE
    /// hypothesis (D-06). The builder's <c>-r &lt;rsp&gt;</c> consumes <b>builder-format</b> lines
    /// <c>&lt;diskPath&gt; @ &lt;treePath&gt;</c> (disk-first), with <c>@u</c> marking an uncompressed
    /// file.
    ///
    /// <para><b>Determinism (D-06, RESEARCH):</b> the builder CRC-sorts the TOC itself
    /// (LessFileEntryCrcNameCompare), so synthesis MUST NOT pre-sort — it emits one line per record
    /// in <see cref="TreFile.Records"/> order, which IS the original data-block order
    /// (responseFileOrder) the builder preserves for byte-identity of the data region.</para>
    ///
    /// <para><b>Security (T-13-05):</b> each record's tree-path is untrusted; the on-disk extract
    /// target is resolved through <see cref="LooseOverridePath.Resolve"/> so a <c>..</c>/rooted
    /// tree-path cannot escape <paramref name="extractDir"/>.</para>
    /// </summary>
    public static class RspSynthesizer
    {
        /// <summary>
        /// Extracts each record of <paramref name="treFile"/> to a disk path under
        /// <paramref name="extractDir"/> and returns the builder-format <c>.rsp</c> text (one line
        /// per record, in <see cref="TreFile.Records"/> order; never pre-sorted). The production
        /// helper decides the <c>@u</c> marker from the PUBLIC
        /// <see cref="TreRecord.CompressionKind"/> (<c>"none"</c> → uncompressed).
        /// </summary>
        /// <param name="treFile">A path-backed <see cref="TreFile"/> (opened via
        /// <see cref="TreFile.Open(string)"/> so <see cref="TreFile.GetRecordData"/> can read payloads).</param>
        /// <param name="extractDir">Directory the record payloads are written under; created if absent.</param>
        public static string Synthesize(TreFile treFile, string extractDir)
        {
            if (treFile == null) throw new ArgumentNullException("treFile");
            if (string.IsNullOrEmpty(extractDir)) throw new ArgumentException("extractDir must not be empty.", "extractDir");

            string resolvedRoot = Path.GetFullPath(extractDir);
            Directory.CreateDirectory(resolvedRoot);

            var sb = new StringBuilder();
            for (int i = 0; i < treFile.Records.Count; i++)
            {
                TreRecord rec = treFile.Records[i];

                // T-13-05: resolve the untrusted tree-path under the extract root (rejects ../rooted).
                string diskPath = LooseOverridePath.Resolve(resolvedRoot, rec.Name);

                Directory.CreateDirectory(Path.GetDirectoryName(diskPath));
                File.WriteAllBytes(diskPath, treFile.GetRecordData(i));

                // Builder-format, disk-first: "<diskPath> @ <treePath>" (or "@u" uncompressed).
                // The tree-path is emitted VERBATIM (forward-slash logical path the archive stored).
                string marker = string.Equals(rec.CompressionKind, "none", StringComparison.Ordinal) ? "@u" : "@";
                sb.Append(diskPath).Append(' ').Append(marker).Append(' ').Append(rec.Name).Append('\n');
            }

            return sb.ToString();
        }
    }
}
