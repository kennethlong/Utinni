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
// Object-template inheritance semantics understood by reading swg-client-v2
// .../sharedObject/.../ObjectTemplate.cpp and the generated Shared*ObjectTemplate.cpp loaders
// (SOE/Bootprint, All Rights Reserved): m_baseData = ObjectTemplateList::fetch(dervName) and every
// typed accessor falls through to the base when the local param is not loaded
// (if(!isLoaded()) return base->getXxx()). Only the on-disk layout and that runtime fallback rule were
// studied — no code, comments, identifier names, or test fixtures copied from any reference source.
// Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;
using System.IO;
using UtinniCoreDotNet.Formats.Decoders;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.Tre;

namespace UtinniCoreDotNet.Formats.ObjectTemplate
{
    /// <summary>
    /// D-01 DERV-chain effective-merge resolver: walks the open template's base chain (one link at a
    /// time, via the DERV base name) and materializes one <see cref="EffectiveField"/> per field whose
    /// effective value is the value from the nearest ancestor (including the open template) that has a
    /// local param chunk — the C# equivalent of the verified client
    /// <c>if(!field.isLoaded()) return base-&gt;getXxx()</c>.
    ///
    /// <para><b>Graceful degradation (D-01 LOCKED):</b> when a base cannot be resolved (the base-locator
    /// returns null — the analog of <see cref="TrePayloadResolver.TryResolve"/> returning false for an
    /// enumerate-only / V6000 / missing archive), the open NEVER throws. The unresolved base is recorded
    /// as a trailing <see cref="BreadcrumbSegment"/> with <c>Resolved=false</c>; fields supplied only by
    /// that branch are NOT invented. The open template's local params stay present and editable.</para>
    ///
    /// <para><b>Depth/cycle guard (NEW defensive control — RESEARCH Security Domain, T-11-04):</b> the
    /// walk maintains a visited-set of resolved base names AND a hard depth cap
    /// (<see cref="MaxDepth"/>). On a revisit (cyclic DERV A→B→A) or cap-exceed, the walk stops and
    /// surfaces the remaining chain as unresolved instead of stack-overflowing.</para>
    ///
    /// <para><b>Pure-managed:</b> resolution reads base-template BYTES through a caller-supplied locator
    /// delegate so the merge logic is xUnit-testable with no TRE archive. The production entry point
    /// <see cref="ResolveViaArchive"/> wires that delegate to <see cref="TrePayloadResolver.TryResolve"/>
    /// — going EXCLUSIVELY through that path so the path-traversal / master-index-containment defense is
    /// never bypassed (T-11-05).</para>
    /// </summary>
    public static class ObjectTemplateResolver
    {
        /// <summary>
        /// Hard depth cap for the DERV chain walk. A chain deeper than this terminates with the
        /// remaining ancestors surfaced as unresolved (the cycle guard catches A→B→A sooner).
        /// </summary>
        public const int MaxDepth = 64;

        /// <summary>
        /// Resolves the effective-merged view for <paramref name="openDoc"/>. The
        /// <paramref name="baseLocator"/> maps a DERV base-template name to that base's raw <c>.iff</c>
        /// bytes, or returns null to signal an UNRESOLVED base (the D-01 degradation hook — mirrors
        /// <see cref="TrePayloadResolver.TryResolve"/> returning false). The locator must never throw
        /// for an unresolvable base; it returns null.
        /// </summary>
        /// <param name="openDoc">The open (most-derived) mutable object template.</param>
        /// <param name="baseLocator">DERV-name → base .iff bytes (null when unresolvable).</param>
        public static EffectiveTemplateView Resolve(MutableObjectTemplate openDoc, Func<string, byte[]> baseLocator)
        {
            if (openDoc == null) throw new ArgumentNullException("openDoc");
            if (baseLocator == null) throw new ArgumentNullException("baseLocator");

            // Field rows in discovery order; a dictionary tracks the first (nearest-ancestor) supplier
            // so a deeper ancestor never overrides a nearer one (nearest-wins == the isLoaded() rule).
            var rows = new List<EffectiveField>();
            var supplied = new HashSet<string>(StringComparer.Ordinal);

            var breadcrumb = new List<BreadcrumbSegment>();

            // 1. The open template's own local params are the local overrides (origin LocalOverride).
            foreach (MutableObjectTemplateParam p in openDoc.LocalParams)
            {
                if (supplied.Add(p.FieldName))
                {
                    rows.Add(new EffectiveField(p.FieldName, p.Value,
                        EffectiveFieldOriginKind.LocalOverride, null));
                }
            }
            breadcrumb.Add(new BreadcrumbSegment(null, isOpenTemplate: true, resolved: true));

            // 2. Walk the DERV chain. visited holds the resolved base names already merged; depth caps
            //    the walk. Either guard stopping the walk marks the pending base unresolved.
            bool guardTripped = false;
            string nextBaseName = openDoc.BaseTemplateName;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            int depth = 0;

            while (!string.IsNullOrEmpty(nextBaseName))
            {
                // Cycle guard: a base name we have already resolved in this walk → stop (A→B→A).
                if (visited.Contains(nextBaseName))
                {
                    guardTripped = true;
                    breadcrumb.Add(new BreadcrumbSegment(nextBaseName, isOpenTemplate: false, resolved: false));
                    break;
                }

                // Depth guard: a pathologically deep chain → stop before recursing further.
                if (depth >= MaxDepth)
                {
                    guardTripped = true;
                    breadcrumb.Add(new BreadcrumbSegment(nextBaseName, isOpenTemplate: false, resolved: false));
                    break;
                }

                byte[] baseBytes = SafeLocate(baseLocator, nextBaseName);
                if (baseBytes == null)
                {
                    // D-01 LOCKED degradation: unresolved base — never throw, never block. Record the
                    // unresolved segment; fields supplied only by this (now-unreachable) branch are not
                    // invented. A pre-declared inherited field marker for the unresolved base is added
                    // only if NOTHING nearer supplied a value (there is nothing to merge here — the
                    // value is unknown), so we simply stop the walk at the unresolved boundary.
                    breadcrumb.Add(new BreadcrumbSegment(nextBaseName, isOpenTemplate: false, resolved: false));
                    break;
                }

                ObjectTemplateView baseView = ParseBase(baseBytes);
                if (baseView == null)
                {
                    // Bytes resolved but did not parse as an object template — treat as unresolved
                    // (still never throw on the open path).
                    breadcrumb.Add(new BreadcrumbSegment(nextBaseName, isOpenTemplate: false, resolved: false));
                    break;
                }

                breadcrumb.Add(new BreadcrumbSegment(nextBaseName, isOpenTemplate: false, resolved: true));
                visited.Add(nextBaseName);

                // Merge each of this ancestor's LOCAL fields that no nearer level already supplied.
                foreach (ObjectTemplateField bf in baseView.Fields)
                {
                    if (!string.Equals(bf.InheritedFrom, "local", StringComparison.Ordinal))
                    {
                        // The @base reference row (InheritedFrom == base name) is not a field value.
                        continue;
                    }
                    if (supplied.Add(bf.Name))
                    {
                        rows.Add(new EffectiveField(bf.Name, DecodeBaseFieldValue(bf),
                            EffectiveFieldOriginKind.Inherited, nextBaseName));
                    }
                }

                nextBaseName = baseView.BaseTemplate;
                depth++;
            }

            return new EffectiveTemplateView(openDoc.RootType, openDoc.Version, rows, breadcrumb, guardTripped);
        }

        /// <summary>
        /// Production entry point: resolves the effective view by fetching base-template bytes through
        /// <see cref="TrePayloadResolver.TryResolve"/> against <paramref name="index"/>. A base name that
        /// the index does not contain, or that <see cref="TrePayloadResolver.TryResolve"/> declines
        /// (enumerate-only / V6000 / missing archive → false), degrades to an unresolved breadcrumb
        /// segment — never throws. Resolution goes EXCLUSIVELY through
        /// <see cref="TrePayloadResolver.TryResolve"/>, so the path-traversal / master-index-containment
        /// defense it enforces is never weakened (T-11-05).
        /// </summary>
        public static EffectiveTemplateView ResolveViaArchive(MutableObjectTemplate openDoc, TreArchiveIndex index)
        {
            if (openDoc == null) throw new ArgumentNullException("openDoc");
            if (index == null) throw new ArgumentNullException("index");

            Func<string, byte[]> locator = baseName =>
            {
                TreEntryDescriptor descriptor;
                if (!index.TryGetDescriptor(baseName, out descriptor))
                {
                    return null; // base not in the loaded archive set → unresolved (degrade).
                }

                byte[] bytes;
                // TrePayloadResolver.TryResolve returns false (never throws) for enumerate-only / V6000 /
                // missing archive — that false-return IS the D-01 degradation hook.
                if (!TrePayloadResolver.TryResolve(descriptor, out bytes))
                {
                    return null;
                }
                return bytes;
            };

            return Resolve(openDoc, locator);
        }

        // The locator contract is "null on unresolvable"; a misbehaving locator that throws an IO/parse
        // error must still degrade rather than crash the open. Path-traversal / integrity throws from
        // TrePayloadResolver are the one case we DO let surface (they are not the enumerate-only path).
        private static byte[] SafeLocate(Func<string, byte[]> baseLocator, string baseName)
        {
            try
            {
                return baseLocator(baseName);
            }
            catch (TreParseException)
            {
                // Integrity / path-traversal failure surfaces — these are not the graceful-degradation
                // case (TryResolve returns false for that, it does not throw).
                throw;
            }
            catch (IOException)
            {
                // A transient IO failure reading a base archive degrades to "unresolved" rather than
                // blocking the open of the (otherwise-editable) local template.
                return null;
            }
        }

        // Parses base .iff bytes into the Phase-7 read-only view. Returns null on any decode failure so
        // an unparseable base degrades to "unresolved" instead of throwing on the open path.
        private static ObjectTemplateView ParseBase(byte[] baseBytes)
        {
            try
            {
                IffDocument doc;
                using (var ms = new MemoryStream(baseBytes, writable: false))
                {
                    doc = IffReader.Read(ms);
                }
                return ObjectTemplateDecoder.Decode(doc);
            }
            catch (IffParseException)
            {
                return null;
            }
            catch (DecoderException)
            {
                return null;
            }
        }

        // The Phase-7 read-only decoder renders a local field's value as a lowercase hex string of its
        // value-region bytes. Re-decode those bytes through the self-describing codec so the effective
        // row carries a typed ObjectTemplateParamValue (consume-exactly-or-hex). On any inconsistency the
        // codec already routes to a hex-fallback value, so this never throws and never invents a type.
        private static ObjectTemplateParamValue DecodeBaseFieldValue(ObjectTemplateField field)
        {
            byte[] valueRegion = HexToBytes(field.Value);
            // Re-encode the field name + value region into a param chunk payload, then decode it through
            // the same codec the local params use, so a base-inherited field is typed identically to a
            // local one. The name+NUL framing is reconstructed; the value region is verbatim.
            byte[] chunk = BuildParamChunkPayload(field.Name, valueRegion);
            ObjectTemplateParamEntry entry = ObjectTemplateParamCodec.Decode(chunk);
            return entry.Value;
        }

        private static byte[] BuildParamChunkPayload(string fieldName, byte[] valueRegion)
        {
            byte[] nameAscii = System.Text.Encoding.ASCII.GetBytes(fieldName ?? string.Empty);
            byte[] chunk = new byte[nameAscii.Length + 1 + valueRegion.Length];
            Buffer.BlockCopy(nameAscii, 0, chunk, 0, nameAscii.Length);
            chunk[nameAscii.Length] = 0; // field-name NUL terminator
            Buffer.BlockCopy(valueRegion, 0, chunk, nameAscii.Length + 1, valueRegion.Length);
            return chunk;
        }

        private static byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return new byte[0];
            if (hex.Length % 2 != 0) return new byte[0]; // defensive — never throw on the open path.
            byte[] result = new byte[hex.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                int hi = HexNibble(hex[i * 2]);
                int lo = HexNibble(hex[i * 2 + 1]);
                if (hi < 0 || lo < 0) return new byte[0];
                result[i] = (byte)((hi << 4) | lo);
            }
            return result;
        }

        private static int HexNibble(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return 10 + (c - 'a');
            if (c >= 'A' && c <= 'F') return 10 + (c - 'A');
            return -1;
        }
    }
}
