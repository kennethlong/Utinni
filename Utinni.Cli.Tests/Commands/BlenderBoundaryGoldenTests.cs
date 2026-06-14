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
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.Commands
{
    /// <summary>
    /// ECO-01 (Phase 16-01) cross-validation golden tests: Blender writes, Utinni reads the SAME
    /// pinned bytes (D-08 / SC4). The two binary goldens are copied VERBATIM from
    /// D:/Code/swg-blender-plugin/tests/golden/ and pinned in-repo (no Git LFS — CON-O-09 resolved).
    ///
    /// <para>These tests exercise ONLY already-shipped verbs (parse-tre, decode-iff) plus a pure
    /// SHA-256 provenance compare, so they compile AND pass at the Task-1 boundary — independent of
    /// the Task-2 validate-bundle verb.</para>
    ///
    /// <para><b>Cross-validation finding (CV-1, surfaced by this plan):</b> the pinned
    /// <c>retail_mini_0005.tre</c> is a SYNTHETIC reader-unit fixture authored in the Blender repo
    /// (tests/test_tre_versions.py), NOT a real TreeFileBuilder export. Its v0005 TOC entries are
    /// laid out CRC-FIRST (<c>crc,length,offset,comp,clen,fnoff</c>, swg-blender-plugin
    /// tre_reader.py <c>TOC_ENTRY_FMT="&lt;Iiiiii"</c>), whereas Utinni reads the v0004/v0005/v0006
    /// family SIZE-FIRST (<c>uncompressedSize,offset,compressor,compressedSize,checksum,nameOffset</c>,
    /// validated against the live SWGEmu client per TreVersion.cs). The two field orders DISAGREE, so
    /// Utinni's parse-tre reads the second TOC int (length=36) into the compressor slot and rejects it
    /// as UnknownCompressor (exit 2). This test asserts that REAL behavior rather than a false
    /// exit-0, keeping the disagreement visible. The boundary contract doc records this in the
    /// version-matrix section. The .msh cross-validation below is the working D-08 proof.</para>
    /// </summary>
    public class BlenderBoundaryGoldenTests
    {
        private static string MaskPath(string actual, string path)
        {
            string escapedPath = path.Replace("\\", "\\\\");
            return actual.Replace(escapedPath, "<fixture-path>").Replace(path, "<fixture-path>");
        }

        // ── .msh cross-validation: Blender writes, Utinni reads (count-only, DEC-A3 clean) ──

        /// <summary>
        /// decode-iff over the pinned Blender static mesh exits 0 and emits a MESH AppearanceSummary
        /// with non-zero counts and NO geometry/vertex payload (count-only — DEC-A3: Utinni reads
        /// structure, never authors/decodes geometry). This is the working D-08 cross-validation proof.
        /// </summary>
        [Fact]
        public void DecodeIff_BlenderStaticMesh_EmitsCountOnlyAppearanceSummary()
        {
            var fixturePath = FixturePath.Resolve("blender/frn_all_bed_sm_s1_l0.msh");
            var result = InProcessCliRunner.Run("decode-iff", fixturePath);

            Assert.Equal(0, result.ExitCode);

            var root = JToken.Parse(result.Stdout);
            Assert.Equal("decode-iff", root["command"].Value<string>());
            Assert.Null(root["error"]);

            JToken r = root["result"];
            Assert.Equal("appearance", r["type"].Value<string>());
            Assert.Equal("mesh", r["kind"].Value<string>());

            // Non-zero structural counts prove Utinni really read the Blender-written bytes.
            Assert.True(r["vertexCount"].Value<int>() > 0, "expected a non-zero vertex count");
            Assert.True(r["shaderCount"].Value<int>() > 0, "expected a non-zero shader count");

            // Count-only contract: the JSON carries NO geometry/vertex array (DEC-A3 — no codec).
            Assert.Null(r["vertices"]);
            Assert.Null(r["geometry"]);
            Assert.Null(r["positions"]);
            Assert.Null(r["indices"]);
        }

        // ── .tre cross-validation: surfaces the CRC-first vs SIZE-first v0005 disagreement (CV-1) ──

        /// <summary>
        /// parse-tre over the pinned Blender synthetic mini .tre. The fixture's v0005 TOC is laid out
        /// CRC-FIRST (Blender reader convention), which DISAGREES with Utinni's SIZE-FIRST v0005 layout
        /// — so Utinni misreads the TOC and rejects it with UnknownCompressor (exit 2). Asserting this
        /// REAL behavior (rather than a false exit-0) keeps the cross-repo format disagreement visible
        /// and guards against a silent change to either the fixture or the reader (CV-1; see class doc).
        /// </summary>
        [Fact]
        public void ParseTre_BlenderSyntheticMiniTre_SurfacesCrcVsSizeFirstDisagreement()
        {
            var fixturePath = FixturePath.Resolve("blender/retail_mini_0005.tre");
            var result = InProcessCliRunner.Run("parse-tre", fixturePath);

            // CV-1: the Blender mini fixture's crc-first v0005 TOC is not readable by Utinni's
            // size-first v0005 reader. Documented finding — NOT a regression to "fix" in the reader.
            Assert.Equal(2, result.ExitCode);
            var root = JToken.Parse(result.Stdout);
            Assert.Equal("parse-tre", root["command"].Value<string>());
            Assert.Equal("UnknownCompressor", root["error"]["kind"].Value<string>());
        }

        // ── SHA-256 provenance fact: silent fixture-refresh drift guard (T-16-04) ──

        /// <summary>
        /// Re-hashes the on-disk pinned BINARY goldens and asserts each computed SHA-256 byte-equals
        /// the value recorded in fixture-hashes.txt. A silent fixture refresh (e.g. re-copying a
        /// different Blender build's bytes) changes the hash and fails this test — the Codex
        /// fixture-provenance finding / T-16-04 drift guard.
        /// </summary>
        [Fact]
        public void BlenderGoldens_MatchPinnedSha256Provenance()
        {
            var hashesPath = FixturePath.Resolve("blender/fixture-hashes.txt");
            Dictionary<string, string> recorded = ParseHashManifest(File.ReadAllText(hashesPath));

            // The manifest MUST pin both binary goldens (drift guard is meaningless if empty).
            Assert.True(recorded.Count >= 2, "fixture-hashes.txt must pin both binary goldens");

            foreach (var kv in recorded)
            {
                string fixturePath = FixturePath.Resolve("blender/" + kv.Key);
                string computed = ComputeSha256Hex(fixturePath);
                Assert.Equal(kv.Value, computed);
            }

            // Explicitly assert the two known goldens are present (not just whatever the manifest lists).
            Assert.True(recorded.ContainsKey("frn_all_bed_sm_s1_l0.msh"));
            Assert.True(recorded.ContainsKey("retail_mini_0005.tre"));
        }

        private static Dictionary<string, string> ParseHashManifest(string text)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            string[] lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                // Format: "{lowercase-hex-sha256}  {filename}" (two-space separator, sha256sum-style).
                int sep = line.IndexOf(' ');
                if (sep <= 0) continue;
                string hash = line.Substring(0, sep).Trim();
                string file = line.Substring(sep).Trim();
                // Strip a leading sha256sum binary-mode marker ('*') if present.
                if (file.StartsWith("*")) file = file.Substring(1);
                map[file] = hash.ToLowerInvariant();
            }
            return map;
        }

        private static string ComputeSha256Hex(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(fs);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                }
                return sb.ToString();
            }
        }
    }
}
