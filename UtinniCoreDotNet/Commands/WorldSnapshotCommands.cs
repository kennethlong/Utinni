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

using UtinniCore.Swg.Math;
using UtinniCore.Utinni;
using UtinniCoreDotNet.Callbacks;
using UtinniCoreDotNet.UndoRedo;

namespace UtinniCoreDotNet.Commands
{
    public class AddWorldSnapshotNodeCommand : IUndoCommand
    {
        private WorldSnapshotReaderWriter.Node nodeCopy;

        public AddWorldSnapshotNodeCommand(WorldSnapshotReaderWriter.Node node) // Node needs to already be created and added and passed to this ctor
        {
            nodeCopy = new WorldSnapshotReaderWriter.Node(node);
        }

        public string GetText()
        {
            return "Added WorldSnapshot Node: (" + nodeCopy.Id + ") " + nodeCopy.ObjectTemplateName;
        }

        public void Execute()
        {
            GroundSceneCallbacks.AddUpdateLoopCall(() =>
            {
                WorldSnapshot.AddNode(nodeCopy);
            });
        }

        public void Undo()
        {
            GroundSceneCallbacks.AddUpdateLoopCall(() =>
            {
                // As we merely store a copy of the node, we need to fetch the actual node first before removing it.
                // 15-09 A9 fix: LastNode / ParentNode.LastChild can be null when nothing live is present;
                // guard ParentNode BEFORE reading .LastChild, capture to a local, and skip the native
                // RemoveNode entirely on null instead of passing a null into the native remove.
                WorldSnapshotReaderWriter.Node node;
                if (nodeCopy.ParentId == 0)
                {
                    node = WorldSnapshotReaderWriter.Get().LastNode;
                }
                else
                {
                    node = nodeCopy.ParentNode != null ? nodeCopy.ParentNode.LastChild : null;
                }

                if (WorldSnapshotCommandGuard.ShouldApply(node))
                {
                    WorldSnapshot.RemoveNode(node);
                }
            });
        }

        public bool AllowMerge() { return false; }

        public bool Merge(IUndoCommand newCommand) { return false; }
    }

    public class RemoveWorldSnapshotNodeCommand : IUndoCommand
    {
        private readonly WorldSnapshotReaderWriter.Node nodeCopy;

        public RemoveWorldSnapshotNodeCommand(WorldSnapshotReaderWriter.Node node)
        {
            nodeCopy = new WorldSnapshotReaderWriter.Node(node);
        }

        public string GetText()
        {
            return "Removed WorldSnapshot Node: (" + nodeCopy.Id + ") " + nodeCopy.ObjectTemplateName;
        }

        public void Execute()
        {
            GroundSceneCallbacks.AddUpdateLoopCall(() =>
            {
                // As we merely store a copy of the node, we need to fetch the actual node first before removing it.
                // 15-09 A9 fix: LastNode / ParentNode.LastChild can be null when nothing live is present;
                // guard ParentNode BEFORE reading .LastChild, capture to a local, and skip the native
                // RemoveNode entirely on null instead of passing a null into the native remove.
                WorldSnapshotReaderWriter.Node node;
                if (nodeCopy.ParentId == 0)
                {
                    node = WorldSnapshotReaderWriter.Get().LastNode;
                }
                else
                {
                    node = nodeCopy.ParentNode != null ? nodeCopy.ParentNode.LastChild : null;
                }

                if (WorldSnapshotCommandGuard.ShouldApply(node))
                {
                    WorldSnapshot.RemoveNode(node);
                }
            });
        }

        public void Undo()
        {
            GroundSceneCallbacks.AddUpdateLoopCall(() =>
            {
                WorldSnapshot.AddNode(nodeCopy);
            });
        }

        public bool AllowMerge() { return false; }

        public bool Merge(IUndoCommand newCommand) { return false; }
    }

    public class WorldSnapshotNodePositionChangedCommand : IUndoCommand
    {
        private readonly WorldSnapshotReaderWriter.Node nodeCopy;
        private readonly Transform originalTransform;
        private readonly Transform newTransform;

        public WorldSnapshotNodePositionChangedCommand(WorldSnapshotReaderWriter.Node node, Transform originalTransform, Transform newTransform)
        {
            nodeCopy = new WorldSnapshotReaderWriter.Node(node);
            this.originalTransform = new Transform(originalTransform);
            this.newTransform = new Transform(newTransform);
        }

        public string GetText()
        {
            return "Changed Node position: (" + nodeCopy.Id + ") " + nodeCopy.ObjectTemplateName;
        }

        private void SetPosition(Vector position)
        {
            GroundSceneCallbacks.AddUpdateLoopCall(() =>
            {
                // 15-09 A9 fix: resolve the in-world object AND the snapshot node FIRST, then guard
                // before ANY dereference. Both native lookups return null when the object/node is not
                // currently instantiated (e.g. Undo against a node whose live object is gone) — the
                // old code dereferenced obj BEFORE node was even resolved -> 0xC0000005 client crash.
                var obj = Network.GetObjectById(nodeCopy.Id);

                WorldSnapshotReaderWriter.Node node;
                if (nodeCopy.ParentId > 0)
                {
                    node = nodeCopy.ParentNode != null ? nodeCopy.ParentNode.GetChildById(nodeCopy.Id) : null;
                }
                else
                {
                    node = WorldSnapshotReaderWriter.Get().GetNodeById(nodeCopy.Id);
                }

                if (!WorldSnapshotCommandGuard.ShouldApply(obj, node))
                {
                    return; // bail gracefully: a missing object/node is skipped, never dereferenced
                }

                obj.Transform.Position = position;
                obj.PositionAndRotationChanged(false, node.Transform.Position);
                node.Transform.Position = position;

                WorldSnapshot.DetailLevelChanged();
            });
        }

        public void Execute()
        {
            SetPosition(newTransform.Position);
        }

        public void Undo()
        {
            SetPosition(originalTransform.Position);
        }

        public bool AllowMerge()
        {
            return false;
        }

        public bool Merge(IUndoCommand newCommand)
        {
            return false;
        }
    }

    public class WorldSnapshotNodeRotationChangedCommand : IUndoCommand
    {
        private readonly WorldSnapshotReaderWriter.Node nodeCopy;
        private readonly Transform originalTransform;
        private readonly Transform newTransform;

        public WorldSnapshotNodeRotationChangedCommand(WorldSnapshotReaderWriter.Node node, Transform originalTransform, Transform newTransform)
        {
            nodeCopy = new WorldSnapshotReaderWriter.Node(node);
            this.originalTransform = new Transform(originalTransform);
            this.newTransform = new Transform(newTransform);
        }

        public string GetText()
        {
            return "Changed Node rotation: (" + nodeCopy.Id + ") " + nodeCopy.ObjectTemplateName;
        }

        private void SetRotation(Transform transform)
        {
            GroundSceneCallbacks.AddUpdateLoopCall(() =>
            {
                // 15-09 A9 fix: resolve object AND node FIRST, single guard before any deref (see
                // SetPosition). The old code dereferenced obj before node was resolved -> null-deref AV.
                var obj = Network.GetObjectById(nodeCopy.Id);

                WorldSnapshotReaderWriter.Node node;
                if (nodeCopy.ParentId > 0)
                {
                    node = nodeCopy.ParentNode != null ? nodeCopy.ParentNode.GetChildById(nodeCopy.Id) : null;
                }
                else
                {
                    node = WorldSnapshotReaderWriter.Get().GetNodeById(nodeCopy.Id);
                }

                if (!WorldSnapshotCommandGuard.ShouldApply(obj, node))
                {
                    return; // bail gracefully: a missing object/node is skipped, never dereferenced
                }

                obj.Transform.CopyRotation(transform);
                obj.PositionAndRotationChanged(false, node.Transform.Position);
                node.Transform.CopyRotation(transform);

                WorldSnapshot.DetailLevelChanged();
            });
        }

        public void Execute()
        {
            SetRotation(newTransform);
        }

        public void Undo()
        {
            SetRotation(originalTransform);
        }

        public bool AllowMerge()
        {
            return false;
        }

        public bool Merge(IUndoCommand newCommand)
        {
            return false;
        }
    }
}
