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

using System;

namespace UtinniCoreDotNet.Formats.Iff
{
    /// <summary>
    /// Document provenance discriminated union — closes 08-REVIEWS HIGH-2 "design hole" by giving
    /// 08-04 (open path), 08-05 (TRE hand-off + degraded fallback), 08-06 (live-patch guard), and
    /// 08-07 (repack guard) one well-typed origin descriptor to gate save modes on.
    ///
    /// <para><b>FOUR sealed cases (exhaustive):</b></para>
    /// <list type="bullet">
    ///   <item><see cref="LooseFile"/> — opened from a loose <c>.iff</c> on disk; enables Save-in-place
    ///         + Save-As + Loose-Override + (if live client present) Live-Patch.</item>
    ///   <item><see cref="TreArchive"/> — opened from a <c>.tre</c> archive via the TRE Browser; enables
    ///         Save-As + Loose-Override + Repack + (if live client present) Live-Patch.</item>
    ///   <item><see cref="ClientMemory"/> — opened from a live mapped client buffer; enables Save-As
    ///         + Loose-Override + (if same-length) Live-Patch. NOTE: no current Phase-8 open path
    ///         constructs a <see cref="ClientMemory"/> instance; 08-06's live-patch menu remains
    ///         DISABLED pending a follow-up phase to wire a discovery path (08-REVIEWS HIGH-2(b)).</item>
    ///   <item><see cref="Unknown"/> — degraded-fallback sentinel constructed by 08-05's TRE Browser
    ///         hand-off when <c>recordIndex</c> cannot be resolved from
    ///         <c>descriptor.ArchiveLocalOffset</c>. All Save modes that pattern-match against the
    ///         three populated cases stay disabled (checker W-3); Save-As is always enabled (it does
    ///         not pattern-match a populated case — see 08-05).</item>
    /// </list>
    ///
    /// <para><b>W-3 contract:</b> downstream Save-mode gates use <c>src is OpenSource.LooseFile</c> /
    /// <c>TreArchive</c> / <c>ClientMemory</c> pattern matches; <see cref="Unknown"/> evaluates FALSE
    /// against all three, so SaveInPlace / Repack / LivePatch / LooseOverride naturally stay disabled
    /// on the degraded fallback. Save-As does NOT pattern-match a populated case and remains enabled
    /// for <see cref="Unknown"/>.</para>
    /// </summary>
    public abstract class OpenSource
    {
        /// <summary>True iff this is a <see cref="LooseFile"/>.</summary>
        public bool IsLooseFile { get { return this is LooseFile; } }

        /// <summary>True iff this is a <see cref="TreArchive"/>.</summary>
        public bool IsTreArchive { get { return this is TreArchive; } }

        /// <summary>True iff this is a <see cref="ClientMemory"/>.</summary>
        public bool IsClientMemory { get { return this is ClientMemory; } }

        /// <summary>True iff this is the <see cref="Unknown"/> sentinel.</summary>
        public bool IsUnknown { get { return this is Unknown; } }

        // Private ctor: only the four nested concrete classes (in this same file) can extend.
        private protected OpenSource() { }

        // ─────────────────────────────────────────────────────────────────────
        // Case 1: LooseFile
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// IFF opened from a loose <c>.iff</c> file on disk.
        /// </summary>
        public sealed class LooseFile : OpenSource, IEquatable<LooseFile>
        {
            /// <summary>Absolute file path the document was loaded from.</summary>
            public string Path { get; }

            /// <summary>Constructs a LooseFile provenance descriptor.</summary>
            public LooseFile(string path)
            {
                if (path == null) throw new ArgumentNullException("path");
                Path = path;
            }

            /// <inheritdoc/>
            public bool Equals(LooseFile other)
            {
                if (ReferenceEquals(other, null)) return false;
                if (ReferenceEquals(other, this)) return true;
                // 08-REVIEW WR-04: Windows file paths are case-insensitive (NTFS default + the project
                // is Windows-only per MEMORY). "C:\swg\foo.iff" and "c:\SWG\foo.iff" reference the
                // same file; Equals + GetHashCode must agree on that.
                return string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase);
            }

            /// <inheritdoc/>
            public override bool Equals(object obj)
            {
                return Equals(obj as LooseFile);
            }

            /// <inheritdoc/>
            public override int GetHashCode()
            {
                // 08-REVIEW WR-04: must agree with Equals (above) — case-insensitive hash so two
                // case-different but otherwise-identical paths bucket the same in dictionaries.
                return Path == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Path);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Case 2: TreArchive
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// IFF opened from a packed <c>.tre</c> archive via the TRE Browser.
        /// </summary>
        public sealed class TreArchive : OpenSource, IEquatable<TreArchive>
        {
            /// <summary>Absolute path to the source <c>.tre</c> archive.</summary>
            public string TrePath { get; }

            /// <summary>Zero-based record index inside the archive's TOC.</summary>
            public int RecordIndex { get; }

            /// <summary>The archive-relative logical path of the record (e.g. <c>datatables/foo.iff</c>).</summary>
            public string LogicalPath { get; }

            /// <summary>Constructs a TreArchive provenance descriptor.</summary>
            public TreArchive(string trePath, int recordIndex, string logicalPath)
            {
                if (trePath == null) throw new ArgumentNullException("trePath");
                if (recordIndex < 0) throw new ArgumentOutOfRangeException("recordIndex", "RecordIndex must be non-negative.");
                if (logicalPath == null) throw new ArgumentNullException("logicalPath");
                TrePath = trePath;
                RecordIndex = recordIndex;
                LogicalPath = logicalPath;
            }

            /// <inheritdoc/>
            public bool Equals(TreArchive other)
            {
                if (ReferenceEquals(other, null)) return false;
                if (ReferenceEquals(other, this)) return true;
                // 08-REVIEW WR-04: TrePath is a Windows file path; LogicalPath is the archive-internal
                // TOC path (SWG treats these case-insensitively). Both compare ordinal-ignore-case so
                // GetHashCode + Equals agree across casing.
                return string.Equals(TrePath, other.TrePath, StringComparison.OrdinalIgnoreCase)
                    && RecordIndex == other.RecordIndex
                    && string.Equals(LogicalPath, other.LogicalPath, StringComparison.OrdinalIgnoreCase);
            }

            /// <inheritdoc/>
            public override bool Equals(object obj)
            {
                return Equals(obj as TreArchive);
            }

            /// <inheritdoc/>
            public override int GetHashCode()
            {
                // 08-REVIEW WR-04: case-insensitive hash matching Equals above.
                int h = TrePath == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(TrePath);
                h = unchecked((h * 397) ^ RecordIndex);
                h = unchecked((h * 397) ^ (LogicalPath == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(LogicalPath)));
                return h;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Case 3: ClientMemory
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// IFF opened from a live mapped client buffer.
        ///
        /// <para><b>Note:</b> no current Phase-8 open path constructs a <see cref="ClientMemory"/>
        /// instance — 08-04 / 08-05 do NOT produce one. 08-06's live-patch menu remains DISABLED
        /// in this phase pending a follow-up phase to wire a discovery path (08-REVIEWS HIGH-2(b)).
        /// The type ships so the discriminated union is exhaustive and 08-06's bounds gate
        /// (<c>LivePatchValidator</c>) can be implemented and unit-tested ahead of the open path.</para>
        /// </summary>
        public sealed class ClientMemory : OpenSource, IEquatable<ClientMemory>
        {
            /// <summary>Base address of the mapped IFF bytes inside the live client process.</summary>
            public IntPtr TargetAddr { get; }

            /// <summary>
            /// Length of the originally mapped region, in bytes. 08-06's same-length-only live-patch
            /// gate refuses any rewritten IFF whose serialized length differs from this value.
            /// </summary>
            public int OriginalMappedLength { get; }

            /// <summary>The logical path of the mapped asset (e.g. <c>datatables/foo.iff</c>).</summary>
            public string LogicalPath { get; }

            /// <summary>Constructs a ClientMemory provenance descriptor.</summary>
            public ClientMemory(IntPtr targetAddr, int originalMappedLength, string logicalPath)
            {
                if (originalMappedLength < 0)
                    throw new ArgumentOutOfRangeException("originalMappedLength", "OriginalMappedLength must be non-negative.");
                if (logicalPath == null) throw new ArgumentNullException("logicalPath");
                TargetAddr = targetAddr;
                OriginalMappedLength = originalMappedLength;
                LogicalPath = logicalPath;
            }

            /// <inheritdoc/>
            public bool Equals(ClientMemory other)
            {
                if (ReferenceEquals(other, null)) return false;
                if (ReferenceEquals(other, this)) return true;
                // 08-REVIEW WR-04: LogicalPath is the SWG-internal asset path (case-insensitive).
                return TargetAddr == other.TargetAddr
                    && OriginalMappedLength == other.OriginalMappedLength
                    && string.Equals(LogicalPath, other.LogicalPath, StringComparison.OrdinalIgnoreCase);
            }

            /// <inheritdoc/>
            public override bool Equals(object obj)
            {
                return Equals(obj as ClientMemory);
            }

            /// <inheritdoc/>
            public override int GetHashCode()
            {
                int h = TargetAddr.GetHashCode();
                h = unchecked((h * 397) ^ OriginalMappedLength);
                // 08-REVIEW WR-04: case-insensitive hash matching Equals above.
                h = unchecked((h * 397) ^ (LogicalPath == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(LogicalPath)));
                return h;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Case 4: Unknown (sealed singleton)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Degraded-fallback sentinel — constructed by 08-05's TRE Browser hand-off when
        /// <c>recordIndex</c> cannot be resolved from <c>descriptor.ArchiveLocalOffset</c> (defensive
        /// path; should not happen with a well-formed descriptor).
        ///
        /// <para><b>W-3 contract:</b> downstream Save-mode gates use <c>src is OpenSource.LooseFile</c>
        /// / <see cref="TreArchive"/> / <see cref="ClientMemory"/> pattern matches; this case matches
        /// NONE of those three, so SaveInPlace / Repack / LivePatch / LooseOverride all stay disabled
        /// on the degraded fallback. Save-As does NOT pattern-match a populated case and remains
        /// enabled per 08-05's tooltip copy.</para>
        ///
        /// <para>Constructed via the static <see cref="Instance"/> property (private constructor; no
        /// fields beyond the discriminator).</para>
        /// </summary>
        public sealed class Unknown : OpenSource
        {
            // Private ctor: the only way to obtain an Unknown is via Instance below.
            private Unknown() { }

            /// <summary>
            /// The single Unknown sentinel instance — reference-equal to itself across all accesses.
            /// </summary>
            public static Unknown Instance { get; } = new Unknown();
        }
    }
}
