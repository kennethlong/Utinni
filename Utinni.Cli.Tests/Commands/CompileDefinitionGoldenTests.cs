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
using System.Linq;
using Newtonsoft.Json.Linq;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.Commands
{
    /// <summary>
    /// Plan 13-05 Task 1: compile-definition (AUTH-02, D-08) — emits the per-class param→type schema
    /// (ParamType/ListType vocabulary) from a minimal authored .tdf. The schema-emit is a deterministic
    /// managed parse (the native run is best-effort / a gate-finding, exercised separately), so these
    /// tests run with --skip-native.
    /// </summary>
    public sealed class CompileDefinitionGoldenTests : IDisposable
    {
        private readonly string _work;

        public CompileDefinitionGoldenTests()
        {
            _work = Path.Combine(Path.GetTempPath(), "compiledef_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_work);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_work)) Directory.Delete(_work, recursive: true); }
            catch { /* best-effort */ }
        }

        private JObject Run(out int exit, params string[] args)
        {
            var r = InProcessCliRunner.Run(args);
            exit = r.ExitCode;
            return JObject.Parse(r.Stdout);
        }

        [Fact]
        public void CompileDefinition_MissingTdf_ExitThree()
        {
            JObject env = Run(out int exit, "compile-definition", "--tdf", Path.Combine(_work, "nope.tdf"),
                "--out", Path.Combine(_work, "o.json"), "--skip-native");
            Assert.Equal(3, exit);
            Assert.Equal("FileNotFound", (string)((JObject)env["error"])["kind"]);
        }

        [Fact]
        public void CompileDefinition_MinimalTdf_EmitsTypedSchema_WithListParam()
        {
            string tdf = FixturePath.Resolve("tdf/mintest.tdf");
            string outA = Path.Combine(_work, "a.json");
            string outB = Path.Combine(_work, "b.json");

            JObject env = Run(out int exit, "compile-definition", "--tdf", tdf, "--out", outA, "--skip-native");
            Assert.Equal(0, exit);
            Assert.Equal(1, (int)((JObject)env["result"])["classCount"]);

            var schema = JObject.Parse(File.ReadAllText(outA));
            var paramsArr = (JArray)schema["classes"]["mintest"]["params"];
            Assert.Equal(5, paramsArr.Count);

            // The ParamType(14)/ListType(4) vocabulary, including at least one ListType != LIST_NONE.
            JObject slots = paramsArr.OfType<JObject>().First(p => (string)p["name"] == "slots");
            Assert.Equal("LIST_LIST", (string)slots["listType"]);
            Assert.Equal("TYPE_INTEGER", (string)slots["type"]);
            Assert.Contains(paramsArr.OfType<JObject>(), p => (string)p["type"] == "TYPE_FLOAT");
            Assert.Contains(paramsArr.OfType<JObject>(), p => (string)p["type"] == "TYPE_BOOL");

            // Deterministic / stable across runs (no __DATE__/abs-path leakage — parsed from the .tdf).
            Run(out int _, "compile-definition", "--tdf", tdf, "--out", outB, "--skip-native");
            Assert.Equal(File.ReadAllText(outA), File.ReadAllText(outB));
        }
    }
}
