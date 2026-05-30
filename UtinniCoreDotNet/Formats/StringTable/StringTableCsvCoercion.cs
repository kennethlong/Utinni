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
// String-table (.stf) CSV/TSV delta-import + export coercion (Phase 10 D-03b). The flat-format analog
// of UtinniCoreDotNet.Formats.Datatable.CsvCellCoercion — pure-managed BCL-only, xUnit-testable from CI
// without the TJT WinForms host. The diff is keyed by NAME (the .stf lookup key), NOT by row index:
// equal text → Unchanged (preserve original bytes, D-02 / SC4); differing text → Changes; a new key →
// Added (auto-id at apply). Every CSV key is validated up front via ValidateName (F8) so the caller can
// BLOCK import before anything is applied — entries are NEVER created-then-found-invalid.

using System;
using System.Collections.Generic;

namespace UtinniCoreDotNet.Formats.StringTable
{
    /// <summary>
    /// Thrown by <see cref="StringTableCsvCoercion.PlanImport"/> when a CSV input trips a DoS sanity cap
    /// (T-10-10). The caller surfaces the message in the preview modal before any allocation explosion.
    /// </summary>
    public sealed class StringTableCsvParseException : Exception
    {
        /// <summary>Creates a CSV parse/sanity exception with the given message.</summary>
        public StringTableCsvParseException(string message) : base(message)
        {
        }
    }

    /// <summary>A planned text change — an existing entry whose imported text differs.</summary>
    public sealed class StringTableEditPatch
    {
        /// <summary>The existing entry to edit (matched by name).</summary>
        public MutableStringTableEntry Entry { get; }

        /// <summary>The imported text to write into the entry.</summary>
        public string NewText { get; }

        /// <summary>Creates an edit patch.</summary>
        public StringTableEditPatch(MutableStringTableEntry entry, string newText)
        {
            Entry = entry;
            NewText = newText;
        }
    }

    /// <summary>A new key in the CSV that the import will create (its key already passed ValidateName).</summary>
    public sealed class StringTableAddedEntry
    {
        /// <summary>The new entry's validated key.</summary>
        public string Key { get; }

        /// <summary>The new entry's text.</summary>
        public string Text { get; }

        /// <summary>Creates an added-entry record.</summary>
        public StringTableAddedEntry(string key, string text)
        {
            Key = key;
            Text = text;
        }
    }

    /// <summary>A CSV row whose key is invalid (charset / empty / duplicate) — blocks the whole import (F8).</summary>
    public sealed class StringTableInvalidRow
    {
        /// <summary>0-based CSV data-row index (excludes the header).</summary>
        public int RowIndex { get; }

        /// <summary>The offending key as it appeared in the CSV.</summary>
        public string Key { get; }

        /// <summary>A human-readable reason (the ValidateName reason, or the duplicate/empty copy).</summary>
        public string Reason { get; }

        /// <summary>Creates an invalid-row record.</summary>
        public StringTableInvalidRow(int rowIndex, string key, string reason)
        {
            RowIndex = rowIndex;
            Key = key;
            Reason = reason;
        }
    }

    /// <summary>
    /// The result of planning a CSV import against a target document: the entries that change, the
    /// entries that stay byte-exact, the new keys to add, and the invalid rows that BLOCK the import (F8).
    /// </summary>
    public sealed class StringTableCsvImportPlan
    {
        /// <summary>Existing entries whose imported text differs — applied on import.</summary>
        public List<StringTableEditPatch> Changes { get; } = new List<StringTableEditPatch>();

        /// <summary>Existing entries whose imported text equals the current text — stay clean (D-02 / SC4).</summary>
        public List<MutableStringTableEntry> Unchanged { get; } = new List<MutableStringTableEntry>();

        /// <summary>New keys in the CSV the import will create (auto-id at apply).</summary>
        public List<StringTableAddedEntry> Added { get; } = new List<StringTableAddedEntry>();

        /// <summary>CSV rows whose key is invalid (charset / empty / duplicate-in-CSV) — block the import.</summary>
        public List<StringTableInvalidRow> Invalid { get; } = new List<StringTableInvalidRow>();

        /// <summary>True iff the plan carries at least one invalid row; the caller MUST block Import (F8).</summary>
        public bool HasBlockingErrors { get { return Invalid.Count > 0; } }
    }

    /// <summary>
    /// Framework-side per-entry CSV diff planner + row serializer for the string-table editor. Pure-
    /// managed, BCL-only, no WinForms / native / TJT dependency — xUnit-testable from CI. The TJT-side
    /// <c>StringTableCsvSerializer</c> does the file I/O + RFC-4180 parse and calls into this helper.
    /// </summary>
    public static class StringTableCsvCoercion
    {
        /// <summary>Row-count sanity cap (T-10-10 DoS mitigation).</summary>
        public const int MaxRows = 100000;

        /// <summary>Per-cell character-size sanity cap (64 KB, T-10-10).</summary>
        public const int MaxCellChars = 64 * 1024;

        /// <summary>
        /// Plans a CSV import against <paramref name="target"/> from a list of <c>(key, text)</c> rows.
        ///
        /// <para><b>Validation first (F8):</b> for each row, if the key is empty OR fails
        /// <see cref="MutableStringTableDocument.ValidateName"/> (charset / leading-digit / uppercase) OR
        /// duplicates an earlier CSV row's key, it is added to <see cref="StringTableCsvImportPlan.Invalid"/>
        /// and NOT diffed/added. When any row is invalid the caller MUST block apply.</para>
        ///
        /// <para><b>Diff (valid rows only):</b> find an existing entry by key; equal text → Unchanged
        /// (original bytes preserved, D-02 / SC4); differing text → Changes; no match → Added.</para>
        ///
        /// Throws <see cref="StringTableCsvParseException"/> when a DoS sanity cap is exceeded.
        /// </summary>
        public static StringTableCsvImportPlan PlanImport(
            MutableStringTableDocument target,
            IReadOnlyList<KeyValuePair<string, string>> rows)
        {
            if (target == null) throw new ArgumentNullException("target");
            if (rows == null) throw new ArgumentNullException("rows");

            if (rows.Count > MaxRows)
            {
                throw new StringTableCsvParseException("Row count exceeds sanity cap (100,000).");
            }

            var plan = new StringTableCsvImportPlan();

            // Index existing entries by name (ordinal) for the by-key diff.
            var byName = new Dictionary<string, MutableStringTableEntry>(StringComparer.Ordinal);
            for (int i = 0; i < target.Entries.Count; i++)
            {
                MutableStringTableEntry entry = target.Entries[i];
                if (!string.IsNullOrEmpty(entry.Name) && !byName.ContainsKey(entry.Name))
                {
                    byName[entry.Name] = entry;
                }
            }

            var seenCsvKeys = new HashSet<string>(StringComparer.Ordinal);

            for (int r = 0; r < rows.Count; r++)
            {
                string key = rows[r].Key ?? string.Empty;
                string text = rows[r].Value ?? string.Empty;

                if (key.Length > MaxCellChars || text.Length > MaxCellChars)
                {
                    throw new StringTableCsvParseException("Cell size exceeds sanity cap (64 KB).");
                }

                MutableStringTableEntry existing;
                byName.TryGetValue(key, out existing);

                // VALIDATION FIRST (F8). ValidateName(key, existing) checks charset / leading-digit /
                // uppercase / empty AND duplicate-against-OTHER-entries — excluding the matched entry so an
                // UPDATE to an existing key is not flagged a duplicate of itself.
                StringTableNameValidation validation = target.ValidateName(key, existing);
                if (!validation.Ok)
                {
                    plan.Invalid.Add(new StringTableInvalidRow(r, key, validation.Reason));
                    continue;
                }

                if (seenCsvKeys.Contains(key))
                {
                    plan.Invalid.Add(new StringTableInvalidRow(
                        r, key, "Duplicate key \"" + key + "\" appears more than once in the CSV."));
                    continue;
                }
                seenCsvKeys.Add(key);

                if (existing != null)
                {
                    if (string.Equals(existing.Text, text, StringComparison.Ordinal))
                    {
                        plan.Unchanged.Add(existing);
                    }
                    else
                    {
                        plan.Changes.Add(new StringTableEditPatch(existing, text));
                    }
                }
                else
                {
                    plan.Added.Add(new StringTableAddedEntry(key, text));
                }
            }

            return plan;
        }

        /// <summary>
        /// Serializes one <c>(key, text)</c> row to an RFC-4180-ish CSV line (each field quote-escaped
        /// when it contains a comma / quote / newline). Used by the TJT-side exporter so the escape rule
        /// lives framework-side (xUnit-coverable) and the export ↔ import round-trip is symmetric.
        /// </summary>
        public static string SerializeRowToCsv(string key, string text)
        {
            return CsvEscape(key) + "," + CsvEscape(text);
        }

        // RFC-4180 + Excel: wrap in double-quotes + double internal quotes when the value contains a
        // comma, a double-quote, or a newline.
        private static string CsvEscape(string value)
        {
            value = value ?? string.Empty;
            bool needsQuote = value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0
                || value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0;
            if (!needsQuote) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
