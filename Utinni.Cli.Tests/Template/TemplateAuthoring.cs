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
//
// Inline TemplateModel authoring + a throwaway temp-dir helper for the verb-level template goldens. The
// templates match the layouts synthesized by TemplateTestFixtures so a verb round-trip is byte-exact.

using System;
using System.Collections.Generic;
using System.IO;
using UtinniCoreDotNet.Formats.Template;

namespace Utinni.Cli.Tests.Template
{
    /// <summary>Authors <see cref="TemplateModel"/>s matching the <see cref="TemplateTestFixtures"/> layouts.</summary>
    internal static class TemplateAuthoring
    {
        // The KERN fixture payload (TemplateTestFixtures.KernPayload): one of every scalar kernel type, in
        // order: i8,u8,i16,u16,i32,u32,f32,f64,cstring,char[8],raw[4],pad[2]. A template describing this
        // exact layout decodes->encodes byte-identical.
        public static TemplateModel KernTemplate(string version)
        {
            return new TemplateModel
            {
                Version = 1,
                MatchAncestorPath = "FORM:TPLX/0/FORM:" + version,
                MatchLeafTag = "KERN",
                MatchTagOnly = false,
                Fields = new List<FieldRecord>
                {
                    new FieldRecord { Name = "i8", Type = KernelType.I8 },
                    new FieldRecord { Name = "u8", Type = KernelType.U8 },
                    new FieldRecord { Name = "i16", Type = KernelType.I16 },
                    new FieldRecord { Name = "u16", Type = KernelType.U16 },
                    new FieldRecord { Name = "i32", Type = KernelType.I32 },
                    new FieldRecord { Name = "u32", Type = KernelType.U32 },
                    new FieldRecord { Name = "f32", Type = KernelType.F32 },
                    new FieldRecord { Name = "f64", Type = KernelType.F64 },
                    new FieldRecord { Name = "name", Type = KernelType.CString, Encoding = "ascii" },
                    new FieldRecord { Name = "tag", Type = KernelType.FixedChar, ByteWidth = 8, Encoding = "ascii" },
                    new FieldRecord { Name = "raw", Type = KernelType.RawBytes, ByteWidth = 4 },
                    new FieldRecord { Name = "pad", Type = KernelType.Pad, ByteWidth = 2 }
                }
            };
        }

        // A template that UNDER-describes the KERN payload (only the leading i8) so the decode consumes far
        // fewer bytes than the payload holds -> roundtrip is NOT byte-identical -> the gate must reject.
        public static TemplateModel WrongLayoutTemplate(string version)
        {
            return new TemplateModel
            {
                Version = 1,
                MatchAncestorPath = "FORM:TPLX/0/FORM:" + version,
                MatchLeafTag = "KERN",
                MatchTagOnly = false,
                Fields = new List<FieldRecord>
                {
                    new FieldRecord { Name = "i8", Type = KernelType.I8 }
                }
            };
        }

        // A template whose leaf tag matches NO leaf in any fixture -> the resolver finds no match.
        public static TemplateModel UnmatchedTagTemplate()
        {
            return new TemplateModel
            {
                Version = 1,
                MatchAncestorPath = "FORM:TPLX/0/FORM:0001",
                MatchLeafTag = "ZZZZ",
                MatchTagOnly = false,
                Fields = new List<FieldRecord>
                {
                    new FieldRecord { Name = "x", Type = KernelType.I32 }
                }
            };
        }
    }

    /// <summary>A throwaway temp directory that recursively deletes itself on dispose.</summary>
    internal sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "utinni-tpl-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Write(string name, byte[] bytes)
        {
            string p = System.IO.Path.Combine(Path, name);
            File.WriteAllBytes(p, bytes);
            return p;
        }

        public string WriteText(string name, string text)
        {
            string p = System.IO.Path.Combine(Path, name);
            File.WriteAllText(p, text);
            return p;
        }

        public void Dispose()
        {
            try { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }
}
