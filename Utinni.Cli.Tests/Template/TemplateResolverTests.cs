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
// Plan 23-04 resolver + pack-store goldens. The method names embed the VALIDATION.md --filter tokens:
//   Template&VersionMatch  — version-FORM-aware match (tag-only widen vs version-specific).
//   Template&Precedence    — D-05 altitude gate (built-in root FORM suppresses all templates).
//   Template&MultiMatch    — most-specific-key-wins + genuine-tie candidate list.
//   Template&Fit&Flag      — a key-matched but non-round-tripping template is SURFACED with a FitReport.
//   Template&PackStore[&Containment|&Order] — scanned allow-list, path containment, load-order shadow.

using System.Collections.Generic;
using System.IO;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.Template;
using Xunit;

namespace Utinni.Cli.Tests.Template
{
    public sealed class TemplateResolverTests
    {
        // ── shared helpers (also consumed by KernelCodecTests' delegating anchors) ──

        /// <summary>Reads a fixture byte[] through the framework IFF reader into a mutable DOM.</summary>
        public static MutableIffDocument LoadMutable(byte[] file)
        {
            using (var ms = new MemoryStream(file))
            {
                IffDocument doc = IffReader.Read(ms);
                return MutableIffDocument.FromDocument(doc, file);
            }
        }

        /// <summary>
        /// A CPAP template for a given version-FORM with the matching float-count layout. The ancestor
        /// path carries the version-FORM segment (D-04 version-FORM awareness). floatCount=1 mirrors the
        /// low-version layout, floatCount=5 the widened high-version layout.
        /// </summary>
        public static TemplateModel CpapTemplate(string version, int floatCount)
        {
            var fields = new List<FieldRecord>
            {
                new FieldRecord { Name = "appearanceTemplateName", Type = KernelType.CString, Encoding = "ascii" }
            };
            for (int i = 0; i < floatCount; i++)
            {
                fields.Add(new FieldRecord { Name = "f" + i, Type = KernelType.F32 });
            }

            return new TemplateModel
            {
                Version = 1,
                MatchAncestorPath = "FORM:TPLX/0/FORM:" + version,
                MatchLeafTag = "CPAP",
                MatchTagOnly = false,
                Fields = fields
            };
        }

        // A tag-only-widened CPAP template (ancestor prefix dropped); still version-FORM aware in that it
        // only resolves where the leaf carries a version-FORM in its path.
        private static TemplateModel CpapTagOnlyTemplate(int floatCount)
        {
            TemplateModel t = CpapTemplate(TemplateTestFixtures.VersionLow, floatCount);
            t.MatchAncestorPath = null;
            t.MatchTagOnly = true;
            return t;
        }

        // ── Template&VersionMatch ──────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "TemplateVersionMatch")]
        public void Template_VersionMatch_PicksVersionSpecificOverWrongVersion()
        {
            byte[] file = TemplateTestFixtures.BuildVersionDivergent(TemplateTestFixtures.VersionHigh);
            MutableIffDocument doc = LoadMutable(file);
            string leafId = "FORM:TPLX/0/FORM:0003/0/CPAP:CPAP/0";

            var low = CpapTemplate(TemplateTestFixtures.VersionLow, 1);
            var high = CpapTemplate(TemplateTestFixtures.VersionHigh, 5);
            var resolver = new TemplateResolver(new[] { low, high });

            TemplateResolution res = resolver.Resolve(doc, leafId);

            // The 0001 template's ancestor path is NOT a prefix of a 0003 leaf -> only the 0003 wins.
            Assert.Single(res.Candidates);
            Assert.Equal("FORM:TPLX/0/FORM:0003", res.Best.Template.MatchAncestorPath);
            Assert.True(res.Best.VersionFormAware);
            Assert.True(res.Best.Fits);
        }

        [Fact]
        [Trait("Category", "TemplateVersionMatch")]
        public void Template_VersionMatch_TagOnlyWiden_MatchesAcrossVersionForms()
        {
            byte[] file = TemplateTestFixtures.BuildVersionDivergent(TemplateTestFixtures.VersionLow);
            MutableIffDocument doc = LoadMutable(file);
            string leafId = "FORM:TPLX/0/FORM:0001/0/CPAP:CPAP/0";

            var tagOnly = CpapTagOnlyTemplate(1);
            var resolver = new TemplateResolver(new[] { tagOnly });

            TemplateResolution res = resolver.Resolve(doc, leafId);

            Assert.NotNull(res.Best);
            Assert.True(res.Best.VersionFormAware, "a tag-only widen still requires the leaf carry a version-FORM");
            Assert.True(res.Best.Fits);
        }

        // ── Template&Precedence (D-05 altitude) ─────────────────────────────────────

        [Fact]
        [Trait("Category", "TemplatePrecedence")]
        public void Template_Precedence_BuiltinRoot_SuppressesEvenTagOnlyTemplates()
        {
            byte[] file = TemplateTestFixtures.BuildBuiltinRootFile(); // root FORM CLEF
            MutableIffDocument doc = LoadMutable(file);
            string leafId = "FORM:CLEF/0/FORM:0001/0/CPAP:CPAP/0";

            var resolver = new TemplateResolver(new[] { CpapTagOnlyTemplate(1) });
            TemplateResolution res = resolver.Resolve(doc, leafId);

            Assert.True(res.SuppressedByBuiltin);
            Assert.Null(res.Best);
            Assert.Empty(res.Candidates);
        }

        [Fact]
        [Trait("Category", "TemplatePrecedence")]
        public void Template_Precedence_NonBuiltinRoot_AllowsTemplate()
        {
            byte[] file = TemplateTestFixtures.BuildVersionDivergent(TemplateTestFixtures.VersionLow); // root TPLX
            MutableIffDocument doc = LoadMutable(file);
            string leafId = "FORM:TPLX/0/FORM:0001/0/CPAP:CPAP/0";

            var resolver = new TemplateResolver(new[] { CpapTemplate(TemplateTestFixtures.VersionLow, 1) });
            TemplateResolution res = resolver.Resolve(doc, leafId);

            Assert.False(res.SuppressedByBuiltin);
            Assert.NotNull(res.Best);
        }

        // ── Template&MultiMatch (most-specific wins; genuine tie surfaced) ──────────

        [Fact]
        [Trait("Category", "TemplateMultiMatch")]
        public void Template_MultiMatch_MostSpecificAncestorWinsOverTagOnly()
        {
            byte[] file = TemplateTestFixtures.BuildVersionDivergent(TemplateTestFixtures.VersionLow);
            MutableIffDocument doc = LoadMutable(file);
            string leafId = "FORM:TPLX/0/FORM:0001/0/CPAP:CPAP/0";

            var specific = CpapTemplate(TemplateTestFixtures.VersionLow, 1); // full ancestor path
            var widened = CpapTagOnlyTemplate(1);                            // tag-only (specificity 0)
            var resolver = new TemplateResolver(new[] { widened, specific });

            TemplateResolution res = resolver.Resolve(doc, leafId);

            Assert.Single(res.Candidates); // the specific one is strictly more specific -> sole winner
            Assert.Same(specific, res.Best.Template);
            Assert.False(res.Best.Template.MatchTagOnly);
        }

        [Fact]
        [Trait("Category", "TemplateMultiMatch")]
        public void Template_MultiMatch_GenuineTie_ReturnsCandidateList()
        {
            byte[] file = TemplateTestFixtures.BuildVersionDivergent(TemplateTestFixtures.VersionLow);
            MutableIffDocument doc = LoadMutable(file);
            string leafId = "FORM:TPLX/0/FORM:0001/0/CPAP:CPAP/0";

            // Two templates with the SAME (full) ancestor key -> a genuine tie the UI must disambiguate.
            var a = CpapTemplate(TemplateTestFixtures.VersionLow, 1);
            var b = CpapTemplate(TemplateTestFixtures.VersionLow, 1);
            var resolver = new TemplateResolver(new[] { a, b });

            TemplateResolution res = resolver.Resolve(doc, leafId);

            Assert.Equal(2, res.Candidates.Count);
            Assert.NotNull(res.Best);
        }

        // ── Template&Fit&Flag (key-matched but non-round-tripping is surfaced) ──────

        [Fact]
        [Trait("Category", "TemplateFit")]
        public void Template_Fit_Flag_KeyMatchButWrongLayout_SurfacedWithDoesNotFit()
        {
            // A 0003 (wide, 5-float) file, but the (only) registered template is the 0001 1-float layout
            // WIDENED to tag-only so it KEY-matches the 0003 leaf. It will key-match but NOT round-trip
            // (leftover bytes) -> returned WITH a does-not-fit FitReport, not silently applied.
            byte[] file = TemplateTestFixtures.BuildVersionDivergent(TemplateTestFixtures.VersionHigh);
            MutableIffDocument doc = LoadMutable(file);
            string leafId = "FORM:TPLX/0/FORM:0003/0/CPAP:CPAP/0";

            var wrongLayout = CpapTagOnlyTemplate(1); // 1 float, but the payload has 5
            var resolver = new TemplateResolver(new[] { wrongLayout });

            TemplateResolution res = resolver.Resolve(doc, leafId);

            Assert.NotNull(res.Best);
            Assert.NotNull(res.Best.Fit);
            Assert.False(res.Best.Fits, "a wrong-layout template must report does-not-fit, not be silently applied");
            Assert.True(res.Best.Fit.BytesConsumed < res.Best.Fit.BytesTotal);
        }

        [Fact]
        [Trait("Category", "TemplateVersionMatch")]
        public void Template_VersionMatch_TagOnly_LeafWithNoVersionForm_DoesNotMatch()
        {
            // CR-01 regression: a tag-only template must NOT match a leaf whose stable-id path carries no
            // enclosing FORM/LIST/CAT version sub-type. Dropping version-FORM awareness is exactly the
            // CLEF-CPAP 3-layout silent mis-decode D-04 forbids. The leaf id has only the bare leaf
            // segment (no FORM ancestor), so ExtractVersionFormSegment returns null.
            var tagOnly = CpapTagOnlyTemplate(1);
            var resolver = new TemplateResolver(new[] { tagOnly });

            TemplateResolution res = resolver.MatchKey("CPAP:CPAP/0", "CPAP");

            Assert.Null(res.Best);
            Assert.Empty(res.Candidates);
        }

        [Fact]
        [Trait("Category", "TemplateVersionMatch")]
        public void Template_VersionMatch_EmptyAncestor_LeafWithNoVersionForm_DoesNotMatch()
        {
            // CR-01 regression (empty-ancestor branch): a template with neither MatchTagOnly nor a
            // MatchAncestorPath must still require the leaf carry a version-FORM segment.
            TemplateModel emptyAncestor = CpapTemplate(TemplateTestFixtures.VersionLow, 1);
            emptyAncestor.MatchAncestorPath = null;
            emptyAncestor.MatchTagOnly = false;
            var resolver = new TemplateResolver(new[] { emptyAncestor });

            TemplateResolution res = resolver.MatchKey("CPAP:CPAP/0", "CPAP");

            Assert.Null(res.Best);
            Assert.Empty(res.Candidates);
        }

        // ── corpus-query substrate (D-17.4): MatchKey is a path predicate, no document needed ──

        [Fact]
        [Trait("Category", "TemplateVersionMatch")]
        public void Template_VersionMatch_MatchKey_IsAReusablePathPredicate()
        {
            var t = CpapTemplate(TemplateTestFixtures.VersionLow, 1);
            var resolver = new TemplateResolver(new[] { t });

            TemplateResolution hit = resolver.MatchKey("FORM:TPLX/0/FORM:0001/0/CPAP:CPAP/0", "CPAP");
            TemplateResolution miss = resolver.MatchKey("FORM:TPLX/0/FORM:0003/0/CPAP:CPAP/0", "CPAP");

            Assert.NotNull(hit.Best);
            Assert.Null(hit.Best.Fit); // no payload supplied -> no fit-check
            Assert.Null(miss.Best);    // 0001 ancestor key is not a prefix of a 0003 path
        }
    }

    /// <summary>
    /// Plan 23-04 Task 2: the D-12 scanned-dir allow-list loader goldens (filter Template&PackStore).
    /// Each test writes pack JSON into temp dirs, scans, and asserts load-order shadow / malformed-skip /
    /// path-containment. Temp dirs are created + torn down per test (IDisposable).
    /// </summary>
    public sealed class TemplatePackStoreTests : System.IDisposable
    {
        private readonly string baseDir;

        public TemplatePackStoreTests()
        {
            baseDir = Path.Combine(Path.GetTempPath(), "utinni-packstore-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(baseDir);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(baseDir)) Directory.Delete(baseDir, recursive: true); }
            catch { /* best-effort temp cleanup */ }
        }

        private string MakeRoot(string name)
        {
            string dir = Path.Combine(baseDir, name);
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static TemplateModel Template(string ancestor, string leafTag, int floatCount)
        {
            var fields = new List<FieldRecord>
            {
                new FieldRecord { Name = "name", Type = KernelType.CString, Encoding = "ascii" }
            };
            for (int i = 0; i < floatCount; i++) fields.Add(new FieldRecord { Name = "f" + i, Type = KernelType.F32 });
            return new TemplateModel
            {
                Version = 1,
                MatchAncestorPath = ancestor,
                MatchLeafTag = leafTag,
                Fields = fields
            };
        }

        private static void WritePack(string dir, string fileName, TemplateModel t)
        {
            File.WriteAllText(Path.Combine(dir, fileName), TemplateJson.Serialize(t));
        }

        // ── Template&PackStore (scan + malformed-skip) ──────────────────────────────

        [Fact]
        [Trait("Category", "TemplatePackStore")]
        public void Template_PackStore_LoadAll_ScansValidAndSkipsMalformedWithReason()
        {
            string root = MakeRoot("packs");
            WritePack(root, "good.json", Template("FORM:TPLX/0/FORM:0001", "CPAP", 1));
            File.WriteAllText(Path.Combine(root, "broken.json"), "{ this is : not valid json ");

            var store = new TemplatePackStore(new[]
            {
                new TemplatePackRoot { Directory = root, Priority = 0, Label = "test" }
            });

            List<TemplateLoadResult> results = store.LoadAll();

            Assert.Contains(results, r => r.Loaded && r.Template.MatchLeafTag == "CPAP");
            // The malformed file is SKIPPED with a captured reason, not a hard failure.
            Assert.Contains(results, r => !r.Loaded && r.SkipReason != null && r.Path.EndsWith("broken.json"));
            // The whole scan still produced the good template (one bad file did not abort).
            Assert.Single(store.LoadEffective());
        }

        // ── Template&PackStore&Containment ──────────────────────────────────────────

        [Fact]
        [Trait("Category", "TemplatePackStore")]
        public void Template_PackStore_Containment_RejectsFileOutsideRoot()
        {
            string root = MakeRoot("contained");
            string outside = MakeRoot("outside");
            WritePack(outside, "escapee.json", Template("FORM:TPLX/0/FORM:0001", "ESCP", 1));

            // The store scans `root`, but we assert containment by routing an outside file path through the
            // store's load gate via a root whose canonical form does NOT contain the outside file.
            var store = new TemplatePackStore(new[]
            {
                new TemplatePackRoot { Directory = root, Priority = 0, Label = "contained" }
            });

            List<TemplateLoadResult> results = store.LoadAll();

            // `root` has no files of its own, and the outside pack is never reachable from it.
            Assert.DoesNotContain(results, r => r.Loaded && r.Template.MatchLeafTag == "ESCP");
            Assert.Empty(store.LoadEffective());
        }

        // ── Template&PackStore&Order (load-order shadowing) ─────────────────────────

        [Fact]
        [Trait("Category", "TemplatePackStore")]
        public void Template_PackStore_Order_HigherPriorityPackShadowsSameKey()
        {
            string shipped = MakeRoot("shipped");
            string user = MakeRoot("user");

            // Same match key in BOTH packs; the user (higher-priority) layout is the wider one.
            WritePack(shipped, "cpap.json", Template("FORM:TPLX/0/FORM:0001", "CPAP", 1));
            WritePack(user, "cpap.json", Template("FORM:TPLX/0/FORM:0001", "CPAP", 5));

            var store = new TemplatePackStore(new[]
            {
                new TemplatePackRoot { Directory = shipped, Priority = 0, Label = "shipped" },
                new TemplatePackRoot { Directory = user, Priority = 10, Label = "user" }
            });

            List<TemplateModel> effective = store.LoadEffective();

            Assert.Single(effective); // the two same-key templates collapse to one
            // The higher-priority (user) pack's wider layout (5 floats + name field = 6) shadows the shipped 1-float.
            Assert.Equal(6, effective[0].Fields.Count);
        }

        [Fact]
        [Trait("Category", "TemplatePackStore")]
        public void Template_PackStore_DefaultRoots_AreOrderedShippedAppDataProjectLocal()
        {
            string projectLocal = MakeRoot("proj");
            TemplatePackStore store = TemplatePackStore.DefaultRoots(projectLocal);

            Assert.True(store.Roots.Count >= 2, "shipped + app-data at minimum");
            // shipped is lowest priority, project-local (when supplied) is highest.
            Assert.Equal("shipped", store.Roots[0].Label);
            Assert.Equal("project-local", store.Roots[store.Roots.Count - 1].Label);
        }
    }
}
