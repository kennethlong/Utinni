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
// String-table (.stf) format understood by reading swg-client-v2 localization sources (SOE/Bootprint,
// All Rights Reserved). No code/comments/identifiers/fixtures copied. Implementation original to Utinni.

using System;
using System.IO;
using System.Text;

namespace UtinniCoreDotNet.Formats.StringTable
{
    /// <summary>
    /// Mutable string-table entry — the per-entry analog of
    /// <c>UtinniCoreDotNet.Formats.Datatable.MutableDataTableCell</c> (it mirrors that type's
    /// captured-slice + IsDirty + reserialize-fresh idiom, plus CaptureState/RestoreState as the
    /// byte-exact undo primitive).
    ///
    /// <para><b>D-02b sourceCrc-preserve-on-edit:</b> the <see cref="Name"/> / <see cref="Text"/>
    /// setters set <see cref="IsDirty"/> and null <see cref="OriginalStringBytes"/> (so the writer
    /// re-emits fresh) but DELIBERATELY preserve <see cref="SourceCrc"/> — a text-only edit keeps the
    /// original source CRC (the engine never re-authors it; recompute would break SC4 byte-stability).
    /// <c>sourceCrc</c> is an <c>int(time(0))</c> timestamp (<c>LocalizedString.cpp:85,335</c>), not a
    /// content hash, and the runtime resolve path does not consult it.</para>
    ///
    /// <para><b>F2b malformed-UTF-16 preservation:</b> an UNTOUCHED entry re-emits its captured
    /// <see cref="OriginalStringBytes"/> verbatim, so a lone-surrogate / U+FFFD text round-trips
    /// byte-identically (it is never re-encoded through <c>Encoding.Unicode</c>).</para>
    /// </summary>
    public sealed class MutableStringTableEntry
    {
        private string name;
        private string text;
        private uint sourceCrc;
        private bool isDirty;
        private bool isAdded;
        private byte[] originalStringBytes;

        /// <summary>The entry's numeric id (machine-managed; never user-set — D-01 "T4 minus id-edit").</summary>
        public uint Id { get; private set; }

        /// <summary>
        /// The entry's symbolic name (resolve key). Setting it to a different value marks the entry
        /// dirty and nulls the captured slice (forcing a fresh re-emit) while preserving SourceCrc.
        /// </summary>
        public string Name
        {
            get { return name; }
            set
            {
                if (string.Equals(name, value, StringComparison.Ordinal)) return;
                name = value;
                isDirty = true;
                originalStringBytes = null;
            }
        }

        /// <summary>
        /// The entry's localized text (UTF-16). Setting it to a different value marks the entry dirty
        /// and nulls the captured slice (forcing a fresh re-emit) while preserving SourceCrc (D-02b).
        /// </summary>
        public string Text
        {
            get { return text; }
            set
            {
                if (string.Equals(text, value, StringComparison.Ordinal)) return;
                text = value;
                isDirty = true;
                originalStringBytes = null;
            }
        }

        /// <summary>The entry's source CRC (an <c>int(time(0))</c> timestamp; preserved on edit per D-02b).</summary>
        public uint SourceCrc { get { return sourceCrc; } }

        /// <summary>True iff this entry has been edited since load (drives fresh-vs-verbatim emit).</summary>
        public bool IsDirty { get { return isDirty; } }

        /// <summary>True iff this entry was added in-editor (never had on-disk original bytes).</summary>
        public bool IsAdded { get { return isAdded; } }

        /// <summary>
        /// The captured original string-block byte slice (id + sourceCrc + charCount + UTF-16 text)
        /// for an untouched entry, or null once the entry is edited / for an added entry. Re-emitted
        /// verbatim by the writer for byte-exactness (F2b).
        /// </summary>
        internal byte[] OriginalStringBytes { get { return originalStringBytes; } }

        /// <summary>Constructs a freshly-loaded entry carrying its captured original string-block slice.</summary>
        internal MutableStringTableEntry(uint id, uint sourceCrc, string text, byte[] capturedSlice)
        {
            Id = id;
            this.sourceCrc = sourceCrc;
            this.text = text;
            this.name = null;
            this.originalStringBytes = capturedSlice;
            this.isDirty = false;
            this.isAdded = false;
        }

        /// <summary>Constructs a freshly-ADDED entry (no original bytes; starts dirty/added).</summary>
        internal MutableStringTableEntry(uint id, string name, string text, uint sourceCrc, bool added)
        {
            Id = id;
            this.name = name;
            this.text = text;
            this.sourceCrc = sourceCrc;
            this.originalStringBytes = null;
            this.isDirty = true;
            this.isAdded = added;
        }

        /// <summary>Sets the symbolic name at load WITHOUT marking dirty (join step in FromBytes).</summary>
        internal void SetLoadedName(string loadedName)
        {
            name = loadedName;
        }

        /// <summary>
        /// Re-serializes this entry's CURRENT string-block bytes (id + sourceCrc + charCount + UTF-16
        /// text) and adopts them as the new <see cref="OriginalStringBytes"/> baseline, then clears the
        /// dirty/added bits. After this call the entry re-emits verbatim from the just-saved bytes until
        /// the next edit (the per-entry half of <c>StringTableEditController.MarkSaved</c>, F10).
        /// </summary>
        internal void RebaselineAfterSave()
        {
            originalStringBytes = SerializeStringBlock();
            isDirty = false;
            isAdded = false;
        }

        /// <summary>Serializes this entry's string-block record (id + sourceCrc + charCount + UTF-16LE text).</summary>
        internal byte[] SerializeStringBlock()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(Id);
                bw.Write(sourceCrc);
                bw.Write((uint)(text == null ? 0 : text.Length)); // charCount = UTF-16 code units
                if (!string.IsNullOrEmpty(text))
                {
                    bw.Write(Encoding.Unicode.GetBytes(text)); // UTF-16LE, verbatim (no unfubar)
                }

                bw.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Immutable snapshot of an entry's mutable state, captured by <see cref="CaptureState"/> and
        /// restored atomically by <see cref="RestoreState"/> (the byte-exact undo primitive — restoring
        /// re-attaches the exact original slice + dirty bit, bypassing the setters' slice-nulling).
        /// </summary>
        internal struct EntryState
        {
            internal readonly string Name;
            internal readonly string Text;
            internal readonly uint SourceCrc;
            internal readonly bool IsDirty;
            internal readonly bool IsAdded;
            internal readonly byte[] OriginalStringBytes;

            internal EntryState(string name, string text, uint sourceCrc, bool isDirty, bool isAdded, byte[] originalStringBytes)
            {
                Name = name;
                Text = text;
                SourceCrc = sourceCrc;
                IsDirty = isDirty;
                IsAdded = isAdded;
                OriginalStringBytes = originalStringBytes;
            }
        }

        /// <summary>Captures a snapshot of all mutable fields (the originalSlice reference is copied).</summary>
        internal EntryState CaptureState()
        {
            return new EntryState(name, text, sourceCrc, isDirty, isAdded, originalStringBytes);
        }

        /// <summary>
        /// Restores a previously-captured snapshot DIRECTLY (bypassing the <see cref="Name"/> /
        /// <see cref="Text"/> setters' slice-nulling), re-attaching the exact original byte slice +
        /// dirty/added bits so the writer re-emits the original bytes verbatim. This is the undo/revert
        /// primitive that makes the SC4 byte-exact undo invariant achievable.
        /// </summary>
        internal void RestoreState(EntryState state)
        {
            name = state.Name;
            text = state.Text;
            sourceCrc = state.SourceCrc;
            isDirty = state.IsDirty;
            isAdded = state.IsAdded;
            originalStringBytes = state.OriginalStringBytes;
        }
    }
}
