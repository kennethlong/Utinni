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

#pragma once

// WR-03 (03-REVIEW): test-only accessors for Game::installCallbacks.
//
// These are NOT part of the public plugin surface. They exist solely to give
// xUnit P/Invoke harnesses (NativeCallbacksHandleTests, GameCallbacksTests)
// and the bridge exports in test_exports.cpp a way to drive the install-
// callback registry without going through the live hkInstall hook (which
// would require a running SWG process).
//
// Discipline: this header is included ONLY by test_exports.cpp. The public
// Game class in swg/game/game.h declares no test-only members; CppSharp
// won't surface these into the managed bindings either (the header lives
// outside the auto-discovered set the generator scans).
//
// Mirrors the test_internal::TestImpl pattern used by plugin_manager.cpp
// for R-B PluginManager test seams (see plugin_manager.cpp ~430).

namespace utinni
{
namespace test_internal
{
// Fires every registered installCallback via the R-H snapshot
// dispatch path. Wraps the implementation in game.cpp; the registry
// itself remains file-scope static so the symbol does not leak.
void triggerInstallCallbacks();

// Returns the current size of the installCallbacks registry. Used
// by utinni_test_installSubscriberCount to assert handle counts in
// NativeCallbacksHandleTests.
int getInstallSubscriberCount();
} // namespace test_internal
} // namespace utinni
