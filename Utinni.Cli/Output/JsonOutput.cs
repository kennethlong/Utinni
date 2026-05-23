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

using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Utinni.Cli.Output
{
    public static class JsonOutput
    {
        // Explicit TypeNameHandling.None per T-04-Json mitigation — prevents
        // type-discriminator injection via serializer configuration.
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new SortedKeyContractResolver(),
            TypeNameHandling = TypeNameHandling.None
        };

        /// <summary>
        /// Emits a success envelope to the writer (defaulting to Console.Out so
        /// Console.SetOut-based test capture works — REVIEWS HIGH-3).
        /// Envelope shape: { "command": cmd, "result": result, "schemaVersion": 1 }
        /// with schemaVersion and command at the ROOT per REVIEWS HIGH-6.
        /// Returns exit code 0.
        /// </summary>
        public static int EmitSuccess(string command, object result, TextWriter writer = null)
        {
            var envelope = new JObject
            {
                ["command"] = command,
                ["result"] = JToken.FromObject(result, JsonSerializer.Create(Settings)),
                ["schemaVersion"] = 1
            };

            WriteEnvelope(envelope, writer);
            return 0;
        }

        /// <summary>
        /// Emits an error envelope to the writer (defaulting to Console.Out so
        /// Console.SetOut-based test capture works — REVIEWS HIGH-3).
        /// Envelope shape: { "command": cmd, "error": { "kind": kind, "message": msg }, "schemaVersion": 1 }
        /// with schemaVersion and command at the ROOT per REVIEWS HIGH-6.
        /// Returns exitCode verbatim.
        /// </summary>
        public static int EmitError(string command, string kind, string message, int exitCode, TextWriter writer = null)
        {
            var envelope = new JObject
            {
                ["command"] = command,
                ["error"] = new JObject
                {
                    ["kind"] = kind,
                    ["message"] = message
                },
                ["schemaVersion"] = 1
            };

            WriteEnvelope(envelope, writer);
            return exitCode;
        }

        private static void WriteEnvelope(JObject envelope, TextWriter writer)
        {
            var sorted = SortJObjectKeys(envelope);
            var serialized = sorted.ToString(Formatting.Indented);

            // Normalise line endings to LF (Formatting.Indented uses Environment.NewLine).
            serialized = serialized.Replace("\r\n", "\n").Replace("\r", "\n");

            // Write through Console.Out (which honours Console.SetOut) — REVIEWS HIGH-3.
            var target = writer ?? System.Console.Out;
            target.Write(serialized);
            target.Write("\n");
        }

        private static JObject SortJObjectKeys(JObject obj)
        {
            var sorted = new JObject();
            foreach (var property in obj.Properties().OrderBy(p => p.Name, System.StringComparer.Ordinal))
            {
                if (property.Value is JObject childObj)
                {
                    sorted[property.Name] = SortJObjectKeys(childObj);
                }
                else if (property.Value is JArray childArr)
                {
                    sorted[property.Name] = SortJArrayChildren(childArr);
                }
                else
                {
                    sorted[property.Name] = property.Value;
                }
            }
            return sorted;
        }

        private static JArray SortJArrayChildren(JArray arr)
        {
            var result = new JArray();
            foreach (var item in arr)
            {
                if (item is JObject itemObj)
                {
                    result.Add(SortJObjectKeys(itemObj));
                }
                else if (item is JArray itemArr)
                {
                    result.Add(SortJArrayChildren(itemArr));
                }
                else
                {
                    result.Add(item);
                }
            }
            return result;
        }
    }
}
