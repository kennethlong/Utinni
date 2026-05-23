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
    }
}
