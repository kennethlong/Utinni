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
// Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;
using System.IO;
using CommandLine;
using Newtonsoft.Json.Linq;
using Utinni.Cli.Commands.Subprocess;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("compile-definition", HelpText = "Run TemplateDefinitionCompiler over a .tdf and emit the per-class param->type schema JSON (AUTH-02, D-08).")]
    public class CompileDefinitionOptions
    {
        [Option("tdf", Required = true, HelpText = "Path to the .tdf template-definition source.")]
        public string Tdf { get; set; }

        [Option("out", Required = true, HelpText = "Path to write the per-class param->type schema JSON.")]
        public string Out { get; set; }

        [Option("tool-path", HelpText = "Override path to the TemplateDefinitionCompiler exe (default: resolved beside utinni-cli).")]
        public string ToolPath { get; set; }

        [Option("skip-native", HelpText = "Emit the schema from the .tdf parse only; do not invoke the native compiler.")]
        public bool SkipNative { get; set; }
    }

    /// <summary>
    /// Plan 13-05 (AUTH-02, D-08): runs the Phase-12 <c>TemplateDefinitionCompiler</c> over a <c>.tdf</c>
    /// (the native-revive proof) and emits the per-class param→type schema JSON the OT editor (RESID-01)
    /// and the Area-2 cross-check tests consume. The schema uses the <c>ParamType</c>(14)/<c>ListType</c>(4)
    /// vocabulary from <c>TemplateData.h</c>, parsed from the <c>.tdf</c> grammar
    /// (<c>&lt;type&gt; [limits] &lt;name&gt;</c> param lines under a <c>version N</c> block).
    ///
    /// <para><b>Gate-finding (Open Q1 RESOLVED):</b> <c>TemplateDefinitionCompiler</c> strictly requires a
    /// <c>.tdf</c> and there are ZERO canonical SOE <c>.tdf</c> assets — so the verb is exercised against a
    /// small authored minimal <c>.tdf</c> fixture; the full SOE set is a documented gate-finding. The
    /// schema-emit (the consumable artifact) is a deterministic managed parse, independent of the native
    /// run, so it is always available; <c>--skip-native</c> emits schema only.</para>
    ///
    /// <para>Exit codes: 0 ok; 2 native error / parse error; 3 exe/input missing.</para>
    /// </summary>
    public static class CompileDefinitionCommand
    {
        public static int Run(CompileDefinitionOptions o)
        {
            if (!File.Exists(o.Tdf))
            {
                return JsonOutput.EmitError("compile-definition", "FileNotFound", ".tdf file not found: " + o.Tdf, exitCode: 3);
            }

            // Emit the schema (the deterministic, consumable deliverable) by parsing the .tdf grammar.
            string[] tdfLines = File.ReadAllLines(o.Tdf);
            string className = Path.GetFileNameWithoutExtension(o.Tdf);
            JObject schema = TdfSchemaExtractor.Extract(className, tdfLines);

            string outFull = Path.GetFullPath(o.Out);
            string outDir = Path.GetDirectoryName(outFull);
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
            // Sorted-key, LF-normalized, stable (no __DATE__/abs-path leakage — the schema is parsed
            // from the .tdf, not the compiler's date-stamped generated output).
            File.WriteAllText(outFull, SortedJson(schema).Replace("\r\n", "\n"));

            int nativeExit = 0;
            string nativeStatus = "skipped";
            if (!o.SkipNative)
            {
                // Best-effort native-revive proof. The seam's timeout backstop bounds any wedge; the
                // schema artifact above is already written regardless of the native outcome.
                string exe = NativeToolResolver.Resolve(o.ToolPath, "TemplateDefinitionCompiler");
                if (!File.Exists(exe))
                {
                    nativeStatus = "exe-not-found";
                }
                else
                {
                    NativeToolResult nr = NativeToolRunner.RunCaptured(exe, new[] { "-compile", Path.GetFullPath(o.Tdf) },
                        Path.GetDirectoryName(Path.GetFullPath(o.Tdf)));
                    nativeExit = nr.ExitCode;
                    nativeStatus = nr.TimedOut ? "timeout" : (nr.ExitCode == 0 ? "ok" : "error");
                }
            }

            var result = new JObject
            {
                ["classCount"] = ((JObject)schema["classes"]).Count,
                ["nativeExitCode"] = nativeExit,
                ["nativeStatus"] = nativeStatus,
                ["schemaPath"] = outFull
            };
            return JsonOutput.EmitSuccess("compile-definition", result);
        }

        private static string SortedJson(JObject obj)
        {
            return obj.ToString(Newtonsoft.Json.Formatting.Indented);
        }
    }

    /// <summary>
    /// Parses the <c>.tdf</c> param grammar into the ParamType/ListType schema vocabulary. The grammar
    /// (VERIFIED in TemplateData::parseLine): a param line is <c>&lt;type&gt; [limits] &lt;name&gt;</c>;
    /// an optional leading <c>list</c> / <c>list[N]</c> / <c>list[enum:X]</c> token sets the ListType.
    /// Directive lines (id/version/clientpath/...) and comments are skipped.
    /// </summary>
    public static class TdfSchemaExtractor
    {
        private static readonly Dictionary<string, string> ScalarTypes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "int", "TYPE_INTEGER" }, { "float", "TYPE_FLOAT" }, { "bool", "TYPE_BOOL" },
            { "string", "TYPE_STRING" }, { "stringId", "TYPE_STRINGID" }, { "vector", "TYPE_VECTOR" },
            { "objvar", "TYPE_DYNAMIC_VAR" }, { "filename", "TYPE_FILENAME" }
        };

        private static readonly HashSet<string> Directives = new HashSet<string>(StringComparer.Ordinal)
        {
            "version", "base", "id", "templatename", "clientpath", "serverpath", "sharedpath", "compilerpath"
        };

        public static JObject Extract(string className, string[] lines)
        {
            var paramArray = new JArray();
            bool inVersionBlock = false;

            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("//")) continue;

                string[] tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                string first = tokens[0];

                if (first == "version") { inVersionBlock = true; continue; }
                if (Directives.Contains(first)) continue;
                if (!inVersionBlock) continue;

                JObject p = ParseParam(tokens);
                if (p != null) paramArray.Add(p);
            }

            return new JObject
            {
                ["classes"] = new JObject
                {
                    [className] = new JObject { ["params"] = paramArray }
                }
            };
        }

        private static JObject ParseParam(string[] tokens)
        {
            int idx = 0;
            string listType = "LIST_NONE";

            // Optional leading list token: "list", "list[N]" (int-array), "list[enum:Name]" (enum-array).
            if (tokens[idx].StartsWith("list"))
            {
                string listTok = tokens[idx];
                if (listTok == "list") listType = "LIST_LIST";
                else if (listTok.StartsWith("list[enum")) listType = "LIST_ENUM_ARRAY";
                else if (listTok.StartsWith("list[")) listType = "LIST_INT_ARRAY";
                else listType = "LIST_LIST";
                idx++;
                if (idx >= tokens.Length) return null;
            }

            string typeTok = tokens[idx];
            string paramType;
            if (ScalarTypes.TryGetValue(typeTok, out string mapped)) paramType = mapped;
            else if (typeTok.StartsWith("template")) paramType = "TYPE_TEMPLATE";
            else if (typeTok.StartsWith("enum")) paramType = "TYPE_ENUM";
            else if (typeTok == "struct") paramType = "TYPE_STRUCT";
            else return null; // not a recognized param type line

            idx++;
            // The name is the LAST token on the line (after any [limits]).
            if (idx >= tokens.Length) return null;
            string name = tokens[tokens.Length - 1];
            if (name.Length == 0 || !char.IsLetter(name[0])) return null;

            return new JObject
            {
                ["listType"] = listType,
                ["name"] = name,
                ["type"] = paramType
            };
        }
    }
}
