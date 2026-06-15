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
using System.Linq;
using Xunit;

namespace UtinniCoreDotNet.Tests
{
    // Phase 17 CPPS-04a — the ABI gate's first layer of defense-in-depth.
    //
    // The blessed baseline (Fixtures/abi-baseline-blockhashes.txt, content-copied to the
    // test output dir) is the committed ABI contract snapshot. The primary [Fact] asserts
    // the freshly-regenerated Generated/UtinniCore.cs surface == the blessed baseline,
    // IGNORING CppSharp's reorder churn (a SET comparison). The negative / synthetic
    // [Fact]s prove the diff actually trips on a real surface change (not just passes), and
    // that reorder churn is invisible while a changed DllImport EntryPoint is caught.
    public class AbiSurfaceTests
    {
        // Tests run from <repo>/UtinniCoreDotNet.Tests/bin/Release/net472/
        // (AppendPlatformToOutputPath=false keeps net472 un-suffixed). Walk up 4 levels to
        // the repo root, then into UtinniCoreDotNet/Generated/UtinniCore.cs.
        private static string FindGeneratedFile()
        {
            var baseDir = AppContext.BaseDirectory;
            var candidate = Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "..", "UtinniCoreDotNet", "Generated", "UtinniCore.cs"));
            if (File.Exists(candidate)) return candidate;
            // +1-level fallback for x86-rerouted output paths (mirrors HeaderDiscoveryTests).
            candidate = Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "..", "..", "UtinniCoreDotNet", "Generated", "UtinniCore.cs"));
            if (File.Exists(candidate)) return candidate;
            throw new FileNotFoundException("Generated/UtinniCore.cs not found from " + baseDir);
        }

        // The committed baseline is content-copied beside the test assembly under Fixtures/.
        private static string FindBaseline()
        {
            var baseDir = AppContext.BaseDirectory;
            var candidate = Path.Combine(baseDir, "Fixtures", "abi-baseline-blockhashes.txt");
            if (File.Exists(candidate)) return candidate;
            // Fallback: the committed source location (4-level walk-up to the repo).
            candidate = Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "..",
                "UtinniCoreDotNet.Tests", "Fixtures", "abi-baseline-blockhashes.txt"));
            if (File.Exists(candidate)) return candidate;
            throw new FileNotFoundException("abi-baseline-blockhashes.txt not found from " + baseDir);
        }

        [Fact]
        public void GeneratedSurface_MatchesBlessedBaseline_IgnoringReorderChurn()
        {
            var current = AbiBlockHash.Extract(FindGeneratedFile());
            var baseline = AbiBlockHash.LoadBaseline(FindBaseline());

            var added = current.Except(baseline).ToList();
            var removed = baseline.Except(current).ToList();

            Assert.True(
                added.Count == 0 && removed.Count == 0,
                "ABI surface drifted from the blessed baseline (re-bless with AbiBlockHash.Rebless "
                + "if intentional — rebuild TJT, re-freeze the fixture, commit together):\n"
                + "ADDED (" + added.Count + "):\n  " + string.Join("\n  ", added.Take(40)) + "\n"
                + "REMOVED (" + removed.Count + "):\n  " + string.Join("\n  ", removed.Take(40)));
        }

        [Fact]
        public void BaselineHasSubstantiveSurface_AtLeastFiftyBlocks()
        {
            var baseline = AbiBlockHash.LoadBaseline(FindBaseline());
            Assert.True(baseline.Count >= 50,
                "The blessed baseline should carry at least 50 public-surface block hashes; got "
                + baseline.Count);
        }

        // --- Negative / synthetic proofs that the diff TRIPS on a real change ---

        [Fact]
        public void ReorderedButIdenticalSurface_YieldsEqualSet()
        {
            // Two snippets with the SAME public surface in DIFFERENT order. The SET must match.
            const string a =
                "namespace N {\n" +
                "  public partial struct __Internal {\n" +
                "    [DllImport(\"UtinniCore\", EntryPoint=\"?alpha@@YAXXZ\")]\n" +
                "    internal static extern void Alpha();\n" +
                "    [DllImport(\"UtinniCore\", EntryPoint=\"?beta@@YAXXZ\")]\n" +
                "    internal static extern void Beta();\n" +
                "  }\n" +
                "}\n";
            const string b =
                "namespace N {\n" +
                "  public partial struct __Internal {\n" +
                "    [DllImport(\"UtinniCore\", EntryPoint=\"?beta@@YAXXZ\")]\n" +
                "    internal static extern void Beta();\n" +
                "    [DllImport(\"UtinniCore\", EntryPoint=\"?alpha@@YAXXZ\")]\n" +
                "    internal static extern void Alpha();\n" +
                "  }\n" +
                "}\n";

            var setA = AbiBlockHash.ExtractFromText(a);
            var setB = AbiBlockHash.ExtractFromText(b);

            Assert.True(setA.SetEquals(setB),
                "Reordered-but-identical surface must produce an equal hash set (reorder churn invisible).");
        }

        [Fact]
        public void AddedMember_IsReportedAsAdded()
        {
            const string baseline =
                "namespace N {\n" +
                "  public partial struct __Internal {\n" +
                "    [DllImport(\"UtinniCore\", EntryPoint=\"?alpha@@YAXXZ\")]\n" +
                "    internal static extern void Alpha();\n" +
                "  }\n" +
                "}\n";
            const string changed =
                "namespace N {\n" +
                "  public partial struct __Internal {\n" +
                "    [DllImport(\"UtinniCore\", EntryPoint=\"?alpha@@YAXXZ\")]\n" +
                "    internal static extern void Alpha();\n" +
                "    [DllImport(\"UtinniCore\", EntryPoint=\"?gamma@@YAXXZ\")]\n" +
                "    internal static extern void Gamma();\n" +
                "  }\n" +
                "}\n";

            var baseSet = AbiBlockHash.ExtractFromText(baseline);
            var changedSet = AbiBlockHash.ExtractFromText(changed);

            var added = changedSet.Except(baseSet).ToList();
            Assert.True(added.Count == 1,
                "Adding one EntryPoint must surface exactly one ADDED block; got " + added.Count);
        }

        [Fact]
        public void RemovedMember_IsReportedAsRemoved()
        {
            const string baseline =
                "namespace N {\n" +
                "  public partial struct __Internal {\n" +
                "    [DllImport(\"UtinniCore\", EntryPoint=\"?alpha@@YAXXZ\")]\n" +
                "    internal static extern void Alpha();\n" +
                "    [DllImport(\"UtinniCore\", EntryPoint=\"?beta@@YAXXZ\")]\n" +
                "    internal static extern void Beta();\n" +
                "  }\n" +
                "}\n";
            const string changed =
                "namespace N {\n" +
                "  public partial struct __Internal {\n" +
                "    [DllImport(\"UtinniCore\", EntryPoint=\"?alpha@@YAXXZ\")]\n" +
                "    internal static extern void Alpha();\n" +
                "  }\n" +
                "}\n";

            var baseSet = AbiBlockHash.ExtractFromText(baseline);
            var changedSet = AbiBlockHash.ExtractFromText(changed);

            var removed = baseSet.Except(changedSet).ToList();
            Assert.True(removed.Count == 1,
                "Removing one EntryPoint must surface exactly one REMOVED block; got " + removed.Count);
        }

        [Fact]
        public void ChangedDllImportEntryPointMangledString_ProducesADifferentHash()
        {
            // The native-ABI anchor: a changed mangled EntryPoint IS an ABI change and MUST
            // shift the hash set even though the managed signature is identical.
            const string original =
                "[DllImport(\"UtinniCore\", EntryPoint=\"??0UtINI@utinni@@QAE@XZ\")]\n";
            const string mangledChanged =
                "[DllImport(\"UtinniCore\", EntryPoint=\"??0UtINI@utinni@@QAE@ABV01@@Z\")]\n";

            var setOriginal = AbiBlockHash.ExtractFromText(original);
            var setChanged = AbiBlockHash.ExtractFromText(mangledChanged);

            Assert.False(setOriginal.SetEquals(setChanged),
                "A changed DllImport EntryPoint mangled string must change the block hash set "
                + "(the native-ABI anchor is in the keyed surface).");
        }
    }
}
