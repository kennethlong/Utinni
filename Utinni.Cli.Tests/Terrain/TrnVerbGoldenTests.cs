// Format understood by reading swg-client-v2/src/engine/shared/library/sharedTerrain (SOE/Bootprint, All
// Rights Reserved) and the EA-IFF-85 public standard. No code, comments, identifier names, or test fixtures
// copied from any reference source. Implementation original to Utinni under MIT.

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Utinni.Cli.Tests.Fixtures.Trn;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.Terrain
{
    /// <summary>
    /// xUnit CLI GOLDEN tests for the terrain verb surface (review concerns #5/#18): the
    /// <c>decode-iff</c> TGEN branch, the <c>decode-trn</c> alias, and <c>roundtrip-trn</c>. Invokes the
    /// verbs through <see cref="InProcessCliRunner"/> (the SAME entry the shipped binary uses) over the
    /// synthesized TGEN fixtures and asserts (a) the emitted JSON envelope schema/shape (navigable tree
    /// present, sorted keys, <c>type:terrain</c> / <c>rootType:TGEN</c>), (b) the exit-code mapping
    /// (valid → 0; FileNotFound → 3; malformed/parse → 2). Closes the verb-golden gap the codec-level
    /// RoundtripTrnTests bypass.
    ///
    /// <para>Filter trait: <c>TrnVerbGolden</c>.</para>
    /// </summary>
    public sealed class TrnVerbGoldenTests : IDisposable
    {
        private readonly string _work;

        public TrnVerbGoldenTests()
        {
            _work = Path.Combine(Path.GetTempPath(), "trnverbgolden_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_work);
        }

        public void Dispose()
        {
            Utinni.Cli.Commands.RoundtripTrnCommand.TestPerturbRoundtripped = null;
            try { if (Directory.Exists(_work)) Directory.Delete(_work, recursive: true); }
            catch { /* best-effort */ }
        }

        private string WriteFixture(string name, byte[] bytes)
        {
            string p = Path.Combine(_work, name);
            File.WriteAllBytes(p, bytes);
            return p;
        }

        // ── decode-iff TGEN branch: navigable envelope (#9) ─────────────────────

        [Fact]
        [Trait("Category", "TrnVerbGolden")]
        public void DecodeIff_OnTgenFixture_EmitsNavigableTerrainEnvelope()
        {
            string path = WriteFixture("comp.trn", TgenFixtureSynthesizer.CompositionalLayer());
            CliResult r = InProcessCliRunner.Run("decode-iff", path);

            Assert.Equal(0, r.ExitCode);
            JObject env = JObject.Parse(r.Stdout);
            Assert.Equal("decode-iff", (string)env["command"]);
            Assert.Equal(1, (int)env["schemaVersion"]);

            JObject result = (JObject)env["result"];
            Assert.Equal("terrain", (string)result["type"]);
            Assert.Equal("TGEN", (string)result["rootType"]);

            // Navigable layer tree present (not summary-only).
            var layers = (JArray)result["layers"];
            Assert.NotNull(layers);
            Assert.NotEmpty(layers);
            JObject layer0 = (JObject)layers[0];
            Assert.NotNull(layer0["name"]);
            Assert.NotNull(layer0["active"]);
            var children = (JArray)layer0["children"];
            Assert.NotNull(children);
            // The compositional fixture holds AHCN(typed) + BALL(dead) + ZZZZ(raw) + BCIR(typed).
            Assert.Contains(children, c => (string)c["kind"] == "typed");
            Assert.Contains(children, c => (string)c["kind"] == "dead");
            Assert.Contains(children, c => (string)c["kind"] == "raw");
            // A typed child exposes named fields + a stable id.
            JObject typed = (JObject)children.First(c => (string)c["tag"] == "AHCN");
            Assert.NotNull(typed["stableId"]);
            Assert.NotEmpty((JArray)typed["fields"]);

            // Palettes array present (six positional slots).
            var palettes = (JArray)result["palettes"];
            Assert.NotNull(palettes);
            Assert.Equal(6, palettes.Count);
        }

        [Fact]
        [Trait("Category", "TrnVerbGolden")]
        public void DecodeIff_SortedKeys_TopLevelResultKeysAreOrdinalSorted()
        {
            string path = WriteFixture("min.trn", TgenFixtureSynthesizer.MinimalTgen());
            CliResult r = InProcessCliRunner.Run("decode-iff", path);
            Assert.Equal(0, r.ExitCode);

            JObject result = (JObject)JObject.Parse(r.Stdout)["result"];
            var keys = result.Properties().Select(p => p.Name).ToList();
            var sorted = keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            Assert.Equal(sorted, keys);
        }

        // ── decode-trn alias: same navigable envelope ───────────────────────────

        [Fact]
        [Trait("Category", "TrnVerbGolden")]
        public void DecodeTrn_Alias_EmitsTerrainEnvelope_Exit0()
        {
            string path = WriteFixture("typed.trn", TgenFixtureSynthesizer.LowVersion("AHCN"));
            CliResult r = InProcessCliRunner.Run("decode-trn", path);

            Assert.Equal(0, r.ExitCode);
            JObject env = JObject.Parse(r.Stdout);
            Assert.Equal("decode-trn", (string)env["command"]);
            JObject result = (JObject)env["result"];
            Assert.Equal("terrain", (string)result["type"]);
            Assert.Equal("TGEN", (string)result["rootType"]);
        }

        [Fact]
        [Trait("Category", "TrnVerbGolden")]
        public void DecodeTrn_FileNotFound_Exit3()
        {
            CliResult r = InProcessCliRunner.Run("decode-trn", Path.Combine(_work, "nope.trn"));
            Assert.Equal(3, r.ExitCode);
            Assert.Equal("FileNotFound", (string)((JObject)JObject.Parse(r.Stdout)["error"])["kind"]);
        }

        [Fact]
        [Trait("Category", "TrnVerbGolden")]
        public void DecodeTrn_MalformedInput_Exit2()
        {
            // A non-TGEN, non-IFF blob — the IFF reader / terrain decoder rejects it with exit 2.
            string path = WriteFixture("bad.trn", new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 });
            CliResult r = InProcessCliRunner.Run("decode-trn", path);
            Assert.Equal(2, r.ExitCode);
            Assert.NotNull(JObject.Parse(r.Stdout)["error"]);
        }

        // ── roundtrip-trn: whole-file byte identity ─────────────────────────────

        [Fact]
        [Trait("Category", "TrnVerbGolden")]
        public void RoundtripTrn_ValidFixture_BytesIdenticalTrue_Exit0()
        {
            string path = WriteFixture("rt.trn", TgenFixtureSynthesizer.CompositionalLayer());
            CliResult r = InProcessCliRunner.Run("roundtrip-trn", path);

            Assert.Equal(0, r.ExitCode);
            JObject env = JObject.Parse(r.Stdout);
            Assert.Equal("roundtrip-trn", (string)env["command"]);
            JObject result = (JObject)env["result"];
            Assert.True((bool)result["bytesIdentical"]);
            Assert.Equal("whole-file", (string)result["comparisonGranularity"]);
            Assert.Equal("TGEN", (string)result["rootType"]);
        }

        [Theory]
        [Trait("Category", "TrnVerbGolden")]
        [InlineData("AHCN")]
        [InlineData("BREC")]
        [InlineData("FSLP")]
        public void RoundtripTrn_HighVersionFixture_BytesIdentical(string tag)
        {
            string path = WriteFixture(tag + "_hi.trn", TgenFixtureSynthesizer.HighVersion(tag));
            CliResult r = InProcessCliRunner.Run("roundtrip-trn", path);
            Assert.Equal(0, r.ExitCode);
            Assert.True((bool)((JObject)JObject.Parse(r.Stdout)["result"])["bytesIdentical"]);
        }

        // WR-01: a NON-byte-identical round-trip must FAIL CLOSED (exit nonzero), not report
        // bytesIdentical=false yet exit 0 — DEC-C3 byte-exact is THE codec gate for this phase, so a
        // CI/agent gating on exit code must catch a write-path regression. The test-only perturbation seam
        // flips a payload byte of the round-tripped output so it diverges from the input while staying
        // structurally parseable (the re-parse-for-validity must still succeed).
        [Fact]
        [Trait("Category", "TrnVerbGolden")]
        public void RoundtripTrn_NotByteIdentical_FailsClosed_ExitNonzero()
        {
            string path = WriteFixture("rtbad.trn", TgenFixtureSynthesizer.LowVersion("AHCN"));
            Utinni.Cli.Commands.RoundtripTrnCommand.TestPerturbRoundtripped = bytes =>
            {
                // Flip the last byte (inside the AHCN DATA payload, not the framing) so the whole-file
                // compare differs but the re-parse still succeeds.
                byte[] copy = (byte[])bytes.Clone();
                copy[copy.Length - 1] ^= 0xFF;
                return copy;
            };

            CliResult r = InProcessCliRunner.Run("roundtrip-trn", path);

            Assert.NotEqual(0, r.ExitCode);
            JObject env = JObject.Parse(r.Stdout);
            Assert.NotNull(env["error"]);
            Assert.Equal("VerifyFailed", (string)((JObject)env["error"])["kind"]);
        }

        [Fact]
        [Trait("Category", "TrnVerbGolden")]
        public void RoundtripTrn_FileNotFound_Exit3()
        {
            CliResult r = InProcessCliRunner.Run("roundtrip-trn", Path.Combine(_work, "nope.trn"));
            Assert.Equal(3, r.ExitCode);
            Assert.Equal("FileNotFound", (string)((JObject)JObject.Parse(r.Stdout)["error"])["kind"]);
        }

        // ── help enumerates the new verbs ───────────────────────────────────────

        [Fact]
        [Trait("Category", "TrnVerbGolden")]
        public void Help_EnumeratesDecodeTrnAndRoundtripTrn()
        {
            CliResult r = InProcessCliRunner.Run("--help");
            // CommandLineParser writes the verb list to stderr (HelpWriter = Console.Error).
            string help = r.Stdout + r.Stderr;
            Assert.Contains("decode-trn", help);
            Assert.Contains("roundtrip-trn", help);
        }
    }
}
