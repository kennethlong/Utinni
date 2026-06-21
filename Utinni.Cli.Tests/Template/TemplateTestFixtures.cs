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
// Composite chunk byte layout understood by reading swg-client-v2/.../sharedFile/.../Iff.cpp +
// per-engine composite writers (SOE/Bootprint, All Rights Reserved): payload scalars are LITTLE-endian
// and C-strings are NUL-terminated (strlen+1 on disk). No code, comments, identifier names, or test
// fixtures copied from any reference source. Implementation original to Utinni under MIT.

using System.Collections.Generic;
using System.IO;
using System.Text;
using UtinniCoreDotNet.Formats.Iff;

namespace Utinni.Cli.Tests.Template
{
    /// <summary>
    /// Synthesizes deterministic template-eligible FORM <c>.iff</c> fixtures THROUGH the framework IFF
    /// primitives (<see cref="MutableIffNode.NewContainer"/> + <see cref="MutableIffNode.AddContainer"/> +
    /// <see cref="MutableIffNode.AddLeaf"/> + <see cref="IffWriter.Write"/>) — the same
    /// compose-through-the-writer idiom as <c>ClefTestFixtures</c>, substituting kernel-encoded payloads.
    /// Building through the writer makes every fixture canonical-by-construction (DEC-C3).
    ///
    /// <para>The fixture root sub-type (<see cref="RootSubType"/>) is one Utinni does NOT decode with a
    /// built-in, so the inner leaves are template-eligible per the D-05 altitude rule (a template never
    /// engages where a built-in owns the root FORM). The version axis (<see cref="VersionLow"/> /
    /// <see cref="VersionHigh"/>) mirrors the CLEF-CPAP version-FORM divergence: the same leaf tag carries
    /// a wider layout at the high version, so a version-aware template picks the right layout.</para>
    /// </summary>
    internal static class TemplateTestFixtures
    {
        // Root FORM sub-type with no Utinni built-in decoder (template-eligible per D-05 altitude).
        public const string RootSubType = "TPLX";

        // The dual-lineage version-FORM axis: low ("SWGEmu-era") vs high ("Infinity-era") where the
        // layout diverges — mirrors ClefTestFixtures' CpapPayload(version) 3-layout case.
        public const string VersionLow = "0001";
        public const string VersionHigh = "0003";

        // ── little-endian primitive encoders (the kernel vocabulary, on-disk LE) ──

        private static byte[] I8(sbyte v) => new[] { (byte)v };
        private static byte[] U8(byte v) => new[] { v };
        private static byte[] I16(short v) => new[] { (byte)(v & 0xFF), (byte)((v >> 8) & 0xFF) };
        private static byte[] U16(ushort v) => new[] { (byte)(v & 0xFF), (byte)((v >> 8) & 0xFF) };

        private static byte[] I32(int v) => new[]
        {
            (byte)(v & 0xFF), (byte)((v >> 8) & 0xFF), (byte)((v >> 16) & 0xFF), (byte)((v >> 24) & 0xFF)
        };

        private static byte[] U32(uint v) => new[]
        {
            (byte)(v & 0xFF), (byte)((v >> 8) & 0xFF), (byte)((v >> 16) & 0xFF), (byte)((v >> 24) & 0xFF)
        };

        private static byte[] F32(float v) => System.BitConverter.GetBytes(v);   // host is LE
        private static byte[] F64(double v) => System.BitConverter.GetBytes(v);  // host is LE

        // NUL-terminated C-string (strlen+1 on disk, NEVER length-prefixed).
        private static byte[] CStr(string s)
        {
            byte[] body = Encoding.ASCII.GetBytes(s ?? "");
            byte[] outBytes = new byte[body.Length + 1];
            System.Array.Copy(body, outBytes, body.Length);
            outBytes[body.Length] = 0x00;
            return outBytes;
        }

        // Fixed-width char[n] (right-padded with NUL).
        private static byte[] FixedChar(string s, int n)
        {
            byte[] buf = new byte[n];
            byte[] body = Encoding.ASCII.GetBytes(s ?? "");
            System.Array.Copy(body, buf, System.Math.Min(body.Length, n));
            return buf;
        }

        private static byte[] Concat(params byte[][] parts)
        {
            using (var ms = new MemoryStream())
            {
                foreach (var p in parts) ms.Write(p, 0, p.Length);
                return ms.ToArray();
            }
        }

        // ── per-type kernel payload (one leaf carrying every scalar kind, in order) ──

        /// <summary>
        /// KERN leaf payload: i8,u8,i16,u16,i32,u32,f32,f64,cstring,char[8],raw[4],pad[2] — one of every
        /// scalar kernel type, LE on-disk. The per-type decode/encode goldens assert against these.
        /// </summary>
        public static byte[] KernPayload()
        {
            return Concat(
                I8(-5), U8(250),
                I16(-1000), U16(60000),
                I32(-123456), U32(4000000000u),
                F32(1.5f), F64(2.25),
                CStr("kernel/test.name"),
                FixedChar("CHAR8", 8),
                new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, // raw[4]
                new byte[] { 0x00, 0x00 }              // pad[2]
            );
        }

        /// <summary>
        /// CNTR leaf payload (CRITICAL count-from-prior shape, DEC-C3): a u32 count field followed by
        /// N records of {u16 id, f32 weight}. The count-recompute golden decodes, adds/removes one
        /// element, re-encodes, and asserts the count field's on-wire bytes now read N±1 AND the whole
        /// payload round-trips byte-exact.
        /// </summary>
        public static byte[] CountFromPriorPayload(int n)
        {
            var parts = new List<byte[]> { U32((uint)n) };
            for (int i = 0; i < n; i++)
            {
                parts.Add(U16((ushort)(100 + i)));
                parts.Add(F32(0.25f * (i + 1)));
            }
            return Concat(parts.ToArray());
        }

        /// <summary>
        /// FIXA leaf payload: a fixed-count array of 3 × i32 followed by a trailing-remainder array of
        /// f32 until end. Exercises both <c>FixedCount</c> and <c>UntilEnd</c> repeat kinds.
        /// </summary>
        public static byte[] FixedAndRemainderArrayPayload()
        {
            return Concat(
                I32(11), I32(22), I32(33),            // fixed-count[3]
                F32(1.0f), F32(2.0f), F32(3.0f), F32(4.0f) // until-end remainder
            );
        }

        /// <summary>
        /// PRST leaf payload: the D-09 composite presets — vector(x,y,z); quaternion(w,x,y,z, w-FIRST);
        /// matrix 3×4 row-major (12 floats); color PackedRgb (3 × u8 r,g,b).
        /// </summary>
        public static byte[] PresetPayload()
        {
            var parts = new List<byte[]>();
            // vector x,y,z
            parts.Add(F32(1.0f)); parts.Add(F32(2.0f)); parts.Add(F32(3.0f));
            // quaternion w,x,y,z (w FIRST)
            parts.Add(F32(1.0f)); parts.Add(F32(0.0f)); parts.Add(F32(0.0f)); parts.Add(F32(0.0f));
            // matrix 3x4 row-major (12 floats)
            for (int i = 0; i < 12; i++) parts.Add(F32(i));
            // color PackedRgb r,g,b
            parts.Add(U8(255)); parts.Add(U8(128)); parts.Add(U8(0));
            return Concat(parts.ToArray());
        }

        // ── compose-through-IffWriter builders ──

        private static byte[] WrapLeaf(string version, string leafTag, byte[] payload)
        {
            MutableIffNode root = MutableIffNode.NewContainer("FORM", RootSubType);
            MutableIffNode ver = root.AddContainer("FORM", version);
            ver.AddLeaf(leafTag, payload);
            return IffWriter.Write(new MutableIffDocument(root));
        }

        /// <summary>FORM TPLX { FORM &lt;version&gt; { KERN } } — every scalar kernel type.</summary>
        public static byte[] BuildKern(string version) => WrapLeaf(version, "KERN", KernPayload());

        /// <summary>FORM TPLX { FORM &lt;version&gt; { CNTR } } — the count-from-prior CRITICAL fixture.</summary>
        public static byte[] BuildCountFromPrior(string version, int n) => WrapLeaf(version, "CNTR", CountFromPriorPayload(n));

        /// <summary>FORM TPLX { FORM &lt;version&gt; { FIXA } } — fixed-count + trailing-remainder arrays.</summary>
        public static byte[] BuildArrays(string version) => WrapLeaf(version, "FIXA", FixedAndRemainderArrayPayload());

        /// <summary>FORM TPLX { FORM &lt;version&gt; { PRST } } — D-09 composite presets.</summary>
        public static byte[] BuildPresets(string version) => WrapLeaf(version, "PRST", PresetPayload());

        /// <summary>
        /// FORM TPLX { FORM &lt;version&gt; { CPAP } } — a CLEF-CPAP-style leaf whose layout WIDENS at the
        /// high version (extra trailing f32s), exercising the version-FORM-aware match. Low = 1 float;
        /// high = 5 floats — divergent layouts under the same leaf tag.
        /// </summary>
        public static byte[] BuildVersionDivergent(string version)
        {
            byte[] payload = version == VersionHigh
                ? Concat(CStr("appearance/test"), F32(2.5f), F32(0.5f), F32(1.5f), F32(1.0f), F32(2.0f))
                : Concat(CStr("appearance/test"), F32(2.5f));
            return WrapLeaf(version, "CPAP", payload);
        }

        /// <summary>
        /// A file whose ROOT FORM sub-type is a built-in-owned type (e.g. "CLEF"), used by the D-05
        /// precedence test to assert a template never engages where a built-in owns the root FORM.
        /// </summary>
        public static byte[] BuildBuiltinRootFile()
        {
            MutableIffNode root = MutableIffNode.NewContainer("FORM", "CLEF");
            MutableIffNode ver = root.AddContainer("FORM", "0001");
            ver.AddLeaf("CPAP", Concat(CStr("appearance/test"), F32(2.5f)));
            return IffWriter.Write(new MutableIffDocument(root));
        }
    }
}
