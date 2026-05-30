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
using System.Collections.Generic;

namespace UtinniCoreDotNet.Formats.StringTable
{
    /// <summary>The result of a name-validation check: <see cref="Ok"/> plus a user-facing <see cref="Reason"/>.</summary>
    public struct StringTableNameValidation
    {
        /// <summary>True iff the proposed name passes the engine ruleset.</summary>
        public bool Ok { get; }

        /// <summary>The user-facing rejection copy, or null when <see cref="Ok"/> is true.</summary>
        public string Reason { get; }

        /// <summary>Creates a validation result.</summary>
        public StringTableNameValidation(bool ok, string reason)
        {
            Ok = ok;
            Reason = reason;
        }
    }

    /// <summary>
    /// Mutable typed string-table model — version + nextUniqueId + ordered entries + the four D-01 T4
    /// model ops (add / remove-by-name / rename / name-validate). The flat-format analog of
    /// <c>MutableDataTableDocument</c> (much simpler: no IFF compose, no per-type cells, no cascade).
    ///
    /// <para><b>D-02 canonical write:</b> the model retains insertion order for the editor; the
    /// <see cref="StringTableWriter"/> imposes the canonical on-disk order (strings id-ascending, names
    /// name-ascending ordinal) at serialize time, matching the SOE <c>std::map</c> iteration in
    /// <c>LocalizedStringTableReaderWriter.cpp:145-203</c>.</para>
    ///
    /// <para><b>D-01 name validation:</b> <see cref="ValidateName"/> ports the FULL SOE
    /// <c>LocalizedStringTable::validateStringName</c> ruleset (<c>LocalizedStringTable.cpp:456-465</c>):
    /// non-empty; first char a lowercase letter; every char in <c>[a-z0-9_+]</c>; no duplicate. The
    /// editor enforces this stricter rule on BOTH add and rename (the SOE <c>rename</c> guard is weaker
    /// — leading-digit + duplicate only).</para>
    /// </summary>
    public sealed class MutableStringTableDocument
    {
        private readonly List<MutableStringTableEntry> entries;
        private byte[] originalFileBytes;

        /// <summary>Format version (0 or 1) — preserved on re-serialize.</summary>
        public int Version { get; }

        /// <summary>The engine's <c>m_nextUniqueId</c> auto-id high-water mark.</summary>
        public uint NextUniqueId { get; private set; }

        /// <summary>The entries, in editor (insertion / load) order.</summary>
        public IList<MutableStringTableEntry> Entries { get { return entries; } }

        /// <summary>The captured original whole-file bytes (for the F2a zero-dirty short-circuit), or null.</summary>
        internal byte[] OriginalFileBytes { get { return originalFileBytes; } }

        /// <summary>Constructs a document with the given version, nextUniqueId, and entries.</summary>
        public MutableStringTableDocument(int version, uint nextUniqueId, IList<MutableStringTableEntry> initialEntries)
        {
            if (version != 0 && version != 1)
            {
                throw new StringTableParseException("Unsupported .stf version " + version + " (expected 0 or 1).");
            }

            if (initialEntries == null) throw new ArgumentNullException("initialEntries");

            Version = version;
            NextUniqueId = nextUniqueId;
            entries = new List<MutableStringTableEntry>(initialEntries);
        }

        /// <summary>Records the captured original whole-file bytes (set by the reader / by MarkSaved).</summary>
        internal void SetOriginalFileBytes(byte[] bytes)
        {
            originalFileBytes = bytes;
        }

        /// <summary>Restores the auto-id high-water mark (used by AddEntry undo/redo to keep ids stable).</summary>
        internal void SetNextUniqueId(uint value)
        {
            NextUniqueId = value;
        }

        /// <summary>True iff any entry is dirty or added (so the writer cannot take the verbatim short-circuit).</summary>
        public bool AnyDirtyOrAdded()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].IsDirty || entries[i].IsAdded) return true;
            }

            return false;
        }

        /// <summary>
        /// Adds a new entry engine-style (SOE <c>addString</c>, <c>LocalizedStringTableReaderWriter.cpp:333-355</c>):
        /// id = current <see cref="NextUniqueId"/>; auto-name <c>"{NNN}_default"</c> (the SOE
        /// <c>sprintf("%03ld_default", m_nextUniqueId)</c> format); sourceCrc = 0 (deterministic for
        /// added entries, D-02b); then <see cref="NextUniqueId"/> is incremented. Returns the new entry.
        /// </summary>
        public MutableStringTableEntry AddEntry()
        {
            uint id = NextUniqueId;
            string autoName = id.ToString("D3") + "_default"; // matches SOE "%03ld_default"
            var entry = new MutableStringTableEntry(id, autoName, string.Empty, 0u, true);
            entries.Add(entry);
            NextUniqueId = NextUniqueId + 1;
            return entry;
        }

        /// <summary>Removes the entry whose <see cref="MutableStringTableEntry.Name"/> equals <paramref name="name"/>; returns it (or null).</summary>
        public MutableStringTableEntry RemoveEntryByName(string name)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].Name, name, StringComparison.Ordinal))
                {
                    MutableStringTableEntry removed = entries[i];
                    entries.RemoveAt(i);
                    return removed;
                }
            }

            return null;
        }

        /// <summary>Renames <paramref name="entry"/> to <paramref name="newName"/>. Caller pre-validates via <see cref="ValidateName"/>.</summary>
        public void RenameKey(MutableStringTableEntry entry, string newName)
        {
            if (entry == null) throw new ArgumentNullException("entry");
            entry.Name = newName;
        }

        /// <summary>
        /// Pure predicate enforcing the FULL SOE <c>validateStringName</c> ruleset (F3b): rejects empty;
        /// requires the first char to be a lowercase letter <c>[a-z]</c> (so a leading digit AND an
        /// uppercase first char both fail); requires every char to be in <c>[a-z0-9_+]</c>; rejects a
        /// duplicate (any OTHER entry with the same name — <paramref name="exclude"/> keeps its own name
        /// on rename). Reason strings match the UI-SPEC validation copy.
        /// </summary>
        public StringTableNameValidation ValidateName(string name, MutableStringTableEntry exclude)
        {
            const string charsetReason =
                "String names use lowercase letters, digits, _ or +, and must start with a letter.";

            if (string.IsNullOrEmpty(name))
            {
                return new StringTableNameValidation(false, "String name can't be empty.");
            }

            char first = name[0];
            if (first >= '0' && first <= '9')
            {
                return new StringTableNameValidation(false, "String names can't start with a digit. Pick another key.");
            }

            if (!(first >= 'a' && first <= 'z'))
            {
                return new StringTableNameValidation(false, charsetReason);
            }

            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool valid = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '+';
                if (!valid)
                {
                    return new StringTableNameValidation(false, charsetReason);
                }
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (!ReferenceEquals(entries[i], exclude) &&
                    string.Equals(entries[i].Name, name, StringComparison.Ordinal))
                {
                    return new StringTableNameValidation(false,
                        "A string named \"" + name + "\" already exists. Pick another key.");
                }
            }

            return new StringTableNameValidation(true, null);
        }
    }
}
