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
    /// ECO-01 (Phase 16-01) tests for the thin <c>validate-bundle</c> CLI verb. The verb is driven
    /// via the in-process CLI STRING runner — <see cref="InProcessCliRunner.Run(string[])"/> with the
    /// verb name as a string — so this file references NO type that Task 2 creates (WARNING-3 fix):
    /// the whole assembly compiles at the Task-1 boundary, and these cases are RED (the unregistered
    /// verb is a parse error, exit 1) until Task 2 registers + implements the verb, then GREEN.
    ///
    /// <para>Every bundle is constructed in a per-test temp directory (copying the committed pinned
    /// .msh in) so absolute-path refs are REAL and machine-portable — the committed Fixtures/blender/
    /// .rsp/.cfg/manifest are reference shapes (R3-5), not the bundle root the verb validates here.</para>
    /// </summary>
    public class ValidateBundleTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // Bundle-builder helper: a Blender-faithful static bundle in a temp dir.
        // ─────────────────────────────────────────────────────────────────────

        private sealed class Bundle : IDisposable
        {
            public string Root;
            public string ManifestPath;
            public string MeshAbs;      // absolute path of the contained mesh
            public string MeshRel;      // "appearance/mesh/frn_all_bed_sm_s1_l0.msh"
            public string RspPath;      // absolute path of rsp/data_compressed_mesh_static.rsp

            public void Dispose()
            {
                try { if (Root != null && Directory.Exists(Root)) Directory.Delete(Root, true); }
                catch { /* best-effort cleanup */ }
            }
        }

        // Builds a structurally-valid Blender static bundle. The .rsp RHS is an ABSOLUTE
        // forward-slashed path to the CONTAINED mesh (Blender-faithful per rsp_builder.format_rsp_line).
        private static Bundle BuildBundle(
            bool includeRspAbsoluteContainedLine = true,
            string extraRspLine = null,
            string clientCfgValue = null,
            string overrideMeshRefInAssets = null)
        {
            string root = Path.Combine(Path.GetTempPath(),
                "utinni-bundle-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            string meshRel = "appearance/mesh/frn_all_bed_sm_s1_l0.msh";
            string meshAbs = Path.Combine(root, meshRel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(meshAbs));
            File.Copy(FixturePath.Resolve("blender/frn_all_bed_sm_s1_l0.msh"), meshAbs);

            // rsp/data_compressed_mesh_static.rsp — Blender-faithful "{rel} @ {abs}" lines.
            string rspDir = Path.Combine(root, "rsp");
            Directory.CreateDirectory(rspDir);
            string rspPath = Path.Combine(rspDir, "data_compressed_mesh_static.rsp");
            string meshAbsFwd = meshAbs.Replace('\\', '/');
            var rspLines = new System.Text.StringBuilder();
            if (includeRspAbsoluteContainedLine)
            {
                rspLines.Append(meshRel + " @ " + meshAbsFwd + "\n");
            }
            if (extraRspLine != null)
            {
                rspLines.Append(extraRspLine);
                if (!extraRspLine.EndsWith("\n")) rspLines.Append("\n");
            }
            File.WriteAllText(rspPath, rspLines.ToString());

            // client_search_paths.cfg
            string cfgPath = Path.Combine(root, "client_search_paths.cfg");
            File.WriteAllText(cfgPath,
                "searchPath_00_12=" + (clientCfgValue ?? root.Replace('\\', '/')) + "\n");

            // swg_export_manifest.json — REAL exporter shape (client_cfg INSIDE assets).
            string assetMeshRef = overrideMeshRefInAssets ?? meshRel;
            var manifest = new JObject
            {
                ["pipeline_version"] = "3.009",
                ["exported_at"] = "2026-05-27T00:00:00+00:00",
                ["bundle_type"] = "static",
                ["output_dir"] = root.Replace('\\', '/'),
                ["author"] = "",
                ["notes"] = "",
                ["assets"] = new JObject
                {
                    ["output_dir"] = root.Replace('\\', '/'),
                    ["mesh"] = assetMeshRef,
                    ["shaders"] = new JArray(),
                    ["textures"] = new JArray(),
                    ["manifest"] = Path.Combine(root, "swg_export_manifest.json").Replace('\\', '/'),
                    ["rsp_files"] = new JArray("rsp/data_compressed_mesh_static.rsp"),
                    ["client_cfg"] = "client_search_paths.cfg"
                },
                ["rsp_files"] = new JArray("rsp/data_compressed_mesh_static.rsp"),
                // Unknown/extra top-level key — must be tolerated (forward-compat, CDX-NEW-10).
                ["client_test_notes"] = new JArray("mount as searchPath")
            };
            string manifestPath = Path.Combine(root, "swg_export_manifest.json");
            File.WriteAllText(manifestPath, manifest.ToString());

            return new Bundle
            {
                Root = root,
                ManifestPath = manifestPath,
                MeshAbs = meshAbs,
                MeshRel = meshRel,
                RspPath = rspPath
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // (a) success — contained-absolute .rsp line, all refs present → valid:true
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void ValidateBundle_HappyPathWithContainedAbsoluteRsp_ExitsZeroValidTrue()
        {
            using (var b = BuildBundle(includeRspAbsoluteContainedLine: true))
            {
                var result = InProcessCliRunner.Run("validate-bundle", b.ManifestPath);

                Assert.Equal(0, result.ExitCode);
                var root = JToken.Parse(result.Stdout);
                Assert.Equal("validate-bundle", root["command"].Value<string>());
                Assert.Null(root["error"]);

                JToken r = root["result"];
                Assert.True(r["valid"].Value<bool>());
                Assert.False(r["hasRejectedRefs"].Value<bool>());
                Assert.Empty((JArray)r["rejectedRefs"]);
                Assert.Empty((JArray)r["missingAssets"]);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // (b) missing-manifest → exit 3 FileNotFound
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void ValidateBundle_MissingManifest_ExitsThreeFileNotFound()
        {
            var result = InProcessCliRunner.Run("validate-bundle",
                @"Z:\nonexistent\swg_export_manifest.json");

            Assert.Equal(3, result.ExitCode);
            var root = JToken.Parse(result.Stdout);
            Assert.Equal("validate-bundle", root["command"].Value<string>());
            Assert.Equal("FileNotFound", root["error"]["kind"].Value<string>());
        }

        // ─────────────────────────────────────────────────────────────────────
        // (c) malformed manifest (invalid JSON) → exit 2 ParseError (C-16)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void ValidateBundle_MalformedManifestJson_ExitsTwoParseError()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "utinni-bundle-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string manifestPath = Path.Combine(root, "swg_export_manifest.json");
                File.WriteAllText(manifestPath, "{ this is not valid json ]]] ");

                var result = InProcessCliRunner.Run("validate-bundle", manifestPath);

                Assert.Equal(2, result.ExitCode);
                var j = JToken.Parse(result.Stdout);
                Assert.Equal("validate-bundle", j["command"].Value<string>());
                Assert.Equal("ParseError", j["error"]["kind"].Value<string>());
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // (d) malformed .rsp (line not "{path} @ {path}") → exit 2 ParseError (C-16)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void ValidateBundle_MalformedRspLine_ExitsTwoParseError()
        {
            using (var b = BuildBundle(includeRspAbsoluteContainedLine: true,
                       extraRspLine: "this line has no at-separator and is garbage"))
            {
                var result = InProcessCliRunner.Run("validate-bundle", b.ManifestPath);

                Assert.Equal(2, result.ExitCode);
                var root = JToken.Parse(result.Stdout);
                Assert.Equal("validate-bundle", root["command"].Value<string>());
                Assert.Equal("ParseError", root["error"]["kind"].Value<string>());
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // (e) ESCAPING refs (path-traversal): different drive/UNC + ".." relative.
        //     Reported as rejectedRefs findings; NEVER File.Exists-probed. (C-15, T-16-01)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void ValidateBundle_EscapingAbsoluteRef_ReportedNotProbed_ValidFalse()
        {
            // Build a manifest in-test whose .rsp RHS canonicalizes onto a DIFFERENT drive.
            using (var b = BuildBundle(
                       includeRspAbsoluteContainedLine: true,
                       extraRspLine: "appearance/mesh/evil.msh @ Z:/outside/evil.msh"))
            {
                var result = InProcessCliRunner.Run("validate-bundle", b.ManifestPath);

                // Structurally valid (parseable) → exit 0, but the escape is a finding.
                Assert.Equal(0, result.ExitCode);
                var root = JToken.Parse(result.Stdout);
                JToken r = root["result"];
                Assert.False(r["valid"].Value<bool>());
                Assert.True(r["hasRejectedRefs"].Value<bool>());
                Assert.NotEmpty((JArray)r["rejectedRefs"]);

                // The escaping path must appear in rejectedRefs (it was rejected, not silently dropped).
                string blob = r["rejectedRefs"].ToString();
                Assert.Contains("evil.msh", blob);
            }
        }

        [Fact]
        public void ValidateBundle_EscapingDotDotRelativeRef_ReportedNotProbed_ValidFalse()
        {
            using (var b = BuildBundle(
                       includeRspAbsoluteContainedLine: true,
                       extraRspLine: "../../../etc/passwd.msh @ ../../../etc/passwd.msh"))
            {
                var result = InProcessCliRunner.Run("validate-bundle", b.ManifestPath);

                Assert.Equal(0, result.ExitCode);
                var root = JToken.Parse(result.Stdout);
                JToken r = root["result"];
                Assert.False(r["valid"].Value<bool>());
                Assert.True(r["hasRejectedRefs"].Value<bool>());
                Assert.NotEmpty((JArray)r["rejectedRefs"]);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // (f) CONTAINED-absolute (CUR-NEW-3): absolute ref whose canonical form is UNDER the
        //     bundle root → ALLOWED, File.Exists-checked, NOT in rejectedRefs.
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void ValidateBundle_ContainedAbsoluteRef_AllowedAndCheckedNotRejected()
        {
            // The happy-path bundle's .rsp already carries a contained-absolute line pointing at the
            // committed pinned .msh under the bundle root. Assert it is treated as a normal asset.
            using (var b = BuildBundle(includeRspAbsoluteContainedLine: true))
            {
                var result = InProcessCliRunner.Run("validate-bundle", b.ManifestPath);

                Assert.Equal(0, result.ExitCode);
                var root = JToken.Parse(result.Stdout);
                JToken r = root["result"];
                Assert.True(r["valid"].Value<bool>());
                Assert.Empty((JArray)r["rejectedRefs"]);
                // The contained mesh exists, so it is NOT a missing asset.
                Assert.Empty((JArray)r["missingAssets"]);
                Assert.True(r["assetsChecked"].Value<int>() >= 1);
            }
        }

        // A contained-absolute ref that does NOT exist on disk → allowed (contained) but a
        // missingAsset finding (it was File.Exists-probed because it is contained), valid:false.
        [Fact]
        public void ValidateBundle_ContainedAbsoluteRefMissingFile_IsMissingAssetNotRejected()
        {
            using (var b = BuildBundle(includeRspAbsoluteContainedLine: true))
            {
                string ghostAbs = Path.Combine(b.Root, "appearance", "mesh", "ghost.msh")
                    .Replace('\\', '/');
                // Re-author the .rsp to ADD a contained-but-missing absolute ref.
                File.AppendAllText(b.RspPath,
                    "appearance/mesh/ghost.msh @ " + ghostAbs + "\n");

                var result = InProcessCliRunner.Run("validate-bundle", b.ManifestPath);

                Assert.Equal(0, result.ExitCode);
                var root = JToken.Parse(result.Stdout);
                JToken r = root["result"];
                Assert.False(r["valid"].Value<bool>());
                // Contained-but-missing is a missingAsset, NOT a rejectedRef (it was allowed + probed).
                Assert.NotEmpty((JArray)r["missingAssets"]);
                Assert.Empty((JArray)r["rejectedRefs"]);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // R3-6 paired containment-equivalence: prove the relative-branch (LooseOverridePath) and the
        // absolute-branch (Path.GetFullPath + shared IsContainedUnderRoot) give IDENTICAL verdicts
        // for contained / escaping / messy-root / sibling-prefix cases — driven through the SHIPPED
        // verb so the absolute branch provably can't drift from the algorithm.
        // ─────────────────────────────────────────────────────────────────────

        // Messy root "C:\bundle\..\bundle" requires the verb to re-canonicalize the root (WR-07).
        // A contained absolute under that messy root must still be ALLOWED.
        [Fact]
        public void ValidateBundle_MessyRootContainedAbsolute_AllowedAfterRootRecanonicalization()
        {
            using (var b = BuildBundle(includeRspAbsoluteContainedLine: true))
            {
                // Pass the manifest via a messy "<root>\..\<leaf>" path that canonicalizes back to root.
                string leaf = Path.GetFileName(b.Root);
                string messyManifest = Path.Combine(b.Root, "..", leaf, "swg_export_manifest.json");

                var result = InProcessCliRunner.Run("validate-bundle", messyManifest);

                Assert.Equal(0, result.ExitCode);
                var root = JToken.Parse(result.Stdout);
                JToken r = root["result"];
                // The contained-absolute mesh stays allowed even with a messy root (root re-canon).
                Assert.True(r["valid"].Value<bool>());
                Assert.Empty((JArray)r["rejectedRefs"]);
            }
        }

        // Sibling-prefix attack: an absolute ref into "<root>X\..." must be REJECTED (the trailing-
        // separator gate prevents "C:\bundleX" from passing a "C:\bundle" prefix check).
        [Fact]
        public void ValidateBundle_SiblingPrefixAbsoluteRef_Rejected()
        {
            using (var b = BuildBundle(includeRspAbsoluteContainedLine: true))
            {
                string sibling = (b.Root + "X").Replace('\\', '/') + "/evil.msh";
                File.AppendAllText(b.RspPath,
                    "appearance/mesh/sibling.msh @ " + sibling + "\n");

                var result = InProcessCliRunner.Run("validate-bundle", b.ManifestPath);

                Assert.Equal(0, result.ExitCode);
                var root = JToken.Parse(result.Stdout);
                JToken r = root["result"];
                Assert.False(r["valid"].Value<bool>());
                Assert.True(r["hasRejectedRefs"].Value<bool>());
                string blob = r["rejectedRefs"].ToString();
                Assert.Contains("sibling.msh", blob);
            }
        }

        // NOTE: the C-17 doc↔verb bucket-parity fact is added in Task 3 (it references the shipped
        // ValidateBundleCommand.BucketFilenames table, which does not exist at the Task-1 boundary).
    }
}
