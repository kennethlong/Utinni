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

using UtinniCoreDotNet.Formats.ObjectTemplate;
using Xunit;

namespace UtinniCoreDotNet.Tests.ObjectTemplate
{
    /// <summary>
    /// Plan 13-06 Task 1 (RESID-01): the schema model + loader — the committed common-class schema
    /// loads, the list/struct params (slots/attributes/hair-customization) classify as structured, and
    /// an absent/malformed schema degrades gracefully (open-path safety, T-13-16).
    /// </summary>
    public sealed class ObjectTemplateSchemaTests
    {
        [Fact]
        public void LoadCommon_LoadsCommittedSchema_WithKnownClasses()
        {
            ObjectTemplateSchemaLoader.ResetCacheForTesting();
            ObjectTemplateSchema schema = ObjectTemplateSchemaLoader.LoadCommon();
            Assert.True(schema.ClassCount >= 2, "the embedded common-class schema must load");
        }

        [Theory]
        [InlineData("SharedDraftSchematicObjectTemplate", "slots")]
        [InlineData("SharedDraftSchematicObjectTemplate", "attributes")]
        [InlineData("SharedTangibleObjectTemplate", "paletteColorCustomizationVariables")]
        public void Classify_KnownListParams_AreStructured(string className, string paramName)
        {
            ObjectTemplateSchema schema = ObjectTemplateSchemaLoader.LoadCommon();
            ObjectTemplateParamSchema p = schema.Classify(className, paramName);
            Assert.NotNull(p);
            Assert.True(p.IsStructured, paramName + " must classify as structured (ListType != LIST_NONE)");
            Assert.NotEqual(ObjectTemplateListType.LIST_NONE, p.ListType);
            Assert.True(schema.IsStructured(className, paramName));
        }

        [Fact]
        public void Classify_UnknownClassOrParam_ReturnsNoMatch()
        {
            ObjectTemplateSchema schema = ObjectTemplateSchemaLoader.LoadCommon();
            Assert.Null(schema.Classify("NoSuchClass", "slots"));
            Assert.Null(schema.Classify("SharedDraftSchematicObjectTemplate", "noSuchParam"));
            Assert.False(schema.IsStructured("NoSuchClass", "x"));
        }

        [Fact]
        public void LoadFromJson_ScalarParam_IsNotStructured()
        {
            const string json = "{ \"classes\": { \"C\": { \"params\": [ " +
                "{ \"name\": \"hp\", \"type\": \"TYPE_INTEGER\", \"listType\": \"LIST_NONE\" } ] } } }";
            ObjectTemplateSchema schema = ObjectTemplateSchemaLoader.LoadFromJson(json);
            ObjectTemplateParamSchema p = schema.Classify("C", "hp");
            Assert.NotNull(p);
            Assert.False(p.IsStructured);
            Assert.Equal(ObjectTemplateParamType.TYPE_INTEGER, p.Type);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not json at all { ]")]
        [InlineData("{ \"classes\": \"not-an-object\" }")]
        [InlineData("12345")]
        public void LoadFromJson_AbsentOrMalformed_DegradesToEmpty_NoThrow(string json)
        {
            ObjectTemplateSchema schema = ObjectTemplateSchemaLoader.LoadFromJson(json);
            Assert.Equal(0, schema.ClassCount);
            Assert.Null(schema.Classify("anything", "x")); // never throws on the open path
        }

        [Fact]
        public void LoadFromJson_UnknownTypeToken_DegradesToTypeNone_NoThrow()
        {
            const string json = "{ \"classes\": { \"C\": { \"params\": [ " +
                "{ \"name\": \"weird\", \"type\": \"TYPE_BOGUS\", \"listType\": \"LIST_BOGUS\" } ] } } }";
            ObjectTemplateSchema schema = ObjectTemplateSchemaLoader.LoadFromJson(json);
            ObjectTemplateParamSchema p = schema.Classify("C", "weird");
            Assert.NotNull(p);
            Assert.Equal(ObjectTemplateParamType.TYPE_NONE, p.Type);
            Assert.Equal(ObjectTemplateListType.LIST_NONE, p.ListType);
        }
    }
}
