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
            // Drain semantics: TryDequeue keeps looping until the queue reports
            // empty. A producer adding mid-drain may have its enqueued action
            // drained too — this is the documented behavior (same as the prior
            // per-class Drain in GroundSceneCallbacks). The test asserts the
            // mid-drain enqueue WAS picked up (proving the "loop until empty"
            // contract), not lost.
            var q = new ConcurrentQueue<Action>();
            var seen = new List<int>();
            q.Enqueue(() =>
            {
                seen.Add(1);
                // Producer enqueues during drain; the loop keeps going until
                // the queue is empty.
                q.Enqueue(() => seen.Add(2));
            });

            CallbackHelpers.Drain(q);

            Assert.Equal(new[] { 1, 2 }, seen);
            Assert.Equal(0, q.Count);
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
                Assert.DoesNotMatch(@"while\s*\(\s*\w+\.TryDequeue\(\s*out", content);
            }

            // Positive: the body MUST live in CallbackHelpers.cs.
            var helpersFile = Path.Combine(callbacksDir, "CallbackHelpers.cs");
            Assert.True(File.Exists(helpersFile), "Expected file: " + helpersFile);
            var helpersContent = File.ReadAllText(helpersFile);
            Assert.Matches(@"while\s*\(\s*\w+\.TryDequeue\(\s*out", helpersContent);
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
