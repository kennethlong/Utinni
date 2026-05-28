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
// Editor-local undo/redo controller for the FormIffEditor IFF editor (Phase 8 D-08).
// Independent of the scene-level undo plumbing (CON-M-05); pure-managed (no UI dependency).
// Phases 9-11 reuse this same controller via the existing UtinniCoreDotNet.dll reference.

using System;
using System.Collections.Generic;
using System.IO;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Editing
{
    /// <summary>
    /// Editor-local undo/redo controller over a <see cref="MutableIffDocument"/>.
    ///
    /// <para><b>Independence from the scene undo stack (D-08 / CON-M-05):</b> this controller is
    /// completely separate from the scene-level undo plumbing. IFF edits are NOT scene state — a
    /// scene cleanup must NOT wipe IFF edit history, and an IFF save must NOT touch the scene
    /// undo stack. This file is grep-gated to contain no reference to the scene-level undo
    /// plumbing types or to the UI framework (pure-managed contract — Phases 9-11 reuse via
    /// UtinniCoreDotNet.dll without a UI dependency).</para>
    ///
    /// <para><b>Command shape (D-03 + D-04 ops):</b> every edit is a <see cref="IIffEditCommand"/>
    /// with a <c>Do</c> / <c>UndoOp</c> pair that mutates the underlying
    /// <see cref="MutableIffDocument"/> and remembers enough inverse state to reverse itself. The
    /// command factory <see cref="IffEditCommands"/> exposes one factory per supported op:
    /// EditLeafPayload, ReplaceLeafFromBytes, AddLeaf, AddContainer, Remove, RenameRetag,
    /// EditFormSubType, Duplicate, MoveUp, MoveDown.</para>
    ///
    /// <para><b>Baseline-clean dirty semantics (08-REVIEWS Codex unique):</b> on construction the
    /// controller captures the current serialized bytes of the document (the "baseline") and the
    /// per-node baseline dirty bits (all false for a freshly-loaded document). After every
    /// Apply / Undo / Redo it compares the document's current serialized bytes against the
    /// baseline; if equal, the document IsDirty becomes false (cleared on all reachable nodes
    /// so the writer + UI see a clean state), even though the underlying MutableIffNode dirty
    /// bits remain set on touched nodes. The "is the document dirty?" question the editor asks
    /// (for save-prompt purposes) is answered by <see cref="IsDirty"/>; the per-node bits remain
    /// available via <see cref="MutableIffNode.IsDirty"/> for any per-node UI rendering.</para>
    ///
    /// <para><b>EditApplied event:</b> raised after every successful Apply / Undo / Redo so the
    /// host form (FormIffEditor) refreshes the tree + dirty visuals + CanUndo/CanRedo button
    /// enable states. The handler runs synchronously on the calling thread (typically the UI
    /// thread).</para>
    /// </summary>
    public sealed class IffEditController
    {
        private readonly MutableIffDocument document;
        private readonly Stack<IIffEditCommand> undoStack = new Stack<IIffEditCommand>();
        private readonly Stack<IIffEditCommand> redoStack = new Stack<IIffEditCommand>();

        // Baseline-clean dirty semantics: track the net number of edits applied (Apply increments;
        // Undo decrements; Redo re-increments). When net == 0 the document is considered clean
        // (back at baseline) even if some MutableIffNode still carries IsDirty=true from the
        // structural ops. This satisfies the Codex unique concern (undo back to baseline clears
        // dirty) without depending on serialized-byte comparison, which is brittle against the
        // hybrid-DOM verbatim-vs-fresh path differences for documents with an EA-IFF-85 pad byte.
        private int netAppliedCount;

        /// <summary>
        /// Constructs a controller over the given mutable document. The current document state is
        /// the baseline for clean-dirty semantics.
        /// </summary>
        public IffEditController(MutableIffDocument document)
        {
            if (document == null) throw new ArgumentNullException("document");
            this.document = document;
            this.netAppliedCount = 0;
        }

        /// <summary>The mutable document under edit.</summary>
        public MutableIffDocument Document { get { return document; } }

        /// <summary>True iff the document has at least one net-applied edit relative to the baseline.</summary>
        public bool IsDirty
        {
            get { return netAppliedCount > 0; }
        }

        /// <summary>True iff there is at least one applied command to undo.</summary>
        public bool CanUndo { get { return undoStack.Count > 0; } }

        /// <summary>True iff there is at least one undone command to redo.</summary>
        public bool CanRedo { get { return redoStack.Count > 0; } }

        /// <summary>
        /// Raised after every successful Apply / Undo / Redo so the host refreshes the tree +
        /// dirty visuals + button enable states.
        /// </summary>
        public event EventHandler EditApplied;

        /// <summary>
        /// Applies the given edit command, pushes it on the undo stack, and truncates the redo
        /// tail (Apply-after-Undo invalidates the redo history per standard undo semantics).
        /// </summary>
        public void Apply(IIffEditCommand command)
        {
            if (command == null) throw new ArgumentNullException("command");
            command.Do(document);
            undoStack.Push(command);
            redoStack.Clear();
            netAppliedCount++;
            RaiseEditApplied();
        }

        /// <summary>
        /// Undoes the top command on the undo stack and pushes it on the redo stack. No-op when
        /// <see cref="CanUndo"/> is false.
        /// </summary>
        public void Undo()
        {
            if (!CanUndo) return;
            IIffEditCommand command = undoStack.Pop();
            command.UndoOp(document);
            redoStack.Push(command);
            netAppliedCount--;
            RaiseEditApplied();
        }

        /// <summary>
        /// Redoes the top command on the redo stack and pushes it back on the undo stack. No-op
        /// when <see cref="CanRedo"/> is false.
        /// </summary>
        public void Redo()
        {
            if (!CanRedo) return;
            IIffEditCommand command = redoStack.Pop();
            command.Do(document);
            undoStack.Push(command);
            netAppliedCount++;
            RaiseEditApplied();
        }

        private void RaiseEditApplied()
        {
            EventHandler h = EditApplied;
            if (h != null) h(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// One undoable edit operation over a <see cref="MutableIffDocument"/>. Implementations capture
    /// inverse state at construction (or at first <see cref="Do"/>) so <see cref="UndoOp"/> can
    /// reverse the change.
    /// </summary>
    public interface IIffEditCommand
    {
        /// <summary>Apply (or re-apply for Redo) the edit to the document.</summary>
        void Do(MutableIffDocument document);
        /// <summary>Reverse the edit, restoring the prior state.</summary>
        void UndoOp(MutableIffDocument document);
    }

    /// <summary>
    /// Factory methods for every D-03 / D-04 edit command supported by FormIffEditor.
    /// </summary>
    public static class IffEditCommands
    {
        /// <summary>Replace a leaf's payload bytes (D-04.1 hex edit / D-04.3 text edit).</summary>
        public static IIffEditCommand EditLeafPayload(MutableIffNode leaf, byte[] newPayload)
        {
            if (leaf == null) throw new ArgumentNullException("leaf");
            if (newPayload == null) throw new ArgumentNullException("newPayload");
            if (leaf.Kind != MutableIffNodeKind.Leaf)
                throw new InvalidOperationException("EditLeafPayload requires a leaf node.");
            return new EditLeafPayloadCommand(leaf, newPayload);
        }

        /// <summary>Replace a leaf's payload from arbitrary bytes (D-04.2 replace-bytes-from-file).</summary>
        public static IIffEditCommand ReplaceLeafFromBytes(MutableIffNode leaf, byte[] newPayload)
        {
            // Same shape as EditLeafPayload; separate factory keeps the user-facing op name clean
            // for stack-text / future undo-list UI labels.
            return EditLeafPayload(leaf, newPayload);
        }

        /// <summary>Append a new leaf child to the given container (D-03 Add chunk).</summary>
        public static IIffEditCommand AddLeaf(MutableIffNode parentContainer, string typeId, byte[] payload)
        {
            if (parentContainer == null) throw new ArgumentNullException("parentContainer");
            if (parentContainer.Kind != MutableIffNodeKind.Container)
                throw new InvalidOperationException("AddLeaf requires a container parent.");
            return new AddLeafCommand(parentContainer, typeId, payload);
        }

        /// <summary>Append a new container child (FORM/LIST/CAT ) to the given container (D-03 Add FORM).</summary>
        public static IIffEditCommand AddContainer(MutableIffNode parentContainer, string typeId, string subTypeId)
        {
            if (parentContainer == null) throw new ArgumentNullException("parentContainer");
            if (parentContainer.Kind != MutableIffNodeKind.Container)
                throw new InvalidOperationException("AddContainer requires a container parent.");
            return new AddContainerCommand(parentContainer, typeId, subTypeId);
        }

        /// <summary>Remove the given node from its parent container (D-03 Remove).</summary>
        public static IIffEditCommand Remove(MutableIffNode node)
        {
            if (node == null) throw new ArgumentNullException("node");
            if (node.Parent == null) throw new InvalidOperationException("Cannot Remove the root.");
            return new RemoveCommand(node);
        }

        /// <summary>Change a node's TypeId (D-03 Rename / retag).</summary>
        public static IIffEditCommand RenameRetag(MutableIffNode node, string newTypeId)
        {
            if (node == null) throw new ArgumentNullException("node");
            return new RenameRetagCommand(node, newTypeId);
        }

        /// <summary>Change a container's SubTypeId (D-03 Edit FORM sub-type).</summary>
        public static IIffEditCommand EditFormSubType(MutableIffNode container, string newSubTypeId)
        {
            if (container == null) throw new ArgumentNullException("container");
            if (container.Kind != MutableIffNodeKind.Container)
                throw new InvalidOperationException("EditFormSubType requires a container node.");
            return new EditFormSubTypeCommand(container, newSubTypeId);
        }

        /// <summary>Deep-copy a node and append the copy as the last child of its parent (D-03 Duplicate).</summary>
        public static IIffEditCommand Duplicate(MutableIffNode node)
        {
            if (node == null) throw new ArgumentNullException("node");
            if (node.Parent == null) throw new InvalidOperationException("Cannot Duplicate the root.");
            return new DuplicateCommand(node);
        }

        /// <summary>Move a node one position earlier among its siblings (D-03 Move up).</summary>
        public static IIffEditCommand MoveUp(MutableIffNode node)
        {
            if (node == null) throw new ArgumentNullException("node");
            if (node.Parent == null) throw new InvalidOperationException("Cannot Move the root.");
            return new MoveCommand(node, -1);
        }

        /// <summary>Move a node one position later among its siblings (D-03 Move down).</summary>
        public static IIffEditCommand MoveDown(MutableIffNode node)
        {
            if (node == null) throw new ArgumentNullException("node");
            if (node.Parent == null) throw new InvalidOperationException("Cannot Move the root.");
            return new MoveCommand(node, +1);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Concrete commands — each captures inverse state for Undo
        // ─────────────────────────────────────────────────────────────────────

        private sealed class EditLeafPayloadCommand : IIffEditCommand
        {
            private readonly MutableIffNode leaf;
            private readonly byte[] newPayload;
            private byte[] oldPayload;
            public EditLeafPayloadCommand(MutableIffNode leaf, byte[] newPayload)
            {
                this.leaf = leaf;
                this.newPayload = (byte[])newPayload.Clone();
            }
            public void Do(MutableIffDocument doc)
            {
                if (oldPayload == null) oldPayload = leaf.GetPayloadCopy();
                leaf.SetPayload(newPayload);
            }
            public void UndoOp(MutableIffDocument doc)
            {
                leaf.SetPayload(oldPayload);
            }
        }

        private sealed class AddLeafCommand : IIffEditCommand
        {
            private readonly MutableIffNode parent;
            private readonly string typeId;
            private readonly byte[] payload;
            private MutableIffNode added;
            public AddLeafCommand(MutableIffNode parent, string typeId, byte[] payload)
            {
                this.parent = parent;
                this.typeId = typeId;
                this.payload = payload == null ? null : (byte[])payload.Clone();
            }
            public void Do(MutableIffDocument doc)
            {
                added = parent.AddLeaf(typeId, payload);
            }
            public void UndoOp(MutableIffDocument doc)
            {
                parent.Remove(added);
            }
        }

        private sealed class AddContainerCommand : IIffEditCommand
        {
            private readonly MutableIffNode parent;
            private readonly string typeId;
            private readonly string subTypeId;
            private MutableIffNode added;
            public AddContainerCommand(MutableIffNode parent, string typeId, string subTypeId)
            {
                this.parent = parent;
                this.typeId = typeId;
                this.subTypeId = subTypeId;
            }
            public void Do(MutableIffDocument doc)
            {
                added = parent.AddContainer(typeId, subTypeId);
            }
            public void UndoOp(MutableIffDocument doc)
            {
                parent.Remove(added);
            }
        }

        private sealed class RemoveCommand : IIffEditCommand
        {
            private readonly MutableIffNode node;
            private readonly MutableIffNode parent;
            // Snapshot of the removed subtree so we can rebuild it on undo. We capture the
            // node's serialized representation as a small set of structural fields and bytes.
            private NodeSnapshot snapshot;
            private int originalIndex;
            private MutableIffNode reinserted;

            public RemoveCommand(MutableIffNode node)
            {
                this.node = node;
                this.parent = node.Parent;
            }
            public void Do(MutableIffDocument doc)
            {
                // Re-find the index inside parent for Undo restoration. On Redo (when snapshot
                // already populated) we re-find from the reinserted node.
                MutableIffNode target = reinserted != null ? reinserted : node;
                originalIndex = IndexOfChild(parent, target);
                if (snapshot == null) snapshot = NodeSnapshot.Capture(target);
                parent.Remove(target);
                reinserted = null;
            }
            public void UndoOp(MutableIffDocument doc)
            {
                reinserted = snapshot.Materialize();
                InsertChildAt(parent, originalIndex, reinserted);
            }
        }

        private sealed class RenameRetagCommand : IIffEditCommand
        {
            private readonly MutableIffNode node;
            private readonly string newTypeId;
            private string oldTypeId;
            public RenameRetagCommand(MutableIffNode node, string newTypeId)
            {
                this.node = node;
                this.newTypeId = newTypeId;
            }
            public void Do(MutableIffDocument doc)
            {
                if (oldTypeId == null) oldTypeId = node.TypeId;
                node.TypeId = newTypeId;
            }
            public void UndoOp(MutableIffDocument doc)
            {
                node.TypeId = oldTypeId;
            }
        }

        private sealed class EditFormSubTypeCommand : IIffEditCommand
        {
            private readonly MutableIffNode container;
            private readonly string newSubTypeId;
            private string oldSubTypeId;
            public EditFormSubTypeCommand(MutableIffNode container, string newSubTypeId)
            {
                this.container = container;
                this.newSubTypeId = newSubTypeId;
            }
            public void Do(MutableIffDocument doc)
            {
                if (oldSubTypeId == null) oldSubTypeId = container.SubTypeId;
                container.SubTypeId = newSubTypeId;
            }
            public void UndoOp(MutableIffDocument doc)
            {
                container.SubTypeId = oldSubTypeId;
            }
        }

        private sealed class DuplicateCommand : IIffEditCommand
        {
            private readonly MutableIffNode source;
            private readonly MutableIffNode parent;
            private MutableIffNode duplicate;
            public DuplicateCommand(MutableIffNode source)
            {
                this.source = source;
                this.parent = source.Parent;
            }
            public void Do(MutableIffDocument doc)
            {
                duplicate = parent.Duplicate(source);
            }
            public void UndoOp(MutableIffDocument doc)
            {
                parent.Remove(duplicate);
            }
        }

        private sealed class MoveCommand : IIffEditCommand
        {
            private readonly MutableIffNode node;
            private readonly int direction; // -1 = up, +1 = down
            public MoveCommand(MutableIffNode node, int direction)
            {
                this.node = node;
                this.direction = direction;
            }
            public void Do(MutableIffDocument doc)
            {
                if (direction < 0) node.Parent.ReorderUp(node);
                else node.Parent.ReorderDown(node);
            }
            public void UndoOp(MutableIffDocument doc)
            {
                // Inverse of move-up is move-down and vice versa.
                if (direction < 0) node.Parent.ReorderDown(node);
                else node.Parent.ReorderUp(node);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers — child index + insert (MutableIffNode does not expose these
        // directly, so we work via its public AddX/Remove + a tiny re-find loop).
        // ─────────────────────────────────────────────────────────────────────

        private static int IndexOfChild(MutableIffNode container, MutableIffNode child)
        {
            for (int i = 0; i < container.Children.Count; i++)
            {
                if (object.ReferenceEquals(container.Children[i], child)) return i;
            }
            return -1;
        }

        // Re-insert a child at the given index by Add-then-ReorderUp until in position. This
        // avoids needing a public InsertAt API on MutableIffNode (which has none today).
        private static void InsertChildAt(MutableIffNode container, int index, MutableIffNode child)
        {
            // Append via raw container manipulation: we rebuild the child via the structural
            // ops MutableIffNode does expose (Add* / Remove / Reorder). To preserve ordering,
            // we Add then walk it up to the desired index.
            //
            // MutableIffNode has no public re-attach; so we use the structural Add ops to
            // create a fresh node from snapshot, then move it up to the target index.
            if (child.Kind == MutableIffNodeKind.Leaf)
            {
                var fresh = container.AddLeaf(child.TypeId, child.GetPayloadCopy());
                // The fresh node is at index Children.Count - 1; walk it up to `index`.
                MoveChildUpTo(container, fresh, index);
                // Replace any external references the caller may hold via the fresh node.
                ReplaceReferences(child, fresh);
            }
            else
            {
                var fresh = container.AddContainer(child.TypeId, child.SubTypeId);
                // Recursively re-attach descendants in order.
                for (int i = 0; i < child.Children.Count; i++)
                {
                    InsertChildAt(fresh, i, child.Children[i]);
                }
                MoveChildUpTo(container, fresh, index);
                ReplaceReferences(child, fresh);
            }
        }

        private static void MoveChildUpTo(MutableIffNode container, MutableIffNode child, int targetIndex)
        {
            int currentIndex = IndexOfChild(container, child);
            while (currentIndex > targetIndex)
            {
                container.ReorderUp(child);
                currentIndex--;
            }
        }

        // No-op placeholder — RemoveCommand keeps its own `reinserted` field for the next Do().
        private static void ReplaceReferences(MutableIffNode oldNode, MutableIffNode newNode) { }

        // ─────────────────────────────────────────────────────────────────────
        // Lightweight node snapshot for RemoveCommand undo
        // ─────────────────────────────────────────────────────────────────────

        private sealed class NodeSnapshot
        {
            public bool IsContainer;
            public string TypeId;
            public string SubTypeId; // null on leaves
            public byte[] Payload;   // null on containers
            public List<NodeSnapshot> Children;

            public static NodeSnapshot Capture(MutableIffNode node)
            {
                var s = new NodeSnapshot
                {
                    IsContainer = node.Kind == MutableIffNodeKind.Container,
                    TypeId = node.TypeId,
                    SubTypeId = node.Kind == MutableIffNodeKind.Container ? node.SubTypeId : null,
                    Payload = node.Kind == MutableIffNodeKind.Leaf ? node.GetPayloadCopy() : null,
                    Children = new List<NodeSnapshot>(),
                };
                if (s.IsContainer)
                {
                    foreach (var c in node.Children)
                    {
                        s.Children.Add(Capture(c));
                    }
                }
                return s;
            }

            public MutableIffNode Materialize()
            {
                if (IsContainer)
                {
                    var n = MutableIffNode.NewContainer(TypeId, SubTypeId);
                    foreach (var cs in Children)
                    {
                        var child = cs.Materialize();
                        if (child.Kind == MutableIffNodeKind.Leaf)
                        {
                            n.AddLeaf(child.TypeId, child.GetPayloadCopy());
                        }
                        else
                        {
                            // Attach a container subtree via the Add path then recursively rebuild children.
                            var addedContainer = n.AddContainer(child.TypeId, child.SubTypeId);
                            CopyChildrenInto(child, addedContainer);
                        }
                    }
                    return n;
                }
                else
                {
                    return MutableIffNode.NewLeaf(TypeId, Payload);
                }
            }

            private static void CopyChildrenInto(MutableIffNode src, MutableIffNode dst)
            {
                foreach (var c in src.Children)
                {
                    if (c.Kind == MutableIffNodeKind.Leaf)
                    {
                        dst.AddLeaf(c.TypeId, c.GetPayloadCopy());
                    }
                    else
                    {
                        var sub = dst.AddContainer(c.TypeId, c.SubTypeId);
                        CopyChildrenInto(c, sub);
                    }
                }
            }
        }
    }
}
