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
// Format-spec parity reference: none — this is a pure-managed safety gate around
// the CON-N-04 Memory.memory.Copy mapped-memory write; no SWG / EA-IFF-85 layout
// is involved.

using System;

namespace UtinniCoreDotNet.Editing
{
    /// <summary>
    /// Outcome of <see cref="LivePatchValidator.Validate"/> — the four mutually-exclusive
    /// dispositions for an in-memory live-patch attempt (Phase 8 Plan 06; D-05.3).
    /// </summary>
    public enum LivePatchValidation
    {
        /// <summary>
        /// All preconditions met (live client, non-zero target, length matches the
        /// originally mapped region). The caller may queue the
        /// <c>Memory.memory.Copy</c> write on the game thread.
        /// </summary>
        Ok,

        /// <summary>
        /// <c>gameIsRunning == false</c>. There is no live client to patch into; the
        /// caller MUST NOT touch <c>Memory.memory.Copy</c> or any other binding that
        /// dereferences <c>UtinniCore.dll</c> game-state — the most-blocking failure
        /// mode and the first check performed by <see cref="LivePatchValidator.Validate"/>.
        /// </summary>
        RefusedNoClient,

        /// <summary>
        /// <c>targetAddr == IntPtr.Zero</c>. The mapped-region descriptor is invalid
        /// (a freshly-default-initialized <c>OpenSource.ClientMemory</c>, a probe that
        /// returned zero, etc.). Writing to address zero would AV the client; refused
        /// before length is considered.
        /// </summary>
        RefusedZeroTarget,

        /// <summary>
        /// <c>rewrittenLength != originalMappedLength</c>. The V1 same-length-only gate
        /// (08-REVIEWS HIGH-3) refuses BOTH growth (would write off the end of the
        /// mapped region) AND shrink (would leave stale tail bytes that the SWG reader
        /// would still interpret as part of the document). The candid UI copy is
        /// "Live patch requires the rewritten IFF to be the same length as the original.
        /// Save to file/repack instead."
        /// </summary>
        RefusedSameLength,
    }

    /// <summary>
    /// Pure-function bounds gate for the in-memory live-patch save mode
    /// (Phase 8 Plan 06; D-05.3; round-2 HIGH-B fold).
    ///
    /// <para><b>Framework-side placement (checker B-1 reasoning, generalized from
    /// 08-04 IffEditController and 08-05 LooseOverridePath / ReloadAssetClassifier /
    /// TreRecordIndexResolver):</b> this validator is pure-managed and BCL-only — no
    /// WinForms reference, no native memory-binding reference, no game-thread-marshal
    /// reference, no reference to any UtinniPlugins type. Same disposition as IffEditController:
    /// pure-managed → eligible for framework placement so it is testable on the CI
    /// runner without a live SWG client. 08-REVIEWS MEDIUM-5 / checker B-1.</para>
    ///
    /// <para><b>CONTEXT D-05.3 alignment (round-2 HIGH-B):</b> CONTEXT D-05.3
    /// explicitly claims the live-patch infra is "implementation-complete and
    /// unit-tested for its bounds gate." Extracting the gate into this pure function
    /// (consumed by the plugin-side live-patch save target via the UtinniCoreDotNet.dll
    /// reference) is the half that makes that claim factually accurate;
    /// <c>UtinniCoreDotNet.Tests/EditingTests/LivePatchValidatorTests.cs</c> is the
    /// other half (5 [Fact]s covering the full gate).</para>
    ///
    /// <para><b>Determinism — fixed check order:</b> the validator checks failure
    /// modes in the fixed order <c>NoClient → ZeroTarget → SameLength</c>. So an
    /// input that fails on multiple checks reports the MOST-BLOCKING failure first
    /// (a no-client + zero-target + wrong-length input is unambiguously
    /// <see cref="LivePatchValidation.RefusedNoClient"/>). The caller learns the
    /// failure mode that would block any retry first, and the test suite asserts
    /// each disposition individually rather than relying on combination semantics.</para>
    ///
    /// <para><b>No dereferences:</b> the validator never dereferences the
    /// <paramref name="targetAddr"/> nor reads any byte array. Its inputs are four
    /// scalars — the LivePatchSaveTarget caller has the source bytes and the
    /// IntPtr; this function is the gate, not the writer. SAME-LENGTH-ONLY
    /// (08-REVIEWS HIGH-3) is enforced as a single
    /// <c>rewrittenLength != originalMappedLength</c> equality check.</para>
    /// </summary>
    public static class LivePatchValidator
    {
        /// <summary>
        /// Validates the four bounds-gate preconditions for an in-memory live patch:
        /// game-running, non-zero target address, same-length rewrite. Returns the
        /// most-blocking failure first per the fixed check order, or
        /// <see cref="LivePatchValidation.Ok"/> when every precondition is met.
        ///
        /// <para>Pure function (no I/O, no side effects). Safe to call from any
        /// thread; the caller does the marshaling for the eventual write.</para>
        /// </summary>
        /// <param name="targetAddr">Base address of the mapped IFF bytes inside the
        /// live client process. <see cref="IntPtr.Zero"/> is the only zero-target
        /// representation checked (<see cref="IntPtr.Zero"/> equals
        /// <c>new IntPtr(0)</c>, so no separate <c>(long)targetAddr == 0</c> check is
        /// needed).</param>
        /// <param name="originalMappedLength">Length, in bytes, of the originally
        /// mapped region as captured at open time (the
        /// <c>OpenSource.ClientMemory.OriginalMappedLength</c> field).</param>
        /// <param name="rewrittenLength">Length, in bytes, of the freshly-serialized
        /// IFF payload to patch in. Negative inputs are not specially handled — they
        /// fail the same-length equality check the same way any other mismatch does
        /// (callers should pass <c>rewritten?.Length ?? 0</c> to handle a null
        /// rewritten defensively).</param>
        /// <param name="gameIsRunning">The result of <c>Game.IsRunning</c> captured by
        /// the caller. Passed in (rather than read here) so the validator stays
        /// pure-managed and BCL-only — no <c>UtinniCore.dll</c> binding reference.</param>
        public static LivePatchValidation Validate(
            IntPtr targetAddr,
            int originalMappedLength,
            int rewrittenLength,
            bool gameIsRunning)
        {
            // (1) Live-client gate — first because it's the most-blocking failure
            //     (no retry is possible without a running client; no other check
            //     matters until this is true).
            if (!gameIsRunning)
            {
                return LivePatchValidation.RefusedNoClient;
            }

            // (2) Zero-target gate — second because the address is what the write
            //     would dereference. IntPtr.Zero is the only zero representation
            //     checked: IntPtr.Zero == new IntPtr(0), so a separate
            //     (long)targetAddr == 0 check would be redundant.
            if (targetAddr == IntPtr.Zero)
            {
                return LivePatchValidation.RefusedZeroTarget;
            }

            // (3) Same-length-only gate (08-REVIEWS HIGH-3) — refuses BOTH growth
            //     (would write off the end of the mapped region) AND shrink (would
            //     leave stale tail bytes the SWG reader would still interpret as
            //     part of the document — the corruption vector the same-length lock
            //     closes).
            if (rewrittenLength != originalMappedLength)
            {
                return LivePatchValidation.RefusedSameLength;
            }

            return LivePatchValidation.Ok;
        }
    }
}
