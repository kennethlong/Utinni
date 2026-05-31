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

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.ObjectTemplate;
using Xunit;

namespace UtinniCoreDotNet.Tests.ObjectTemplate
{
    /// <summary>
    /// Coverage for the D-01 DERV-chain effective-merge resolver (11-02 Task 1):
    /// <see cref="ObjectTemplateResolver"/>. Synthetic OT fixtures are built THROUGH the framework
    /// primitives (<see cref="MutableIffNode"/> + <see cref="IffWriter"/>) so each base resolves and
    /// parses by construction, and an in-memory name→bytes dictionary stands in for the TRE archive
    /// (the production locator delegates to <c>TrePayloadResolver.TryResolve</c>). Asserts:
    /// <list type="bullet">
    ///   <item>single-level override + inherit with correct effective values + origins;</item>
    ///   <item>a 3-level chain resolving each field to its nearest supplier against a hand-computed table;</item>
    ///   <item>an unresolved base degrades (NO throw; origin UnresolvedBase; locals editable);</item>
    ///   <item>a cyclic chain terminates via the guard (test completes; remainder unresolved).</item>
    /// </list>
    /// </summary>
    public class ObjectTemplateResolverTests
    {
        // ── Synthetic-fixture builders (mirror the MutableObjectTemplateTests idiom) ──

        private static byte[] EncodeParam(string name, byte[] valueRegion)
        {
            using (var ms = new MemoryStream())
            {
                byte[] n = Encoding.ASCII.GetBytes(name);
                ms.Write(n, 0, n.Length);
                ms.WriteByte(0);
                if (valueRegion != null && valueRegion.Length > 0) ms.Write(valueRegion, 0, valueRegion.Length);
                return ms.ToArray();
            }
        }

        private static byte[] Int32Le(int v)
        {
            return new[] { (byte)(v & 0xFF), (byte)((v >> 8) & 0xFF), (byte)((v >> 16) & 0xFF), (byte)((v >> 24) & 0xFF) };
        }

        private static byte[] BoolValueRegion(bool b)
        {
            return new byte[] { 1, (byte)(b ? 1 : 0) };
        }

        private static byte[] IntValueRegion(int v, char delta)
        {
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(1);           // SINGLE tag
                ms.WriteByte((byte)delta); // delta byte
                byte[] le = Int32Le(v);
                ms.Write(le, 0, le.Length);
                return ms.ToArray();
            }
        }

        // Builds FORM <rootType> { [FORM DERV { NAME baseName }], FORM <version> { PCNT count, <params> } }.
        private static byte[] BuildTemplate(string rootType, string version, string baseName,
            IList<KeyValuePair<string, byte[]>> paramChunks)
        {
            MutableIffNode root = MutableIffNode.NewContainer("FORM", rootType);

            if (baseName != null)
            {
                MutableIffNode derv = root.AddContainer("FORM", "DERV");
                using (var ms = new MemoryStream())
                {
                    byte[] n = Encoding.ASCII.GetBytes(baseName);
                    ms.Write(n, 0, n.Length);
                    ms.WriteByte(0);
                    derv.AddLeaf("XXXX", ms.ToArray());
                }
            }

            MutableIffNode versionForm = root.AddContainer("FORM", version);
            versionForm.AddLeaf("PCNT", Int32Le(paramChunks.Count));
            foreach (KeyValuePair<string, byte[]> p in paramChunks)
            {
                versionForm.AddLeaf("XXXX", EncodeParam(p.Key, p.Value));
            }

            return IffWriter.Write(new MutableIffDocument(root));
        }

        private static MutableObjectTemplate Load(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
            {
                IffDocument doc = IffReader.Read(ms);
                MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);
                return MutableObjectTemplate.FromMutableIff(mut);
            }
        }

        private static KeyValuePair<string, byte[]> P(string name, byte[] valueRegion)
        {
            return new KeyValuePair<string, byte[]>(name, valueRegion);
        }

        private static EffectiveField Field(EffectiveTemplateView view, string name)
        {
            foreach (EffectiveField f in view.Fields)
            {
                if (f.FieldName == name) return f;
            }
            return null;
        }

        // ── Single-level: T overrides A, inherits B from base ────────────────

        [Fact]
        public void Resolve_SingleLevel_OverrideAndInherit_HaveCorrectValuesAndOrigins()
        {
            const string baseName = "object/tangible/base/shared_base.iff";

            // Base supplies A=10 and B=true.
            byte[] baseBytes = BuildTemplate("SHOT", "0000", null, new List<KeyValuePair<string, byte[]>>
            {
                P("fieldA", IntValueRegion(10, ' ')),
                P("fieldB", BoolValueRegion(true))
            });

            // Child overrides A=99, leaves B inherited.
            byte[] childBytes = BuildTemplate("SHOT", "0000", baseName, new List<KeyValuePair<string, byte[]>>
            {
                P("fieldA", IntValueRegion(99, ' '))
            });

            var locator = LocatorFor(baseName, baseBytes);
            EffectiveTemplateView view = ObjectTemplateResolver.Resolve(Load(childBytes), locator);

            EffectiveField a = Field(view, "fieldA");
            Assert.NotNull(a);
            Assert.Equal(EffectiveFieldOriginKind.LocalOverride, a.Origin);
            Assert.Equal(99, a.EffectiveValue.IntValue);

            EffectiveField b = Field(view, "fieldB");
            Assert.NotNull(b);
            Assert.Equal(EffectiveFieldOriginKind.Inherited, b.Origin);
            Assert.Equal(baseName, b.OriginAncestorName);
            Assert.True(b.EffectiveValue.BoolValue);
        }

        // ── Multi-level (child → mid → base): nearest supplier wins ──────────

        [Fact]
        public void Resolve_ThreeLevelChain_ResolvesEachFieldToNearestSupplier()
        {
            const string baseName = "object/base.iff";
            const string midName = "object/mid.iff";

            // base: x=1, y=1, z=1
            byte[] baseBytes = BuildTemplate("SHOT", "0000", null, new List<KeyValuePair<string, byte[]>>
            {
                P("x", IntValueRegion(1, ' ')),
                P("y", IntValueRegion(1, ' ')),
                P("z", IntValueRegion(1, ' '))
            });
            // mid (DERV base): overrides y=2, z=2
            byte[] midBytes = BuildTemplate("SHOT", "0000", baseName, new List<KeyValuePair<string, byte[]>>
            {
                P("y", IntValueRegion(2, ' ')),
                P("z", IntValueRegion(2, ' '))
            });
            // child (DERV mid): overrides z=3
            byte[] childBytes = BuildTemplate("SHOT", "0000", midName, new List<KeyValuePair<string, byte[]>>
            {
                P("z", IntValueRegion(3, ' '))
            });

            var locator = LocatorFor(new Dictionary<string, byte[]>
            {
                { baseName, baseBytes },
                { midName, midBytes }
            });

            EffectiveTemplateView view = ObjectTemplateResolver.Resolve(Load(childBytes), locator);

            // Hand-computed table:
            //   x → inherited from base, value 1
            //   y → inherited from mid,  value 2
            //   z → local override,      value 3
            EffectiveField x = Field(view, "x");
            Assert.Equal(EffectiveFieldOriginKind.Inherited, x.Origin);
            Assert.Equal(baseName, x.OriginAncestorName);
            Assert.Equal(1, x.EffectiveValue.IntValue);

            EffectiveField y = Field(view, "y");
            Assert.Equal(EffectiveFieldOriginKind.Inherited, y.Origin);
            Assert.Equal(midName, y.OriginAncestorName);
            Assert.Equal(2, y.EffectiveValue.IntValue);

            EffectiveField z = Field(view, "z");
            Assert.Equal(EffectiveFieldOriginKind.LocalOverride, z.Origin);
            Assert.Equal(3, z.EffectiveValue.IntValue);

            // Breadcrumb: open → mid → base, all resolved.
            Assert.Equal(3, view.Breadcrumb.Count);
            Assert.True(view.Breadcrumb[0].IsOpenTemplate);
            Assert.Equal(midName, view.Breadcrumb[1].Name);
            Assert.True(view.Breadcrumb[1].Resolved);
            Assert.Equal(baseName, view.Breadcrumb[2].Name);
            Assert.True(view.Breadcrumb[2].Resolved);
            Assert.False(view.GuardTripped);
        }

        // ── Unresolved base: NO throw; origin info; locals editable ──────────

        [Fact]
        public void Resolve_UnresolvedBase_DoesNotThrow_AndKeepsLocalParams()
        {
            const string baseName = "object/encrypted/v6000_base.iff";

            byte[] childBytes = BuildTemplate("SHOT", "0000", baseName, new List<KeyValuePair<string, byte[]>>
            {
                P("localField", IntValueRegion(42, ' '))
            });

            // Locator returns null for EVERY name — the enumerate-only / V6000 / missing analog.
            Func<string, byte[]> unresolvable = _ => null;

            MutableObjectTemplate openDoc = Load(childBytes);
            EffectiveTemplateView view = ObjectTemplateResolver.Resolve(openDoc, unresolvable);

            // The open did not throw and the local param is present + editable.
            EffectiveField local = Field(view, "localField");
            Assert.NotNull(local);
            Assert.Equal(EffectiveFieldOriginKind.LocalOverride, local.Origin);
            Assert.Equal(42, local.EffectiveValue.IntValue);

            // The unresolved base is recorded in the breadcrumb (trailing Resolved=false segment).
            BreadcrumbSegment last = view.Breadcrumb[view.Breadcrumb.Count - 1];
            Assert.Equal(baseName, last.Name);
            Assert.False(last.Resolved);

            // The open template's local params remain editable on the underlying model.
            openDoc.EditOverride("localField", ObjectTemplateParamValue.FromInt(7, (byte)' '));
            Assert.True(openDoc.IsDirty);
        }

        // ── Cyclic chain A→B→A terminates via the visited-set guard ──────────

        [Fact]
        public void Resolve_CyclicChain_TerminatesViaGuard_NoOverflow()
        {
            const string nameA = "object/a.iff";
            const string nameB = "object/b.iff";

            // A DERVs B; B DERVs A → a cycle. Each carries one local field.
            byte[] aBytes = BuildTemplate("SHOT", "0000", nameB, new List<KeyValuePair<string, byte[]>>
            {
                P("fromA", IntValueRegion(1, ' '))
            });
            byte[] bBytes = BuildTemplate("SHOT", "0000", nameA, new List<KeyValuePair<string, byte[]>>
            {
                P("fromB", IntValueRegion(2, ' '))
            });

            var locator = LocatorFor(new Dictionary<string, byte[]>
            {
                { nameA, aBytes },
                { nameB, bBytes }
            });

            // Open the template that IS A (its DERV points at B which points back at A).
            EffectiveTemplateView view = ObjectTemplateResolver.Resolve(Load(aBytes), locator);

            // The walk terminated (test completes — no StackOverflow) and flagged the guard.
            Assert.True(view.GuardTripped);

            // Both reachable fields were merged before the cycle was detected.
            Assert.NotNull(Field(view, "fromA"));   // local override on the open template
            Assert.NotNull(Field(view, "fromB"));   // inherited from B

            // The trailing breadcrumb segment marks the revisited base unresolved.
            BreadcrumbSegment last = view.Breadcrumb[view.Breadcrumb.Count - 1];
            Assert.False(last.Resolved);
        }

        // ── Depth-cap guard terminates a pathologically deep linear chain ────

        [Fact]
        public void Resolve_DeepChain_TerminatesViaDepthCap()
        {
            // Build a linear chain longer than MaxDepth: t0 → t1 → … → tN.
            int chainLength = ObjectTemplateResolver.MaxDepth + 10;
            var map = new Dictionary<string, byte[]>();
            for (int i = 0; i < chainLength; i++)
            {
                string self = "object/t" + i + ".iff";
                string next = (i + 1 < chainLength) ? "object/t" + (i + 1) + ".iff" : null;
                map[self] = BuildTemplate("SHOT", "0000", next, new List<KeyValuePair<string, byte[]>>
                {
                    P("f" + i, IntValueRegion(i, ' '))
                });
            }

            var locator = LocatorFor(map);
            EffectiveTemplateView view = ObjectTemplateResolver.Resolve(Load(map["object/t0.iff"]), locator);

            Assert.True(view.GuardTripped);
            // The depth cap stopped the walk; fields beyond the cap were not merged.
            Assert.Null(Field(view, "f" + (chainLength - 1)));
        }

        // ── Locator helpers ──────────────────────────────────────────────────

        private static Func<string, byte[]> LocatorFor(string name, byte[] bytes)
        {
            return LocatorFor(new Dictionary<string, byte[]> { { name, bytes } });
        }

        private static Func<string, byte[]> LocatorFor(IDictionary<string, byte[]> map)
        {
            return name =>
            {
                byte[] bytes;
                return map.TryGetValue(name, out bytes) ? bytes : null;
            };
        }
    }
}
