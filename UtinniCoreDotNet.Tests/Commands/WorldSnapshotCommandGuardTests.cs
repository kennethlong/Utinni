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

using UtinniCoreDotNet.Commands;
using Xunit;

namespace UtinniCoreDotNet.Tests.Commands
{
    /// <summary>
    /// Framework-leg tests for the Plan 15-09 <see cref="WorldSnapshotCommandGuard"/> bail-on-null
    /// decision (PROD-W2-WS / GAP 1.1 / checker B-1). The helper is pure/BCL-only; it carries the
    /// skip-vs-apply decision the four WS IUndoCommand bodies consult BEFORE dereferencing a
    /// possibly-null <c>Network.GetObjectById</c> / <c>GetNodeById</c> lookup (the 15-SMOKE A9 crash).
    /// Class name contains "WorldSnapshotCommandGuard" so the plan's
    /// <c>--filter WorldSnapshotCommandGuard</c> selects exactly these facts. The native bridge that
    /// returns the real nullable lookups is static CppSharp and not loadable in the xUnit host, so the
    /// DECISION is what we cover here — plain <c>new object()</c> / <c>null</c> stand in for the lookups.
    /// </summary>
    public class WorldSnapshotCommandGuardTests
    {
        // Test 1: a null single lookup must NOT be dereferenced -> ShouldApply returns false.
        [Fact]
        public void WorldSnapshotCommandGuard_ShouldApply_NullSingleLookup_ReturnsFalse()
        {
            Assert.False(WorldSnapshotCommandGuard.ShouldApply(null));
        }

        // Test 2: a non-null single lookup is safe to dereference -> ShouldApply returns true.
        [Fact]
        public void WorldSnapshotCommandGuard_ShouldApply_NonNullSingleLookup_ReturnsTrue()
        {
            Assert.True(WorldSnapshotCommandGuard.ShouldApply(new object()));
        }

        // Test 3: any combination involving at least one null -> two-arg ShouldApply returns false.
        [Theory]
        [InlineData(false, false)] // obj null,     node null
        [InlineData(false, true)]  // obj null,     node non-null
        [InlineData(true, false)]  // obj non-null, node null
        public void WorldSnapshotCommandGuard_ShouldApply_AnyNullPair_ReturnsFalse(bool objPresent, bool nodePresent)
        {
            object objLookup = objPresent ? new object() : null;
            object nodeLookup = nodePresent ? new object() : null;

            Assert.False(WorldSnapshotCommandGuard.ShouldApply(objLookup, nodeLookup));
        }

        // Test 4: the two-arg helper returns true ONLY when BOTH lookups are non-null; no throw on
        // any null combination (exercised by Test 3 + this happy-path fact).
        [Fact]
        public void WorldSnapshotCommandGuard_ShouldApply_BothNonNull_ReturnsTrue()
        {
            Assert.True(WorldSnapshotCommandGuard.ShouldApply(new object(), new object()));
        }
    }
}
