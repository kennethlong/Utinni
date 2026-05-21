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
    // The outer Count > 0 check from the original code was dropped because
    // TryDequeue already returns false on an empty queue, and the Count read was
    // racey under concurrent producers anyway.
    //
    // Visibility note: Drain is `internal` (mirrors the prior per-class shape) so
    // [InternalsVisibleTo("UtinniCoreDotNet.Tests")] in AssemblyInfo.cs makes it
    // reachable for the regression tests in UtinniCoreDotNet.Tests.
    public static class CallbackHelpers
    {
        internal static void Drain(ConcurrentQueue<Action> queue)
        {
            while (queue.TryDequeue(out var func))
            {
                func();
            }
        }
    }
}
