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
using Xunit.Sdk;

namespace Utinni.Cli.Tests.Infrastructure
{
    public static class GoldenTestRunner
    {
        /// <summary>
        /// Compares actualJson to the expected JSON golden fixture at
        /// &lt;BaseDirectory&gt;/Fixtures/&lt;fixtureKey&gt;.expected.json using JToken.DeepEquals.
        /// On mismatch, dumps expected.json + actual.json to &lt;BaseDirectory&gt;/TestResults/&lt;sanitized-key&gt;/
        /// and throws XunitException with a diff snippet.
        /// </summary>
        public static void Matches(string fixtureKey, string actualJson)
        {
            var fixturePath = FixturePath.Resolve(fixtureKey + ".expected.json");
            var expectedRaw = File.ReadAllText(fixturePath).Replace("\r\n", "\n").Replace("\r", "\n");

            var expected = JToken.Parse(expectedRaw);
            var actual = JToken.Parse(actualJson.Replace("\r\n", "\n").Replace("\r", "\n"));

            if (!JToken.DeepEquals(expected, actual))
            {
                DumpMismatch(fixtureKey, expected.ToString(), actual.ToString(), ".json");
            }
        }

        /// <summary>
        /// Compares actualText to the expected text golden fixture at
        /// &lt;BaseDirectory&gt;/Fixtures/&lt;fixtureKey&gt;.expected.txt using exact string comparison
        /// after CRLF normalisation.
        /// On mismatch, dumps expected.txt + actual.txt and throws XunitException.
        /// </summary>
        public static void MatchesText(string fixtureKey, string actualText)
        {
            var fixturePath = FixturePath.Resolve(fixtureKey + ".expected.txt");
            var expectedText = File.ReadAllText(fixturePath).Replace("\r\n", "\n").Replace("\r", "\n");
            var normalizedActual = actualText.Replace("\r\n", "\n").Replace("\r", "\n");

            if (!string.Equals(expectedText, normalizedActual, StringComparison.Ordinal))
            {
                DumpMismatch(fixtureKey, expectedText, normalizedActual, ".txt");
            }
        }

        private static void DumpMismatch(string fixtureKey, string expectedContent, string actualContent, string extension)
        {
            var sanitizedKey = fixtureKey.Replace('/', '_').Replace('\\', '_');
            var dumpDir = Path.Combine(AppContext.BaseDirectory, "TestResults", sanitizedKey);
            Directory.CreateDirectory(dumpDir);

            File.WriteAllText(Path.Combine(dumpDir, "expected" + extension), expectedContent);
            File.WriteAllText(Path.Combine(dumpDir, "actual" + extension), actualContent);

            var expectedHead = expectedContent.Substring(0, Math.Min(2000, expectedContent.Length));
            var actualHead = actualContent.Substring(0, Math.Min(2000, actualContent.Length));

            throw new XunitException(
                "Golden mismatch for " + fixtureKey + ".\n\n" +
                "Expected (head 2000):\n" + expectedHead + "\n\n" +
                "Actual (head 2000):\n" + actualHead + "\n\n" +
                "Full dumps at: " + dumpDir);
        }
    }
}
