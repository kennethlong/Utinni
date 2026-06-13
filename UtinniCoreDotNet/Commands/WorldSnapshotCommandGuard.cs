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
// 15-09 WorldSnapshot command bail-on-null DECISION helper (PROD-W2-WS, GAP 1.1).
//
// WHY THIS EXISTS (15-SMOKE Checklist A9 — BLOCKING crash):
//   The four WorldSnapshot IUndoCommand bodies (Position / Rotation / Add /
//   Remove) resolve their target via the NATIVE static lookups
//   `Network.GetObjectById(id)` and `WorldSnapshotReaderWriter.Get().GetNodeById(id)`
//   (also `.ParentNode.GetChildById` / `.LastChild` / `.LastNode`). EVERY one of
//   these returns NULL when the in-world object / node is not currently
//   instantiated (e.g. invoking editor Undo after a bulk op against a node whose
//   live object is no longer present). The shipped command bodies dereferenced
//   the result with NO null guard → `0xC0000005` READ target=0x0 — a hard SWG
//   client crash (15-SMOKE A9, `0f80af9` / `7604627`).
//
//   This is the bail-on-null DECISION the command bodies must consult BEFORE any
//   `obj.` or `node.` dereference: resolve the object AND node first, then ask
//   ShouldApply; if it returns false, the body returns without touching the
//   would-be-null reference.
//
// FRAMEWORK-LEG DISCIPLINE (checker B-1 — mirrors WorldSnapshotBulkComposer /
// Phase-8 LooseOverridePath / Phase-9 CsvCellCoercion / LivePatchValidator):
//   PURE, BCL-ONLY. It does NOT reference the native UtinniCore game-bridge
//   namespace (so it never touches the native Object/Node CppSharp types), the
//   WinForms UI assembly, or the ground-scene update-loop marshalling callbacks.
//   The parameters are typed `object` so the helper is
//   loadable in the xUnit host — the native bridge that actually returns the
//   nullable lookups is static CppSharp and cannot be loaded in the test process,
//   so the DECISION (skip-vs-apply) is what we unit-test, not the native call.
// ─────────────────────────────────────────────────────────────────────────────

namespace UtinniCoreDotNet.Commands
{
    /// <summary>
    /// Pure, BCL-only bail-on-null decision for the WorldSnapshot IUndoCommand bodies. A WS command
    /// must resolve its native object/node lookups FIRST and consult these helpers BEFORE dereferencing
    /// them, because <c>Network.GetObjectById</c> / <c>WorldSnapshotReaderWriter.Get().GetNodeById</c>
    /// (and the parent-node accessors) return <c>null</c> when the in-world object/node is not currently
    /// instantiated — dereferencing that null crashes the SWG client (15-SMOKE A9, GAP 1.1).
    /// </summary>
    public static class WorldSnapshotCommandGuard
    {
        /// <summary>
        /// True only when the single resolved lookup is non-null — i.e. it is safe to dereference.
        /// A null lookup (the absent in-world object/node) returns false: the caller must skip the
        /// dereference rather than null-deref the SWG client.
        /// </summary>
        public static bool ShouldApply(object lookup)
        {
            return lookup != null;
        }

        /// <summary>
        /// True only when BOTH resolved lookups (the in-world object AND the snapshot node) are
        /// non-null. A position/rotation change dereferences both, so either being null means the
        /// caller must bail before touching either reference. Never throws on any null combination.
        /// </summary>
        public static bool ShouldApply(object objLookup, object nodeLookup)
        {
            return objLookup != null && nodeLookup != null;
        }
    }
}
