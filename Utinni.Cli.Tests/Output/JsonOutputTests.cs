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
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Utinni.Cli.Output;
using Xunit;

namespace Utinni.Cli.Tests.Output
{
    public class JsonOutputTests
    {
        // Test 1 (REVIEWS HIGH-6 regression guard): envelope shape — schemaVersion + command at ROOT
        [Fact]
        public void EmitSuccess_Envelope_HasTopLevelSchemaVersionAndCommand()
        {
            var sw = new StringWriter();
            JsonOutput.EmitSuccess("test-command", new { foo = "bar" }, sw);
            var json = sw.ToString();

            var root = JObject.Parse(json);

            // Root keys: command, result, schemaVersion (sorted)
            Assert.Equal(1, root["schemaVersion"].Value<int>());
            Assert.Equal("test-command", root["command"].Value<string>());
            Assert.Equal("bar", root["result"]["foo"].Value<string>());

            // CRITICAL: schemaVersion and command must NOT be nested inside result
            Assert.Null(root["result"]["schemaVersion"]);
            Assert.Null(root["result"]["command"]);
        }

        // Test 2 (T-04-Json mitigation): TypeNameHandling must be None
        [Fact]
        public void JsonOutput_Settings_HasTypeNameHandlingNone()
        {
            // Access private Settings field via reflection
            var settingsField = typeof(JsonOutput).GetField(
                "Settings",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(settingsField);
            var settings = (JsonSerializerSettings)settingsField.GetValue(null);

            Assert.Equal(TypeNameHandling.None, settings.TypeNameHandling);
        }

        // Test 3a (REVIEWS HIGH-3): EmitSuccess default writer routes through Console.Out for SetOut capture
        [Fact]
        public void EmitSuccess_DefaultWriter_RoutesThroughConsoleOutForSetOutCapture()
        {
            var prevOut = Console.Out;
            var sw = new StringWriter();
            try
            {
                Console.SetOut(sw);
                JsonOutput.EmitSuccess("test", new { foo = 1 });
            }
            finally
            {
                Console.SetOut(prevOut);
            }

            var captured = sw.ToString();
            Assert.False(string.IsNullOrEmpty(captured), "EmitSuccess with default writer should write to Console.Out");
            // Must parse as JSON and contain the command name
            var root = JObject.Parse(captured);
            Assert.Equal("test", root["command"].Value<string>());
        }

        // Test 3b (REVIEWS HIGH-3): EmitError default writer routes through Console.Out for SetOut capture
        [Fact]
        public void EmitError_DefaultWriter_RoutesThroughConsoleOutForSetOutCapture()
        {
            var prevOut = Console.Out;
            var sw = new StringWriter();
            try
            {
                Console.SetOut(sw);
                JsonOutput.EmitError("test-error", "SomeKind", "some message", exitCode: 2);
            }
            finally
            {
                Console.SetOut(prevOut);
            }

            var captured = sw.ToString();
            Assert.False(string.IsNullOrEmpty(captured), "EmitError with default writer should write to Console.Out");
            var root = JObject.Parse(captured);
            Assert.Equal("test-error", root["command"].Value<string>());
        }

        // Test 4: sorted-key contract resolver — typed class path
        [Fact]
        public void EmitSuccess_WithUnsortedPocoProperties_SortsKeysAlphabetically()
        {
            var sw = new StringWriter();
            // Properties declared in non-alpha order: z before a
            JsonOutput.EmitSuccess("sort-test", new UnsortedPoco { z = 2, a = 1 }, sw);
            var json = sw.ToString();

            var root = JObject.Parse(json);
            var resultProps = root["result"] as JObject;
            Assert.NotNull(resultProps);

            var propNames = new System.Collections.Generic.List<string>();
            foreach (var prop in resultProps.Properties())
            {
                propNames.Add(prop.Name);
            }

            // "a" must come before "z" (Ordinal sort)
            var aIndex = propNames.IndexOf("a");
            var zIndex = propNames.IndexOf("z");
            Assert.True(aIndex < zIndex, $"Expected 'a' before 'z' in sorted output but got: {string.Join(", ", propNames)}");
        }

        // Test 5: recursive JObject sort — dynamic path (Pitfall 10)
        [Fact]
        public void EmitSuccess_WithDeeplyNestedUnsortedJObject_SortsAtEveryDepth()
        {
            // Build a JObject with keys inserted in non-alpha order at multiple depths
            var nested = new JObject
            {
                ["z_inner"] = "last",
                ["a_inner"] = "first"
            };
            var arrItem = new JObject
            {
                ["z_arr"] = 99,
                ["a_arr"] = 11
            };
            var arr = new JArray { arrItem };
            var dynamicResult = new JObject
            {
                ["z_root"] = nested,
                ["a_root"] = arr,
                ["m_root"] = "middle"
            };

            var sw = new StringWriter();
            JsonOutput.EmitSuccess("deep-sort", dynamicResult, sw);
            var json = sw.ToString();

            var root = JObject.Parse(json);
            var result = root["result"] as JObject;
            Assert.NotNull(result);

            // Verify root-level sort: a_root < m_root < z_root
            var rootKeys = new System.Collections.Generic.List<string>();
            foreach (var prop in result.Properties()) { rootKeys.Add(prop.Name); }
            Assert.True(rootKeys.IndexOf("a_root") < rootKeys.IndexOf("m_root"), "a_root must come before m_root");
            Assert.True(rootKeys.IndexOf("m_root") < rootKeys.IndexOf("z_root"), "m_root must come before z_root");

            // Verify nested object sort: a_inner < z_inner
            var nestedResult = result["z_root"] as JObject;
            Assert.NotNull(nestedResult);
            var nestedKeys = new System.Collections.Generic.List<string>();
            foreach (var prop in nestedResult.Properties()) { nestedKeys.Add(prop.Name); }
            Assert.True(nestedKeys.IndexOf("a_inner") < nestedKeys.IndexOf("z_inner"), "a_inner must come before z_inner");

            // Verify array item sort: a_arr < z_arr
            var arrResult = result["a_root"] as JArray;
            Assert.NotNull(arrResult);
            var arrItemResult = arrResult[0] as JObject;
            Assert.NotNull(arrItemResult);
            var arrKeys = new System.Collections.Generic.List<string>();
            foreach (var prop in arrItemResult.Properties()) { arrKeys.Add(prop.Name); }
            Assert.True(arrKeys.IndexOf("a_arr") < arrKeys.IndexOf("z_arr"), "a_arr must come before z_arr");
        }

        // Test 6: LF + UTF-8 no BOM encoding contract
        [Fact]
        public void EmitSuccess_OutputBytes_StartWithNonBomAndUseLfOnly()
        {
            // Part A: no CRLF sequences in string output
            var strWriter = new StringWriter();
            JsonOutput.EmitSuccess("enc-test", new { x = "y" }, strWriter);
            var str = strWriter.ToString();
            Assert.DoesNotContain("\r\n", str);

            // Part B: no UTF-8 BOM (first 3 bytes must NOT be EF BB BF)
            var ms = new MemoryStream();
            var noBomEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            using (var streamWriter = new StreamWriter(ms, noBomEncoding, 1024, leaveOpen: true))
            {
                JsonOutput.EmitSuccess("enc-test", new { x = "y" }, streamWriter);
                streamWriter.Flush();
            }
            ms.Position = 0;
            var bytes = ms.ToArray();
            Assert.True(bytes.Length >= 3, "Output should have at least 3 bytes");
            // BOM would be 0xEF, 0xBB, 0xBF
            bool hasBom = bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            Assert.False(hasBom, "Output must not start with UTF-8 BOM");
        }

        // Test 7: error envelope shape — top-level with mutual exclusion
        [Fact]
        public void EmitError_Envelope_HasTopLevelSchemaVersionAndErrorMutuallyExclusiveWithResult()
        {
            var sw = new StringWriter();
            int code = JsonOutput.EmitError("test-command", "BadMagic", "explanatory message", exitCode: 2, writer: sw);
            var json = sw.ToString();

            var root = JObject.Parse(json);

            Assert.Equal(1, root["schemaVersion"].Value<int>());
            Assert.Equal("test-command", root["command"].Value<string>());
            Assert.Equal("BadMagic", root["error"]["kind"].Value<string>());
            Assert.Equal("explanatory message", root["error"]["message"].Value<string>());
            Assert.Equal(2, code);

            // CRITICAL: no nested schemaVersion in error; result must be absent (mutually exclusive)
            Assert.Null(root["error"]["schemaVersion"]);
            Assert.Null(root["result"]);
        }

        // Test 8: exit-code passthrough
        [Fact]
        public void EmitSuccess_ReturnsZero_EmitError_ReturnsExitCodeArgument()
        {
            var devNull = new StringWriter();
            Assert.Equal(0, JsonOutput.EmitSuccess("cmd", new { }, devNull));
            Assert.Equal(1, JsonOutput.EmitError("cmd", "K", "M", exitCode: 1, writer: devNull));
            Assert.Equal(42, JsonOutput.EmitError("cmd", "K", "M", exitCode: 42, writer: devNull));
        }

        // Test 9 (REVIEWS HIGH-3 testability hatch): explicit TextWriter injection
        [Fact]
        public void EmitSuccess_WithExplicitTextWriter_RoutesToSuppliedWriterNotConsole()
        {
            var prevOut = Console.Out;
            var consoleCapture = new StringWriter();
            var explicitWriter = new StringWriter();
            try
            {
                Console.SetOut(consoleCapture);
                JsonOutput.EmitSuccess("injection-test", new { data = "value" }, explicitWriter);
            }
            finally
            {
                Console.SetOut(prevOut);
            }

            // The supplied writer must have received the output
            Assert.False(string.IsNullOrEmpty(explicitWriter.ToString()), "Explicit writer must receive output");

            // Console.Out (as captured via SetOut) must NOT have received any output
            Assert.True(string.IsNullOrEmpty(consoleCapture.ToString()), "Console.Out must NOT receive output when explicit writer is supplied");
        }

        // Helper POCO for Test 4 with properties in non-alphabetical declaration order
        private class UnsortedPoco
        {
            public int z { get; set; }
            public int a { get; set; }
        }
    }
}
