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
using System.Reflection;
using Xunit;
using UtinniCoreDotNet.Callbacks;

namespace UtinniCoreDotNet.Tests
{
    // C-04 regression: GroundSceneCallbacks.DequeuePostDrawLoopCalls previously drained
    // preDrawLoopCallQueue (typo in the original code). The fix introduces a shared
    // Drain(ConcurrentQueue<Action>) helper so the queue-vs-method correspondence is
    // explicit and this class of bug cannot recur (CON-O-02 default-fallback per D-12).
    //
    // The dequeue methods are private static, but the Drain helper is internal (via
    // [InternalsVisibleTo("UtinniCoreDotNet.Tests")] on UtinniCoreDotNet's
    // AssemblyInfo.cs). Tests reach Drain directly + reach the private dequeue methods
    // via reflection (single-line BindingFlags lookup; no helper extraction warranted).
    [Collection("StaticCallbackState")]
    public class GroundSceneCallbacksTests
    {
        private static ConcurrentQueue<Action> GetQueue(string fieldName)
        {
            var f = typeof(GroundSceneCallbacks).GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(f);
            return (ConcurrentQueue<Action>)f.GetValue(null);
        }

        private static void InvokePrivate(string methodName, params object[] args)
        {
            var m = typeof(GroundSceneCallbacks).GetMethod(
                methodName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(m);
            m.Invoke(null, args);
        }

        private static void ClearAllQueues()
        {
            GroundSceneCallbacks.Drain(GetQueue("updateLoopCallQueue"));
            GroundSceneCallbacks.Drain(GetQueue("preDrawLoopCallQueue"));
            GroundSceneCallbacks.Drain(GetQueue("postDrawLoopCallQueue"));
        }

        [Fact]
        public void DequeuePostDrawLoopCalls_DrainsPostQueue_NotPreQueue()
        {
            ClearAllQueues();
            int preCount = 0;
            int postCount = 0;
            GroundSceneCallbacks.AddPreDrawLoopCall(() => preCount++);
            GroundSceneCallbacks.AddPreDrawLoopCall(() => preCount++);
            GroundSceneCallbacks.AddPostDrawLoopCall(() => postCount++);
            GroundSceneCallbacks.AddPostDrawLoopCall(() => postCount++);
            GroundSceneCallbacks.AddPostDrawLoopCall(() => postCount++);

            InvokePrivate("DequeuePostDrawLoopCalls", IntPtr.Zero);

            Assert.Equal(3, postCount);
            Assert.Equal(0, preCount);
            Assert.Equal(2, GetQueue("preDrawLoopCallQueue").Count);
            Assert.Equal(0, GetQueue("postDrawLoopCallQueue").Count);

            ClearAllQueues();
        }

        [Fact]
        public void Drain_EmptyQueue_DoesNothing()
        {
            var q = new ConcurrentQueue<Action>();
            var ex = Record.Exception(() => GroundSceneCallbacks.Drain(q));
            Assert.Null(ex);
            Assert.Equal(0, q.Count);
        }

        [Fact]
        public void DequeueUpdateLoopCalls_DrainsUpdateQueue_NotPreOrPostQueue()
        {
            ClearAllQueues();
            int updateCount = 0;
            int preCount = 0;
            int postCount = 0;
            GroundSceneCallbacks.AddUpdateLoopCall(() => updateCount++);
            GroundSceneCallbacks.AddUpdateLoopCall(() => updateCount++);
            GroundSceneCallbacks.AddPreDrawLoopCall(() => preCount++);
            GroundSceneCallbacks.AddPostDrawLoopCall(() => postCount++);

            InvokePrivate("DequeueUpdateLoopCalls", IntPtr.Zero, 0.0f);

            Assert.Equal(2, updateCount);
            Assert.Equal(0, preCount);
            Assert.Equal(0, postCount);
            Assert.Equal(1, GetQueue("preDrawLoopCallQueue").Count);
            Assert.Equal(1, GetQueue("postDrawLoopCallQueue").Count);

            ClearAllQueues();
        }
    }
}
