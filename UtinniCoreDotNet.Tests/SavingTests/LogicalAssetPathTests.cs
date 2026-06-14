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

using UtinniCoreDotNet.Saving;
using Xunit;

namespace UtinniCoreDotNet.Tests.SavingTests
{
    /// <summary>
    /// Coverage for the open-side logical-path derivation (15-20, 15-SMOKE Checklist D-ii). A raw
    /// <c>Open…</c> dialog yields only an absolute filesystem path; the loose-override save needs the
    /// asset's client-root-relative logical subpath so the override lands where the SWG client resolves
    /// it (<c>loose/string/en/ui_auc.stf</c>), not flat (<c>loose/ui_auc.stf</c>).
    ///
    /// <para>Consumes <see cref="LogicalAssetPath"/> via the existing ProjectReference to
    /// UtinniCoreDotNet.csproj (which type-forwards to the netstandard2.0 PathContainment lib). The
    /// SDK-style test csproj's default <c>**/*.cs</c> glob auto-includes this file — no linked-source
    /// entry. All facts pass absolute strings + root strings directly (no real disk dependency).</para>
    /// </summary>
    public class LogicalAssetPathTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // Happy path: subpath preserved
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void TryFromAbsolute_FileUnderRoot_PreservesSubpath()
        {
            // The D-ii defect case: a .stf opened from <root>\string\en\ui_auc.stf must derive the
            // FULL logical subpath, not just the filename.
            bool ok = LogicalAssetPath.TryFromAbsolute(
                @"D:\SWGEmu-Client\SWGEmu\string\en\ui_auc.stf",
                @"D:\SWGEmu-Client\SWGEmu\",
                out string logical);
            Assert.True(ok);
            Assert.Equal("string/en/ui_auc.stf", logical);
        }

        [Fact]
        public void TryFromAbsolute_LoosePrefixedFile_StripsLeadingLooseSegment()
        {
            // Loose round-trip parity (matches the shipped .prt behavior): a file opened from inside
            // the loose tree must re-save to the SAME loose location, so the leading 'loose/' segment
            // is stripped from the derived logical path.
            bool ok = LogicalAssetPath.TryFromAbsolute(
                @"D:\SWGEmu-Client\SWGEmu\loose\appearance\pt_airport_race_light.prt",
                @"D:\SWGEmu-Client\SWGEmu\",
                out string logical);
            Assert.True(ok);
            Assert.Equal("appearance/pt_airport_race_light.prt", logical);
        }

        [Fact]
        public void TryFromAbsolute_FileDirectlyUnderRoot_DerivesFilenameOnly()
        {
            bool ok = LogicalAssetPath.TryFromAbsolute(
                @"D:\SWGEmu-Client\SWGEmu\foo.stf",
                @"D:\SWGEmu-Client\SWGEmu\",
                out string logical);
            Assert.True(ok);
            Assert.Equal("foo.stf", logical);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Outside-root: false, NOT an exception (caller keeps filename fallback)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void TryFromAbsolute_FileOutsideRoot_ReturnsFalse()
        {
            bool ok = LogicalAssetPath.TryFromAbsolute(
                @"C:\elsewhere\x.stf",
                @"D:\SWGEmu-Client\SWGEmu\",
                out string logical);
            Assert.False(ok);
            Assert.Null(logical);
        }

        [Fact]
        public void TryFromAbsolute_PrefixMatchSiblingRoot_ReturnsFalse()
        {
            // 'D:\SWGEmu-Client\SWGEmu-X\...' must NOT match root 'D:\SWGEmu-Client\SWGEmu' — the
            // trailing-separator gate prevents a sibling sharing the prefix from passing.
            bool ok = LogicalAssetPath.TryFromAbsolute(
                @"D:\SWGEmu-Client\SWGEmu-X\foo.stf",
                @"D:\SWGEmu-Client\SWGEmu",
                out string logical);
            Assert.False(ok);
            Assert.Null(logical);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Root trailing-separator tolerance + idempotence
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void TryFromAbsolute_RootWithAndWithoutTrailingSeparator_AreEquivalent()
        {
            bool okA = LogicalAssetPath.TryFromAbsolute(
                @"D:\SWGEmu-Client\SWGEmu\string\en\ui_auc.stf",
                @"D:\SWGEmu-Client\SWGEmu",
                out string a);
            bool okB = LogicalAssetPath.TryFromAbsolute(
                @"D:\SWGEmu-Client\SWGEmu\string\en\ui_auc.stf",
                @"D:\SWGEmu-Client\SWGEmu\",
                out string b);
            Assert.True(okA);
            Assert.True(okB);
            Assert.Equal(a, b);
            Assert.Equal("string/en/ui_auc.stf", a);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Case-insensitive root match (Windows FS)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void TryFromAbsolute_CaseInsensitiveRootMatch_Derives()
        {
            // Opened path with a different-case root prefix must still match (NTFS default).
            bool ok = LogicalAssetPath.TryFromAbsolute(
                @"d:\swgemu-client\swgemu\string\en\ui_auc.stf",
                @"D:\SWGEmu-Client\SWGEmu\",
                out string logical);
            Assert.True(ok);
            Assert.Equal("string/en/ui_auc.stf", logical);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Separator normalization to forward slash
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void TryFromAbsolute_ResultUsesForwardSlashes()
        {
            bool ok = LogicalAssetPath.TryFromAbsolute(
                @"D:\SWGEmu-Client\SWGEmu\string\en\ui_auc.stf",
                @"D:\SWGEmu-Client\SWGEmu\",
                out string logical);
            Assert.True(ok);
            Assert.DoesNotContain('\\', logical);
            Assert.False(logical.StartsWith("/"), "logical must not have a leading separator.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // No '..' escape + defensive null/empty handling (never throws)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void TryFromAbsolute_DerivedPathNeverContainsDotDot()
        {
            // A non-canonical opened path that canonicalizes back under the root must derive a clean
            // logical path containing no '..' segment.
            bool ok = LogicalAssetPath.TryFromAbsolute(
                @"D:\SWGEmu-Client\SWGEmu\string\en\..\en\ui_auc.stf",
                @"D:\SWGEmu-Client\SWGEmu\",
                out string logical);
            Assert.True(ok);
            Assert.DoesNotContain("..", logical);
            Assert.Equal("string/en/ui_auc.stf", logical);
        }

        [Fact]
        public void TryFromAbsolute_NullOrEmptyInputs_ReturnFalseNotThrow()
        {
            Assert.False(LogicalAssetPath.TryFromAbsolute(null, @"D:\SWGEmu-Client\SWGEmu", out string a));
            Assert.Null(a);
            Assert.False(LogicalAssetPath.TryFromAbsolute(@"D:\SWGEmu-Client\SWGEmu\foo.stf", null, out string b));
            Assert.Null(b);
            Assert.False(LogicalAssetPath.TryFromAbsolute("", @"D:\SWGEmu-Client\SWGEmu", out string c));
            Assert.Null(c);
            Assert.False(LogicalAssetPath.TryFromAbsolute(@"D:\SWGEmu-Client\SWGEmu\foo.stf", "", out string d));
            Assert.Null(d);
        }

        [Fact]
        public void TryFromAbsolute_RootItselfWithNoRemainder_ReturnsFalse()
        {
            // The opened path IS the root directory (no asset remainder) — nothing to derive.
            bool ok = LogicalAssetPath.TryFromAbsolute(
                @"D:\SWGEmu-Client\SWGEmu",
                @"D:\SWGEmu-Client\SWGEmu",
                out string logical);
            Assert.False(ok);
            Assert.Null(logical);
        }

        [Fact]
        public void TryFromAbsolute_LooseRootDirectoryOnly_ReturnsFalse()
        {
            // Path is exactly <root>\loose with no file remainder after stripping 'loose/' — false.
            bool ok = LogicalAssetPath.TryFromAbsolute(
                @"D:\SWGEmu-Client\SWGEmu\loose",
                @"D:\SWGEmu-Client\SWGEmu",
                out string logical);
            Assert.False(ok);
            Assert.Null(logical);
        }
    }
}
