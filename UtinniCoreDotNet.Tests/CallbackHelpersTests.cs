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
using System.Collections.Generic;
using System.IO;
using Xunit;
using UtinniCoreDotNet.Callbacks;

namespace UtinniCoreDotNet.Tests
{
    // Phase 3 R-A / IN-05 regression tests (per 03-CONTEXT D-11): the shared
    // Drain helper consolidates three previously-duplicated bodies that lived in
    // GameCallbacks / GroundSceneCallbacks / ObjectCallbacks. The tests below
    // verify FIFO drain semantics + the negative grep gate that asserts no
    // `while ... TryDequeue` body remains inline in any *Callbacks.cs file
    // outside CallbackHelpers.cs (so reintroducing the duplication trips CI).
    //
    // [Collection("StaticCallbackState")] keeps the suite serialized against
    // other callback-touching tests that share static registry state.
    [Collection("StaticCallbackState")]
    public class CallbackHelpersTests
    {
        [Fact]
        public void Drain_EmptyQueue_DoesNothing()
        {
            var q = new ConcurrentQueue<Action>();
            var ex = Record.Exception(() => CallbackHelpers.Drain(q));
            Assert.Null(ex);
            Assert.Equal(0, q.Count);
        }

        [Fact]
        public void Drain_MultipleEnqueued_InvokesAllInFifoOrder()
        {
            var q = new ConcurrentQueue<Action>();
            var seen = new List<int>();
            q.Enqueue(() => seen.Add(1));
            q.Enqueue(() => seen.Add(2));
            q.Enqueue(() => seen.Add(3));

            CallbackHelpers.Drain(q);

            Assert.Equal(new[] { 1, 2, 3 }, seen);
            Assert.Equal(0, q.Count);
        }

        [Fact]
        public void Drain_ProducerEnqueuesDuringDrain_StopsAtSnapshot()
        {
            // Snapshot-bound drain (Phase 16): each Drain processes ONLY the items
            // present at entry. A callback that enqueues more work mid-drain does NOT
            // have that work run in the same drain — the mid-drain enqueue waits for
            // the NEXT drain. (This is the property that makes a self-re-enqueuing
            // per-frame callback safe; see Drain_SelfReEnqueuingCallback below.)
            var q = new ConcurrentQueue<Action>();
            var seen = new List<int>();
            q.Enqueue(() =>
            {
                seen.Add(1);
                q.Enqueue(() => seen.Add(2)); // enqueued DURING the drain
            });

            CallbackHelpers.Drain(q);

            // First drain ran only the one snapshotted item; the deferred one waits.
            Assert.Equal(new[] { 1 }, seen);
            Assert.Equal(1, q.Count);

            // The next drain processes the deferred item.
            CallbackHelpers.Drain(q);
            Assert.Equal(new[] { 1, 2 }, seen);
            Assert.Equal(0, q.Count);
        }

        [Fact]
        public void Drain_SelfReEnqueuingCallback_TerminatesAndRunsOncePerDrain()
        {
            // Regression (Phase 16 — the live-bridge game-thread freeze): a callback
            // that re-enqueues ITSELF (main.cs game-state refresh: each tick refreshes
            // the cache then re-enqueues for the next tick) MUST run exactly once per
            // drain and the drain MUST terminate. Under the prior unbounded
            // `while (TryDequeue) func()` this never emptied the queue → infinite loop
            // on the game thread → client froze on load. The Task-with-timeout guard
            // makes a regression FAIL (after 5s) instead of hanging the test run.
            var q = new ConcurrentQueue<Action>();
            int runs = 0;
            Action refresh = null;
            refresh = () =>
            {
                runs++;
                q.Enqueue(refresh); // self-re-enqueue for the next tick
            };
            q.Enqueue(refresh);

            var first = System.Threading.Tasks.Task.Run(() => CallbackHelpers.Drain(q));
            Assert.True(first.Wait(TimeSpan.FromSeconds(5)),
                "Drain did not terminate within 5s — a self-re-enqueuing callback re-introduced an unbounded drain.");
            Assert.Equal(1, runs);     // ran exactly once this drain
            Assert.Equal(1, q.Count);  // re-enqueued itself for the next tick

            // Subsequent drains keep the once-per-drain cadence (termination proven above).
            CallbackHelpers.Drain(q);
            Assert.Equal(2, runs);
            Assert.Equal(1, q.Count);

            CallbackHelpers.Drain(q);
            Assert.Equal(3, runs);
            Assert.Equal(1, q.Count);
        }

        // Negative grep gate: assert the inline `while ... TryDequeue` body
        // exists in CallbackHelpers.cs and in NO OTHER file under Callbacks/.
        // If a future change reintroduces a per-class duplicate, this test
        // goes red. Source-file resolution mirrors UtinniCfgTests.FindCfg.
        [Fact]
        public void Drain_NoDuplicateBodies_RemainInPerClassFiles()
        {
            var callbacksDir = FindCallbacksDir();
            var perClassFiles = new[]
            {
                Path.Combine(callbacksDir, "GameCallbacks.cs"),
                Path.Combine(callbacksDir, "GroundSceneCallbacks.cs"),
                Path.Combine(callbacksDir, "ObjectCallbacks.cs"),
            };

            foreach (var f in perClassFiles)
            {
                Assert.True(File.Exists(f), "Expected file: " + f);
                var content = File.ReadAllText(f);
                // Loop-form-agnostic: no drain body (any loop calling TryDequeue(out ...))
                // may live in a per-class file — it belongs only in CallbackHelpers.cs.
                Assert.DoesNotMatch(@"\w+\.TryDequeue\(\s*out", content);
            }

            // Positive: the drain body MUST live in CallbackHelpers.cs.
            var helpersFile = Path.Combine(callbacksDir, "CallbackHelpers.cs");
            Assert.True(File.Exists(helpersFile), "Expected file: " + helpersFile);
            var helpersContent = File.ReadAllText(helpersFile);
            Assert.Matches(@"\w+\.TryDequeue\(\s*out", helpersContent);
        }

        private static string FindCallbacksDir()
        {
            // 4-level walk-up mirrors UtinniCfgTests.FindCfg precedent.
            var baseDir = AppContext.BaseDirectory;
            var candidate = Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "..", "UtinniCoreDotNet", "Callbacks"));
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            candidate = Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "..", "..", "UtinniCoreDotNet", "Callbacks"));
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            throw new DirectoryNotFoundException(
                "Could not locate UtinniCoreDotNet/Callbacks from " + baseDir);
        }
    }
}
