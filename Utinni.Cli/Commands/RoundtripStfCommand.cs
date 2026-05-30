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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Formats.StringTable;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("roundtrip-stf", HelpText = "Parse -> optional edit -> serialize -> re-parse a .stf; assert byte-exact identity on untouched entries.")]
    public class RoundtripStfOptions
    {
        [Value(0, MetaName = "path", Required = true, HelpText = "Path to the .stf file.")]
        public string Path { get; set; }

        [Option("edit-text", HelpText = "KEY=VALUE: set entry KEY's text to VALUE (single text edit).")]
        public string EditText { get; set; }
    }

    /// <summary>
    /// CF-02 max-harness for the flat <c>.stf</c> write path — the string-table analog of
    /// <see cref="RoundtripTabCommand"/>. Loads a <c>.stf</c>, optionally applies a single
    /// <c>--edit-text KEY=VALUE</c> mutation, serializes via Plan 10-01's
    /// <see cref="StringTableWriter"/>, re-parses, and reports byte-exact identity in a sorted-key JSON
    /// envelope. This is the automated CI gate for Phase 10 Success Criterion 4 ("non-ASCII round-trips
    /// cleanly") and the no-corruption guarantee.
    ///
    /// <para><b>Comparison granularity:</b>
    ///   <list type="bullet">
    ///     <item>no-mutate -> <c>whole-file</c> vs the ORIGINAL bytes. The 10-01 F2a zero-dirty
    ///           short-circuit returns the captured original bytes verbatim for ANY untouched file
    ///           (canonical OR non-canonical), so an untouched round-trip is always whole-file exact.
    ///           (This supersedes the plan's F6 "canonical-normalized for non-canonical no-mutate"
    ///           contract, which assumed no short-circuit — F2a makes normalize-on-save apply only
    ///           when an entry is actually dirty.)</item>
    ///     <item><c>--edit-text</c> -> <c>per-entry-slice</c>: every UNTOUCHED entry's string-block
    ///           bytes must be byte-identical (paired by id, so canonical ordering is irrelevant), AND
    ///           the edited entry's <c>sourceCrc</c> is preserved (D-02b).</item>
    ///   </list></para>
    ///
    /// <para><b>Exit codes (mirroring <see cref="RoundtripTabCommand"/>):</b>
    ///   0 success; 1 UsageError; 2 StringTableParseException / IOException; 3 FileNotFound. Generic
    ///   <see cref="System.Exception"/> is intentionally NOT caught.</para>
    /// </summary>
    public static class RoundtripStfCommand
    {
        public static int Run(RoundtripStfOptions o)
        {
            bool hasEdit = !string.IsNullOrEmpty(o.EditText);
            string editKey = null;
            string editValue = null;

            if (hasEdit)
            {
                int eq = o.EditText.IndexOf('=');
                if (eq <= 0)
                {
                    return JsonOutput.EmitError("roundtrip-stf", "UsageError",
                        "--edit-text must be KEY=VALUE with a non-empty KEY, e.g. greeting=Hello; got '" + o.EditText + "'.", exitCode: 1);
                }

                editKey = o.EditText.Substring(0, eq);
                editValue = o.EditText.Substring(eq + 1);
            }

            if (!File.Exists(o.Path))
            {
                return JsonOutput.EmitError("roundtrip-stf", "FileNotFound",
                    ".stf file not found: " + o.Path, exitCode: 3);
            }

            try
            {
                byte[] loadedBytes = File.ReadAllBytes(o.Path);
                StringTableDocument doc = StringTableDocument.FromBytes(loadedBytes);
                MutableStringTableDocument mut = doc.Mutable;

                string mutationDescription = "none";
                uint editedPreCrc = 0;
                bool sourceCrcPreserved = false;

                if (hasEdit)
                {
                    MutableStringTableEntry target = mut.Entries.FirstOrDefault(
                        e => string.Equals(e.Name, editKey, StringComparison.Ordinal));
                    if (target == null)
                    {
                        return JsonOutput.EmitError("roundtrip-stf", "StringTableParseException",
                            "--edit-text key '" + editKey + "' was not found in the string table.", exitCode: 2);
                    }

                    editedPreCrc = target.SourceCrc;
                    target.Text = editValue; // setter preserves SourceCrc (D-02b)
                    mutationDescription = "edit-text(" + editKey + ")";
                    sourceCrcPreserved = target.SourceCrc == editedPreCrc;
                }

                byte[] roundtrippedBytes = StringTableWriter.Serialize(mut);
                StringTableDocument rtDoc = StringTableDocument.FromBytes(roundtrippedBytes); // re-parse validity check

                bool bytesEqualUntouched;
                string granularity;
                JToken firstMismatch = JValue.CreateNull();

                if (!hasEdit)
                {
                    // The F2a short-circuit (10-01) returns the original bytes verbatim for any
                    // untouched file, so an untouched round-trip is unconditionally whole-file exact.
                    bytesEqualUntouched = loadedBytes.Length == roundtrippedBytes.Length
                        && loadedBytes.SequenceEqual(roundtrippedBytes);
                    granularity = "whole-file";
                }
                else
                {
                    granularity = "per-entry-slice";
                    bytesEqualUntouched = CompareUntouchedEntrySlices(
                        loadedBytes, roundtrippedBytes, editKey, out firstMismatch);
                }

                var result = new JObject
                {
                    ["bytesEqualUntouched"] = bytesEqualUntouched,
                    ["comparisonGranularity"] = granularity,
                    ["entries"] = rtDoc.Mutable.Entries.Count,
                    ["firstMismatch"] = firstMismatch,
                    ["mutationApplied"] = mutationDescription,
                    ["nextUniqueId"] = rtDoc.NextUniqueId,
                    ["source"] = o.Path,
                    ["version"] = rtDoc.Version
                };

                if (hasEdit)
                {
                    result["sourceCrcPreserved"] = sourceCrcPreserved;
                }

                return JsonOutput.EmitSuccess("roundtrip-stf", result);
            }
            catch (StringTableParseException ex)
            {
                return JsonOutput.EmitError("roundtrip-stf", "StringTableParseException", ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("roundtrip-stf", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception is intentionally NOT caught.
        }

        // Per-entry string-block-slice comparison after an --edit-text mutation. Re-parses BOTH byte
        // arrays fresh, indexes entries by id, and compares every UNTOUCHED entry's captured original
        // string-block bytes (malformed UTF-16 included). The edited entry is excluded by name.
        private static bool CompareUntouchedEntrySlices(
            byte[] loadedBytes, byte[] roundtrippedBytes, string editKey, out JToken firstMismatch)
        {
            firstMismatch = JValue.CreateNull();

            MutableStringTableDocument loadedFresh = StringTableDocument.FromBytes(loadedBytes).Mutable;
            MutableStringTableDocument rtFresh = StringTableDocument.FromBytes(roundtrippedBytes).Mutable;

            var rtById = new Dictionary<uint, MutableStringTableEntry>();
            foreach (MutableStringTableEntry e in rtFresh.Entries)
            {
                rtById[e.Id] = e;
            }

            foreach (MutableStringTableEntry loaded in loadedFresh.Entries)
            {
                if (string.Equals(loaded.Name, editKey, StringComparison.Ordinal))
                {
                    continue; // the edited entry is expected to differ
                }

                MutableStringTableEntry rt;
                if (!rtById.TryGetValue(loaded.Id, out rt))
                {
                    firstMismatch = new JObject { ["id"] = loaded.Id, ["reason"] = "missing-after-roundtrip" };
                    return false;
                }

                byte[] a = loaded.GetOriginalStringBytesForCompare();
                byte[] b = rt.GetOriginalStringBytesForCompare();
                if (a.Length != b.Length || !a.SequenceEqual(b))
                {
                    firstMismatch = new JObject { ["id"] = loaded.Id, ["reason"] = "slice-differs" };
                    return false;
                }
            }

            return true;
        }
    }
}
