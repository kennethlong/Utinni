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
// Schema vocabulary mirrors sharedTemplateDefinition/TemplateData.h ParamType(14)/ListType(4).
// Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;

namespace UtinniCoreDotNet.Formats.ObjectTemplate
{
    /// <summary>
    /// The object-template parameter type vocabulary (the 14 <c>ParamType</c> values from the SOE
    /// <c>TemplateData.h</c>), mirrored managed-side so the schema is self-describing.
    /// </summary>
    public enum ObjectTemplateParamType
    {
        TYPE_NONE, TYPE_COMMENT, TYPE_INTEGER, TYPE_FLOAT, TYPE_BOOL, TYPE_STRING, TYPE_STRINGID,
        TYPE_VECTOR, TYPE_DYNAMIC_VAR, TYPE_TEMPLATE, TYPE_ENUM, TYPE_STRUCT, TYPE_TRIGGER_VOLUME,
        TYPE_FILENAME
    }

    /// <summary>The 4 <c>ListType</c> values. Anything other than <see cref="LIST_NONE"/> is the
    /// multi-chunk list/struct residual RESID-01 renders as a structured typed widget.</summary>
    public enum ObjectTemplateListType
    {
        LIST_NONE, LIST_LIST, LIST_INT_ARRAY, LIST_ENUM_ARRAY
    }

    /// <summary>Per-param schema entry: its <see cref="ObjectTemplateParamType"/> + <see cref="ObjectTemplateListType"/>.</summary>
    public sealed class ObjectTemplateParamSchema
    {
        public string Name { get; }
        public ObjectTemplateParamType Type { get; }
        public ObjectTemplateListType ListType { get; }

        /// <summary>True iff this is a multi-chunk list/struct param (ListType != LIST_NONE) — the
        /// codec's RawBytesHexFallback residual the editor renders as a structured typed widget (D-07).</summary>
        public bool IsStructured { get { return ListType != ObjectTemplateListType.LIST_NONE; } }

        public ObjectTemplateParamSchema(string name, ObjectTemplateParamType type, ObjectTemplateListType listType)
        {
            Name = name;
            Type = type;
            ListType = listType;
        }
    }

    /// <summary>
    /// Plan 13-06 (RESID-01, D-08): the static per-class param→type schema the Object Template Editor
    /// consults at open to decide whether a param renders as a structured typed widget (slots /
    /// attributes / hair-customization lists) or — for the rare/exotic tail — a typed LABEL + hex.
    /// A pure data model (BCL only); loading is <see cref="ObjectTemplateSchemaLoader"/>.
    /// </summary>
    public sealed class ObjectTemplateSchema
    {
        private readonly Dictionary<string, Dictionary<string, ObjectTemplateParamSchema>> _classes;

        public ObjectTemplateSchema(Dictionary<string, Dictionary<string, ObjectTemplateParamSchema>> classes)
        {
            _classes = classes ?? new Dictionary<string, Dictionary<string, ObjectTemplateParamSchema>>(StringComparer.Ordinal);
        }

        /// <summary>The number of classes in the schema.</summary>
        public int ClassCount { get { return _classes.Count; } }

        /// <summary>
        /// Resolves the schema entry for <paramref name="paramName"/> on <paramref name="className"/>,
        /// or null if the class or param is not in the schema (a graceful no-match — the editor then
        /// keeps the existing hex display for that param).
        /// </summary>
        public ObjectTemplateParamSchema Classify(string className, string paramName)
        {
            if (string.IsNullOrEmpty(className) || string.IsNullOrEmpty(paramName)) return null;
            Dictionary<string, ObjectTemplateParamSchema> classParams;
            if (!_classes.TryGetValue(className, out classParams)) return null;
            ObjectTemplateParamSchema p;
            return classParams.TryGetValue(paramName, out p) ? p : null;
        }

        /// <summary>True iff (className, paramName) is a known structured (ListType != LIST_NONE) param.</summary>
        public bool IsStructured(string className, string paramName)
        {
            ObjectTemplateParamSchema p = Classify(className, paramName);
            return p != null && p.IsStructured;
        }
    }
}
