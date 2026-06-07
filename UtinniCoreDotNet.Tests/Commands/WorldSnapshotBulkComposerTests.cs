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

using System.Collections.Generic;
using UtinniCoreDotNet.Commands;
using Xunit;

namespace UtinniCoreDotNet.Tests.Commands
{
    /// <summary>
    /// Framework-leg tests for the Plan 15-01 <see cref="WorldSnapshotBulkComposer"/> bulk-op
    /// command-composition helper (PROD-W2-WS / D-01 / checker B-1). The helper is pure/BCL-only; it
    /// computes the ORDERED plan of which shipped <c>WorldSnapshotCommands</c> the TJT-side
    /// <c>WorldSnapshotImpl</c> composes inside one atomic <c>AddUpdateLoopCall</c>. Class name
    /// contains "SnapshotBulk" so the plan's <c>--filter SnapshotBulk</c> selects it.
    /// </summary>
    public class WorldSnapshotBulkComposerTests
    {
        // Test 1: ComposeMove returns N PositionChanged descriptors in input order, one per id.
        [Fact]
        public void SnapshotBulk_ComposeMove_ReturnsOnePositionChangedPerId_InOrder()
        {
            var ids = new[] { 10, 20, 30 };

            var plan = WorldSnapshotBulkComposer.ComposeMove(ids, 1.5f, -2.0f, 3.25f);

            Assert.Equal(3, plan.Count);
            for (int i = 0; i < ids.Length; i++)
            {
                Assert.Equal(WorldSnapshotBulkOpKind.PositionChanged, plan[i].Kind);
                Assert.Equal(ids[i], plan[i].NodeId);          // input order preserved
                Assert.Equal(1.5f, plan[i].DeltaX);
                Assert.Equal(-2.0f, plan[i].DeltaY);
                Assert.Equal(3.25f, plan[i].DeltaZ);
            }
        }

        // Test 2: ComposeDelete returns N Remove descriptors in input order.
        [Fact]
        public void SnapshotBulk_ComposeDelete_ReturnsOneRemovePerId_InOrder()
        {
            var ids = new[] { 7, 8, 9, 10 };

            var plan = WorldSnapshotBulkComposer.ComposeDelete(ids);

            Assert.Equal(ids.Length, plan.Count);
            for (int i = 0; i < ids.Length; i++)
            {
                Assert.Equal(WorldSnapshotBulkOpKind.Remove, plan[i].Kind);
                Assert.Equal(ids[i], plan[i].NodeId);
            }
        }

        // Test 3: ComposeRetemplate returns the remove+add pairing per node (Remove first, then Add
        // carrying the new template), in input order.
        [Fact]
        public void SnapshotBulk_ComposeRetemplate_ReturnsRemoveThenAddPairPerNode()
        {
            var ids = new[] { 100, 200 };
            const string tpl = "object/tangible/foo/shared_bar.iff";

            var plan = WorldSnapshotBulkComposer.ComposeRetemplate(ids, tpl);

            Assert.Equal(ids.Length * 2, plan.Count);

            // node 100: remove then add
            Assert.Equal(WorldSnapshotBulkOpKind.Remove, plan[0].Kind);
            Assert.Equal(100, plan[0].NodeId);
            Assert.Equal(WorldSnapshotBulkOpKind.Add, plan[1].Kind);
            Assert.Equal(100, plan[1].NodeId);
            Assert.Equal(tpl, plan[1].NewObjectTemplate);

            // node 200: remove then add
            Assert.Equal(WorldSnapshotBulkOpKind.Remove, plan[2].Kind);
            Assert.Equal(200, plan[2].NodeId);
            Assert.Equal(WorldSnapshotBulkOpKind.Add, plan[3].Kind);
            Assert.Equal(200, plan[3].NodeId);
            Assert.Equal(tpl, plan[3].NewObjectTemplate);
        }

        // Test 4: empty / null id list yields zero descriptors (no throw) across all three ops.
        [Fact]
        public void SnapshotBulk_EmptyOrNullIds_YieldZeroDescriptors_NoThrow()
        {
            var empty = new List<int>();

            Assert.Empty(WorldSnapshotBulkComposer.ComposeMove(empty, 1, 1, 1));
            Assert.Empty(WorldSnapshotBulkComposer.ComposeDelete(empty));
            Assert.Empty(WorldSnapshotBulkComposer.ComposeRetemplate(empty, "x"));

            Assert.Empty(WorldSnapshotBulkComposer.ComposeMove(null, 1, 1, 1));
            Assert.Empty(WorldSnapshotBulkComposer.ComposeDelete(null));
            Assert.Empty(WorldSnapshotBulkComposer.ComposeRetemplate(null, "x"));
        }

        // Test 5: a duplicate id list preserves count == input count (no silent dedupe that would
        // desync the composed-command count from the user's selection and corrupt undo).
        [Fact]
        public void SnapshotBulk_DuplicateIds_AreNotDeduped()
        {
            var dupes = new[] { 5, 5, 5 };

            var move = WorldSnapshotBulkComposer.ComposeMove(dupes, 0, 0, 0);
            Assert.Equal(3, move.Count);

            var del = WorldSnapshotBulkComposer.ComposeDelete(dupes);
            Assert.Equal(3, del.Count);

            // retemplate emits a remove+add pair per (duplicated) id → 2x the input count
            var retmpl = WorldSnapshotBulkComposer.ComposeRetemplate(dupes, "x");
            Assert.Equal(6, retmpl.Count);
        }
    }
}
