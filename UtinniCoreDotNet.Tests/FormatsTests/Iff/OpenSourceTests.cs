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
using Xunit;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Tests.FormatsTests.Iff
{
    /// <summary>
    /// Unit coverage for the document-provenance discriminated union (Task 3 of 08-01).
    /// Validates the FOUR sealed cases (LooseFile, TreArchive, ClientMemory, Unknown), the four
    /// IsX convenience predicates, pattern-matching against each concrete type, value equality
    /// on the three populated cases, the singleton invariant on Unknown, and the W-3
    /// exhaustiveness contract that Unknown.Instance does NOT match LooseFile / TreArchive /
    /// ClientMemory pattern-match gates (so downstream Save-mode gates stay disabled on the
    /// degraded fallback).
    /// </summary>
    public class OpenSourceTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // Construction stores the correct fields per populated case
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void LooseFile_Ctor_StoresPath()
        {
            var src = new OpenSource.LooseFile(@"D:\mods\foo.iff");
            Assert.Equal(@"D:\mods\foo.iff", src.Path);
        }

        [Fact]
        public void LooseFile_NullPath_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new OpenSource.LooseFile(null));
        }

        [Fact]
        public void TreArchive_Ctor_StoresAllThreeFields()
        {
            var src = new OpenSource.TreArchive(@"D:\client\packed.tre", 42, "datatables/foo.iff");
            Assert.Equal(@"D:\client\packed.tre", src.TrePath);
            Assert.Equal(42, src.RecordIndex);
            Assert.Equal("datatables/foo.iff", src.LogicalPath);
        }

        [Fact]
        public void TreArchive_NegativeRecordIndex_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new OpenSource.TreArchive("foo.tre", -1, "datatables/foo.iff"));
        }

        [Fact]
        public void TreArchive_NullTrePath_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new OpenSource.TreArchive(null, 0, "datatables/foo.iff"));
        }

        [Fact]
        public void TreArchive_NullLogicalPath_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new OpenSource.TreArchive("foo.tre", 0, null));
        }

        [Fact]
        public void ClientMemory_Ctor_StoresAllThreeFields()
        {
            var addr = new IntPtr(0x12345678);
            var src = new OpenSource.ClientMemory(addr, 1024, "datatables/foo.iff");
            Assert.Equal(addr, src.TargetAddr);
            Assert.Equal(1024, src.OriginalMappedLength);
            Assert.Equal("datatables/foo.iff", src.LogicalPath);
        }

        [Fact]
        public void ClientMemory_NegativeLength_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new OpenSource.ClientMemory(IntPtr.Zero, -1, "x"));
        }

        [Fact]
        public void ClientMemory_NullLogicalPath_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new OpenSource.ClientMemory(IntPtr.Zero, 0, null));
        }

        // ─────────────────────────────────────────────────────────────────────
        // IsX convenience predicates flip correctly across all four cases
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void IsX_LooseFile_OnlyIsLooseFileTrue()
        {
            OpenSource src = new OpenSource.LooseFile("foo.iff");
            Assert.True(src.IsLooseFile);
            Assert.False(src.IsTreArchive);
            Assert.False(src.IsClientMemory);
            Assert.False(src.IsUnknown);
        }

        [Fact]
        public void IsX_TreArchive_OnlyIsTreArchiveTrue()
        {
            OpenSource src = new OpenSource.TreArchive("a.tre", 0, "b");
            Assert.False(src.IsLooseFile);
            Assert.True(src.IsTreArchive);
            Assert.False(src.IsClientMemory);
            Assert.False(src.IsUnknown);
        }

        [Fact]
        public void IsX_ClientMemory_OnlyIsClientMemoryTrue()
        {
            OpenSource src = new OpenSource.ClientMemory(IntPtr.Zero, 0, "x");
            Assert.False(src.IsLooseFile);
            Assert.False(src.IsTreArchive);
            Assert.True(src.IsClientMemory);
            Assert.False(src.IsUnknown);
        }

        [Fact]
        public void IsX_Unknown_OnlyIsUnknownTrue()
        {
            OpenSource src = OpenSource.Unknown.Instance;
            Assert.False(src.IsLooseFile);
            Assert.False(src.IsTreArchive);
            Assert.False(src.IsClientMemory);
            Assert.True(src.IsUnknown);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Pattern-match against each concrete type works
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void PatternMatch_LooseFile_BindsPath()
        {
            OpenSource src = new OpenSource.LooseFile("foo.iff");
            if (src is OpenSource.LooseFile lf)
            {
                Assert.Equal("foo.iff", lf.Path);
            }
            else
            {
                Assert.True(false, "Expected OpenSource.LooseFile pattern match to bind.");
            }
        }

        [Fact]
        public void PatternMatch_TreArchive_BindsAllThreeFields()
        {
            OpenSource src = new OpenSource.TreArchive("a.tre", 3, "b");
            if (src is OpenSource.TreArchive tre)
            {
                Assert.Equal("a.tre", tre.TrePath);
                Assert.Equal(3, tre.RecordIndex);
                Assert.Equal("b", tre.LogicalPath);
            }
            else
            {
                Assert.True(false, "Expected OpenSource.TreArchive pattern match to bind.");
            }
        }

        [Fact]
        public void PatternMatch_ClientMemory_BindsAllThreeFields()
        {
            var addr = new IntPtr(unchecked((int)0xDEADBEEF));
            OpenSource src = new OpenSource.ClientMemory(addr, 256, "logical");
            if (src is OpenSource.ClientMemory cm)
            {
                Assert.Equal(addr, cm.TargetAddr);
                Assert.Equal(256, cm.OriginalMappedLength);
                Assert.Equal("logical", cm.LogicalPath);
            }
            else
            {
                Assert.True(false, "Expected OpenSource.ClientMemory pattern match to bind.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Value equality on the three populated cases
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Equals_LooseFile_SamePath_AreEqual()
        {
            var a = new OpenSource.LooseFile("foo.iff");
            var b = new OpenSource.LooseFile("foo.iff");
            Assert.Equal(a, b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Equals_LooseFile_DifferentPath_AreNotEqual()
        {
            var a = new OpenSource.LooseFile("foo.iff");
            var b = new OpenSource.LooseFile("bar.iff");
            Assert.NotEqual(a, b);
        }

        [Fact]
        public void Equals_TreArchive_AllFieldsMatch_AreEqual()
        {
            var a = new OpenSource.TreArchive("a.tre", 7, "x");
            var b = new OpenSource.TreArchive("a.tre", 7, "x");
            Assert.Equal(a, b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Equals_TreArchive_DifferentRecordIndex_AreNotEqual()
        {
            var a = new OpenSource.TreArchive("a.tre", 7, "x");
            var b = new OpenSource.TreArchive("a.tre", 8, "x");
            Assert.NotEqual(a, b);
        }

        [Fact]
        public void Equals_ClientMemory_AllFieldsMatch_AreEqual()
        {
            var addr = new IntPtr(0x1000);
            var a = new OpenSource.ClientMemory(addr, 64, "x");
            var b = new OpenSource.ClientMemory(addr, 64, "x");
            Assert.Equal(a, b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Equals_ClientMemory_DifferentAddr_AreNotEqual()
        {
            var a = new OpenSource.ClientMemory(new IntPtr(0x1000), 64, "x");
            var b = new OpenSource.ClientMemory(new IntPtr(0x2000), 64, "x");
            Assert.NotEqual(a, b);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 08-REVIEW WR-04: case-insensitive Equals + GetHashCode on Windows paths.
        // Equality contract: if Equals returns true, GetHashCode MUST match. The Equals/GetHashCode
        // halves must use the same comparer.
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void LooseFile_GetHashCode_PathCaseInsensitive_AgreesWithEquals()
        {
            var lower = new OpenSource.LooseFile(@"C:\swg-client\datatables\foo.iff");
            var upper = new OpenSource.LooseFile(@"C:\SWG-CLIENT\DATATABLES\FOO.IFF");
            var mixed = new OpenSource.LooseFile(@"c:\swg-Client\DataTables\Foo.IFF");

            Assert.Equal(lower, upper);
            Assert.Equal(lower, mixed);
            Assert.Equal(lower.GetHashCode(), upper.GetHashCode());
            Assert.Equal(lower.GetHashCode(), mixed.GetHashCode());
        }

        [Fact]
        public void LooseFile_DifferentPaths_AreNotEqual_AcrossCasing()
        {
            var a = new OpenSource.LooseFile(@"C:\swg-client\foo.iff");
            var b = new OpenSource.LooseFile(@"C:\swg-client\bar.iff");
            Assert.NotEqual(a, b);
        }

        [Fact]
        public void TreArchive_GetHashCode_PathsCaseInsensitive_AgreesWithEquals()
        {
            var lower = new OpenSource.TreArchive(
                @"C:\swg-client\packed.tre", 42, "datatables/foo.iff");
            var upper = new OpenSource.TreArchive(
                @"C:\SWG-CLIENT\PACKED.TRE", 42, "DATATABLES/FOO.IFF");

            Assert.Equal(lower, upper);
            Assert.Equal(lower.GetHashCode(), upper.GetHashCode());
        }

        [Fact]
        public void TreArchive_DifferentRecordIndex_StillNotEqual_AcrossCasing()
        {
            // Sanity: case-insensitive paths do NOT collapse different RecordIndex values.
            var a = new OpenSource.TreArchive(@"C:\packed.tre", 42, "x/y.iff");
            var b = new OpenSource.TreArchive(@"c:\PACKED.tre", 43, "X/Y.iff");
            Assert.NotEqual(a, b);
        }

        [Fact]
        public void ClientMemory_GetHashCode_LogicalPathCaseInsensitive_AgreesWithEquals()
        {
            var addr = new IntPtr(0x12345678);
            var lower = new OpenSource.ClientMemory(addr, 1024, "datatables/foo.iff");
            var upper = new OpenSource.ClientMemory(addr, 1024, "DATATABLES/FOO.IFF");

            Assert.Equal(lower, upper);
            Assert.Equal(lower.GetHashCode(), upper.GetHashCode());
        }

        // ─────────────────────────────────────────────────────────────────────
        // Unknown singleton invariant
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Unknown_Instance_AccessedTwice_IsReferenceEqual()
        {
            var a = OpenSource.Unknown.Instance;
            var b = OpenSource.Unknown.Instance;
            Assert.Same(a, b);
        }

        [Fact]
        public void Unknown_Instance_IsValueEqualToItself()
        {
            var a = OpenSource.Unknown.Instance;
            Assert.Equal(a, OpenSource.Unknown.Instance);
        }

        [Fact]
        public void Unknown_HasNoPublicConstructor()
        {
            // Reflection check: the only way to obtain an Unknown is OpenSource.Unknown.Instance.
            var ctors = typeof(OpenSource.Unknown).GetConstructors(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.Empty(ctors);
        }

        // ─────────────────────────────────────────────────────────────────────
        // W-3 exhaustiveness contract — Unknown matches NONE of the three populated cases
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Unknown_PatternMatchLooseFile_IsFalse()
        {
            OpenSource src = OpenSource.Unknown.Instance;
            Assert.False(src is OpenSource.LooseFile);
        }

        [Fact]
        public void Unknown_PatternMatchTreArchive_IsFalse()
        {
            OpenSource src = OpenSource.Unknown.Instance;
            Assert.False(src is OpenSource.TreArchive);
        }

        [Fact]
        public void Unknown_PatternMatchClientMemory_IsFalse()
        {
            OpenSource src = OpenSource.Unknown.Instance;
            Assert.False(src is OpenSource.ClientMemory);
        }
    }
}
