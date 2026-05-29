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
using UtinniCoreDotNet.Saving;
using Xunit;

namespace UtinniCoreDotNet.Tests.SavingTests
{
    /// <summary>
    /// Path-traversal regression suite for the framework-side root-containment helper
    /// (08-REVIEWS MEDIUM-8 / Phase 8 Plan 05 Task 1). Covers each rejection class as a
    /// separate [Fact] plus a happy-path success.
    ///
    /// <para>Consumes <see cref="LooseOverridePath"/> via the existing ProjectReference to
    /// UtinniCoreDotNet.csproj — NO linked-source <c>&lt;Compile Include&gt;</c> entry (checker
    /// B-1). The SDK-style test csproj's default <c>**/*.cs</c> glob auto-includes this file.</para>
    /// </summary>
    public class LooseOverridePathTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // Null / empty arguments
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Resolve_NullRoot_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                LooseOverridePath.Resolve(null, "foo.iff"));
        }

        [Fact]
        public void Resolve_EmptyRoot_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                LooseOverridePath.Resolve("", "foo.iff"));
        }

        [Fact]
        public void Resolve_NullRelPath_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                LooseOverridePath.Resolve(@"C:\swg-client", null));
        }

        [Fact]
        public void Resolve_EmptyRelPath_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                LooseOverridePath.Resolve(@"C:\swg-client", ""));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Rooted relAssetPath rejection
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Resolve_DriveRootedRelPath_Throws()
        {
            // C:\evil.iff escapes via Path.Combine (would silently replace root) — must be
            // rejected before the combine.
            Assert.Throws<ArgumentException>(() =>
                LooseOverridePath.Resolve(@"C:\swg-client", @"C:\evil.iff"));
        }

        [Fact]
        public void Resolve_DriveRelativeRelPath_Throws()
        {
            // "D:foo" is rooted-but-drive-relative — Path.IsPathRooted returns true.
            Assert.Throws<ArgumentException>(() =>
                LooseOverridePath.Resolve(@"C:\swg-client", "D:foo"));
        }

        [Fact]
        public void Resolve_LeadingSeparatorRelPath_Throws()
        {
            // "\unc" is rooted — must be rejected.
            Assert.Throws<ArgumentException>(() =>
                LooseOverridePath.Resolve(@"C:\swg-client", @"\unc"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // '..' traversal rejection (explicit segment-level check)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Resolve_DotDotSegment_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                LooseOverridePath.Resolve(@"C:\swg-client", @"..\evil.iff"));
        }

        [Fact]
        public void Resolve_DotDotSegmentNested_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                LooseOverridePath.Resolve(@"C:\swg-client", @"loot\..\..\evil.iff"));
        }

        [Fact]
        public void Resolve_AltSeparatorDotDot_Throws()
        {
            // Alt-separator inputs ('/' on Windows) must still trip the '..' segment check —
            // segment split is on BOTH '/' and '\\'.
            Assert.Throws<ArgumentException>(() =>
                LooseOverridePath.Resolve(@"C:\swg-client", "../../evil"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Prefix-match attack rejection (sibling directory with shared prefix)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Resolve_PrefixMatchSiblingRoot_DoesNotEscape()
        {
            // If the caller supplies root 'C:\swg-client' and a relAssetPath that combines
            // legally under it, the StartsWith gate must use 'C:\swg-client\' (with trailing
            // separator) so that 'C:\swg-clientx\...' cannot pass even if it shared the prefix
            // of the root string. This test exercises the happy path with an unrelated valid
            // input to make sure the gate is wired; the negative is exercised indirectly by the
            // '..' / drive-rooted rejections above (no input can construct a sibling root
            // through the helper because Path.Combine + non-rooted rel cannot land outside the
            // intended root).
            string root = @"C:\swg-client";
            string full = LooseOverridePath.Resolve(root, @"loot\good.iff");
            // Result MUST be under 'C:\swg-client\' with the trailing separator — not under
            // 'C:\swg-clientx\...'.
            Assert.StartsWith(@"C:\swg-client\", full, StringComparison.OrdinalIgnoreCase);
            Assert.False(full.StartsWith(@"C:\swg-clientx", StringComparison.OrdinalIgnoreCase),
                "Resolved path must not start with a sibling root sharing a string prefix.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Happy path
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Resolve_NormalRelPath_ReturnsFullPathUnderRoot()
        {
            string root = @"C:\swg-client";
            string full = LooseOverridePath.Resolve(root, @"datatables\foo.iff");
            // GetFullPath normalizes to OS separators on Windows; the result must START with the
            // root + trailing separator (OrdinalIgnoreCase).
            Assert.StartsWith(@"C:\swg-client\", full, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(@"datatables\foo.iff", full, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Resolve_AltSeparatorRelPath_NormalizesToOsForm()
        {
            string root = @"C:\swg-client";
            string full = LooseOverridePath.Resolve(root, "datatables/foo.iff");
            Assert.StartsWith(@"C:\swg-client\", full, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(@"datatables\foo.iff", full, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Resolve_RootWithTrailingSeparator_AcceptedIdempotently()
        {
            // The caller may supply the root with or without a trailing separator — the helper
            // must handle both identically.
            string fullA = LooseOverridePath.Resolve(@"C:\swg-client", @"foo.iff");
            string fullB = LooseOverridePath.Resolve(@"C:\swg-client\", @"foo.iff");
            Assert.Equal(fullA, fullB, ignoreCase: true);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 08-REVIEW WR-07: Resolve re-canonicalizes the resolvedRoot defensively
        //
        // The prior version trusted the docstring's "caller is responsible" claim, but every
        // observed caller (FormIffEditor.ResolveClientRoot) fed paths from
        // Process.MainModule.FileName / GetWorkingDirectory / Utinni.ini — none guaranteed
        // canonical. A non-canonical root caused the StartsWith gate to falsely reject
        // legitimate paths because the candidate side was Path.GetFullPath'd but the root was
        // not. These tests pin the fix in place.
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Resolve_RootWithDotDotSegment_NormalizesAndAccepts()
        {
            // Non-canonical root containing '..' that collapses to a real directory must NOT
            // falsely reject a legitimate child path. Pre-fix: StartsWith compared
            //   "C:\swg-client\foo.iff" against "C:\swg-client\..\swg-client\"
            // and returned false. Post-fix: root canonicalized to "C:\swg-client" before the
            // trailing-separator + StartsWith logic.
            string root = @"C:\swg-client\..\swg-client";
            string full = LooseOverridePath.Resolve(root, @"datatables\foo.iff");
            Assert.StartsWith(@"C:\swg-client\", full, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(@"datatables\foo.iff", full, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Resolve_RootWithSingleDotSegment_NormalizesAndAccepts()
        {
            // './' segment in the root — Path.GetFullPath collapses it; pre-fix the StartsWith
            // gate would have compared the un-canonicalized form.
            string root = @"C:\swg-client\.\subdir\..\.";
            string full = LooseOverridePath.Resolve(root, @"foo.iff");
            // Resolves to C:\swg-client\foo.iff after canonicalization.
            Assert.StartsWith(@"C:\swg-client\", full, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(@"foo.iff", full, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Resolve_RootWithMixedSeparators_NormalizesAndAccepts()
        {
            // Mixed-separator root — Path.GetFullPath normalizes to OS-native form before
            // the StartsWith gate runs.
            string root = @"C:/swg-client/datatables/..";
            string full = LooseOverridePath.Resolve(root, @"foo.iff");
            Assert.StartsWith(@"C:\swg-client\", full, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(@"foo.iff", full, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Resolve_RootWithBothCanonicalAndNonCanonicalForms_ResultIsIdempotent()
        {
            // Two roots that canonicalize to the same path must produce the same resolved value
            // (post-canonicalization the trailing-separator logic + StartsWith are stable).
            string fullA = LooseOverridePath.Resolve(@"C:\swg-client", @"datatables\foo.iff");
            string fullB = LooseOverridePath.Resolve(@"C:\swg-client\..\swg-client", @"datatables\foo.iff");
            string fullC = LooseOverridePath.Resolve(@"C:/swg-client", @"datatables/foo.iff");
            Assert.Equal(fullA, fullB, ignoreCase: true);
            Assert.Equal(fullA, fullC, ignoreCase: true);
        }
    }
}
