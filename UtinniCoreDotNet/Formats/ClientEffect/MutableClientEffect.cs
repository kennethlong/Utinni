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
// ClientEffect (.iff / FORM CLEF) tree layout understood by reading swg-client-v2
// .../clientGame/.../ClientEffectTemplate.cpp (SOE/Bootprint, All Rights Reserved): FORM CLEF -> FORM
// <version> (0001/0002/0003) -> N flat command-leaf chunks (CPAP/PSND/CLGT/CAMS/FFBK) read by a
// while-not-at-end-of-form switch (no count chunk, unlike PEFT). Only the on-disk layout was studied
// — no code, comments, identifier names, or test fixtures copied from any reference source.
// Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;
using UtinniCoreDotNet.Formats.Decoders;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Formats.ClientEffect
{
    /// <summary>
    /// Mutable typed ClientEffect model over a captured <see cref="MutableIffDocument"/> — the editable
    /// counterpart to the read-only typed walk, mirroring <c>MutableParticleEffect</c>.
    ///
    /// <para><b>Byte-exact posture:</b> the authoritative bytes live in the captured
    /// <see cref="MutableIffDocument"/>. Untouched leaves re-emit verbatim through
    /// <see cref="IffWriter"/>; a length-changing string/scalar edit re-encodes a command leaf in place
    /// (<see cref="EditCommandPayload"/>); add/remove/reorder delegate to <see cref="MutableIffNode"/>
    /// and the writer re-rolls every FORM length automatically (the verified length-ripple DOM).</para>
    ///
    /// <para><b>Degrade-don't-abort:</b> an unrecognized CLEF version FORM raw-preserves the WHOLE CLEF
    /// (D-13); an unknown command tag OR a truncated/undecodable KNOWN command tag OR a residual-byte
    /// command degrades to a raw command view (REVIEWS #4/#7), each re-emitting its captured slice
    /// verbatim. ONLY a CONTAINER-framing malformation (wrong root, forged count) throws
    /// <see cref="ClientEffectParseException"/> — never a command-leaf payload (REVIEWS #4).</para>
    /// </summary>
    public sealed class MutableClientEffect
    {
        // Absolute cap on the flat command count — reject a forged/oversized count before the read loop
        // allocates (T-22-forged-count). A real effect has a handful of commands.
        internal const int MaxNodeCount = 16 * 1024 * 1024;

        private readonly List<ClientEffectCommand> _commands;

        private MutableClientEffect(MutableIffDocument source, string version, bool rawPreserved,
            MutableIffNode versionForm, List<ClientEffectCommand> commands)
        {
            SourceIff = source;
            Version = version;
            IsRawPreserved = rawPreserved;
            VersionForm = versionForm;
            _commands = commands;
        }

        /// <summary>The captured raw-IFF document — the byte source for byte-exact re-emit.</summary>
        public MutableIffDocument SourceIff { get; }

        /// <summary>The CLEF version FORM tag (e.g. "0003"), or the unrecognized tag when raw-preserved.</summary>
        public string Version { get; }

        /// <summary>
        /// True when the CLEF version FORM tag was not recognized and the whole sub-tree was
        /// raw-preserved (D-13). A raw-preserved effect still opens + round-trips byte-exact.
        /// </summary>
        public bool IsRawPreserved { get; }

        /// <summary>The ordered command views (empty when the effect is raw-preserved).</summary>
        public IReadOnlyList<ClientEffectCommand> Commands { get { return _commands.AsReadOnly(); } }

        /// <summary>True iff any mutation has been applied since parse.</summary>
        public bool IsDirty { get; private set; }

        /// <summary>The version FORM node (parent of the flat command leaves).</summary>
        internal MutableIffNode VersionForm { get; }

        // Recognized CLEF version FORM tags. Anything else raw-preserves the whole CLEF (D-13).
        private static readonly HashSet<string> KnownEffectVersions = new HashSet<string>(StringComparer.Ordinal)
        {
            "0001", "0002", "0003"
        };

        /// <summary>
        /// Parses an effect from a captured <see cref="MutableIffDocument"/>. Asserts the root is
        /// <c>FORM CLEF</c> (else throws), finds the version FORM; an unrecognized version raw-preserves
        /// the whole sub-tree (D-13). For a recognized version it walks the FLAT command-leaf list
        /// (no count chunk, unlike PEFT) building one <see cref="ClientEffectCommand"/> per leaf — a
        /// KNOWN tag is decoded inside a per-command <c>catch (DecoderException)</c> so a
        /// truncated/undecodable/residual-byte payload degrades to a raw view (REVIEWS #4/#7). Each
        /// command's <see cref="ClientEffectCommand.StableId"/> is set from
        /// <see cref="MutableIffDocument.DeriveStableId"/> — the ordinal-path id contract (REVIEWS #1).
        /// </summary>
        public static MutableClientEffect FromMutableIff(MutableIffDocument mutable)
        {
            if (mutable == null) throw new ArgumentNullException("mutable");
            MutableIffNode root = mutable.Root;
            if (root == null || root.Kind != MutableIffNodeKind.Container || root.SubTypeId != "CLEF")
            {
                throw new ClientEffectParseException(ClientEffectParseError.UnexpectedForm,
                    "ClientEffect root is not a FORM CLEF.");
            }

            MutableIffNode versionForm = FirstContainerChild(root);
            if (versionForm == null)
            {
                throw new ClientEffectParseException(ClientEffectParseError.UnexpectedForm,
                    "FORM CLEF has no version sub-form.");
            }

            string version = versionForm.SubTypeId;
            if (!KnownEffectVersions.Contains(version))
            {
                // D-13: unrecognized version → raw-preserve the whole sub-tree. No command walk, no
                // throw. The captured bytes re-emit verbatim through IffWriter.
                return new MutableClientEffect(mutable, version, true, versionForm,
                    new List<ClientEffectCommand>());
            }

            // The command-leaf list is FLAT — the direct leaf children of the version FORM, in order.
            // Count is bounded by the number of leaf children present (a forged count cannot conjure
            // leaves); guard before the build loop allocates (T-22-forged-count).
            var leafChildren = new List<MutableIffNode>();
            foreach (MutableIffNode child in versionForm.Children)
            {
                if (child.Kind == MutableIffNodeKind.Leaf) leafChildren.Add(child);
            }
            GuardCount(leafChildren.Count, "command");

            // The version FORM's ordinal-path prefix is computed ONCE (REVIEWS #1) — the SAME id
            // terrain's DecodeIffCommand surfaces. The version FORM is the first container child of the
            // root, so its prefix is "<rootId>/<versionFormId>/".
            string rootId = MutableIffDocument.DeriveStableId(root, "", 0);
            int versionOrdinal = IndexOf(root, versionForm);
            string versionPrefix = rootId + "/"
                + MutableIffDocument.DeriveStableId(versionForm, "", versionOrdinal) + "/";

            var commands = new List<ClientEffectCommand>(leafChildren.Count);
            // Walk the version FORM's children in physical order so the ordinal matches the on-disk
            // position (the stable-id contract is ordinal-over-ALL-children, not over leaves-only).
            int ordinal = 0;
            foreach (MutableIffNode child in versionForm.Children)
            {
                if (child.Kind != MutableIffNodeKind.Leaf) { ordinal++; continue; }

                ClientEffectCommand cmd;
                try
                {
                    // Per-command catch → raw view (REVIEWS #4 — the Particle :187-201 idiom): a known
                    // tag whose payload is truncated/undecodable/has-residual-bytes degrades to raw,
                    // NEVER a hard abort. An unknown tag also surfaces a DecoderException here.
                    cmd = ClientEffectCommand.Decode(child, version);
                }
                catch (DecoderException)
                {
                    cmd = ClientEffectCommand.Raw(child, version);
                }
                cmd.StableId = MutableIffDocument.DeriveStableId(child, versionPrefix, ordinal);
                commands.Add(cmd);
                ordinal++;
            }

            return new MutableClientEffect(mutable, version, false, versionForm, commands);
        }

        /// <summary>
        /// Re-encodes a command leaf's payload in place via <see cref="ClefFieldCodec"/> output, then
        /// marks it dirty so the DOM re-rolls the version FORM + CLEF FORM lengths automatically (the
        /// verified length-ripple). Refuses to edit when the effect is raw-preserved.
        /// </summary>
        public void EditCommandPayload(MutableIffNode leaf, byte[] newPayload)
        {
            if (leaf == null) throw new ArgumentNullException("leaf");
            if (newPayload == null) throw new ArgumentNullException("newPayload");
            if (IsRawPreserved)
            {
                throw new InvalidOperationException(
                    "This effect is raw-preserved (unrecognized version) — typed editing is refused.");
            }
            leaf.SetPayload(newPayload);
            IsDirty = true;
        }

        /// <summary>
        /// Appends a new command leaf of the given tag + payload to the version FORM. The DOM re-rolls
        /// FORM lengths on serialize. Refuses when raw-preserved.
        /// </summary>
        public MutableIffNode AddCommand(string tag, byte[] payload)
        {
            RequireEditable();
            if (tag == null) throw new ArgumentNullException("tag");
            if (payload == null) throw new ArgumentNullException("payload");
            MutableIffNode leaf = VersionForm.AddLeaf(tag, payload);
            IsDirty = true;
            return leaf;
        }

        /// <summary>Removes a command leaf from the version FORM. Returns true iff a leaf was removed.</summary>
        public bool RemoveCommand(MutableIffNode leaf)
        {
            RequireEditable();
            if (leaf == null) throw new ArgumentNullException("leaf");
            bool removed = VersionForm.Remove(leaf);
            if (removed) IsDirty = true;
            return removed;
        }

        /// <summary>Moves a command leaf one position toward the start (reorder up).</summary>
        public void ReorderCommandUp(MutableIffNode leaf)
        {
            RequireEditable();
            if (leaf == null) throw new ArgumentNullException("leaf");
            VersionForm.ReorderUp(leaf);
            IsDirty = true;
        }

        /// <summary>Moves a command leaf one position toward the end (reorder down).</summary>
        public void ReorderCommandDown(MutableIffNode leaf)
        {
            RequireEditable();
            if (leaf == null) throw new ArgumentNullException("leaf");
            VersionForm.ReorderDown(leaf);
            IsDirty = true;
        }

        /// <summary>
        /// Removes a command by its ordinal-path stable id, delegating to the
        /// <see cref="MutableIffDocument.RemoveByStableId"/> document entry point so the CLI and editor
        /// address a command the same way (REVIEWS #1). Returns true iff a node was removed.
        /// </summary>
        public bool RemoveByStableId(string stableId)
        {
            RequireEditable();
            bool removed = SourceIff.RemoveByStableId(stableId);
            if (removed) IsDirty = true;
            return removed;
        }

        /// <summary>Serializes to byte-exact output via <see cref="IffWriter"/> — no bespoke writer.</summary>
        public byte[] Serialize()
        {
            return IffWriter.Write(SourceIff);
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private void RequireEditable()
        {
            if (IsRawPreserved)
            {
                throw new InvalidOperationException(
                    "This effect is raw-preserved (unrecognized version) — structural editing is refused.");
            }
        }

        // Cap guard (T-22-forged-count): rejects a command count above the absolute cap. The flat list
        // is bounded by the leaf children actually present (a forged count cannot conjure leaves), so
        // this is the over-allocation backstop applied BEFORE the build loop.
        private static void GuardCount(int count, string what)
        {
            if (count < 0)
            {
                throw new ClientEffectParseException(ClientEffectParseError.ForgedCount,
                    what + " count is negative: " + count + ".");
            }
            if (count > MaxNodeCount)
            {
                throw new ClientEffectParseException(ClientEffectParseError.ForgedCount,
                    what + " count " + count + " exceeds the cap " + MaxNodeCount + ".");
            }
        }

        private static MutableIffNode FirstContainerChild(MutableIffNode parent)
        {
            foreach (MutableIffNode child in parent.Children)
            {
                if (child.Kind == MutableIffNodeKind.Container) return child;
            }
            return null;
        }

        private static int IndexOf(MutableIffNode parent, MutableIffNode child)
        {
            for (int i = 0; i < parent.Children.Count; i++)
                if (ReferenceEquals(parent.Children[i], child)) return i;
            return 0;
        }
    }
}
