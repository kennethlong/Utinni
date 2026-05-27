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
using UtinniCoreDotNet.Formats.Decoders;
using UtinniCoreDotNet.Formats.Iff;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("decode-iff", HelpText = "Decode a typed IFF asset (datatable / string-table / object-template) to JSON.")]
    public class DecodeIffOptions
    {
        [Value(0, MetaName = "path", Required = true, HelpText = "Path to the .iff/.tab/.stf file.")]
        public string Path { get; set; }
    }

    /// <summary>
    /// Reads an IFF asset via the shared <see cref="IffReader"/>, dispatches on its root FORM tag to
    /// the matching per-type decoder in <c>UtinniCoreDotNet.Formats.Decoders</c>, and emits the
    /// decoded model as a sorted-key schemaVersion:1 JSON envelope via <see cref="JsonOutput"/>.
    ///
    /// <para>This verb is the golden-test harness for the decoders — the TRE Browser detail pane
    /// (07-04b) calls the SAME decoders over the SAME <see cref="IffReader"/> output, so the CLI and
    /// the UI never drift (success criterion #4; Pitfall 7).</para>
    /// </summary>
    public static class DecodeIffCommand
    {
        public static int Run(DecodeIffOptions o)
        {
            // FileNotFound check: exit 3 (D-02), mirroring inspect-iff.
            if (!File.Exists(o.Path))
            {
                return JsonOutput.EmitError("decode-iff", "FileNotFound",
                    "IFF file not found: " + o.Path, exitCode: 3);
            }

            try
            {
                var doc = IffReader.Read(o.Path);
                object result;
                if (!TryDecode(doc, o.Path, out result, out string unsupportedTag))
                {
                    return JsonOutput.EmitError("decode-iff", "UnsupportedForm",
                        "No structured decoder for root form '" + unsupportedTag + "'.", exitCode: 2);
                }
                return JsonOutput.EmitSuccess("decode-iff", result);
            }
            catch (DecoderException ex)
            {
                return JsonOutput.EmitError("decode-iff", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IffParseException ex)
            {
                return JsonOutput.EmitError("decode-iff", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("decode-iff", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception intentionally NOT caught — unexpected types bubble for diagnosis.
        }

        /// <summary>
        /// Dispatches on the root FORM sub-type tag to the matching decoder and builds the result
        /// projection. Returns false (with the offending tag) when no decoder matches.
        /// </summary>
        private static bool TryDecode(IffDocument doc, string sourcePath, out object result, out string unsupportedTag)
        {
            result = null;
            unsupportedTag = null;

            var root = doc.Root as IffContainerChunk;
            string subType = root?.SubTypeId;

            if (subType == DataTableDecoder.RootSubType)
            {
                result = BuildDataTableResult(DataTableDecoder.Decode(doc), sourcePath);
                return true;
            }

            unsupportedTag = DescribeRoot(doc.Root);
            return false;
        }

        private static object BuildDataTableResult(DataTableView dt, string sourcePath)
        {
            return new
            {
                columns = dt.Columns
                    .Select(c => new { kind = c.Kind.ToString(), name = c.Name, typeSpec = c.TypeSpec })
                    .ToList(),
                rowCount = dt.Rows.Count,
                rows = dt.Rows,
                source = sourcePath,
                type = "datatable",
                version = dt.Version
            };
        }

        private static string DescribeRoot(IffChunk root)
        {
            if (root is IffContainerChunk c)
            {
                return (c.TypeId ?? "").TrimEnd() + " " + (c.SubTypeId ?? "").TrimEnd();
            }
            return root != null ? (root.TypeId ?? "").TrimEnd() : "(none)";
        }
    }
}
