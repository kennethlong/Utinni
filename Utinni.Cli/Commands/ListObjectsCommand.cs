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
using System.Text;
using CommandLine;
using UtinniCoreDotNet.Formats.Iff;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("list-objects", HelpText = "List world-snapshot objects from a ws.iff via the TRE reader.")]
    public class ListObjectsOptions
    {
        [Value(0, MetaName = "path", Required = true, HelpText = "Path to a ws.iff.")]
        public string Path { get; set; }
    }

    public static class ListObjectsCommand
    {
        // 07-01 Task 3 (review consensus #7): the OBJS template-name block is now read through the
        // shared Formats/Iff parser — the SAME core the TRE Browser detail pane uses — instead of
        // the provisional sentinel byte-scan. This keeps success criterion #4's "parse-tre AND
        // list-objects" genuinely lock-step on one IFF code path.
        public static int Run(ListObjectsOptions o)
        {
            // FileNotFound check: exit 3 (D-02).
            if (!File.Exists(o.Path))
            {
                return JsonOutput.EmitError("list-objects", "FileNotFound",
                    "World-snapshot file not found: " + o.Path, exitCode: 3);
            }

            IffDocument doc;
            try
            {
                doc = IffReader.Read(o.Path);
            }
            catch (IffParseException ex)
            {
                // IffParseError.Kind.ToString() yields the structured error.kind string.
                return JsonOutput.EmitError("list-objects", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("list-objects", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception is intentionally NOT caught — unexpected types bubble up.

            // Locate the OBJS leaf via the parser (PROP/OBJS are leaves, not containers).
            IffLeafChunk objs = doc.AllNodesInPreorder
                .OfType<IffLeafChunk>()
                .FirstOrDefault(c => c.TypeId == "OBJS");

            if (objs == null)
            {
                return JsonOutput.EmitError("list-objects", "NoObjsChunk",
                    "No OBJS chunk found in the supplied file.", exitCode: 2);
            }

            // The OBJS payload is the same null-terminated ASCII template-name block the byte-scan
            // read — but now bounds-checked by the parser, and with the 8-byte chunk header stripped.
            List<string> templatePaths = ExtractNullTerminatedStrings(objs.Data);

            // REVIEWS HIGH-7: objects sorted by id Ordinal for golden stability (LOCKED contract).
            var sortedPaths = templatePaths
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();

            return JsonOutput.EmitSuccess("list-objects", new
            {
                source  = o.Path,
                objects = sortedPaths
                    .Select(p => new
                    {
                        id           = p,          // REVIEWS HIGH-7: stable id = templateName
                        templateName = p
                    })
                    .ToList()
            });
        }

        /// <summary>
        /// Extracts null-terminated ASCII strings from the OBJS payload (empty segments — e.g. a
        /// trailing terminator — are skipped).
        /// </summary>
        private static List<string> ExtractNullTerminatedStrings(byte[] bytes)
        {
            var result = new List<string>();
            int current = 0;

            while (current < bytes.Length)
            {
                int nullPos = current;
                while (nullPos < bytes.Length && bytes[nullPos] != 0)
                {
                    nullPos++;
                }

                if (nullPos > current)
                {
                    result.Add(Encoding.ASCII.GetString(bytes, current, nullPos - current));
                }

                current = nullPos + 1;
            }

            return result;
        }
    }
}
