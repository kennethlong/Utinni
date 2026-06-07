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

// ─────────────────────────────────────────────────────────────────────────────
// 15-01 WorldSnapshot bulk-op command-composition helper (PROD-W2-WS, D-01/D-02).
//
// FRAMEWORK-LEG DISCIPLINE (checker B-1 — mirrors Phase-8 LooseOverridePath /
// Phase-9 CsvCellCoercion / Phase-8 LivePatchValidator):
//   This file is a PURE, BCL-ONLY helper. It computes the ORDERED PLAN of which
//   shipped per-node snapshot commands to compose for a bulk move / delete /
//   retemplate — it does NOT touch the WinForms layer, the native game-thread
//   marshalling callbacks, the native reader/writer, or the IUndoCommand types
//   themselves. The TJT-side bulk methods consume this plan and enqueue exactly N
//   existing per-node commands inside ONE game-frame update call so the whole bulk
//   lands atomically and is undoable.
//
// UNDO WIRING (CONTEXT discretion area, decided here):
//   N ORDERED descriptors — NOT a single composite. The shipped per-node
//   WorldSnapshotCommands (Add/Remove/PositionChanged) are already the unit of
//   undo (each marshals via AddUpdateLoopCall and snapshots the node in its ctor).
//   Composing them as N ordered descriptors keeps the helper a trivial pure map,
//   preserves per-node undo granularity, and avoids re-implementing undo. A bulk
//   retemplate emits a Remove + Add pair PER node (Remove first, then Add), in
//   input order, mirroring the per-node "remove + add" idiom the SOE pipeline
//   uses for object-template swaps.
//
// COUNT INTEGRITY (T-15-05b adjacent): the plan NEVER silently dedupes a
// duplicate id list — a desync between the composed-command count and the user's
// selection would corrupt the undo stack. Duplicate ids yield duplicate
// descriptors (count == input count for Move/Delete; 2x for Retemplate).
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;

namespace UtinniCoreDotNet.Commands
{
    /// <summary>
    /// The shipped <c>WorldSnapshotCommands</c> command kind a bulk-op descriptor maps to.
    /// </summary>
    public enum WorldSnapshotBulkOpKind
    {
        /// <summary>Compose a <c>WorldSnapshotNodePositionChangedCommand</c> for the node.</summary>
        PositionChanged,

        /// <summary>Compose a <c>RemoveWorldSnapshotNodeCommand</c> for the node.</summary>
        Remove,

        /// <summary>Compose an <c>AddWorldSnapshotNodeCommand</c> (after a Remove, for retemplate).</summary>
        Add,
    }

    /// <summary>
    /// One step of a bulk-operation plan: which shipped command to compose for which node id, plus
    /// the per-node arguments (move delta / new object-template). Pure data — no behavior, no native
    /// or WinForms references.
    /// </summary>
    public sealed class WorldSnapshotBulkOpDescriptor
    {
        public WorldSnapshotBulkOpDescriptor(WorldSnapshotBulkOpKind kind, int nodeId)
        {
            Kind = kind;
            NodeId = nodeId;
        }

        /// <summary>Which shipped command to compose.</summary>
        public WorldSnapshotBulkOpKind Kind { get; }

        /// <summary>The target node id (the stable selection key from the placements table).</summary>
        public int NodeId { get; }

        /// <summary>Move delta X (only meaningful for <see cref="WorldSnapshotBulkOpKind.PositionChanged"/>).</summary>
        public float DeltaX { get; set; }

        /// <summary>Move delta Y (only meaningful for <see cref="WorldSnapshotBulkOpKind.PositionChanged"/>).</summary>
        public float DeltaY { get; set; }

        /// <summary>Move delta Z (only meaningful for <see cref="WorldSnapshotBulkOpKind.PositionChanged"/>).</summary>
        public float DeltaZ { get; set; }

        /// <summary>
        /// The new object-template (only meaningful for the <see cref="WorldSnapshotBulkOpKind.Add"/>
        /// half of a retemplate pair). Null for Move/Delete.
        /// </summary>
        public string NewObjectTemplate { get; set; }
    }

    /// <summary>
    /// Pure, BCL-only helper that turns a selection of node ids + bulk parameters into an ordered
    /// list of bulk-operation descriptors. The TJT-side <c>WorldSnapshotImpl</c> derives a plan via
    /// this helper and enqueues exactly the named shipped commands inside ONE atomic game-frame
    /// update call. This helper does NOT execute, marshal, or undo anything — it only computes the
    /// composition plan (checker B-1 framework-leg discipline).
    /// </summary>
    public static class WorldSnapshotBulkComposer
    {
        /// <summary>
        /// Compose a bulk MOVE: one <see cref="WorldSnapshotBulkOpKind.PositionChanged"/> descriptor
        /// per id, in input order. A null/empty id list yields an empty plan (no throw). Duplicate
        /// ids are preserved (no silent dedupe — see count-integrity note).
        /// </summary>
        public static IReadOnlyList<WorldSnapshotBulkOpDescriptor> ComposeMove(
            IEnumerable<int> nodeIds, float dx, float dy, float dz)
        {
            var plan = new List<WorldSnapshotBulkOpDescriptor>();
            if (nodeIds == null)
            {
                return plan;
            }

            foreach (int id in nodeIds)
            {
                plan.Add(new WorldSnapshotBulkOpDescriptor(WorldSnapshotBulkOpKind.PositionChanged, id)
                {
                    DeltaX = dx,
                    DeltaY = dy,
                    DeltaZ = dz,
                });
            }

            return plan;
        }

        /// <summary>
        /// Compose a bulk DELETE: one <see cref="WorldSnapshotBulkOpKind.Remove"/> descriptor per id,
        /// in input order. Null/empty → empty plan. Duplicates preserved.
        /// </summary>
        public static IReadOnlyList<WorldSnapshotBulkOpDescriptor> ComposeDelete(IEnumerable<int> nodeIds)
        {
            var plan = new List<WorldSnapshotBulkOpDescriptor>();
            if (nodeIds == null)
            {
                return plan;
            }

            foreach (int id in nodeIds)
            {
                plan.Add(new WorldSnapshotBulkOpDescriptor(WorldSnapshotBulkOpKind.Remove, id));
            }

            return plan;
        }

        /// <summary>
        /// Compose a bulk RETEMPLATE: a Remove + Add PAIR per id, in input order (Remove first, then
        /// Add carrying the new object-template). Null/empty id list → empty plan. Duplicates
        /// preserved (each duplicate id emits its own remove+add pair).
        /// </summary>
        public static IReadOnlyList<WorldSnapshotBulkOpDescriptor> ComposeRetemplate(
            IEnumerable<int> nodeIds, string newTemplate)
        {
            var plan = new List<WorldSnapshotBulkOpDescriptor>();
            if (nodeIds == null)
            {
                return plan;
            }

            foreach (int id in nodeIds)
            {
                plan.Add(new WorldSnapshotBulkOpDescriptor(WorldSnapshotBulkOpKind.Remove, id));
                plan.Add(new WorldSnapshotBulkOpDescriptor(WorldSnapshotBulkOpKind.Add, id)
                {
                    NewObjectTemplate = newTemplate,
                });
            }

            return plan;
        }
    }
}
