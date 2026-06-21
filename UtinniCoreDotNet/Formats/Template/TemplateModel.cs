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
// SCHEMA LAYER (plan 23-02, D-01/D-08/D-11/D-13). The fixed POCO shapes themselves (TemplateModel /
// FieldRecord / RepeatSpec / NamedValueMap / KernelType / RepeatKind) are FROZEN in
// ITemplateContracts.cs (Wave 0, plan 23-01) and are NOT redefined here — re-declaring them would be a
// duplicate-type error and would violate the frozen-contract invariant. This file adds the supporting
// model machinery the schema layer needs around those frozen shapes:
//   - TemplateException: the typed error TemplateJson throws on a malformed template document.
//   - TemplateModelDefaults: the schema's default constants (encoding default "ascii", current version),
//     and a deep-copy clone helper that gives the POCO model the same defensive-copy discipline the
//     other Formats/* POCOs (ClientEffectCommand) follow.

using System;
using System.Collections.Generic;

namespace UtinniCoreDotNet.Formats.Template
{
    /// <summary>
    /// Typed error raised while (de)serializing a template document (plan 23-02): a malformed enum member
    /// (unknown <see cref="KernelType"/> / <see cref="RepeatKind"/>), structurally invalid JSON, or a
    /// document that cannot be coerced to the fixed <see cref="TemplateModel"/> POCO. Surfacing a typed
    /// exception (rather than silently defaulting to kernel type 0) is the threat-T-23-02-EN mitigation:
    /// a hostile or typo'd template fails loudly instead of mis-decoding a chunk.
    /// </summary>
    [Serializable]
    public sealed class TemplateException : Exception
    {
        /// <summary>Creates a template error with a human-readable message.</summary>
        public TemplateException(string message) : base(message)
        {
        }

        /// <summary>Creates a template error wrapping an underlying cause (e.g. a Newtonsoft parse error).</summary>
        public TemplateException(string message, Exception inner) : base(message, inner)
        {
        }
    }

    /// <summary>
    /// Schema-layer defaults and POCO helpers for the frozen <see cref="TemplateModel"/> family. The model
    /// types live in ITemplateContracts.cs; this is the behaviour that travels with them.
    /// </summary>
    public static class TemplateModelDefaults
    {
        /// <summary>
        /// Current template schema version stamped on freshly authored templates (D-13). A document that
        /// omits its <see cref="TemplateModel.Version"/> defaults to 0 on load so older/forward templates
        /// still deserialize (forward-migration), but newly authored ones carry this.
        /// </summary>
        public const int CurrentVersion = 1;

        /// <summary>
        /// Default string encoding for <see cref="KernelType.CString"/> / <see cref="KernelType.FixedChar"/>
        /// fields when the template does not name one (the D-08 grammar default).
        /// </summary>
        public const string DefaultEncoding = "ascii";

        /// <summary>
        /// Deep-copies a <see cref="TemplateModel"/> (including its full <see cref="FieldRecord"/> tree)
        /// so callers can hold a private snapshot without aliasing the source's mutable lists/maps — the
        /// same defensive-copy discipline the other Formats/* POCOs follow. Returns null for a null input.
        /// </summary>
        public static TemplateModel Clone(TemplateModel source)
        {
            if (source == null)
            {
                return null;
            }

            return new TemplateModel
            {
                Version = source.Version,
                MatchAncestorPath = source.MatchAncestorPath,
                MatchLeafTag = source.MatchLeafTag,
                MatchTagOnly = source.MatchTagOnly,
                Fields = CloneFields(source.Fields)
            };
        }

        private static List<FieldRecord> CloneFields(List<FieldRecord> fields)
        {
            if (fields == null)
            {
                return null;
            }

            var copy = new List<FieldRecord>(fields.Count);
            foreach (FieldRecord f in fields)
            {
                copy.Add(CloneField(f));
            }

            return copy;
        }

        private static FieldRecord CloneField(FieldRecord f)
        {
            if (f == null)
            {
                return null;
            }

            return new FieldRecord
            {
                Name = f.Name,
                Type = f.Type,
                ByteWidth = f.ByteWidth,
                Encoding = f.Encoding,
                Repeat = CloneRepeat(f.Repeat),
                ValueMap = CloneValueMap(f.ValueMap),
                StructFields = CloneFields(f.StructFields)
            };
        }

        private static RepeatSpec CloneRepeat(RepeatSpec r)
        {
            if (r == null)
            {
                return null;
            }

            return new RepeatSpec
            {
                Kind = r.Kind,
                FixedCount = r.FixedCount,
                CountFieldName = r.CountFieldName
            };
        }

        private static NamedValueMap CloneValueMap(NamedValueMap m)
        {
            if (m == null)
            {
                return null;
            }

            return new NamedValueMap
            {
                IsFlags = m.IsFlags,
                Entries = m.Entries == null ? null : new Dictionary<string, long>(m.Entries)
            };
        }
    }
}
