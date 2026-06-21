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
// D-17.2 fit-check as a PURE function: FitChecker.FitCheck(template, payloadBytes) -> FitReport. No file
// I/O, no mutation of the inputs. The make-or-break signal is ConsumedExactly (a correct template
// consumes the whole payload and no more); PerField carries a soft per-field plausibility battery driven
// by the standalone TypePlausibility predicate library. The same pure function backs both the Tier-B live
// round-trip indicator (D-07) AND the future Tier-C corpus scoring -- baking the reuse in at zero
// speculative cost (D-17). The FitReport POCO itself is frozen in ITemplateContracts.cs.

using System.Collections.Generic;
using UtinniCoreDotNet.Formats.Decoders;

namespace UtinniCoreDotNet.Formats.Template
{
    /// <summary>
    /// The pure fit-check (D-17.2). <see cref="FitCheck"/> runs the kernel decode, catching a truncation
    /// (a template that over-reads the payload), and reports whether the template consumed the payload
    /// exactly plus a per-field plausibility verdict. No side effects, no I/O -- safe to call repeatedly
    /// (the Tier-B live indicator) and reusable by Tier-C scoring.
    /// </summary>
    public static class FitChecker
    {
        /// <summary>
        /// Reports how exactly <paramref name="template"/> fits <paramref name="payload"/>. A clean decode
        /// that lands on the end of the payload reports <see cref="FitReport.ConsumedExactly"/> = true; a
        /// decode that leaves trailing bytes reports false with <see cref="FitReport.BytesConsumed"/> &lt;
        /// <see cref="FitReport.BytesTotal"/>; a decode that over-reads (truncation) reports false with the
        /// full payload length as the total and the consume count clamped to it. Pure: no mutation, no I/O.
        /// </summary>
        public static FitReport FitCheck(TemplateModel template, byte[] payload)
        {
            byte[] bytes = payload ?? new byte[0];
            var perField = new List<FieldPlausibility>();

            try
            {
                int consumed;
                int total;
                DecodedTemplate decoded = KernelCodec.DecodeInternal(template, bytes, out consumed, out total);

                BuildPlausibility(template, decoded, perField);

                // WR-05: a template with no fields decodes to consumed == total == 0 against an empty
                // payload, which would report a vacuous green "fits". A zero-field template defines no
                // layout, so it is NEVER a real fit -- treat it as does-not-fit regardless of byte counts
                // so the live indicator (and Tier-C scoring) cannot be fooled by an empty layout.
                bool hasFields = template != null && template.Fields != null && template.Fields.Count > 0;

                return new FitReport
                {
                    ConsumedExactly = hasFields && consumed == total,
                    BytesConsumed = consumed,
                    BytesTotal = total,
                    PerField = perField
                };
            }
            catch (DecoderException)
            {
                // The template over-read the payload (claimed more bytes than exist): it does NOT fit.
                // Report the full payload length as the total; no per-field battery on a failed decode.
                return new FitReport
                {
                    ConsumedExactly = false,
                    BytesConsumed = bytes.Length,
                    BytesTotal = bytes.Length,
                    PerField = perField
                };
            }
        }

        // Walks the decoded top-level fields, attaching a plausibility verdict per field via the standalone
        // TypePlausibility predicates. Only top-level scalar fields get a verdict here (the soft Tier-B/-C
        // signal); structs/arrays are summarized by their presence (Plausible = true when decoded).
        private static void BuildPlausibility(TemplateModel template, DecodedTemplate decoded,
            List<FieldPlausibility> into)
        {
            if (template == null || template.Fields == null || decoded == null || decoded.Values == null)
            {
                return;
            }

            foreach (FieldRecord f in template.Fields)
            {
                if (f == null || string.IsNullOrEmpty(f.Name))
                {
                    continue;
                }

                object value;
                if (!decoded.Values.TryGetValue(f.Name, out value))
                {
                    continue;
                }

                into.Add(new FieldPlausibility
                {
                    FieldName = f.Name,
                    Plausible = IsPlausible(f, value)
                });
            }
        }

        private static bool IsPlausible(FieldRecord f, object value)
        {
            switch (f.Type)
            {
                case KernelType.F32:
                    return value is float && TypePlausibility.LooksLikeFloat((float)value);
                case KernelType.F64:
                    return value is double && TypePlausibility.LooksLikeFloat((double)value);
                case KernelType.CString:
                    return value is string; // a decoded C-string is well-formed by construction
                case KernelType.I8:
                case KernelType.U8:
                case KernelType.I16:
                case KernelType.U16:
                case KernelType.I32:
                case KernelType.U32:
                    // An integer decoded from its fixed-width bytes is always byte-valid; it is plausible
                    // by construction. (A field the author marks as a count gets the LooksLikeCount soft
                    // signal at the resolver/Tier-C layer that knows which fields are counts.)
                    return value is long;
                default:
                    // Struct / Array / FixedChar / RawBytes / Pad: decoded successfully => structurally fits.
                    return value != null;
            }
        }
    }
}
