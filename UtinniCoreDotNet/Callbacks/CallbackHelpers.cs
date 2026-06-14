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
using System.Collections.Concurrent;

namespace UtinniCoreDotNet.Callbacks
{
    // Phase 3 R-A / IN-05 consolidation (per 03-CONTEXT D-11) — single shared body
    // for managed callback queue drain. Previously duplicated in
    // GameCallbacks / GroundSceneCallbacks / ObjectCallbacks; consolidated here so
    // the queue-vs-method correspondence is enforced in exactly one place and the
    // C-04 class of bug (typo'd queue drain) cannot recur. Closes the IN-05
    // carry-over from Phase 02.1.
    //
    // SNAPSHOT-BOUND drain (Phase 16 fix): each call processes only the items
    // PRESENT AT ENTRY — captured once via the Count snapshot — and stops, even if
    // a drained callback enqueues more work. Items enqueued DURING the drain (e.g. a
    // per-frame callback that re-enqueues ITSELF for the next tick) are left for the
    // NEXT drain rather than run again this frame.
    //
    // Why this matters: an earlier unbounded `while (TryDequeue) func()` looped until
    // the queue was empty, so a self-re-enqueuing callback (the live-bridge game-state
    // refresh, main.cs) made the queue never empty -> infinite loop on the game thread
    // -> client froze on load. Every other caller enqueues one-shots, so bounding the
    // drain to a single frame's snapshot is behavior-preserving for them and makes the
    // documented "re-enqueues itself each tick" pattern actually safe.
    //
    // Count on a ConcurrentQueue is an O(1)-ish snapshot (no allocation, hot-path safe);
    // under concurrent producers it is merely an upper bound on this frame's work, which
    // is exactly the intended semantics — late arrivals wait for the next drain.
    //
    // Visibility note: Drain is `internal` (mirrors the prior per-class shape) so
    // [InternalsVisibleTo("UtinniCoreDotNet.Tests")] in AssemblyInfo.cs makes it
    // reachable for the regression tests in UtinniCoreDotNet.Tests.
    public static class CallbackHelpers
    {
        internal static void Drain(ConcurrentQueue<Action> queue)
        {
            // Snapshot the count at entry; drain at most that many. Re-enqueues during
            // the drain do NOT extend this pass (prevents the self-re-enqueue hang).
            for (int remaining = queue.Count; remaining > 0 && queue.TryDequeue(out var func); remaining--)
            {
                func();
            }
        }
    }
}
