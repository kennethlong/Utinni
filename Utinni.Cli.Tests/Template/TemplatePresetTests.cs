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
// Wave 1 (plan 23-02) schema-shape goldens for the shipped D-09 preset PACK. For THIS plan a
// load + schema-shape assertion suffices (the byte-exact DECODE of each preset lands with the kernel
// codec in 23-03). These assert the pinned layouts: w-FIRST quaternion, 12-field 3x4 matrix, 3xu8
// color default, vector x/y/z order, and that every preset deserializes cleanly with a version field.
// Filter tokens live in the method names: Template, Preset.

using System;
using System.IO;
using System.Linq;
using UtinniCoreDotNet.Formats.Template;
using Xunit;

namespace Utinni.Cli.Tests.Template
{
    /// <summary>
    /// Schema-shape goldens for the shipped <c>Presets/*.json</c> pack (D-09 pinned byte layouts). Filter
    /// tokens are in the method names: <c>Template</c>, <c>Preset</c>.
    /// </summary>
    public sealed class TemplatePresetTests
    {
        // The presets ship next to the assembly (CopyToOutputDirectory=PreserveNewest). Resolve from the
        // app base directory (NOT Assembly.Location) because xUnit shadow-copies the test assembly to a
        // Temp dir; AppContext.BaseDirectory points at the real build output where Content was copied.
        private static readonly string PresetDir =
            Path.Combine(AppContext.BaseDirectory, "Formats", "Template", "Presets");

        private static TemplateModel Load(string fileName)
        {
            string path = Path.Combine(PresetDir, fileName);
            Assert.True(File.Exists(path), "shipped preset not found next to the assembly: " + path);
            return TemplateJson.Deserialize(File.ReadAllText(path));
        }

        private static FieldRecord StructField(TemplateModel m)
        {
            FieldRecord f = m.Fields.Single();
            Assert.Equal(KernelType.Struct, f.Type);
            return f;
        }

        public static readonly object[][] AllPresets =
        {
            new object[] { "vector.json" },
            new object[] { "quaternion.json" },
            new object[] { "matrix.json" },
            new object[] { "color.json" },
            new object[] { "colorArgb32.json" },
            new object[] { "colorArgbF.json" },
            new object[] { "stringId.json" }
        };

        [Theory]
        [MemberData(nameof(AllPresets))]
        [Trait("Category", "TemplatePreset")]
        public void Template_Preset_EveryShippedPreset_DeserializesWithVersion(string fileName)
        {
            TemplateModel m = Load(fileName);
            Assert.NotNull(m.Fields);
            Assert.NotEmpty(m.Fields);
            // Each preset carries a D-13 version field (defaults to 0 only when absent; all ship with 1).
            Assert.True(m.Version >= 1, fileName + " must carry a version field >= 1");
        }

        [Fact]
        [Trait("Category", "TemplatePreset")]
        public void Template_Preset_Vector_IsThreeF32_XYZ_InOrder()
        {
            FieldRecord s = StructField(Load("vector.json"));
            Assert.Equal(new[] { "x", "y", "z" }, s.StructFields.Select(f => f.Name).ToArray());
            Assert.All(s.StructFields, f => Assert.Equal(KernelType.F32, f.Type));
        }

        [Fact]
        [Trait("Category", "TemplatePreset")]
        public void Template_Preset_Quaternion_IsFourF32_WFirst()
        {
            FieldRecord s = StructField(Load("quaternion.json"));
            Assert.Equal(new[] { "w", "x", "y", "z" }, s.StructFields.Select(f => f.Name).ToArray());
            Assert.All(s.StructFields, f => Assert.Equal(KernelType.F32, f.Type));
        }

        [Fact]
        [Trait("Category", "TemplatePreset")]
        public void Template_Preset_Matrix_IsTwelveF32_3x4RowMajor()
        {
            FieldRecord s = StructField(Load("matrix.json"));
            Assert.Equal(12, s.StructFields.Count);
            Assert.All(s.StructFields, f => Assert.Equal(KernelType.F32, f.Type));
        }

        [Fact]
        [Trait("Category", "TemplatePreset")]
        public void Template_Preset_Color_DefaultIsThreeU8_RGB()
        {
            FieldRecord s = StructField(Load("color.json"));
            Assert.Equal(new[] { "r", "g", "b" }, s.StructFields.Select(f => f.Name).ToArray());
            Assert.All(s.StructFields, f => Assert.Equal(KernelType.U8, f.Type));
        }

        [Fact]
        [Trait("Category", "TemplatePreset")]
        public void Template_Preset_ColorArgb32_IsSingleU32()
        {
            TemplateModel m = Load("colorArgb32.json");
            FieldRecord f = m.Fields.Single();
            Assert.Equal(KernelType.U32, f.Type);
        }

        [Fact]
        [Trait("Category", "TemplatePreset")]
        public void Template_Preset_ColorArgbF_IsFourF32_ARGB()
        {
            FieldRecord s = StructField(Load("colorArgbF.json"));
            Assert.Equal(new[] { "a", "r", "g", "b" }, s.StructFields.Select(f => f.Name).ToArray());
            Assert.All(s.StructFields, f => Assert.Equal(KernelType.F32, f.Type));
        }

        [Fact]
        [Trait("Category", "TemplatePreset")]
        public void Template_Preset_StringId_IsTwoCStrings_TableThenText()
        {
            FieldRecord s = StructField(Load("stringId.json"));
            Assert.Equal(new[] { "table", "text" }, s.StructFields.Select(f => f.Name).ToArray());
            Assert.All(s.StructFields, f => Assert.Equal(KernelType.CString, f.Type));
        }
    }
}
