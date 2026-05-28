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
using UtinniCoreDotNet.Editing;
using Xunit;

namespace UtinniCoreDotNet.Tests.EditingTests
{
    /// <summary>
    /// Bounds-gate regression suite for the pure-function live-patch validator
    /// (Phase 8 Plan 06 Task 2; round-2 HIGH-B). Covers each refusal class as a
    /// separate [Fact] plus a happy-path Ok success.
    ///
    /// <para><b>CONTEXT D-05.3 claim alignment (round-2 HIGH-B):</b> CONTEXT D-05.3
    /// states the live-patch infra is "implementation-complete and unit-tested for
    /// its bounds gate." This file is the unit-test half of that claim. The validator
    /// is a pure function so the entire bounds-gate contract is exercised in CI
    /// without an injected SWG client.</para>
    ///
    /// <para><b>Refusal-check ordering (deterministic):</b> the validator checks
    /// gameIsRunning FIRST, then targetAddr, then length. So a no-client +
    /// zero-target + wrong-length input is unambiguously RefusedNoClient (the
    /// caller learns the most-blocking failure mode first).</para>
    ///
    /// <para>Consumes <see cref="LivePatchValidator"/> via the existing ProjectReference
    /// to UtinniCoreDotNet.csproj — NO linked-source <c>&lt;Compile Include&gt;</c> entry
    /// (checker B-1). The SDK-style test csproj's default <c>**/*.cs</c> glob auto-includes
    /// this file (it lives under <c>EditingTests/</c>, outside the <c>Fixtures\**</c>
    /// exclusion).</para>
    /// </summary>
    public class LivePatchValidatorTests
    {
        // Arbitrary non-zero IntPtr used for "targetAddr is valid" inputs. Doesn't matter
        // what the actual address is — the validator never dereferences it; it just checks
        // != IntPtr.Zero.
        private static readonly IntPtr NonZeroTargetAddr = new IntPtr(0x10000000);

        [Fact]
        public void Validate_NoClient_RefusedNoClient()
        {
            // gameIsRunning: false → RefusedNoClient regardless of other inputs.
            // Even with a valid target + matched length, no live client = no write.
            LivePatchValidation result = LivePatchValidator.Validate(
                targetAddr: NonZeroTargetAddr,
                originalMappedLength: 1024,
                rewrittenLength: 1024,
                gameIsRunning: false);

            Assert.Equal(LivePatchValidation.RefusedNoClient, result);
        }

        [Fact]
        public void Validate_ZeroTarget_RefusedZeroTarget()
        {
            // gameIsRunning: true, targetAddr: IntPtr.Zero → RefusedZeroTarget.
            // Length doesn't matter; a zero target address never resolves to a valid
            // mapped region, so the write is refused before length is even considered.
            LivePatchValidation result = LivePatchValidator.Validate(
                targetAddr: IntPtr.Zero,
                originalMappedLength: 1024,
                rewrittenLength: 1024,
                gameIsRunning: true);

            Assert.Equal(LivePatchValidation.RefusedZeroTarget, result);
        }

        [Fact]
        public void Validate_GrowthLength_RefusedSameLength()
        {
            // gameIsRunning: true, targetAddr: non-zero, rewrittenLength > originalMappedLength
            // → RefusedSameLength. SAME-LENGTH-ONLY V1 gate (08-REVIEWS HIGH-3) refuses
            // BOTH growth AND shrink — growth would write off the end of the mapped region.
            const int original = 1024;
            const int rewritten = original + 1; // grew by 1 byte
            LivePatchValidation result = LivePatchValidator.Validate(
                targetAddr: NonZeroTargetAddr,
                originalMappedLength: original,
                rewrittenLength: rewritten,
                gameIsRunning: true);

            Assert.Equal(LivePatchValidation.RefusedSameLength, result);
        }

        [Fact]
        public void Validate_ShrinkLength_RefusedSameLength()
        {
            // gameIsRunning: true, targetAddr: non-zero, rewrittenLength < originalMappedLength
            // → RefusedSameLength. SAME-LENGTH-ONLY V1 gate (08-REVIEWS HIGH-3) refuses
            // BOTH growth AND shrink — shrink would leave stale tail bytes that the SWG
            // reader would still interpret as part of the document (the stale-tail-bytes
            // corruption vector that the same-length gate closes).
            const int original = 1024;
            const int rewritten = original - 1; // shrunk by 1 byte
            LivePatchValidation result = LivePatchValidator.Validate(
                targetAddr: NonZeroTargetAddr,
                originalMappedLength: original,
                rewrittenLength: rewritten,
                gameIsRunning: true);

            Assert.Equal(LivePatchValidation.RefusedSameLength, result);
        }

        [Fact]
        public void Validate_SameLengthHappyPath_Ok()
        {
            // All preconditions met: gameIsRunning=true, targetAddr non-zero,
            // rewrittenLength == originalMappedLength → Ok.
            const int len = 1024;
            LivePatchValidation result = LivePatchValidator.Validate(
                targetAddr: NonZeroTargetAddr,
                originalMappedLength: len,
                rewrittenLength: len,
                gameIsRunning: true);

            Assert.Equal(LivePatchValidation.Ok, result);
        }
    }
}
