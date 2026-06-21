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
// Wave 1 (plan 23-02) GREEN tests for the schema layer (D-01/D-08/D-11/D-13): TemplateJson round-trips
// the fixed TemplateModel POCO, defaults a missing version field safely (forward migration), and is
// immune to a hostile $type / TypeNameHandling gadget payload. Filter tokens live in the method names:
// Template, Json, Roundtrip, Version, Security.

using System.Collections.Generic;
using UtinniCoreDotNet.Formats.Template;
using Xunit;

namespace Utinni.Cli.Tests.Template
{
    /// <summary>
    /// Schema-layer goldens for <see cref="TemplateJson"/> (D-01/D-08/D-11/D-13). Filter tokens are in
    /// the method names: <c>Template</c>, <c>Json</c>, <c>Roundtrip</c>, <c>Version</c>, <c>Security</c>.
    /// </summary>
    public sealed class TemplateJsonTests
    {
        // A model exercising one field of every kernel kind + a count-from-prior array + a struct +
        // an enum map + a flags map (D-08/D-10/D-11). Round-trip stability is the make-or-break here.
        private static TemplateModel BuildEveryKindModel()
        {
            return new TemplateModel
            {
                Version = 2,
                MatchAncestorPath = "TPLX/0003",
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
                    new FieldRecord { Name = "tag8", Type = KernelType.FixedChar, ByteWidth = 8, Encoding = "ascii" },
                    new FieldRecord { Name = "raw4", Type = KernelType.RawBytes, ByteWidth = 4 },
                    new FieldRecord { Name = "pad2", Type = KernelType.Pad, ByteWidth = 2 },
                    // count-from-prior count field + the array it drives (DEC-C3)
                    new FieldRecord { Name = "count", Type = KernelType.U32 },
                    new FieldRecord
                    {
                        Name = "records",
                        Type = KernelType.Array,
                        Repeat = new RepeatSpec { Kind = RepeatKind.CountFromField, CountFieldName = "count" },
                        StructFields = new List<FieldRecord>
                        {
                            new FieldRecord { Name = "id", Type = KernelType.U16 },
                            new FieldRecord { Name = "weight", Type = KernelType.F32 }
                        }
                    },
                    // a nested struct
                    new FieldRecord
                    {
                        Name = "vec",
                        Type = KernelType.Struct,
                        StructFields = new List<FieldRecord>
                        {
                            new FieldRecord { Name = "x", Type = KernelType.F32 },
                            new FieldRecord { Name = "y", Type = KernelType.F32 },
                            new FieldRecord { Name = "z", Type = KernelType.F32 }
                        }
                    },
                    // an enum-decorated int (D-11)
                    new FieldRecord
                    {
                        Name = "shape",
                        Type = KernelType.I32,
                        ValueMap = new NamedValueMap
                        {
                            IsFlags = false,
                            Entries = new Dictionary<string, long> { { "Cube", 0 }, { "Sphere", 1 }, { "Cone", 2 } }
                        }
                    },
                    // a flags-decorated int (D-11)
                    new FieldRecord
                    {
                        Name = "options",
                        Type = KernelType.U32,
                        ValueMap = new NamedValueMap
                        {
                            IsFlags = true,
                            Entries = new Dictionary<string, long> { { "Visible", 1 }, { "Collidable", 2 }, { "Shadowed", 3 } }
                        }
                    }
                }
            };
        }

        [Fact]
        [Trait("Category", "TemplateJsonRoundtrip")]
        public void Template_Json_Roundtrip_EveryKind_DeserializesToEqualModel()
        {
            TemplateModel original = BuildEveryKindModel();

            string json = TemplateJson.Serialize(original);
            TemplateModel restored = TemplateJson.Deserialize(json);

            // Re-serializing the restored model must produce byte-identical JSON (round-trip-stable, D-01).
            string reserialized = TemplateJson.Serialize(restored);
            Assert.Equal(json, reserialized);

            // Spot-check the structural identity end-to-end.
            Assert.Equal(original.Version, restored.Version);
            Assert.Equal(original.MatchAncestorPath, restored.MatchAncestorPath);
            Assert.Equal(original.MatchLeafTag, restored.MatchLeafTag);
            Assert.Equal(original.Fields.Count, restored.Fields.Count);

            FieldRecord arr = restored.Fields.Find(f => f.Name == "records");
            Assert.NotNull(arr);
            Assert.Equal(KernelType.Array, arr.Type);
            Assert.Equal(RepeatKind.CountFromField, arr.Repeat.Kind);
            Assert.Equal("count", arr.Repeat.CountFieldName);
            Assert.Equal(2, arr.StructFields.Count);

            FieldRecord shape = restored.Fields.Find(f => f.Name == "shape");
            Assert.False(shape.ValueMap.IsFlags);
            Assert.Equal(1L, shape.ValueMap.Entries["Sphere"]);

            FieldRecord options = restored.Fields.Find(f => f.Name == "options");
            Assert.True(options.ValueMap.IsFlags);
            Assert.Equal(3L, options.ValueMap.Entries["Shadowed"]);
        }

        [Fact]
        [Trait("Category", "TemplateJsonRoundtrip")]
        public void Template_Json_Roundtrip_SortedKeys_AreStableAcrossSerialize()
        {
            // Two models with the SAME content but different insertion order must serialize identically
            // (sorted-key output → clean diffs, D-01).
            TemplateModel a = new TemplateModel
            {
                Version = 1,
                MatchLeafTag = "ABCD",
                MatchAncestorPath = "TPLX",
                Fields = new List<FieldRecord> { new FieldRecord { Name = "x", Type = KernelType.F32 } }
            };
            TemplateModel b = new TemplateModel
            {
                MatchAncestorPath = "TPLX",
                Fields = new List<FieldRecord> { new FieldRecord { Name = "x", Type = KernelType.F32 } },
                MatchLeafTag = "ABCD",
                Version = 1
            };

            Assert.Equal(TemplateJson.Serialize(a), TemplateJson.Serialize(b));
        }

        [Fact]
        [Trait("Category", "TemplateJsonVersion")]
        public void Template_Json_Version_MissingVersionField_DefaultsSafelyWithoutThrowing()
        {
            // A template JSON with NO version field must deserialize (forward-migration, D-13) and default
            // Version to 0 rather than throwing.
            const string noVersion =
                "{ \"matchLeafTag\": \"KERN\", \"fields\": [ { \"name\": \"x\", \"type\": \"F32\" } ] }";

            TemplateModel model = TemplateJson.Deserialize(noVersion);

            Assert.Equal(0, model.Version);
            Assert.Equal("KERN", model.MatchLeafTag);
            Assert.Single(model.Fields);
            Assert.Equal(KernelType.F32, model.Fields[0].Type);
        }

        [Fact]
        [Trait("Category", "TemplateJsonSecurity")]
        public void Template_Json_Security_TypeNameHandlingGadget_IsIgnored()
        {
            // A hostile $type gadget payload must be ignored: with TypeNameHandling = None the value is
            // deserialized into the fixed POCO with no type resolution (no gadget instantiation).
            const string gadget =
                "{ \"$type\": \"System.Diagnostics.Process, System\", " +
                "\"version\": 1, \"matchLeafTag\": \"KERN\", " +
                "\"fields\": [ { \"$type\": \"System.IO.FileInfo, mscorlib\", \"name\": \"x\", \"type\": \"F32\" } ] }";

            TemplateModel model = TemplateJson.Deserialize(gadget);

            // It still parses as a plain TemplateModel (no exception, no type confusion).
            Assert.IsType<TemplateModel>(model);
            Assert.Equal(1, model.Version);
            Assert.Equal("KERN", model.MatchLeafTag);
            Assert.Single(model.Fields);
            Assert.IsType<FieldRecord>(model.Fields[0]);
            Assert.Equal("x", model.Fields[0].Name);
        }

        [Fact]
        [Trait("Category", "TemplateJsonSecurity")]
        public void Template_Json_Security_MalformedEnumValue_ThrowsTemplateException()
        {
            // An out-of-range / unknown kernel type member must throw a typed TemplateException rather than
            // silently defaulting to type 0 (threat T-23-02-EN).
            const string badEnum =
                "{ \"version\": 1, \"matchLeafTag\": \"KERN\", " +
                "\"fields\": [ { \"name\": \"x\", \"type\": \"NotARealKernelType\" } ] }";

            Assert.Throws<TemplateException>(() => TemplateJson.Deserialize(badEnum));
        }
    }
}
