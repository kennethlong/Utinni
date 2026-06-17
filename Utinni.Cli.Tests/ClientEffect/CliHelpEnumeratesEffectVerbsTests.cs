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

using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.ClientEffect
{
    /// <summary>
    /// D-12 smoke: <c>utinni-cli --help</c> enumerates the three new <c>effect-*</c> verbs (proves the
    /// Type[] ParseArguments wiring took). Filter trait: <c>CliHelpEnumerates</c>.
    /// </summary>
    public sealed class CliHelpEnumeratesEffectVerbsTests
    {
        [Fact]
        [Trait("Category", "CliHelpEnumerates")]
        public void Help_EnumeratesAllThreeEffectVerbs()
        {
            CliResult r = InProcessCliRunner.Run("--help");
            // CommandLineParser writes the verb list to stderr (HelpWriter = Console.Error).
            string help = r.Stdout + r.Stderr;
            Assert.Contains("decode-effect", help);
            Assert.Contains("roundtrip-effect", help);
            Assert.Contains("apply-save-effect", help);
        }
    }
}
