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
//
// GREEN (plan 23-05): the verb-level byte-exact gate (roundtrip-template) + D-14 worked-example goldens.
// The worked-example fixtures double as the DEC-C3 byte-exact gate. Method names embed the VALIDATION.md
// --filter tokens (Template, Roundtrip, WorkedExample, Decode, List).

using System.IO;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Formats.Template;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.Template
{
    /// <summary>
    /// Verb-level (utinni-cli) decode-&gt;encode byte-exact goldens for the template path, plus the
    /// decode-with-template envelope + list-templates inventory goldens. Filter tokens: Template,
    /// Roundtrip, WorkedExample, Decode, List.
    /// </summary>
    public sealed class RoundtripTemplateCommandTests
    {
        // ── verb-level byte-exact (PROD-IFFT-02) — filter "Template&Roundtrip" ──

        [Theory]
        [Trait("Category", "TemplateRoundtrip")]
        [InlineData(TemplateTestFixtures.VersionLow)]
        [InlineData(TemplateTestFixtures.VersionHigh)]
        public void Template_Roundtrip_VerbDecodeEncodeFixture_BytesIdentical(string version)
        {
            using (var tmp = new TempDir())
            {
                string iff = tmp.Write("kern.iff", TemplateTestFixtures.BuildKern(version));
                string tpl = tmp.WriteText("kern.json", TemplateJson.Serialize(TemplateAuthoring.KernTemplate(version)));

                CliResult res = InProcessCliRunner.Run("roundtrip-template", iff, "--template", tpl);

                Assert.Equal(0, res.ExitCode);
                JObject env = JObject.Parse(res.Stdout);
                Assert.True((bool)env["result"]["bytesIdentical"]);
            }
        }

        [Fact]
        [Trait("Category", "TemplateRoundtrip")]
        public void Template_Roundtrip_CorruptedFixture_Exit2()
        {
            using (var tmp = new TempDir())
            {
                // A template that under-describes the payload (claims 1 trailing f32, fixture has more)
                // makes decode->encode NOT byte-identical (or short-decodes) -> the gate must reject (exit 2).
                string iff = tmp.Write("kern.iff", TemplateTestFixtures.BuildKern(TemplateTestFixtures.VersionLow));
                string tpl = tmp.WriteText("wrong.json",
                    TemplateJson.Serialize(TemplateAuthoring.WrongLayoutTemplate(TemplateTestFixtures.VersionLow)));

                CliResult res = InProcessCliRunner.Run("roundtrip-template", iff, "--template", tpl);

                Assert.Equal(2, res.ExitCode);
            }
        }

        // ── decode-with-template envelope (PROD-IFFT-02) — filter "Template&Decode" ──

        [Fact]
        [Trait("Category", "TemplateDecode")]
        public void Template_Decode_EligibleLeaf_EmitsEnvelopeWithFit()
        {
            using (var tmp = new TempDir())
            {
                string iff = tmp.Write("kern.iff", TemplateTestFixtures.BuildKern(TemplateTestFixtures.VersionLow));
                string tpl = tmp.WriteText("kern.json",
                    TemplateJson.Serialize(TemplateAuthoring.KernTemplate(TemplateTestFixtures.VersionLow)));

                CliResult res = InProcessCliRunner.Run("decode-with-template", iff, "--template", tpl);

                Assert.Equal(0, res.ExitCode);
                JObject env = JObject.Parse(res.Stdout);
                Assert.True((bool)env["result"]["matched"]);
                Assert.True((bool)env["result"]["fits"]);
                Assert.True((bool)env["result"]["fit"]["consumedExactly"]);
            }
        }

        [Fact]
        [Trait("Category", "TemplateDecode")]
        public void Template_Decode_MissingFile_Exit3()
        {
            CliResult res = InProcessCliRunner.Run("decode-with-template", "does-not-exist.iff");
            Assert.Equal(3, res.ExitCode);
        }

        [Fact]
        [Trait("Category", "TemplateDecode")]
        public void Template_Decode_UnmatchedChunk_ClearNoMatchEnvelope_NotCrash()
        {
            using (var tmp = new TempDir())
            {
                // A real fixture but a template whose leaf tag does not match any leaf -> matched=false,
                // exit 0 (a clear no-match envelope, never a crash).
                string iff = tmp.Write("kern.iff", TemplateTestFixtures.BuildKern(TemplateTestFixtures.VersionLow));
                string tpl = tmp.WriteText("other.json",
                    TemplateJson.Serialize(TemplateAuthoring.UnmatchedTagTemplate()));

                CliResult res = InProcessCliRunner.Run("decode-with-template", iff, "--template", tpl);

                Assert.Equal(0, res.ExitCode);
                JObject env = JObject.Parse(res.Stdout);
                Assert.False((bool)env["result"]["matched"]);
                Assert.NotNull((string)env["result"]["reason"]);
            }
        }

        // ── D-14 worked-example goldens (PROD-IFFT-03) — filter "Template&WorkedExample&Roundtrip" ──

        [Theory]
        [Trait("Category", "TemplateWorkedExampleRoundtrip")]
        [InlineData(TemplateTestFixtures.VersionLow)]
        [InlineData(TemplateTestFixtures.VersionHigh)]
        public void Template_WorkedExample_Roundtrip_CountedRecords_BytesIdentical(string version)
        {
            using (var tmp = new TempDir())
            {
                // The count-from-prior worked example applied to its synthesized CNTR fixture round-trips
                // byte-exact at both the low and high version FORM (matchTagOnly engages on either).
                string iff = tmp.Write("cntr.iff", TemplateTestFixtures.BuildCountFromPrior(version, 3));

                CliResult res = InProcessCliRunner.Run("roundtrip-template", iff, "--template", ExamplePath("counted_records.json"));

                Assert.Equal(0, res.ExitCode);
                JObject env = JObject.Parse(res.Stdout);
                Assert.True((bool)env["result"]["bytesIdentical"]);
            }
        }

        [Theory]
        [Trait("Category", "TemplateWorkedExampleRoundtrip")]
        [InlineData(TemplateTestFixtures.VersionLow)]
        [InlineData(TemplateTestFixtures.VersionHigh)]
        public void Template_WorkedExample_Roundtrip_FlatComposite_BytesIdentical(string version)
        {
            using (var tmp = new TempDir())
            {
                string iff = tmp.Write("prst.iff", TemplateTestFixtures.BuildPresets(version));

                CliResult res = InProcessCliRunner.Run("roundtrip-template", iff, "--template", ExamplePath("flat_composite.json"));

                Assert.Equal(0, res.ExitCode);
                JObject env = JObject.Parse(res.Stdout);
                Assert.True((bool)env["result"]["bytesIdentical"]);
            }
        }

        // The shipped worked-example template path (CopyToOutputDirectory -> next to the test assembly).
        private static string ExamplePath(string name)
        {
            return Path.Combine(System.AppContext.BaseDirectory, "Formats", "Template", "Examples", name);
        }

        // ── list-templates inventory (D-12) — filter "Template&List" ──

        [Fact]
        [Trait("Category", "TemplateList")]
        public void Template_List_EnumeratesScannedPacks_ReportsMatchKey()
        {
            using (var tmp = new TempDir())
            {
                tmp.WriteText("kern.json", TemplateJson.Serialize(TemplateAuthoring.KernTemplate(TemplateTestFixtures.VersionLow)));

                CliResult res = InProcessCliRunner.Run("list-templates", "--templates-dir", tmp.Path);

                Assert.Equal(0, res.ExitCode);
                JObject env = JObject.Parse(res.Stdout);
                JArray templates = (JArray)env["result"]["templates"];
                Assert.NotEmpty(templates);
                // Every listed template reports a match key.
                foreach (JToken t in templates)
                {
                    Assert.False(string.IsNullOrEmpty((string)t["matchKey"]));
                }
            }
        }
    }
}
