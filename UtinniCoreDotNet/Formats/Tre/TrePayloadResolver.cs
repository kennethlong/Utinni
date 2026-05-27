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
using System.IO;
using System.IO.Compression;

namespace UtinniCoreDotNet.Formats.Tre
{
    /// <summary>
    /// The shared on-demand single-payload resolver consumed by BOTH the CLI and the browser.
    /// ONE pinned contract — <see cref="TryResolve"/> — so the read path is identical on both
    /// sides (no second throwing overload). An enumerate-only entry resolves to false/null
    /// WITHOUT attempting any decode.
    /// </summary>
    public static class TrePayloadResolver
    {
        private const int MaxBlockSize = 256 * 1024 * 1024;

        /// <summary>
        /// Resolves a single descriptor's payload bytes. Returns false (with <paramref name="payload"/>
        /// null) for an <see cref="TreEntryDescriptor.EnumerateOnly"/> entry WITHOUT decoding; returns
        /// true with the decompressed bytes for a readable entry, opening
        /// <see cref="TreEntryDescriptor.ResolvedArchivePath"/> at the descriptor's archive-local
        /// offset. Throws <see cref="TreParseException"/> on a path-traversal attempt or a malformed
        /// payload (these are integrity failures, not the enumerate-only case).
        /// </summary>
        public static bool TryResolve(TreEntryDescriptor d, out byte[] payload)
        {
            payload = null;

            if (d.EnumerateOnly)
            {
                return false; // encrypted/unverified payload — caller shows an enumerate-only banner.
            }

            if (string.IsNullOrEmpty(d.ResolvedArchivePath))
            {
                return false;
            }

            // T-07-05: confirm the resolved archive path stays within the master-index base dir
            // (reject ".." / rooted tree-file names that would escape it). Loose per-archive
            // descriptors (TreeFileIndex < 0) point at a real archive path directly — skip the
            // containment check for those.
            if (d.TreeFileIndex >= 0 && !string.IsNullOrEmpty(d.MasterIndexBaseDir))
            {
                if (!string.IsNullOrEmpty(d.TreeFileName) &&
                    (d.TreeFileName.Contains("..") || Path.IsPathRooted(d.TreeFileName)))
                {
                    throw new TreParseException(TreParseError.Truncated,
                        "Refusing to resolve tree-file name '" + d.TreeFileName + "' (path traversal).");
                }
                string baseFull = Path.GetFullPath(d.MasterIndexBaseDir);
                string resolvedFull = Path.GetFullPath(d.ResolvedArchivePath);
                if (!resolvedFull.StartsWith(EnsureTrailingSep(baseFull), StringComparison.OrdinalIgnoreCase))
                {
                    throw new TreParseException(TreParseError.Truncated,
                        "Refusing to resolve '" + resolvedFull + "' outside the master-index base directory.");
                }
            }

            if (!File.Exists(d.ResolvedArchivePath))
            {
                return false;
            }

            if (d.CompressedLength < 0 || d.ArchiveLocalOffset < 0)
            {
                throw new TreParseException(TreParseError.NegativeLength,
                    "Descriptor for '" + d.Path + "' has a negative offset/length.");
            }

            byte[] raw = new byte[d.CompressedLength];
            using (var fs = new FileStream(d.ResolvedArchivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (d.ArchiveLocalOffset > fs.Length - d.CompressedLength)
                {
                    throw new TreParseException(TreParseError.Truncated,
                        "Descriptor for '" + d.Path + "' points past the end of '" + d.ResolvedArchivePath + "'.");
                }
                fs.Seek(d.ArchiveLocalOffset, SeekOrigin.Begin);
                int total = 0;
                while (total < raw.Length)
                {
                    int n = fs.Read(raw, total, raw.Length - total);
                    if (n == 0) break;
                    total += n;
                }
                if (total != raw.Length)
                {
                    throw new TreParseException(TreParseError.Truncated,
                        "Short read resolving '" + d.Path + "' (" + total + " of " + raw.Length + " bytes).");
                }
            }

            payload = Inflate(raw, d.Compressor, d.Length, d.Path);
            return true;
        }

        private static string EnsureTrailingSep(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return dir;
            char last = dir[dir.Length - 1];
            if (last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar) return dir;
            return dir + Path.DirectorySeparatorChar;
        }

        /// <summary>
        /// Inflates per compressor (0=none, 1=raw deflate, 2=zlib RFC1950). Mirrors the TreFile
        /// inflate contract: zlib headers are validated (%31) and the 4-byte Adler trailer dropped;
        /// failures raise InvalidZlibTrailer/Truncated; output is capped at 256 MB.
        /// </summary>
        private static byte[] Inflate(byte[] raw, int compressor, int declaredLength, string pathForError)
        {
            if (compressor == 0)
            {
                return raw;
            }

            byte[] body;
            if (compressor == 2)
            {
                if (raw.Length < 6)
                {
                    throw new TreParseException(TreParseError.InvalidZlibTrailer,
                        "Payload '" + pathForError + "' declared zlib but is too short for a header + Adler trailer.");
                }
                if (raw[0] != 0x78 || (((raw[0] << 8) | raw[1]) % 31 != 0))
                {
                    throw new TreParseException(TreParseError.InvalidZlibTrailer,
                        "Payload '" + pathForError + "' declared zlib but has an invalid RFC1950 header.");
                }
                body = new byte[raw.Length - 2 - 4];
                Buffer.BlockCopy(raw, 2, body, 0, body.Length);
            }
            else if (compressor == 1)
            {
                body = raw;
            }
            else
            {
                throw new TreParseException(TreParseError.UnknownCompressor,
                    "Payload '" + pathForError + "' declares unknown compressor " + compressor + ".");
            }

            int cap = declaredLength > 0 && declaredLength <= MaxBlockSize ? declaredLength : MaxBlockSize;
            try
            {
                using (var ms = new MemoryStream(body))
                using (var deflate = new DeflateStream(ms, CompressionMode.Decompress))
                using (var outMs = new MemoryStream())
                {
                    byte[] buffer = new byte[65536];
                    int total = 0;
                    while (true)
                    {
                        int n = deflate.Read(buffer, 0, buffer.Length);
                        if (n == 0) break;
                        total += n;
                        if (total > cap)
                        {
                            throw new TreParseException(TreParseError.DeflateExpansionExceedsCap,
                                "Payload '" + pathForError + "' inflate exceeded cap of " + cap + " bytes.");
                        }
                        outMs.Write(buffer, 0, n);
                    }
                    return outMs.ToArray();
                }
            }
            catch (TreParseException) { throw; }
            catch (Exception ex)
            {
                TreParseError kind = compressor == 2 ? TreParseError.InvalidZlibTrailer : TreParseError.Truncated;
                throw new TreParseException(kind,
                    "Payload '" + pathForError + "' inflate failed (" + ex.GetType().Name + ": " + ex.Message + ").");
            }
        }
    }
}
