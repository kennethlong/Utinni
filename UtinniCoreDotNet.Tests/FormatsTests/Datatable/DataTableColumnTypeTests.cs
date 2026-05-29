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

using Xunit;
using UtinniCoreDotNet.Formats.Datatable;

namespace UtinniCoreDotNet.Tests.FormatsTests.Datatable
{
    /// <summary>
    /// Coverage for the DTII column type-spec parser + MangleValue port (Task 1 of 09-01).
    /// Validates every DT_* discriminator, the corrected enum grammar (Assumption A1), the
    /// DT_PackedObjVars / DT_BitVector parse-back validators, and the per-DT_* MangleValue matrix.
    ///
    /// Test naming convention: [Method]_[Scenario]_[ExpectedOutcome] (Phase 1 D-04).
    /// </summary>
    public class DataTableColumnTypeTests
    {
        // ── Discriminator parse ──────────────────────────────────────────────

        [Fact]
        public void Parse_Int_TypeAndBasicTypeAreInt()
        {
            var ct = new DataTableColumnType("i");
            Assert.Equal(DataType.Int, ct.Type);
            Assert.Equal(DataType.Int, ct.BasicType);
        }

        [Fact]
        public void Parse_Int_DefaultIsZeroWhenNoBracket()
        {
            var ct = new DataTableColumnType("i");
            Assert.Equal("0", ct.DefaultValue);
        }

        [Fact]
        public void Parse_Int_DefaultFromBracketGroup()
        {
            var ct = new DataTableColumnType("i[42]");
            Assert.Equal("42", ct.DefaultValue);
        }

        [Fact]
        public void Parse_Float_TypeAndBasicTypeAreFloat()
        {
            var ct = new DataTableColumnType("f");
            Assert.Equal(DataType.Float, ct.Type);
            Assert.Equal(DataType.Float, ct.BasicType);
        }

        [Fact]
        public void Parse_String_TypeAndBasicTypeAreString()
        {
            var ct = new DataTableColumnType("s");
            Assert.Equal(DataType.String, ct.Type);
            Assert.Equal(DataType.String, ct.BasicType);
        }

        [Fact]
        public void Parse_Comment_TypeAndBasicTypeAreComment()
        {
            var ct = new DataTableColumnType("c");
            Assert.Equal(DataType.Comment, ct.Type);
            Assert.Equal(DataType.Comment, ct.BasicType);
        }

        [Fact]
        public void Parse_HashString_BasicTypeIsInt()
        {
            var ct = new DataTableColumnType("h");
            Assert.Equal(DataType.HashString, ct.Type);
            Assert.Equal(DataType.Int, ct.BasicType);
        }

        [Fact]
        public void Parse_PackedObjVars_BasicTypeIsString()
        {
            var ct = new DataTableColumnType("p");
            Assert.Equal(DataType.PackedObjVars, ct.Type);
            Assert.Equal(DataType.String, ct.BasicType);
        }

        [Fact]
        public void Parse_Bool_BasicTypeIsInt_DefaultZero()
        {
            var ct = new DataTableColumnType("b");
            Assert.Equal(DataType.Bool, ct.Type);
            Assert.Equal(DataType.Int, ct.BasicType);
            Assert.Equal("0", ct.DefaultValue);
        }

        [Fact]
        public void Parse_Bool_DefaultOnePreserved()
        {
            var ct = new DataTableColumnType("b[1]");
            Assert.Equal("1", ct.DefaultValue);
        }

        [Fact]
        public void Parse_UnknownDiscriminator_BasicTypeIsUnknown()
        {
            var ct = new DataTableColumnType("q");
            Assert.Equal(DataType.Unknown, ct.BasicType);
        }

        [Fact]
        public void Parse_CrossTableEnum_Z_TreatedAsUnknown()
        {
            var ct = new DataTableColumnType("z(some_table)");
            Assert.Equal(DataType.Unknown, ct.Type);
            Assert.Equal(DataType.Unknown, ct.BasicType);
        }

        [Fact]
        public void Constructor_NullSpec_Throws()
        {
            Assert.Throws<DataTableParseException>(() => new DataTableColumnType(null));
        }

        [Fact]
        public void Constructor_EmptySpec_Throws()
        {
            Assert.Throws<DataTableParseException>(() => new DataTableColumnType(""));
        }

        // ── Corrected enum grammar (Assumption A1) ───────────────────────────

        [Fact]
        public void Parse_Enum_CorrectedGrammar_BuildsMapAndDefault()
        {
            // e(a=1,b=2,c=3)[b] — parentheses delimit the list, '=' separates label/value,
            // square brackets carry the default. (RESEARCH Assumption A1 corrected grammar.)
            var ct = new DataTableColumnType("e(a=1,b=2,c=3)[b]");
            Assert.Equal(DataType.Enum, ct.Type);
            Assert.Equal(DataType.Int, ct.BasicType);
            Assert.Equal("b", ct.DefaultValue);
            Assert.Equal(3, ct.EnumMap.Count);
            Assert.Equal(1, ct.EnumMap["a"]);
            Assert.Equal(2, ct.EnumMap["b"]);
            Assert.Equal(3, ct.EnumMap["c"]);
        }

        [Fact]
        public void Parse_Enum_DefaultNotInMap_BasicTypeUnknown()
        {
            var ct = new DataTableColumnType("e(a=1,b=2)[zzz]");
            Assert.Equal(DataType.Unknown, ct.BasicType);
        }

        [Fact]
        public void Parse_BitVector_BitsPreShifted()
        {
            // v(low=1,mid=2,high=3) → 1<<0, 1<<1, 1<<2 = 1, 2, 4.
            var ct = new DataTableColumnType("v(low=1,mid=2,high=3)[NONE]");
            Assert.Equal(DataType.BitVector, ct.Type);
            Assert.Equal(1, ct.EnumMap["low"]);
            Assert.Equal(2, ct.EnumMap["mid"]);
            Assert.Equal(4, ct.EnumMap["high"]);
        }

        [Fact]
        public void Parse_BitVector_NoneDefaultAllowed()
        {
            var ct = new DataTableColumnType("v(a=1,b=2)[NONE]");
            Assert.Equal(DataType.Int, ct.BasicType);
        }

        [Fact]
        public void TypeSpec_RoundTripsOriginalString()
        {
            var ct = new DataTableColumnType("e(a=1,b=2)[a]");
            Assert.Equal("e(a=1,b=2)[a]", ct.TypeSpec);
        }

        // ── MangleValue matrix ───────────────────────────────────────────────

        [Fact]
        public void Mangle_Int_PassesThroughUnchanged()
        {
            var ct = new DataTableColumnType("i");
            string v = "123";
            Assert.True(ct.MangleValue(ref v));
            Assert.Equal("123", v);
        }

        [Fact]
        public void Mangle_Empty_SubstitutesDefault()
        {
            var ct = new DataTableColumnType("i[7]");
            string v = "";
            Assert.True(ct.MangleValue(ref v));
            Assert.Equal("7", v);
        }

        [Fact]
        public void Mangle_EmptyRequired_ReturnsFalse()
        {
            var ct = new DataTableColumnType("s[required]");
            string v = "";
            Assert.False(ct.MangleValue(ref v));
        }

        [Fact]
        public void Mangle_EmptyUnique_ReturnsFalse()
        {
            var ct = new DataTableColumnType("s[unique]");
            string v = "";
            Assert.False(ct.MangleValue(ref v));
        }

        [Fact]
        public void Mangle_BoolValid_True()
        {
            var ct = new DataTableColumnType("b");
            string v0 = "0";
            string v1 = "1";
            Assert.True(ct.MangleValue(ref v0));
            Assert.True(ct.MangleValue(ref v1));
        }

        [Fact]
        public void Mangle_BoolInvalid_False()
        {
            var ct = new DataTableColumnType("b");
            string v = "2";
            Assert.False(ct.MangleValue(ref v));
        }

        [Fact]
        public void Mangle_HashString_ReplacesWithCrcInt()
        {
            var ct = new DataTableColumnType("h");
            string v = "Foo";
            Assert.True(ct.MangleValue(ref v));
            uint expected = DataTableHashCrc.Compute("Foo");
            Assert.Equal(unchecked((int)expected).ToString(System.Globalization.CultureInfo.InvariantCulture), v);
        }

        [Fact]
        public void Mangle_EnumHit_ReplacesWithMappedInt()
        {
            var ct = new DataTableColumnType("e(red=10,green=20,blue=30)[red]");
            string v = "green";
            Assert.True(ct.MangleValue(ref v));
            Assert.Equal("20", v);
        }

        [Fact]
        public void Mangle_EnumMiss_ReturnsFalse()
        {
            var ct = new DataTableColumnType("e(red=10,green=20)[red]");
            string v = "purple";
            Assert.False(ct.MangleValue(ref v));
        }

        [Fact]
        public void Mangle_BitVectorMultiLabel_OrsBits()
        {
            var ct = new DataTableColumnType("v(low=1,mid=2,high=3)[NONE]");
            string v = "low,high";
            Assert.True(ct.MangleValue(ref v));
            // low=1<<0=1, high=1<<2=4 → OR = 5.
            Assert.Equal("5", v);
        }

        [Fact]
        public void Mangle_BitVectorNone_ReturnsZero()
        {
            var ct = new DataTableColumnType("v(a=1,b=2)[NONE]");
            string v = "NONE";
            Assert.True(ct.MangleValue(ref v));
            Assert.Equal("0", v);
        }

        [Fact]
        public void Mangle_StringPassesThrough()
        {
            var ct = new DataTableColumnType("s");
            string v = "anything goes";
            Assert.True(ct.MangleValue(ref v));
            Assert.Equal("anything goes", v);
        }

        // ── PackedObjVars parse-back validator ───────────────────────────────

        [Fact]
        public void Mangle_PackedObjVarsValid_True()
        {
            var ct = new DataTableColumnType("p");
            // name|type|value| triple, terminated by $|.
            string v = "color|0|red|$|";
            Assert.True(ct.MangleValue(ref v));
        }

        [Fact]
        public void Mangle_PackedObjVarsTruncated_False()
        {
            var ct = new DataTableColumnType("p");
            // name|type| with no value field and no terminator.
            string v = "color|0";
            Assert.False(ct.MangleValue(ref v));
        }

        [Fact]
        public void Mangle_PackedObjVarsBadIntField_False()
        {
            var ct = new DataTableColumnType("p");
            // type field contains a non-digit.
            string v = "color|xx|red|$|";
            Assert.False(ct.MangleValue(ref v));
        }
    }
}
