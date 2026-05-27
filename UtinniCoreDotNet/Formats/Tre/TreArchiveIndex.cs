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
// (SOE/Bootprint, All Rights Reserved). No code, comments, identifier names, or test fixtures copied
// from any reference source. Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;
using System.IO;

namespace UtinniCoreDotNet.Formats.Tre
{
    /// <summary>
    /// A resolution-complete pointer to one enumerated entry. Carries the FULL chain needed to
    /// read the payload back — not just a source-archive name. <see cref="TrePayloadResolver"/>
    /// opens <see cref="ResolvedArchivePath"/> at <see cref="ArchiveLocalOffset"/> and reads
    /// <see cref="CompressedLength"/> bytes.
    /// </summary>
    public struct TreEntryDescriptor
    {
        /// <summary>The entry's virtual path (the browse-tree key).</summary>
        public string Path;
        public uint Crc;
        /// <summary>Lineage of the archive this entry resolves into (informational).</summary>
        public TreVersion Version;
        /// <summary>
        /// Authoritative: when true, the payload is not directly decodable (V5000/V6000 single
        /// archive) and <see cref="TrePayloadResolver.TryResolve"/> returns false without decoding.
        /// </summary>
        public bool EnumerateOnly;
        /// <summary>Per-entry compressor (0=none, 1=deflate, 2=zlib).</summary>
        public int Compressor;
        /// <summary>Byte offset of the payload INSIDE <see cref="ResolvedArchivePath"/>.</summary>
        public long ArchiveLocalOffset;
        public int Length;
        public int CompressedLength;
        /// <summary>For a master index: the directory the tree-file names resolve against.</summary>
        public string MasterIndexBaseDir;
        /// <summary>For a master-index entry: index into the tree-file name list (-1 for a loose archive).</summary>
        public int TreeFileIndex;
        /// <summary>For a master-index entry: the tree-file name; for a loose archive: its file name.</summary>
        public string TreeFileName;
        /// <summary>The physical <c>.tre</c> path the resolver opens to read this entry's bytes.</summary>
        public string ResolvedArchivePath;
    }

    /// <summary>
    /// The shared browse-index facade over a master index (COT2000/SearchTOC via
    /// <see cref="CotMasterIndex"/>) or per-archive <c>.tre</c> files (via <see cref="TreFile"/>).
    /// Exposes the FLAT path list the browser filters over (paths only — no payloads) and a
    /// resolution-complete descriptor per path. BOTH the CLI and the browser consume this one
    /// path, mechanically enforcing the "one shared Formats/Tre code path" criterion.
    /// </summary>
    public sealed class TreArchiveIndex
    {
        private readonly Dictionary<string, TreEntryDescriptor> _byPath;
        private readonly List<string> _allPaths;

        /// <summary>The flat, ordered list of virtual paths (built once — the filter walks this).</summary>
        public IReadOnlyList<string> AllPaths { get { return _allPaths.AsReadOnly(); } }

        private TreArchiveIndex(List<string> allPaths, Dictionary<string, TreEntryDescriptor> byPath)
        {
            _allPaths = allPaths;
            _byPath = byPath;
        }

        public bool TryGetDescriptor(string path, out TreEntryDescriptor descriptor)
        {
            return _byPath.TryGetValue(path, out descriptor);
        }

        /// <summary>
        /// Builds an index from a master index file (COT2000/SearchTOC), a single <c>.tre</c>
        /// archive, or a directory of <c>.tre</c> archives.
        /// </summary>
        public static TreArchiveIndex Build(string clientDirOrIndexPath)
        {
            if (clientDirOrIndexPath == null)
            {
                throw new ArgumentNullException("clientDirOrIndexPath");
            }

            var allPaths = new List<string>();
            var byPath = new Dictionary<string, TreEntryDescriptor>(StringComparer.Ordinal);

            if (Directory.Exists(clientDirOrIndexPath))
            {
                foreach (string archive in Directory.GetFiles(clientDirOrIndexPath, "*.tre"))
                {
                    AddArchive(archive, allPaths, byPath);
                }
                return new TreArchiveIndex(allPaths, byPath);
            }

            if (!File.Exists(clientDirOrIndexPath))
            {
                throw new FileNotFoundException("TRE source not found: " + clientDirOrIndexPath, clientDirOrIndexPath);
            }

            if (CotMasterIndex.IsMasterIndex(clientDirOrIndexPath))
            {
                AddMasterIndex(clientDirOrIndexPath, allPaths, byPath);
            }
            else
            {
                AddArchive(clientDirOrIndexPath, allPaths, byPath);
            }
            return new TreArchiveIndex(allPaths, byPath);
        }

        private static void AddMasterIndex(string tocPath, List<string> allPaths, Dictionary<string, TreEntryDescriptor> byPath)
        {
            CotMasterIndex index = CotMasterIndex.Open(tocPath);
            string baseDir = Path.GetDirectoryName(Path.GetFullPath(tocPath)) ?? ".";

            foreach (CotEntry e in index.Entries)
            {
                string treeName = (e.TreeFileIndex >= 0 && e.TreeFileIndex < index.TreeFileNames.Count)
                    ? index.TreeFileNames[e.TreeFileIndex] : null;
                string resolved = treeName != null ? Path.Combine(baseDir, treeName) : null;

                var d = new TreEntryDescriptor
                {
                    Path = e.Path,
                    Crc = e.Crc,
                    Version = TreVersion.V6000,        // COT2000 is the Restoration lineage
                    EnumerateOnly = false,             // resolved by direct archive-local read
                    Compressor = e.Compressor,
                    ArchiveLocalOffset = e.Offset,
                    Length = e.Length,
                    CompressedLength = e.CompressedLength,
                    MasterIndexBaseDir = baseDir,
                    TreeFileIndex = e.TreeFileIndex,
                    TreeFileName = treeName,
                    ResolvedArchivePath = resolved
                };
                Add(allPaths, byPath, d);
            }
        }

        private static void AddArchive(string archivePath, List<string> allPaths, Dictionary<string, TreEntryDescriptor> byPath)
        {
            TreFile tre = TreFile.Open(archivePath);
            string fullArchive = Path.GetFullPath(archivePath);
            string baseDir = Path.GetDirectoryName(fullArchive) ?? ".";
            string fileName = Path.GetFileName(fullArchive);

            foreach (TreRecord r in tre.Records)
            {
                var d = new TreEntryDescriptor
                {
                    Path = r.Name,
                    Crc = (uint)r.Checksum,
                    Version = tre.Header.Version,
                    EnumerateOnly = tre.Header.EnumerateOnly,
                    Compressor = r.Compressor,
                    ArchiveLocalOffset = r.Offset,
                    Length = r.UncompressedSize,
                    CompressedLength = r.CompressedSize,
                    MasterIndexBaseDir = baseDir,
                    TreeFileIndex = -1,
                    TreeFileName = fileName,
                    ResolvedArchivePath = fullArchive
                };
                Add(allPaths, byPath, d);
            }
        }

        private static void Add(List<string> allPaths, Dictionary<string, TreEntryDescriptor> byPath, TreEntryDescriptor d)
        {
            if (string.IsNullOrEmpty(d.Path)) return;
            if (!byPath.ContainsKey(d.Path)) // first occurrence wins (mirrors archive mount precedence)
            {
                byPath.Add(d.Path, d);
                allPaths.Add(d.Path);
            }
        }
    }
}
