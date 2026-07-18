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

// Goal B Wave 2 (contract v18): id-keyed undo support for the ADVERTISED client's live
// snapshot. The classic WorldSnapshotCommands hold native Node COPIES and re-resolve them
// against the SWGEmu offline reader — none of which exists on the advertised client, where
// every operation keys by int64 network id through UtinniCore.Utinni.WorldSnapshotLive.
// A record snapshots exactly the fields the wsAddNodeAt replay shim takes; Replay() honors
// the HARD one-batch subtree contract (top node + ALL children re-added inside a single
// game-thread call, so the engine's next update tick sees the whole subtree — never a
// partial one). All Capture/Replay/remove work is GAME-THREAD-ONLY; the commands marshal
// via GroundSceneCallbacks.AddUpdateLoopCall like their SWGEmu counterparts.

using System.Collections.Generic;
using UtinniCore.Swg.Math;
using UtinniCore.Utinni;
using UtinniCoreDotNet.Callbacks;
using UtinniCoreDotNet.UndoRedo;

namespace UtinniCoreDotNet.Commands
{
    /// <summary>
    /// A plain managed snapshot of one live-snapshot node (plus its subtree) — the id-keyed
    /// analog of the classic commands' native Node copy. Carries exactly the wsAddNodeAt
    /// replay fields. Capture and Replay are game-thread-only.
    /// </summary>
    public sealed class LiveWorldSnapshotNodeRecord
    {
        public long Id;
        public long ContainedById;
        public string ObjectTemplateName = "";
        public int CellIndex;
        public Transform NodeTransform; // parent-relative, per the node model
        public float Radius;
        public uint PortalLayoutCrc;
        public readonly List<LiveWorldSnapshotNodeRecord> Children = new List<LiveWorldSnapshotNodeRecord>();

        /// <summary>
        /// GAME THREAD ONLY. Reads the node + its whole enumerable subtree through the live
        /// snapshot rows. Returns null when the id is not a (non-tombstoned, authored) node.
        /// </summary>
        public static LiveWorldSnapshotNodeRecord Capture(long id)
        {
            using (var info = new WorldSnapshotNodeInfo())
            {
                if (!WorldSnapshotLive.GetNodeInfo(id, info))
                {
                    return null;
                }

                var record = new LiveWorldSnapshotNodeRecord
                {
                    Id = id,
                    ContainedById = info.ContainedById,
                    ObjectTemplateName = WorldSnapshotLive.GetNodeTemplateName(id) ?? "",
                    CellIndex = info.CellIndex,
                    NodeTransform = new Transform(info.Transform), // copy out of the POD view before dispose
                    Radius = info.Radius,
                    PortalLayoutCrc = info.PortalLayoutCrc,
                };

                int childCount = info.ChildCount;
                for (int i = 0; i < childCount; i++)
                {
                    long childId = WorldSnapshotLive.GetChildIdAt(id, i);
                    if (childId == 0)
                    {
                        continue;
                    }

                    var child = Capture(childId);
                    if (child != null)
                    {
                        record.Children.Add(child);
                    }
                }

                return record;
            }
        }

        /// <summary>
        /// GAME THREAD ONLY. Re-adds the recorded subtree at its recorded ids — top node and
        /// ALL children in this one call (the one-batch contract). Returns true when the top
        /// node re-added; child re-adds are best-effort data ops per the provider pin (a
        /// child under a not-yet-spawned POB appears when the POB re-streams).
        /// </summary>
        public bool Replay()
        {
            if (!WorldSnapshotLive.AddNodeAt(Id, ContainedById, ObjectTemplateName, CellIndex, NodeTransform, Radius, PortalLayoutCrc))
            {
                return false;
            }

            ReplaySubtree(this);
            return true;
        }

        private static void ReplaySubtree(LiveWorldSnapshotNodeRecord node)
        {
            foreach (var child in node.Children)
            {
                WorldSnapshotLive.AddNodeAt(child.Id, child.ContainedById, child.ObjectTemplateName, child.CellIndex, child.NodeTransform, child.Radius, child.PortalLayoutCrc);
                ReplaySubtree(child);
            }
        }
    }

    /// <summary>
    /// Undo command for an interactive add on the advertised client. Construct with a record
    /// captured AFTER the add succeeded (so it carries the provider-minted ids). Undo removes
    /// by id; Execute (redo) replays the recorded subtree at those same ids.
    /// </summary>
    public class AddLiveWorldSnapshotNodeCommand : IUndoCommand
    {
        private readonly LiveWorldSnapshotNodeRecord record;

        public AddLiveWorldSnapshotNodeCommand(LiveWorldSnapshotNodeRecord record)
        {
            this.record = record;
        }

        public string GetText()
        {
            return "Added WorldSnapshot Node: (" + record.Id + ") " + record.ObjectTemplateName;
        }

        public void Execute()
        {
            GroundSceneCallbacks.AddUpdateLoopCall(() =>
            {
                record.Replay();
            });
        }

        public void Undo()
        {
            GroundSceneCallbacks.AddUpdateLoopCall(() =>
            {
                // Tri-state: 1 removed, 0 miss (id already gone — null-safe by contract),
                // -1 occupied (someone stands inside; leave the node — re-running Undo after
                // stepping out works because the id re-resolves).
                WorldSnapshotLive.RemoveNode(record.Id);
            });
        }

        public bool AllowMerge() { return false; }

        public bool Merge(IUndoCommand newCommand) { return false; }
    }

    /// <summary>
    /// Undo command for a remove on the advertised client. Construct with a record captured
    /// BEFORE the remove ran. Execute (redo) removes by id; Undo replays the recorded subtree
    /// at its original ids (the whole reason wsAddNodeAt exists).
    /// </summary>
    public class RemoveLiveWorldSnapshotNodeCommand : IUndoCommand
    {
        private readonly LiveWorldSnapshotNodeRecord record;

        public RemoveLiveWorldSnapshotNodeCommand(LiveWorldSnapshotNodeRecord record)
        {
            this.record = record;
        }

        public string GetText()
        {
            return "Removed WorldSnapshot Node: (" + record.Id + ") " + record.ObjectTemplateName;
        }

        public void Execute()
        {
            GroundSceneCallbacks.AddUpdateLoopCall(() =>
            {
                WorldSnapshotLive.RemoveNode(record.Id);
            });
        }

        public void Undo()
        {
            GroundSceneCallbacks.AddUpdateLoopCall(() =>
            {
                record.Replay();
            });
        }

        public bool AllowMerge() { return false; }

        public bool Merge(IUndoCommand newCommand) { return false; }
    }
}
