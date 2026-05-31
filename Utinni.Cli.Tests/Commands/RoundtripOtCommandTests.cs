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
using System.Text;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Formats.Iff;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.Commands
{
    /// <summary>
    /// Tier-2 goldens for the <c>roundtrip-ot</c> CLI command — the CF-02 PARAM-level max-harness for the
    /// object-template write path (the typed analog of <c>roundtrip-tab</c>'s per-cell slice). Proves
    /// untouched params stay byte-exact after a typed override / revert / edit mutation, that a
    /// complex-param hex-fallback round-trips byte-exact, and that an unresolved-base fixture round-trips
    /// without throwing (the writer never resolves the base).
    ///
    /// <para>Naming convention: [Method]_[Scenario]_[ExpectedOutcome].</para>
    /// </summary>
    public class RoundtripOtCommandTests
    {
        private sealed class TempOt : IDisposable
        {
            public string Path { get; }

            public TempOt(byte[] bytes)
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "utinni-roundtrip-ot-" + Guid.NewGuid().ToString("N") + ".iff");
                File.WriteAllBytes(Path, bytes);
            }

            public void Dispose()
            {
                try { if (File.Exists(Path)) File.Delete(Path); }
                catch (IOException) { /* best-effort */ }
            }
        }

        private static JToken RunAndParse(out int exitCode, params string[] args)
        {
            CliResult result = InProcessCliRunner.Run(args);
            exitCode = result.ExitCode;
            return JToken.Parse(result.Stdout);
        }

        private static JToken ResultOf(JToken envelope)
        {
            return envelope["result"];
        }

        // ── OT fixture builders ──────────────────────────────────────────────

        private static byte[] EncodeParam(string name, byte[] valueRegion)
        {
            using (var ms = new MemoryStream())
            {
                byte[] n = Encoding.ASCII.GetBytes(name);
                ms.Write(n, 0, n.Length);
                ms.WriteByte(0);
                if (valueRegion != null && valueRegion.Length > 0) ms.Write(valueRegion, 0, valueRegion.Length);
                return ms.ToArray();
            }
        }

        private static byte[] Int32Le(int v)
        {
            return new[] { (byte)(v & 0xFF), (byte)((v >> 8) & 0xFF), (byte)((v >> 16) & 0xFF), (byte)((v >> 24) & 0xFF) };
        }

        private static byte[] IntValueRegion(int v, char delta)
        {
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(1);
                ms.WriteByte((byte)delta);
                byte[] le = Int32Le(v);
                ms.Write(le, 0, le.Length);
                return ms.ToArray();
            }
        }

        // A WEIGHTED_LIST tag (2) — the generic decoder routes this to a hex-fallback value.
        private static byte[] WeightedListValueRegion()
        {
            // tag(2 = WEIGHTED_LIST) + an arbitrary trailing body the scalar path won't consume.
            return new byte[] { 2, 0x01, 0x00, 0x00, 0x00, 0xAB, 0xCD };
        }

        private static byte[] BuildTemplate(string baseName, IList<KeyValuePair<string, byte[]>> paramChunks)
        {
            MutableIffNode root = MutableIffNode.NewContainer("FORM", "SHOT");
            if (baseName != null)
            {
                MutableIffNode derv = root.AddContainer("FORM", "DERV");
                using (var ms = new MemoryStream())
                {
                    byte[] n = Encoding.ASCII.GetBytes(baseName);
                    ms.Write(n, 0, n.Length);
                    ms.WriteByte(0);
                    derv.AddLeaf("XXXX", ms.ToArray());
                }
            }
            MutableIffNode versionForm = root.AddContainer("FORM", "0000");
            versionForm.AddLeaf("PCNT", Int32Le(paramChunks.Count));
            foreach (KeyValuePair<string, byte[]> p in paramChunks)
            {
                versionForm.AddLeaf("XXXX", EncodeParam(p.Key, p.Value));
            }
            return IffWriter.Write(new MutableIffDocument(root));
        }

        private static KeyValuePair<string, byte[]> P(string name, byte[] valueRegion)
        {
            return new KeyValuePair<string, byte[]>(name, valueRegion);
        }

        private static byte[] MultiParamTemplate(string baseName)
        {
            return BuildTemplate(baseName, new List<KeyValuePair<string, byte[]>>
            {
                P("hitPoints", IntValueRegion(100, ' ')),
                P("armor", IntValueRegion(5, ' ')),
                P("scale", IntValueRegion(1, ' '))
            });
        }

        // ── No-mutation whole-file byte-exact ────────────────────────────────

        [Fact]
        public void Roundtrip_NoMutate_ExitsZero_WholeFileByteExact()
        {
            using (var f = new TempOt(MultiParamTemplate("object/base.iff")))
            {
                JToken env = RunAndParse(out int exit, "roundtrip-ot", f.Path);
                Assert.Equal(0, exit);
                JToken r = ResultOf(env);
                Assert.True((bool)r["paramsEqualUntouched"]);
                Assert.Equal("whole-file", (string)r["comparisonGranularity"]);
                Assert.Equal("none", (string)r["mutationApplied"]);
                Assert.Equal(3, (int)r["localParamCount"]);
            }
        }

        // ── Multi-level chain: untouched params byte-exact after an edit override ──

        [Fact]
        public void Roundtrip_Edit_LeavesUntouchedParamsByteExact()
        {
            using (var f = new TempOt(MultiParamTemplate("object/base.iff")))
            {
                JToken env = RunAndParse(out int exit, "roundtrip-ot", f.Path, "--edit", "hitPoints", "--value-int", "250");
                Assert.Equal(0, exit);
                JToken r = ResultOf(env);
                Assert.True((bool)r["paramsEqualUntouched"]);
                Assert.Equal("per-param-payload", (string)r["comparisonGranularity"]);
                Assert.Equal("edit(hitPoints)", (string)r["mutationApplied"]);
            }
        }

        [Fact]
        public void Roundtrip_AddOverride_LeavesUntouchedParamsByteExact()
        {
            using (var f = new TempOt(MultiParamTemplate("object/base.iff")))
            {
                JToken env = RunAndParse(out int exit, "roundtrip-ot", f.Path, "--add-override", "newField", "--value-int", "7");
                Assert.Equal(0, exit);
                JToken r = ResultOf(env);
                Assert.True((bool)r["paramsEqualUntouched"]);
                Assert.Equal(4, (int)r["localParamCount"]);
            }
        }

        [Fact]
        public void Roundtrip_RemoveOverride_LeavesUntouchedParamsByteExact()
        {
            using (var f = new TempOt(MultiParamTemplate("object/base.iff")))
            {
                JToken env = RunAndParse(out int exit, "roundtrip-ot", f.Path, "--remove-override", "armor");
                Assert.Equal(0, exit);
                JToken r = ResultOf(env);
                Assert.True((bool)r["paramsEqualUntouched"]);
                Assert.Equal(2, (int)r["localParamCount"]);
            }
        }

        // ── Complex-param hex-fallback round-trips byte-exact ────────────────

        [Fact]
        public void Roundtrip_HexFallbackParam_RoundTripsByteExact()
        {
            // A template carrying a WEIGHTED_LIST param (routes to hex fallback) plus typed scalars.
            byte[] bytes = BuildTemplate("object/base.iff", new List<KeyValuePair<string, byte[]>>
            {
                P("complexList", WeightedListValueRegion()),
                P("hitPoints", IntValueRegion(42, ' '))
            });

            using (var f = new TempOt(bytes))
            {
                // No-mutation: the hex-fallback param must round-trip byte-for-byte (whole-file exact).
                JToken env = RunAndParse(out int exit, "roundtrip-ot", f.Path);
                Assert.Equal(0, exit);
                JToken r = ResultOf(env);
                Assert.True((bool)r["paramsEqualUntouched"]);

                // Editing the OTHER (typed) param leaves the hex-fallback param byte-identical.
                JToken env2 = RunAndParse(out int exit2, "roundtrip-ot", f.Path, "--edit", "hitPoints", "--value-int", "99");
                Assert.Equal(0, exit2);
                Assert.True((bool)ResultOf(env2)["paramsEqualUntouched"]);
            }
        }

        // ── Unresolved-base degradation: round-trips without throwing ────────

        [Fact]
        public void Roundtrip_UnresolvedBaseFixture_RoundTripsWithoutThrowing()
        {
            // The base is never resolved by the writer (it only serializes local params). A DERV name
            // that points at an enumerate-only/V6000/missing base still round-trips byte-exact.
            byte[] bytes = MultiParamTemplate("object/encrypted/v6000_base.iff");
            using (var f = new TempOt(bytes))
            {
                JToken env = RunAndParse(out int exit, "roundtrip-ot", f.Path, "--edit", "armor", "--value-int", "12");
                Assert.Equal(0, exit);
                JToken r = ResultOf(env);
                Assert.True((bool)r["paramsEqualUntouched"]);
                Assert.Equal("object/encrypted/v6000_base.iff", (string)r["baseTemplate"]);
            }
        }

        // ── Exit codes ───────────────────────────────────────────────────────

        [Fact]
        public void Roundtrip_FileNotFound_ExitsThree()
        {
            JToken env = RunAndParse(out int exit, "roundtrip-ot",
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N") + ".iff"));
            Assert.Equal(3, exit);
            Assert.Equal("FileNotFound", (string)env["error"]["kind"]);
        }

        [Fact]
        public void Roundtrip_MutuallyExclusiveMutations_ExitsOne()
        {
            using (var f = new TempOt(MultiParamTemplate("object/base.iff")))
            {
                JToken env = RunAndParse(out int exit, "roundtrip-ot", f.Path,
                    "--edit", "armor", "--value-int", "1", "--remove-override", "scale");
                Assert.Equal(1, exit);
            }
        }

        // ── Verb is registered (appears in --help) ───────────────────────────

        [Fact]
        public void RoundtripOt_VerbIsRegistered_AppearsInHelp()
        {
            CliResult result = InProcessCliRunner.Run("--help");
            Assert.Contains("roundtrip-ot", result.Stdout + result.Stderr);
        }
    }
}
