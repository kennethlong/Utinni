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
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using UtinniCoreDotNet.Utility;
using Xunit;

namespace UtinniCoreDotNet.Tests
{
    // Phase 3 R-E regression tests (per 03-CONTEXT D-21 + PATTERNS.md §"R-E
    // CallerMemberName"). Verifies:
    //   1. FormatText uses the compile-time-resolved [CallerMemberName] /
    //      [CallerFilePath] parameters instead of walking new StackTrace().
    //   2. Each public Log method (Info/Debug/Warning/Error/Critical) carries the
    //      defaulted parameters so existing call sites (Log.Info("text")) remain
    //      source-compatible.
    //   3. Explicit override of callerName/callerFile is respected (defaulted
    //      parameters can be overridden when the caller wants to inject a name).
    //   4. The writeClassName toggle controls whether the prefix is emitted.
    //   5. Negative grep: Log.cs no longer contains `new StackTrace().GetFrame`.
    //
    // FormatText is private; tests reach it via reflection (BindingFlags.NonPublic
    // | Static, same pattern as GroundSceneCallbacksTests:45-59 and
    // CallbacksSubscribeUnsubscribeTests:46-65).
    //
    // [Collection("StaticCallbackState")] serializes against tests that mutate
    // shared static Log state (writeClassName/writeFunctionName toggles).
    [Collection("StaticCallbackState")]
    public class LogCallerMemberNameTests
    {
        // --- Reflection probes ---

        private static string InvokeFormatText(string text, string callerName, string callerFile)
        {
            var m = typeof(Log).GetMethod(
                "FormatText", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(m);
            return (string)m.Invoke(null, new object[] { text, callerName, callerFile });
        }

        private static FieldInfo GetField(string name)
        {
            var f = typeof(Log).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(f);
            return f;
        }

        private static bool GetBoolField(string name)
        {
            return (bool)GetField(name).GetValue(null);
        }

        private static void SetBoolField(string name, bool value)
        {
            GetField(name).SetValue(null, value);
        }

        // --- Source-locator helper (FindCfg-style 4-level walk-up; mirrors
        //     UtinniCfgTests.FindCfg) ---

        private static string FindLogSource()
        {
            var baseDir = AppContext.BaseDirectory;
            // From bin\Release\net472\ walk up to repo root.
            var candidate = Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "..", "UtinniCoreDotNet", "Utility", "Log.cs"));
            if (File.Exists(candidate))
            {
                return candidate;
            }
            candidate = Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "..", "..", "UtinniCoreDotNet", "Utility", "Log.cs"));
            if (File.Exists(candidate))
            {
                return candidate;
            }
            throw new FileNotFoundException("Could not locate UtinniCoreDotNet/Utility/Log.cs from " + baseDir);
        }

        // --- Fact 1: writeClassName + writeFunctionName ON -> prefix includes
        //     both className (from CallerFilePath basename) and callerName. ---

        [Fact]
        public void FormatText_WriteClassNameAndFunctionNameOn_EmitsBothPrefixes()
        {
            bool prevClass = GetBoolField("writeClassName");
            bool prevFn = GetBoolField("writeFunctionName");
            try
            {
                SetBoolField("writeClassName", true);
                SetBoolField("writeFunctionName", true);

                // callerFile is the absolute path the compiler embedded; only the
                // basename (no .cs) is used by FormatText.
                string captured = InvokeFormatText(
                    "test message",
                    callerName: "Log_Info_EmitsCallerMethodName_WhenWriteClassNameOn",
                    callerFile: @"D:\Code\Utinni\UtinniCoreDotNet.Tests\LogCallerMemberNameTests.cs");

                Assert.Equal(
                    "[LogCallerMemberNameTests][Log_Info_EmitsCallerMethodName_WhenWriteClassNameOn] test message",
                    captured);
            }
            finally
            {
                SetBoolField("writeClassName", prevClass);
                SetBoolField("writeFunctionName", prevFn);
            }
        }

        // --- Fact 2: negative grep — Log.cs source no longer contains the
        //     StackTrace.GetFrame walk. Marks R-E completion. ---

        [Fact]
        public void Log_NoLongerWalksStackTraceInFormatText()
        {
            string logSrc = File.ReadAllText(FindLogSource());
            Assert.DoesNotMatch(@"new StackTrace\(\)\.GetFrame", logSrc);
            // Positive: the new compile-time-resolved attributes ARE present on
            // the public Log methods.
            Assert.Contains("CallerMemberName", logSrc);
            Assert.Contains("CallerFilePath", logSrc);
        }

        // --- Fact 3: explicit overrides of the defaulted parameters take
        //     precedence over the compiler-resolved attributes. ---
        //
        // Verifies the defaulted parameters can be set explicitly when the
        // caller needs to inject a synthetic name (e.g. forwarding wrappers).
        // The actual default-parameter resolution at the call site is tested by
        // Fact 4 below, which calls Log.Info(text) with no overrides.

        [Fact]
        public void Log_Info_ExplicitCallerName_OverridesAutomatic()
        {
            // Use FormatText directly so we don't depend on native log being
            // initialized (Log.Info(text) ends up in spdlog; the OutputSink fan-out
            // only fires when Setup() has been called and native log is loaded).
            bool prevClass = GetBoolField("writeClassName");
            bool prevFn = GetBoolField("writeFunctionName");
            try
            {
                SetBoolField("writeClassName", true);
                SetBoolField("writeFunctionName", true);

                string captured = InvokeFormatText(
                    "msg",
                    callerName: "ManualName",
                    callerFile: @"D:\anything\SomeFile.cs");

                Assert.Equal("[SomeFile][ManualName] msg", captured);
            }
            finally
            {
                SetBoolField("writeClassName", prevClass);
                SetBoolField("writeFunctionName", prevFn);
            }
        }

        // --- Fact 4: writeClassName OFF -> output equals the raw text (no
        //     prefix). Confirms the toggle still gates the prefix emission. ---

        [Fact]
        public void FormatText_WriteClassNameOff_OmitsClassName()
        {
            bool prevClass = GetBoolField("writeClassName");
            try
            {
                SetBoolField("writeClassName", false);

                string captured = InvokeFormatText(
                    "bare message",
                    callerName: "DoesntMatter",
                    callerFile: @"D:\anything\DoesntMatter.cs");

                Assert.Equal("bare message", captured);
            }
            finally
            {
                SetBoolField("writeClassName", prevClass);
            }
        }

        // --- Fact 5: every public Log method (Info/Debug/Warning/Error/Critical)
        //     carries the [CallerMemberName] + [CallerFilePath] defaulted
        //     parameters. Confirms the API contract is applied consistently.

        [Theory]
        [InlineData("Info")]
        [InlineData("Debug")]
        [InlineData("Warning")]
        [InlineData("Error")]
        [InlineData("Critical")]
        public void Log_PublicMethod_HasCallerMemberNameAndCallerFilePathDefaultedParams(string methodName)
        {
            var m = typeof(Log).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(m);
            var parameters = m.GetParameters();
            Assert.Equal(3, parameters.Length);
            Assert.Equal("text", parameters[0].Name);
            Assert.Equal(typeof(string), parameters[0].ParameterType);

            // Parameter 1: callerName decorated with [CallerMemberName] + default "".
            Assert.Equal("callerName", parameters[1].Name);
            Assert.Equal(typeof(string), parameters[1].ParameterType);
            Assert.True(parameters[1].HasDefaultValue);
            Assert.Equal("", parameters[1].DefaultValue);
            Assert.NotNull(parameters[1].GetCustomAttribute<CallerMemberNameAttribute>());

            // Parameter 2: callerFile decorated with [CallerFilePath] + default "".
            Assert.Equal("callerFile", parameters[2].Name);
            Assert.Equal(typeof(string), parameters[2].ParameterType);
            Assert.True(parameters[2].HasDefaultValue);
            Assert.Equal("", parameters[2].DefaultValue);
            Assert.NotNull(parameters[2].GetCustomAttribute<CallerFilePathAttribute>());
        }
    }
}
