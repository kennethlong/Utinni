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
        // Max file size before we reject it (256 MB, same cap as TRE parser T-04-DoS).
        private const int MaxFileSize = 256 * 1024 * 1024;

        public static int Run(ListObjectsOptions o)
        {
            // FileNotFound check: exit 3 (D-02).
            if (!File.Exists(o.Path))
            {
                return JsonOutput.EmitError("list-objects", "FileNotFound",
                    "World-snapshot file not found: " + o.Path, exitCode: 3);
            }

            byte[] bytes;
            try
            {
                var info = new FileInfo(o.Path);
                if (info.Length > MaxFileSize)
                {
                    return JsonOutput.EmitError("list-objects", "FileTooLarge",
                        "File size " + info.Length + " exceeds 256 MB cap.", exitCode: 2);
                }
                bytes = File.ReadAllBytes(o.Path);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("list-objects", "IoError", ex.Message, exitCode: 2);
            }

            // REVIEWS MEDIUM-13: byte-scan is provisional architectural debt; Phase 6+ refactor on
            // top of Plan 04-03's IffReader. See 04-02-PLAN.md <pitfalls> Pitfall A.
            //
            // Locate the OBJS chunk sentinel (4 ASCII bytes: 'O' 'B' 'J' 'S' = 0x4F 0x42 0x4A 0x53).
            // After the sentinel: 4 bytes big-endian length L, then L bytes of null-terminated strings.
            int objsIndex = IndexOfObjs(bytes);
            if (objsIndex < 0)
            {
                return JsonOutput.EmitError("list-objects", "NoObjsChunk",
                    "No OBJS chunk found in the supplied file.", exitCode: 2);
            }

            // Read big-endian length (4 bytes at objsIndex+4).
            if (objsIndex + 8 > bytes.Length)
            {
                return JsonOutput.EmitError("list-objects", "Truncated",
                    "OBJS header is incomplete (file ends before length field).", exitCode: 2);
            }

            int chunkLength = (bytes[objsIndex + 4] << 24)
                            | (bytes[objsIndex + 5] << 16)
                            | (bytes[objsIndex + 6] << 8)
                            | bytes[objsIndex + 7];

            if (chunkLength < 0 || objsIndex + 8 + chunkLength > bytes.Length)
            {
                return JsonOutput.EmitError("list-objects", "Truncated",
                    "OBJS chunk payload at offset " + (objsIndex + 8) + " with length " + chunkLength + " exceeds file bounds.", exitCode: 2);
            }

            // Extract null-terminated ASCII strings from the payload.
            List<string> templatePaths = ExtractNullTerminatedStrings(bytes, objsIndex + 8, chunkLength);

            // REVIEWS HIGH-7: objects sorted by id Ordinal for golden stability.
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
        /// Searches <paramref name="bytes"/> for the 4-byte 'OBJS' sentinel (big-endian ASCII).
        /// Returns the byte index of the 'O' byte, or -1 if not found.
        /// </summary>
        private static int IndexOfObjs(byte[] bytes)
        {
            // 0x4F=O 0x42=B 0x4A=J 0x53=S
            for (int i = 0; i <= bytes.Length - 4; i++)
            {
                if (bytes[i]     == 0x4F &&
                    bytes[i + 1] == 0x42 &&
                    bytes[i + 2] == 0x4A &&
                    bytes[i + 3] == 0x53)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Extracts null-terminated ASCII strings from a byte slice.
        /// </summary>
        private static List<string> ExtractNullTerminatedStrings(byte[] bytes, int payloadStart, int payloadLength)
        {
            var result = new List<string>();
            int current = payloadStart;
            int end = payloadStart + payloadLength;

            while (current < end)
            {
                // Find the next null byte within the payload.
                int nullPos = current;
                while (nullPos < end && bytes[nullPos] != 0)
                {
                    nullPos++;
                }

                if (nullPos > current)
                {
                    string s = Encoding.ASCII.GetString(bytes, current, nullPos - current);
                    result.Add(s);
                }

                // Advance past the null terminator.
                current = nullPos + 1;
            }

            return result;
        }
    }
}
