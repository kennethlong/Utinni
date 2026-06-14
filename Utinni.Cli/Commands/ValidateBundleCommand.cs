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
using CommandLine;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Saving;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("validate-bundle", HelpText =
        "Validate a Blender export bundle (swg_export_manifest.json + .rsp + .cfg) as TEXT - "
        + "no 3D/IFF codec. Emits a JSON envelope with explicit valid/hasRejectedRefs fields.")]
    public class ValidateBundleOptions
    {
        [Value(0, MetaName = "path", Required = true,
            HelpText = "Path to a bundle root OR a swg_export_manifest.json file.")]
        public string Path { get; set; }
    }

    /// <summary>
    /// ECO-01 (Phase 16-01) — the thin <c>validate-bundle</c> verb. Validates the TEXT contract
    /// surface of a Blender export bundle (manifest JSON + .rsp search-path lines + .cfg fragment)
    /// and existence-checks referenced assets whose canonical path is CONTAINED under the bundle
    /// root. It opens NO binary mesh/IFF/TRE payloads (DEC-A3 clean — the contract doc points at
    /// parse-tre / decode-iff / inspect-iff for those; reachability is documented, not re-implemented).
    ///
    /// <para><b>Error taxonomy</b> (mirrors InspectIffCommand): missing path → FileNotFound exit 3;
    /// an unparseable manifest/.rsp → ParseError exit 2; IOException → IoError exit 2. A
    /// STRUCTURALLY-valid bundle (parseable manifest/.rsp/.cfg) exits 0 EVEN WITH missing/rejected
    /// refs — those are findings carried in the envelope, not parse failures. Agents MUST read the
    /// envelope <c>valid</c>/<c>hasRejectedRefs</c> fields, not the exit code alone (CDX-NEW-9).</para>
    ///
    /// <para><b>Path containment</b> (C-15 / T-16-01 / CUR-NEW-3 / R3-6): every asset ref drawn from
    /// the manifest or a .rsp RHS is classified through the SINGLE shared
    /// <see cref="LooseOverridePath.IsContainedUnderRoot"/> predicate — the source-of-truth
    /// containment gate (see LooseOverridePath.cs). A RELATIVE ref routes through
    /// <see cref="LooseOverridePath.Resolve"/> (which uses the SAME predicate internally); an
    /// ABSOLUTE ref (the real-Blender .rsp RHS, CUR-NEW-3) is canonicalized via Path.GetFullPath then
    /// passed to the SAME predicate. A CONTAINED absolute is ALLOWED and File.Exists-checked; an
    /// ESCAPING ref is recorded as a rejectedRefs finding and is NEVER File.Exists-probed.</para>
    ///
    /// <para><b>Symlink/junction policy:</b> containment is decided LEXICALLY on the canonical path
    /// (Path.GetFullPath). This text validator does NOT follow symlinks/junctions — a
    /// contained-but-symlinked target pointing off-bundle is out of scope (the live tier's runtime
    /// root containment is the enforcement boundary).</para>
    /// </summary>
    public static class ValidateBundleCommand
    {
        /// <summary>
        /// The suffix → bucket → .rsp filename map (RESEARCH B2, sourced from swg-blender-plugin
        /// rsp_builder.py: _BUCKET_RULES + RSP_FILENAMES). Single named table so the Task-3 doc↔verb
        /// parity test (C-17) can assert these filenames appear verbatim in the contract doc.
        /// </summary>
        private static readonly KeyValuePair<string, string>[] SuffixBucketRules = new[]
        {
            new KeyValuePair<string, string>(".mp3", "music"),
            new KeyValuePair<string, string>(".wav", "sample"),
            new KeyValuePair<string, string>(".dds", "texture"),
            new KeyValuePair<string, string>(".ans", "animation"),
            new KeyValuePair<string, string>(".mgn", "mesh_skeletal"),
            new KeyValuePair<string, string>(".msh", "mesh_static"),
        };

        private static readonly Dictionary<string, string> BucketRspFilename =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "music", "data_uncompressed_music.rsp" },
                { "sample", "data_uncompressed_sample.rsp" },
                { "texture", "data_compressed_texture.rsp" },
                { "animation", "data_compressed_animation.rsp" },
                { "mesh_static", "data_compressed_mesh_static.rsp" },
                { "mesh_skeletal", "data_compressed_mesh_skeletal.rsp" },
                { "other", "data_compressed_other.rsp" },
            };

        /// <summary>
        /// The concrete .rsp bucket filenames the verb knows about — the SAME literal set the
        /// Task-3 contract doc must list verbatim (C-17 doc↔verb parity contract).
        /// </summary>
        public static string[] BucketFilenames
        {
            get
            {
                var list = new List<string>(BucketRspFilename.Values);
                list.Sort(StringComparer.Ordinal);
                return list.ToArray();
            }
        }

        public static int Run(ValidateBundleOptions o)
        {
            // Resolve the manifest path: accept a bundle root (dir) OR a manifest file path.
            string manifestPath;
            string bundleRoot;
            if (Directory.Exists(o.Path))
            {
                bundleRoot = Path.GetFullPath(o.Path);
                manifestPath = Path.Combine(bundleRoot, "swg_export_manifest.json");
            }
            else
            {
                manifestPath = o.Path;
                bundleRoot = Path.GetDirectoryName(Path.GetFullPath(o.Path));
            }

            if (!File.Exists(manifestPath))
            {
                return JsonOutput.EmitError("validate-bundle", "FileNotFound",
                    "Bundle manifest not found: " + manifestPath, exitCode: 3);
            }

            try
            {
                bundleRoot = Path.GetFullPath(bundleRoot);

                // Parse the manifest as the authoritative bundle index. A JSON failure → ParseError.
                JObject manifest;
                try
                {
                    manifest = JObject.Parse(File.ReadAllText(manifestPath));
                }
                catch (Newtonsoft.Json.JsonException ex)
                {
                    return JsonOutput.EmitError("validate-bundle", "ParseError",
                        "Malformed manifest JSON: " + ex.Message, exitCode: 2);
                }

                var assetsChecked = 0;
                var missingAssets = new List<string>();
                var rejectedRefs = new List<JObject>();
                var bucketMismatches = new List<string>();
                var rspFilesValidated = new List<string>();

                // ── Gather asset refs from the REAL manifest shape (R3-5) ──
                // The `assets` object (mesh, shaders[], textures[], rsp_files[], client_cfg) plus the
                // top-level `rsp_files`. Unknown top-level fields are tolerated (CDX-NEW-10).
                var manifestRefs = new List<string>();
                JObject assets = manifest["assets"] as JObject;
                if (assets != null)
                {
                    AddRef(manifestRefs, assets["mesh"]);
                    AddArrayRefs(manifestRefs, assets["shaders"]);
                    AddArrayRefs(manifestRefs, assets["textures"]);
                    AddRef(manifestRefs, assets["client_cfg"]);
                    // assets.rsp_files are the .rsp files themselves — validated as .rsp below.
                }

                // The .rsp file list (top-level and/or inside assets).
                var rspRefs = new List<string>();
                AddArrayRefs(rspRefs, manifest["rsp_files"]);
                if (assets != null) AddArrayRefs(rspRefs, assets["rsp_files"]);

                // ── Existence-check the manifest asset refs through the shared containment gate ──
                foreach (string r in manifestRefs)
                {
                    CheckAssetRef(r, bundleRoot, "manifest",
                        ref assetsChecked, missingAssets, rejectedRefs);
                }

                // ── Validate each .rsp file: parse lines, classify buckets, check RHS refs ──
                foreach (string rspRel in DistinctOrder(rspRefs))
                {
                    string rspResolved;
                    if (!TryContainResolve(rspRel, bundleRoot, out rspResolved, rejectedRefs, ".rsp"))
                    {
                        // The .rsp file path itself escapes the root — recorded as a rejected ref.
                        continue;
                    }
                    if (!File.Exists(rspResolved))
                    {
                        missingAssets.Add(rspRel);
                        continue;
                    }

                    rspFilesValidated.Add(rspRel);
                    ValidateRspFile(rspResolved, bundleRoot,
                        ref assetsChecked, missingAssets, rejectedRefs, bucketMismatches);
                }

                // ── Validate the client_search_paths.cfg fragment grammar (if referenced) ──
                if (assets != null)
                {
                    string cfgRel = (assets["client_cfg"] != null) ? assets["client_cfg"].ToString() : null;
                    if (!string.IsNullOrEmpty(cfgRel))
                    {
                        string cfgResolved;
                        if (TryContainResolve(cfgRel, bundleRoot, out cfgResolved, rejectedRefs, ".cfg")
                            && File.Exists(cfgResolved))
                        {
                            ValidateCfgFragment(cfgResolved);
                        }
                    }
                }

                bool hasRejectedRefs = rejectedRefs.Count > 0;
                bool valid = !hasRejectedRefs
                    && missingAssets.Count == 0
                    && bucketMismatches.Count == 0;

                var result = new JObject
                {
                    ["assetsChecked"] = assetsChecked,
                    ["bucketMismatches"] = new JArray(bucketMismatches.ToArray()),
                    ["bundleRoot"] = bundleRoot.Replace('\\', '/'),
                    ["hasRejectedRefs"] = hasRejectedRefs,
                    ["missingAssets"] = new JArray(missingAssets.ToArray()),
                    ["rejectedRefs"] = new JArray(rejectedRefs.ToArray()),
                    ["rspFilesValidated"] = new JArray(rspFilesValidated.ToArray()),
                    ["valid"] = valid
                };

                return JsonOutput.EmitSuccess("validate-bundle", result);
            }
            catch (ParseError ex)
            {
                return JsonOutput.EmitError("validate-bundle", "ParseError", ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("validate-bundle", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception intentionally NOT caught — unexpected types bubble for diagnosis.
        }

        // ── .rsp parsing: each line is "{treefile_path} @ {explicit_path}" (rsp_builder.py) ──
        private static void ValidateRspFile(
            string rspPath, string bundleRoot,
            ref int assetsChecked, List<string> missingAssets,
            List<JObject> rejectedRefs, List<string> bucketMismatches)
        {
            string[] lines = File.ReadAllText(rspPath)
                .Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            string rspFileName = Path.GetFileName(rspPath);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;

                // Grammar: exactly "{lhs} @ {rhs}" — a line without the two-field "@" form is a
                // structural .rsp parse failure (ParseError exit 2, C-16).
                int sep = line.IndexOf(" @ ", StringComparison.Ordinal);
                if (sep < 0)
                {
                    throw new ParseError("Malformed .rsp line in " + rspFileName
                        + " (expected '{treefile_path} @ {explicit_path}'): " + line);
                }
                string lhs = line.Substring(0, sep).Trim();
                string rhs = line.Substring(sep + 3).Trim();
                if (lhs.Length == 0 || rhs.Length == 0)
                {
                    throw new ParseError("Malformed .rsp line in " + rspFileName
                        + " (empty field): " + line);
                }

                // Bucket classification of the LHS treefile path — the bucket's expected .rsp
                // filename must match the file this line lives in (doc↔verb bucket rules).
                string bucket = ClassifyBucket(lhs);
                string expectedFile;
                if (BucketRspFilename.TryGetValue(bucket, out expectedFile)
                    && !string.Equals(expectedFile, rspFileName, StringComparison.OrdinalIgnoreCase))
                {
                    bucketMismatches.Add(lhs + " (bucket " + bucket + " expects "
                        + expectedFile + " but is in " + rspFileName + ")");
                }

                // The RHS explicit_path is normally ABSOLUTE (real Blender). Check it through the
                // shared containment gate: contained → File.Exists-checked; escaping → rejected,
                // never probed (CUR-NEW-3 / C-15).
                CheckAssetRef(rhs, bundleRoot, ".rsp:" + rspFileName,
                    ref assetsChecked, missingAssets, rejectedRefs);
            }
        }

        // ── .cfg fragment grammar: searchPath{priority}= / searchPath_{sku:02d}_{priority}= ──
        private static void ValidateCfgFragment(string cfgPath)
        {
            string[] lines = File.ReadAllText(cfgPath)
                .Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    throw new ParseError("Malformed client cfg fragment line (expected 'key=value'): "
                        + line);
                }
                string key = line.Substring(0, eq).Trim();
                // Accept the legacy 'searchPath{priority}' and SWGSource 'searchPath_{sku}_{priority}'
                // dialects (rsp_builder.client_search_path_snippet).
                if (!key.StartsWith("searchPath", StringComparison.Ordinal))
                {
                    throw new ParseError("Unexpected client cfg key (expected a searchPath* override): "
                        + key);
                }
            }
        }

        // Classify a treefile path into its bucket (suffix match, then catch-all "other").
        private static string ClassifyBucket(string relpath)
        {
            string lower = relpath.Replace('\\', '/').ToLowerInvariant();
            foreach (var rule in SuffixBucketRules)
            {
                if (lower.EndsWith(rule.Key, StringComparison.Ordinal)) return rule.Value;
            }
            return "other";
        }

        // Classify a ref through the SINGLE shared containment gate; existence-check contained refs.
        private static void CheckAssetRef(
            string reference, string bundleRoot, string origin,
            ref int assetsChecked, List<string> missingAssets, List<JObject> rejectedRefs)
        {
            if (string.IsNullOrWhiteSpace(reference)) return;

            string resolved;
            if (!TryContainResolve(reference, bundleRoot, out resolved, rejectedRefs, origin))
            {
                // Escaping ref — already recorded as rejectedRefs by TryContainResolve. NEVER probed.
                return;
            }

            assetsChecked++;
            if (!File.Exists(resolved) && !Directory.Exists(resolved))
            {
                missingAssets.Add(reference);
            }
        }

        // Routes BOTH relative and absolute refs through the SAME shared IsContainedUnderRoot
        // predicate (source-of-truth: LooseOverridePath.cs). Returns true with the canonical
        // contained path; returns false and records a rejectedRefs finding for any escape.
        private static bool TryContainResolve(
            string reference, string bundleRoot, out string resolved,
            List<JObject> rejectedRefs, string origin)
        {
            resolved = null;
            string normalized = reference.Trim();

            if (System.IO.Path.IsPathRooted(normalized))
            {
                // ABSOLUTE ref (the real-Blender .rsp RHS case, CUR-NEW-3). LooseOverridePath.Resolve
                // rejects rooted inputs by design, so canonicalize then route through the SAME shared
                // predicate the relative branch uses. Never File.Exists-probe an escape.
                string canonical;
                try
                {
                    canonical = System.IO.Path.GetFullPath(normalized);
                }
                catch (Exception ex)
                {
                    rejectedRefs.Add(Reject(reference, origin, "uncanonicalizable absolute path: " + ex.Message));
                    return false;
                }

                if (LooseOverridePath.IsContainedUnderRoot(bundleRoot, canonical))
                {
                    resolved = canonical;
                    return true;
                }
                rejectedRefs.Add(Reject(reference, origin, "absolute path escapes the bundle root"));
                return false;
            }

            // RELATIVE ref → LooseOverridePath.Resolve (uses the SAME IsContainedUnderRoot internally;
            // rejects rooted/'..' escapes by throwing ArgumentException — recorded, never probed).
            try
            {
                resolved = LooseOverridePath.Resolve(bundleRoot, normalized);
                return true;
            }
            catch (ArgumentException ex)
            {
                rejectedRefs.Add(Reject(reference, origin, "relative ref escapes the bundle root: " + ex.Message));
                return false;
            }
        }

        private static JObject Reject(string reference, string origin, string reason)
        {
            return new JObject
            {
                ["origin"] = origin,
                ["reason"] = reason,
                ["ref"] = reference
            };
        }

        private static void AddRef(List<string> sink, JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return;
            string s = token.ToString();
            if (!string.IsNullOrWhiteSpace(s)) sink.Add(s);
        }

        private static void AddArrayRefs(List<string> sink, JToken token)
        {
            JArray arr = token as JArray;
            if (arr == null) return;
            foreach (JToken t in arr) AddRef(sink, t);
        }

        private static IEnumerable<string> DistinctOrder(List<string> items)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string s in items)
            {
                if (seen.Add(s)) yield return s;
            }
        }

        /// <summary>
        /// Internal structural-parse exception for the manifest/.rsp/.cfg text grammar. Surfaced as
        /// the JSON "ParseError" exit-2 taxonomy by <see cref="Run"/>.
        /// </summary>
        private sealed class ParseError : Exception
        {
            public ParseError(string message) : base(message) { }
        }
    }
}
