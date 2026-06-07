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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Formats.StringTable;
using UtinniCoreDotNet.Saving;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("apply-save-stf", HelpText = "Apply ONE string-table text edit to a loose-override .stf, verify untouched entries byte-identical, then atomically commit the mutated bytes.")]
    public class ApplySaveStfOptions
    {
        [Value(0, MetaName = "relAsset", Required = true, HelpText = "Relative asset path under --root (loose-override destination).")]
        public string RelAsset { get; set; }

        [Option("root", Required = true, HelpText = "Client root for the loose-override destination; relAsset is resolved + contained under it.")]
        public string Root { get; set; }

        [Option("edit-text", HelpText = "KEY=VALUE: set entry KEY's text to VALUE (single text edit).")]
        public string EditText { get; set; }
    }

    /// <summary>
    /// Plan 14-03a Task 2 (MCP-02 / AUTH-05): the string-table member of the <c>apply-save-*</c> family.
    /// Applies EXACTLY ONE <c>--edit-text KEY=VALUE</c> mutation to a loose-override .stf, serializes via
    /// <see cref="StringTableWriter"/>, re-parses for validity, verifies byte-identity on the UNTOUCHED
    /// entries (paired by id, order-independent) AND that the edited entry's <c>sourceCrc</c> is
    /// preserved (D-02b), and — ONLY on a clean verify — atomically commits the SAME mutated bytes.
    /// Fails closed (exit 2, no write) on a failed verify.
    ///
    /// <para>Exit codes: 0 ok; 1 usage (malformed KEY=VALUE / no mutation); 2 verify-failed | missing
    /// key | parse | path-containment; 3 file-not-found.</para>
    /// </summary>
    public static class ApplySaveStfCommand
    {
        // Test-only seam (see ApplySaveTabCommand.TestPerturbSerialized).
        internal static Func<byte[], byte[]> TestPerturbSerialized;

        public static int Run(ApplySaveStfOptions o)
        {
            bool hasEdit = !string.IsNullOrEmpty(o.EditText);
            if (!hasEdit)
            {
                return JsonOutput.EmitError("apply-save-stf", "UsageError",
                    "apply-save-stf requires exactly one mutation: --edit-text KEY=VALUE.", exitCode: 1);
            }

            int eq = o.EditText.IndexOf('=');
            if (eq <= 0)
            {
                return JsonOutput.EmitError("apply-save-stf", "UsageError",
                    "--edit-text must be KEY=VALUE with a non-empty KEY, e.g. greeting=Hello; got '" + o.EditText + "'.", exitCode: 1);
            }
            string editKey = o.EditText.Substring(0, eq);
            string editValue = o.EditText.Substring(eq + 1);

            string destPath;
            try
            {
                destPath = LooseOverridePath.Resolve(o.Root, o.RelAsset);
            }
            catch (ArgumentException ex)
            {
                return JsonOutput.EmitError("apply-save-stf", "PathContainment", ex.Message, exitCode: 2);
            }

            if (!File.Exists(destPath))
            {
                return JsonOutput.EmitError("apply-save-stf", "FileNotFound",
                    "loose-override asset not found: " + destPath, exitCode: 3);
            }

            try
            {
                byte[] loadedBytes = File.ReadAllBytes(destPath);
                StringTableDocument doc = StringTableDocument.FromBytes(loadedBytes);
                MutableStringTableDocument mut = doc.Mutable;

                MutableStringTableEntry target = mut.Entries.FirstOrDefault(
                    e => string.Equals(e.Name, editKey, StringComparison.Ordinal));
                if (target == null)
                {
                    return JsonOutput.EmitError("apply-save-stf", "StringTableParseException",
                        "--edit-text key '" + editKey + "' was not found in the string table.", exitCode: 2);
                }

                uint editedPreCrc = target.SourceCrc;
                target.Text = editValue; // setter preserves SourceCrc (D-02b)
                bool sourceCrcPreserved = target.SourceCrc == editedPreCrc;

                byte[] mutatedBytes = StringTableWriter.Serialize(mut);

                if (TestPerturbSerialized != null)
                {
                    mutatedBytes = TestPerturbSerialized(mutatedBytes);
                }

                try
                {
                    StringTableDocument.FromBytes(mutatedBytes); // structural-validity re-parse
                }
                catch (StringTableParseException ex)
                {
                    return JsonOutput.EmitError("apply-save-stf", "VerifyFailed",
                        "serialized bytes failed to re-parse; nothing written: " + ex.Message, exitCode: 2);
                }

                JToken firstMismatch;
                bool bytesEqualUntouched = CompareUntouchedEntrySlices(
                    loadedBytes, mutatedBytes, editKey, out firstMismatch);

                if (!bytesEqualUntouched || !sourceCrcPreserved)
                {
                    return JsonOutput.EmitError("apply-save-stf", "VerifyFailed",
                        "untouched-region byte-identity (or sourceCrc) check failed; nothing written.", exitCode: 2);
                }

                SaveCommandIo.WriteAtomic(destPath, mutatedBytes);

                var result = new JObject
                {
                    ["backupPath"] = JValue.CreateNull(),
                    ["bytesEqualUntouched"] = true,
                    ["bytesWritten"] = mutatedBytes.Length,
                    ["comparisonGranularity"] = "per-entry-slice",
                    ["firstMismatch"] = JValue.CreateNull(),
                    ["mutationApplied"] = "edit-text(" + editKey + ")",
                    ["path"] = destPath,
                    ["sourceCrcPreserved"] = true,
                    ["validated"] = true,
                    ["written"] = true
                };
                return JsonOutput.EmitSuccess("apply-save-stf", result);
            }
            catch (StringTableParseException ex)
            {
                return JsonOutput.EmitError("apply-save-stf", "StringTableParseException", ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("apply-save-stf", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception is intentionally NOT caught.
        }

        // Per-entry string-block-slice comparison (REUSES the RoundtripStfCommand algorithm). Re-parses
        // BOTH byte arrays fresh, indexes entries by id, and compares every UNTOUCHED entry's captured
        // original string-block bytes. The edited entry is excluded by name.
        private static bool CompareUntouchedEntrySlices(
            byte[] loadedBytes, byte[] mutatedBytes, string editKey, out JToken firstMismatch)
        {
            firstMismatch = JValue.CreateNull();

            MutableStringTableDocument loadedFresh = StringTableDocument.FromBytes(loadedBytes).Mutable;
            MutableStringTableDocument rtFresh = StringTableDocument.FromBytes(mutatedBytes).Mutable;

            var rtById = new Dictionary<uint, MutableStringTableEntry>();
            foreach (MutableStringTableEntry e in rtFresh.Entries)
            {
                rtById[e.Id] = e;
            }

            foreach (MutableStringTableEntry loaded in loadedFresh.Entries)
            {
                if (string.Equals(loaded.Name, editKey, StringComparison.Ordinal)) continue;

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
