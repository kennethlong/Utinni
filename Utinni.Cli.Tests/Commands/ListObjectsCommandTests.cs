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
using System.IO;
using Newtonsoft.Json.Linq;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.Commands
{
    /// <summary>
    /// Tier-2 golden tests for the list-objects CLI command.
    /// Each test runs the command in-process via InProcessCliRunner and compares output
    /// against committed expected.json goldens via JToken.DeepEquals.
    /// </summary>
    public class ListObjectsCommandTests
    {
        /// <summary>
        /// Masks the absolute fixture path in the CLI's JSON output with the stable sentinel
        /// used in committed expected.json files.
        /// The CLI emits Windows paths in JSON with escaped backslashes (e.g. "D:\\Code\\..."),
        /// so we replace both the literal path and its JSON-escaped form.
        /// </summary>
        private static string MaskPath(string actual, string path)
        {
            string escapedPath = path.Replace("\\", "\\\\");
            return actual.Replace(escapedPath, "<fixture-path>").Replace(path, "<fixture-path>");
        }

        /// <summary>
        /// PLAN: Task 2.4 test 8.
        /// list-objects on synthesized-ws.iff exits 0; result.objects contains 3 sorted entries.
        /// Each entry carries both id and templateName fields (REVIEWS HIGH-7).
        /// </summary>
        [Fact]
        public void Run_WithSynthesizedWsIff_ExitsZeroAndMatchesGolden()
        {
            var fixturePath = FixturePath.Resolve("world-snapshot/synthesized-ws.iff");
            var result = InProcessCliRunner.Run("list-objects", fixturePath);

            Assert.Equal(0, result.ExitCode);
            var masked = MaskPath(result.Stdout, fixturePath);
            GoldenTestRunner.Matches("world-snapshot/synthesized-ws", masked);
        }

        /// <summary>
        /// PLAN: Task 2.4 test 9.
        /// list-objects with a missing path exits 3 with FileNotFound error.
        /// </summary>
        [Fact]
        public void Run_WithMissingFile_ExitsThreeAndMatchesFileNotFoundShape()
        {
            var result = InProcessCliRunner.Run("list-objects", @"Z:\nonexistent\world-snapshot.iff");

            Assert.Equal(3, result.ExitCode);
            var root = JToken.Parse(result.Stdout);
            Assert.Equal("list-objects",  root["command"].Value<string>());
            Assert.Equal("FileNotFound",   root["error"]["kind"].Value<string>());
        }

        /// <summary>
        /// 07-01 Task 3: a valid IFF document with NO OBJS chunk exits 2 with the documented
        /// NoObjsChunk error kind (the migration reads through the shared IffReader path).
        /// </summary>
        [Fact]
        public void Run_WithObjsLessIff_ExitsTwoWithNoObjsChunk()
        {
            // Minimal valid IFF: FORM:TEST containing a single empty DATA leaf (no OBJS).
            byte[] iff = BuildFormWithLeaf("TEST", "DATA", new byte[0]);
            string temp = Path.Combine(Path.GetTempPath(), "ws-noobjs-" + Guid.NewGuid().ToString("N") + ".iff");
            File.WriteAllBytes(temp, iff);
            try
            {
                var result = InProcessCliRunner.Run("list-objects", temp);
                Assert.Equal(2, result.ExitCode);
                var root = JToken.Parse(result.Stdout);
                Assert.Equal("list-objects", root["command"].Value<string>());
                Assert.Equal("NoObjsChunk", root["error"]["kind"].Value<string>());
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { }
            }
        }

        /// <summary>Builds a minimal big-endian IFF FORM:&lt;subType&gt; with one leaf chunk.</summary>
        private static byte[] BuildFormWithLeaf(string subType, string leafType, byte[] leafData)
        {
            using (var ms = new MemoryStream())
            {
                // Leaf: 4-char type + BE length + payload (even length, no pad needed here).
                byte[] leaf;
                using (var lm = new MemoryStream())
                {
                    WriteAscii4(lm, leafType);
                    WriteInt32Be(lm, leafData.Length);
                    lm.Write(leafData, 0, leafData.Length);
                    leaf = lm.ToArray();
                }
                int formContentLength = 4 + leaf.Length; // subType + leaf chunk
                WriteAscii4(ms, "FORM");
                WriteInt32Be(ms, formContentLength);
                WriteAscii4(ms, subType);
                ms.Write(leaf, 0, leaf.Length);
                return ms.ToArray();
            }
        }

        private static void WriteAscii4(Stream s, string fourCc)
        {
            for (int i = 0; i < 4; i++) s.WriteByte((byte)fourCc[i]);
        }

        private static void WriteInt32Be(Stream s, int v)
        {
            s.WriteByte((byte)(v >> 24));
            s.WriteByte((byte)(v >> 16));
            s.WriteByte((byte)(v >> 8));
            s.WriteByte((byte)v);
        }
    }
}
