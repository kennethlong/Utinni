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
// SCHEMA LAYER (de)serializer (plan 23-02, D-01/D-13). A template is a portable, self-contained JSON
// file = the canonical source of truth (D-01); this is the only code that turns that file into the frozen
// TemplateModel POCO and back.
//
// SECURITY (threat T-23-02-DE): deserialization targets a FIXED POCO model with TypeNameHandling = None
// explicitly set, so a hostile template's $type gadget is never resolved or instantiated.
// SECURITY (threat T-23-02-EN): an unknown KernelType / RepeatKind member throws a typed
// TemplateException rather than silently defaulting to type 0 and mis-decoding a chunk.
//
// DIFFABILITY (D-01): Serialize emits sorted-key, indented JSON via a contract resolver that orders
// properties alphabetically, so two equal models always produce byte-identical text and template files
// diff cleanly in version control.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace UtinniCoreDotNet.Formats.Template
{
    /// <summary>
    /// Newtonsoft (de)serializer for the frozen <see cref="TemplateModel"/> POCO (plan 23-02). Enum
    /// members ((de)serialize as strings for human-authorability; output keys are sorted for clean diffs;
    /// deserialization is locked to the fixed POCO with no <c>TypeNameHandling</c> gadget resolution.
    /// </summary>
    public static class TemplateJson
    {
        // Shared converters: enums as human-readable strings; an unknown member throws (caught and
        // rewrapped as a TemplateException by Deserialize) rather than defaulting to the zero member.
        private static readonly StringEnumConverter EnumConverter = new StringEnumConverter();

        // SERIALIZE settings: indented + sorted keys (D-01 diffability). Enum-as-string for authoring.
        private static readonly JsonSerializerSettings WriteSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new SortedPropertyContractResolver(),
            Converters = new JsonConverter[] { EnumConverter },
            NullValueHandling = NullValueHandling.Ignore,
            // No type metadata is ever emitted.
            TypeNameHandling = TypeNameHandling.None
        };

        // DESERIALIZE settings: fixed POCO only. TypeNameHandling = None is the T-23-02-DE mitigation —
        // a hostile "$type" property is ignored (never resolved to a CLR type). MissingMemberHandling is
        // Ignore so a forward template with extra fields still loads (D-13 forward-migration).
        private static readonly JsonSerializerSettings ReadSettings = new JsonSerializerSettings
        {
            ContractResolver = new SortedPropertyContractResolver(),
            Converters = new JsonConverter[] { EnumConverter },
            TypeNameHandling = TypeNameHandling.None,
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        /// <summary>
        /// Serializes a <see cref="TemplateModel"/> to canonical, sorted-key, indented JSON text (D-01).
        /// Throws <see cref="TemplateException"/> for a null model.
        /// </summary>
        public static string Serialize(TemplateModel template)
        {
            if (template == null)
            {
                throw new TemplateException("Cannot serialize a null template.");
            }

            try
            {
                return JsonConvert.SerializeObject(template, WriteSettings);
            }
            catch (JsonException ex)
            {
                throw new TemplateException("Failed to serialize the template to JSON.", ex);
            }
        }

        /// <summary>
        /// Deserializes template JSON text into the fixed <see cref="TemplateModel"/> POCO. A missing
        /// <c>version</c> defaults to 0 (D-13 forward-migration). An unknown enum member, malformed JSON,
        /// or a document that cannot be coerced to the POCO throws <see cref="TemplateException"/>. The
        /// fixed-POCO target plus <c>TypeNameHandling = None</c> means a hostile <c>$type</c> gadget is
        /// ignored, not instantiated (threat T-23-02-DE).
        /// </summary>
        public static TemplateModel Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                throw new TemplateException("Cannot deserialize an empty template document.");
            }

            try
            {
                TemplateModel model = JsonConvert.DeserializeObject<TemplateModel>(json, ReadSettings);
                if (model == null)
                {
                    throw new TemplateException("Template document deserialized to null.");
                }

                return model;
            }
            catch (JsonException ex)
            {
                // Newtonsoft surfaces an unknown StringEnumConverter member as a JsonSerializationException;
                // rewrap every parse/coerce failure as the typed schema error (T-23-02-EN).
                throw new TemplateException("Malformed template document: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Contract resolver that orders serialized properties alphabetically by their JSON name so the
        /// output is stable regardless of CLR member declaration order (D-01 clean diffs).
        /// </summary>
        private sealed class SortedPropertyContractResolver : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                IList<JsonProperty> properties = base.CreateProperties(type, memberSerialization);
                return properties.OrderBy(p => p.PropertyName, StringComparer.Ordinal).ToList();
            }
        }
    }
}
