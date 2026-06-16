// Format understood by reading swg-client-v2/src/engine/shared/library/sharedTerrain/src/shared/generator/
// {TerrainGenerator,TerrainGeneratorType,TerrainGeneratorLoader,AffectorHeight,AffectorShader,Boundary,Filter,
//  ShaderGroup,FractalGroup,BitmapGroup}.{cpp,h,def} (SOE/Bootprint, All Rights Reserved) and the EA-IFF-85
// public standard. Per Pitfall 6 (20-RESEARCH.md): IFF chunk tags+lengths are big-endian, but chunk PAYLOAD
// scalars are little-endian on the original Win32 client — this synthesizer emits framing via IffWriter
// (big-endian, no trailing pad) and packs DATA payload scalars little-endian to match the IffPayloadCursor
// read convention. No code, comments, identifier names, or test fixtures copied from any reference source.
// Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UtinniCoreDotNet.Formats.Iff;

namespace Utinni.Cli.Tests.Fixtures.Trn
{
    /// <summary>
    /// Hand-emits ≤200-byte <c>FORM TGEN</c> byte arrays by composing a <see cref="MutableIffDocument"/>
    /// tree and serializing it through <see cref="IffWriter"/> (D-12). This is the COMMITTED golden corpus
    /// for the terrain (<c>.trn</c>) codec: tiny, deterministic, and isolating the SWGEmu-vs-Infinity
    /// version matrix without shipping any real (large / v6000+ encrypted) retail asset (D-14).
    ///
    /// <para><b>Both-lineage matrix (review concerns #11/#13):</b> for EVERY Tier-1 tag in
    /// <see cref="TgenEraVersions.Tier1Tags"/> the synthesizer can emit a LOW-version fixture (version =
    /// <see cref="TgenEraVersions.SwgEmuEra"/>[tag]) and a HIGH-version fixture (version =
    /// <see cref="TgenEraVersions.InfinityEra"/>[tag]) — the full low+high matrix is built HERE in Wave 0
    /// so downstream plans assert against the complete matrix in wave order, not against assumed-then-pinned
    /// versions landing last.</para>
    ///
    /// <para><b>Structure emitted (per 20-RESEARCH.md taxonomy + palette load order):</b>
    /// <c>FORM TGEN</c> → <c>FORM 0000</c> (the single-version TGEN body) → optional palette FORMs in the
    /// fixed load order (shader/flora/radial/environment/fractal/bitmap) → optional <c>LYRS</c> →
    /// <c>LAYR</c> FORMs → typed boundary/filter/affector FORMs, each shaped
    /// <c>FORM &lt;tag&gt; → FORM &lt;version&gt; → DATA</c> (the ubiquitous leaf). DATA payload scalars are
    /// LITTLE-endian; all FORM/chunk framing is big-endian via <see cref="IffWriter"/> (no trailing pad).</para>
    ///
    /// <para><b>Budget:</b> every emitted fixture is ≤200 bytes (committed-golden budget). The
    /// <see cref="AssertAllFixturesWellFramed"/> self-test enumerates the full matrix and asserts each fixture
    /// re-parses via <see cref="IffReader"/> without throwing AND is ≤200 bytes (mitigation T-20-01 — a
    /// mis-framed fixture would mask real codec bugs downstream).</para>
    /// </summary>
    public static class TgenFixtureSynthesizer
    {
        /// <summary>The committed-golden byte budget per fixture.</summary>
        public const int MaxFixtureBytes = 200;

        // ─────────────────────────────────────────────────────────────────────
        // Little-endian DATA payload scalar primitives (Pitfall 6).
        // ─────────────────────────────────────────────────────────────────────

        private static byte[] Int32Le(int v) => new[]
        {
            (byte)(v & 0xFF),
            (byte)((v >> 8) & 0xFF),
            (byte)((v >> 16) & 0xFF),
            (byte)((v >> 24) & 0xFF),
        };

        private static byte[] FloatLe(float v)
        {
            byte[] b = BitConverter.GetBytes(v);
            if (!BitConverter.IsLittleEndian) Array.Reverse(b);
            return b;
        }

        private static byte[] CString(string s)
        {
            byte[] text = Encoding.ASCII.GetBytes(s ?? string.Empty);
            var result = new byte[text.Length + 1];
            Array.Copy(text, result, text.Length);
            result[text.Length] = 0x00; // NUL terminator
            return result;
        }

        private static byte[] Concat(params byte[][] parts)
        {
            using (var ms = new MemoryStream())
            {
                foreach (var p in parts)
                    if (p != null && p.Length > 0) ms.Write(p, 0, p.Length);
                return ms.ToArray();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Tree-build helpers — compose a MutableIffDocument and serialize via IffWriter.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Adds a typed-tag form <c>FORM &lt;tag&gt; → FORM &lt;version&gt; → DATA(payload)</c> under the
        /// given parent container. The version is supplied as a 4-char zero-padded FORM sub-type
        /// (e.g. "0000"); the DATA payload is the little-endian-packed scalar blob for that tag/version.
        /// </summary>
        private static void AddTypedForm(MutableIffNode parent, string tag, string version, byte[] dataPayload)
        {
            MutableIffNode tagForm = parent.AddContainer("FORM", tag);
            MutableIffNode versionForm = tagForm.AddContainer("FORM", version);
            versionForm.AddLeaf("DATA", dataPayload ?? new byte[0]);
        }

        // Default tiny payloads per Tier-1 tag (small, deterministic, version-aware where the lineage diverges).
        // These are STRUCTURAL placeholders — the typed-field decode is Wave 1; here we only need well-framed,
        // byte-budgeted DATA leaves that re-parse and exercise the both-lineage version path.
        private static byte[] DefaultPayloadFor(string tag, string version)
        {
            switch (tag)
            {
                // Affectors — operation enum (int32) + one scalar; high version adds a feather scalar.
                case "AHCN": // height constant: [operation][height]
                    return WithHighFeather(version, Concat(Int32Le(1), FloatLe(12.5f)));
                case "AHTR": // height terrace: [fraction][height]
                    return WithHighFeather(version, Concat(FloatLe(0.5f), FloatLe(8.0f)));
                case "ACCN": // color constant: [r][g][b]
                    return WithHighFeather(version, Concat(Int32Le(0x20), Int32Le(0x40), Int32Le(0x60)));
                case "ACRH": // color ramp height: [low][high]
                    return WithHighFeather(version, Concat(FloatLe(0.0f), FloatLe(64.0f)));
                case "ASCN": // shader constant: [familyId]; v0001 adds feather-override
                    return WithHighFeather(version, Int32Le(3));
                case "ASRP": // shader replace: [srcFamily][dstFamily]
                    return WithHighFeather(version, Concat(Int32Le(1), Int32Le(2)));
                case "AFCN":
                case "AFSC": // flora static collidable constant: [familyId][density]
                    return WithHighFeather(version, Concat(Int32Le(5), FloatLe(0.25f)));
                case "AFSN": // flora static non-collidable constant: [familyId][density]
                    return WithHighFeather(version, Concat(Int32Le(6), FloatLe(0.10f)));

                // Boundaries — BCIR v0000 has NO feather; v0001+ carries feather fn + distance.
                case "BCIR": // [centerX][centerZ][radius] (+ feather at high)
                    return WithHighFeather(version, Concat(FloatLe(100f), FloatLe(200f), FloatLe(50f)));
                case "BREC": // [x0][z0][x1][z1] (+ feather at high)
                    return WithHighFeather(version, Concat(FloatLe(0f), FloatLe(0f), FloatLe(10f), FloatLe(10f)));

                // Filters — FHGT/FSLP low/high height + feather.
                case "FHGT": // [lowHeight][highHeight] (+ feather at high)
                    return WithHighFeather(version, Concat(FloatLe(-5f), FloatLe(5f)));
                case "FSLP": // [minSlope][maxSlope] (+ feather at high)
                    return WithHighFeather(version, Concat(FloatLe(0f), FloatLe(1f)));

                default:
                    // Unknown/long-tail: a tiny opaque blob (raw-fallback target shape).
                    return new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            }
        }

        // High-version (Infinity-era) arms add a feather-function enum + feather distance, mirroring the
        // research "higher FORM versions add feather-override/feather-function fields" divergence. Arms whose
        // version is the baseline "0000" get no extra bytes — proving the version-dispatch path can differ
        // between lineages. (Real observation: SWGEmu and Infinity share versions for all but BREC, so most
        // arms emit identical bytes; this synthetic feather-add still exercises the divergence-capable path.)
        private static byte[] WithHighFeather(string version, byte[] lowPayload)
        {
            bool isHigh = !string.Equals(version, "0000", StringComparison.Ordinal);
            if (!isHigh) return lowPayload;
            return Concat(lowPayload, Int32Le(0) /* feather fn */, FloatLe(0.2f) /* feather distance */);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public matrix builders (named in the plan <behavior>).
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A ≤200-byte <c>FORM TGEN</c> (version <c>0000</c>) with NO palettes and NO <c>LYRS</c> — legal
        /// per Pitfall 3 (every palette + LYRS is an optional form).
        /// </summary>
        public static byte[] MinimalTgen()
        {
            MutableIffNode tgen = MutableIffNode.NewContainer("FORM", "TGEN");
            tgen.AddContainer("FORM", "0000");
            return Emit(tgen);
        }

        /// <summary>
        /// A <c>FORM TGEN</c> whose body contains exactly ONE typed tag form at the requested version,
        /// wrapped in a single <c>LYRS</c> → <c>LAYR</c>. The DATA payload is the default little-endian
        /// blob for the tag/version pair. Reads its version from <see cref="TgenEraVersions"/> via the
        /// low/high convenience methods below; this generic form takes an explicit version.
        /// </summary>
        public static byte[] WithTypedTag(string tag, string version, byte[] dataPayload = null)
        {
            if (tag == null) throw new ArgumentNullException(nameof(tag));
            if (version == null) throw new ArgumentNullException(nameof(version));
            MutableIffNode tgen = MutableIffNode.NewContainer("FORM", "TGEN");
            MutableIffNode body = tgen.AddContainer("FORM", "0000");
            MutableIffNode lyrs = body.AddContainer("FORM", "LYRS");
            MutableIffNode layr = lyrs.AddContainer("FORM", TgenEraVersions.SwgEmuEra["LAYR"]);
            AddTypedForm(layr, tag, version, dataPayload ?? DefaultPayloadFor(tag, version));
            return Emit(tgen);
        }

        /// <summary>Convenience: emit the LOW ("SWGEmu-era") fixture for a Tier-1 tag.</summary>
        public static byte[] LowVersion(string tag) => WithTypedTag(tag, TgenEraVersions.Low(tag));

        /// <summary>Convenience: emit the HIGH ("Infinity-era") fixture for a Tier-1 tag.</summary>
        public static byte[] HighVersion(string tag) => WithTypedTag(tag, TgenEraVersions.High(tag));

        /// <summary>
        /// Emit an affector fixture (height/color/shader/flora) at the given era's version. <paramref name="era"/>
        /// is one of <see cref="TgenEraVersions.SwgEmuEra"/> / <see cref="TgenEraVersions.InfinityEra"/>.
        /// </summary>
        public static byte[] WithAffector(string tag, IReadOnlyDictionary<string, string> era)
            => WithTypedTag(tag, era[tag]);

        /// <summary>
        /// Emit a boundary fixture. <c>WithBoundary("BCIR", SwgEmuEra)</c> emits a v0000 BoundaryCircle
        /// WITHOUT feather; <c>WithBoundary("BCIR", InfinityEra)</c> emits a feather-bearing version
        /// (proves the version-divergence path).
        /// </summary>
        public static byte[] WithBoundary(string tag, IReadOnlyDictionary<string, string> era)
            => WithTypedTag(tag, era[tag]);

        /// <summary>Emit a filter fixture (FHGT/FSLP) at the given era's version.</summary>
        public static byte[] WithFilter(string tag, IReadOnlyDictionary<string, string> era)
            => WithTypedTag(tag, era[tag]);

        /// <summary>
        /// A <c>FORM TGEN</c> containing a leaf-bearing form whose FourCC is OUTSIDE the Tier-1 set
        /// (raw-fallback target). The tag is carried as a typed-form-shaped <c>FORM &lt;tag&gt; → FORM 0000
        /// → DATA</c> so the decoder meets an unrecognized-but-well-framed node.
        /// </summary>
        public static byte[] WithUnknownTag(string tag = "ZZZZ")
        {
            MutableIffNode tgen = MutableIffNode.NewContainer("FORM", "TGEN");
            MutableIffNode body = tgen.AddContainer("FORM", "0000");
            MutableIffNode lyrs = body.AddContainer("FORM", "LYRS");
            MutableIffNode layr = lyrs.AddContainer("FORM", TgenEraVersions.SwgEmuEra["LAYR"]);
            AddTypedForm(layr, tag, "0000", new byte[] { 0x01, 0x02, 0x03, 0x04 });
            return Emit(tgen);
        }

        /// <summary>
        /// A <c>FORM TGEN</c> containing a DEAD-tag form (recognized-and-skipped target — D-03). The DEAD
        /// tags (<c>BALL</c>/<c>BSPL</c>/<c>AHSM</c>/<c>AHBM</c>/<c>ACBM</c>/<c>ASBM</c>/<c>AFBM</c>) are
        /// consumed by the loader without reading payload; emit as a bare <c>FORM &lt;tag&gt;</c> with an
        /// empty body so the decoder must recognize-and-skip, never raw-fallback it as editable.
        /// </summary>
        public static byte[] WithDeadTag(string tag = "BALL")
        {
            MutableIffNode tgen = MutableIffNode.NewContainer("FORM", "TGEN");
            MutableIffNode body = tgen.AddContainer("FORM", "0000");
            MutableIffNode lyrs = body.AddContainer("FORM", "LYRS");
            MutableIffNode layr = lyrs.AddContainer("FORM", TgenEraVersions.SwgEmuEra["LAYR"]);
            // DEAD form: the loader enters+exits without reading a payload. Emit an empty inner version form.
            MutableIffNode deadForm = layr.AddContainer("FORM", tag);
            deadForm.AddContainer("FORM", "0000");
            return Emit(tgen);
        }

        /// <summary>
        /// A <c>FORM TGEN</c> with a KNOWN Tier-1 tag whose DATA leaf is cut off mid-field — a truncated
        /// raw-fallback / non-editable target (review concern #4/#17). The DATA payload is shorter than the
        /// tag's known field list, so the typed decoder must degrade to raw rather than over-read.
        /// </summary>
        public static byte[] WithTruncatedKnownTag(string tag = "AHCN")
        {
            // AHCN's known low-version layout is [int32 operation][float height] = 8 bytes; emit only 3.
            byte[] truncated = new byte[] { 0x01, 0x00, 0x00 };
            return WithTypedTag(tag, TgenEraVersions.Low(tag), truncated);
        }

        /// <summary>
        /// ONE <c>LAYR</c> containing, in order, an <c>AHCN</c> typed form, a <c>BALL</c> DEAD tag, a
        /// <c>ZZZZ</c> unknown tag, and a <c>BCIR</c> typed form — the adjacency fixture for review
        /// concern #16 (a DEAD/unknown sibling must not corrupt the byte-exact re-emit of an edited typed
        /// neighbour).
        /// </summary>
        public static byte[] CompositionalLayer()
        {
            MutableIffNode tgen = MutableIffNode.NewContainer("FORM", "TGEN");
            MutableIffNode body = tgen.AddContainer("FORM", "0000");
            MutableIffNode lyrs = body.AddContainer("FORM", "LYRS");
            MutableIffNode layr = lyrs.AddContainer("FORM", TgenEraVersions.SwgEmuEra["LAYR"]);

            AddTypedForm(layr, "AHCN", TgenEraVersions.Low("AHCN"), DefaultPayloadFor("AHCN", TgenEraVersions.Low("AHCN")));
            // DEAD BALL (empty body)
            MutableIffNode dead = layr.AddContainer("FORM", "BALL");
            dead.AddContainer("FORM", "0000");
            // Unknown ZZZZ
            AddTypedForm(layr, "ZZZZ", "0000", new byte[] { 0x09, 0x08 });
            // Typed BCIR (low / no feather)
            AddTypedForm(layr, "BCIR", TgenEraVersions.Low("BCIR"), DefaultPayloadFor("BCIR", TgenEraVersions.Low("BCIR")));

            return Emit(tgen);
        }

        /// <summary>
        /// A <c>FORM TGEN</c> whose single layer carries an <c>IHDR</c> layer-item-header DATA leaf
        /// (<c>[int32 active][cstring name]</c>) followed by one typed <c>AHCN</c> affector. This is the
        /// active-flag read↔write parity fixture (Plan 03 <c>--field active</c>, concern #10): the decoder
        /// reads <see cref="UtinniCoreDotNet.Formats.Terrain.TerrainLayer.Active"/> from the SAME IHDR int32
        /// the edit path mutates. <paramref name="active"/> seeds the on-disk flag; <paramref name="name"/>
        /// the layer name.
        /// </summary>
        public static byte[] WithLayerHeader(bool active = true, string name = "layer0")
        {
            MutableIffNode tgen = MutableIffNode.NewContainer("FORM", "TGEN");
            MutableIffNode body = tgen.AddContainer("FORM", "0000");
            MutableIffNode lyrs = body.AddContainer("FORM", "LYRS");
            MutableIffNode layr = lyrs.AddContainer("FORM", TgenEraVersions.SwgEmuEra["LAYR"]);

            // IHDR layer-item header: [int32 active][cstring name] (the decoder reads active @ offset 0).
            MutableIffNode ihdr = layr.AddContainer("FORM", "IHDR");
            ihdr.AddLeaf("DATA", Concat(Int32Le(active ? 1 : 0), CString(name)));

            // One typed AHCN affector so the layer has an editable typed sibling too.
            AddTypedForm(layr, "AHCN", TgenEraVersions.Low("AHCN"), DefaultPayloadFor("AHCN", TgenEraVersions.Low("AHCN")));
            return Emit(tgen);
        }

        /// <summary>
        /// A <c>FORM TGEN</c> whose single <c>LAYR</c> carries <paramref name="siblingCount"/> SAME-TAG
        /// (<c>AHCN</c>) sibling forms so node ordinals reach double digits — the CR-01 prefix-collision
        /// regression shape. Because every sibling is tag <c>AHCN</c>, the node at LAYR-child ordinal 1 has
        /// stable id <c>.../FORM:AHCN/1</c>, which is a bare-string PREFIX of the leaf id under the node at
        /// ordinal 10 (<c>.../FORM:AHCN/10/FORM:0000/0/DATA:DATA/0</c>) — an unanchored <c>StartsWith</c> gate
        /// resolves a leaf in node-10 to node-1's editability.
        ///
        /// <para>The sibling at <paramref name="nonEditableOrdinal"/> is emitted with a TRUNCATED payload
        /// (shorter than AHCN's known 8-byte layout) so it raw-falls-back to NON-editable; EVERY other sibling
        /// gets a valid 8-byte payload so it decodes typed + editable. Pass <c>nonEditableOrdinal = 1</c> and a
        /// count ≥ 11 to make node-1 (non-editable) collide with node-10 (editable).</para>
        ///
        /// <para>NOT part of the ≤200-byte committed-golden self-test — this is a focused regression fixture
        /// built large on purpose.</para>
        /// </summary>
        public static byte[] WithManySameTagSiblings(int siblingCount, int nonEditableOrdinal)
        {
            if (siblingCount < 1) throw new ArgumentOutOfRangeException(nameof(siblingCount));
            MutableIffNode tgen = MutableIffNode.NewContainer("FORM", "TGEN");
            MutableIffNode body = tgen.AddContainer("FORM", "0000");
            MutableIffNode lyrs = body.AddContainer("FORM", "LYRS");
            MutableIffNode layr = lyrs.AddContainer("FORM", TgenEraVersions.SwgEmuEra["LAYR"]);

            // The LAYR-body child ordinal of each sibling IS its add order (0-based). All siblings share the
            // AHCN tag so their stable-id prefixes collide on ordinal substrings; one sibling is deliberately
            // truncated (raw-fallback / non-editable) so editability differs across the colliding pair.
            string version = TgenEraVersions.Low("AHCN");
            for (int i = 0; i < siblingCount; i++)
            {
                byte[] payload = (i == nonEditableOrdinal)
                    ? new byte[] { 0x01, 0x00, 0x00 }                     // truncated -> raw-fallback, non-editable
                    : Concat(Int32Le(i), FloatLe(i + 0.5f));              // valid AHCN [operation][height] -> editable
                AddTypedForm(layr, "AHCN", version, payload);
            }
            return Emit(tgen);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Self-test (NON-Skipped) — mitigation T-20-01.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Enumerates the FULL both-lineage matrix plus the minimal/unknown/dead/truncated/compositional
        /// fixtures and asserts each one (a) re-parses via <see cref="IffReader"/> without throwing AND
        /// (b) is ≤ <see cref="MaxFixtureBytes"/> bytes. Returns the (name → byte-length) map so the caller
        /// can assert the matrix is non-empty and covers every Tier-1 tag at low+high. Throws on any
        /// mis-framed or over-budget fixture.
        /// </summary>
        public static IReadOnlyDictionary<string, int> AssertAllFixturesWellFramed()
        {
            var lengths = new Dictionary<string, int>(StringComparer.Ordinal);

            void Check(string name, byte[] bytes)
            {
                if (bytes.Length > MaxFixtureBytes)
                    throw new InvalidOperationException(
                        "Fixture '" + name + "' is " + bytes.Length + " bytes — exceeds the "
                        + MaxFixtureBytes + "-byte committed-golden budget.");
                // Re-parse: a mis-framed fixture throws here (IffParseException) and fails the self-test.
                using (var ms = new MemoryStream(bytes))
                {
                    IffReader.Read(ms);
                }
                lengths[name] = bytes.Length;
            }

            Check("MinimalTgen", MinimalTgen());

            foreach (string tag in TgenEraVersions.Tier1Tags)
            {
                Check(tag + ":low", LowVersion(tag));
                Check(tag + ":high", HighVersion(tag));
            }

            Check("UnknownTag:ZZZZ", WithUnknownTag());
            Check("DeadTag:BALL", WithDeadTag());
            Check("TruncatedKnownTag:AHCN", WithTruncatedKnownTag());
            Check("CompositionalLayer", CompositionalLayer());

            return lengths;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Emit primitive.
        // ─────────────────────────────────────────────────────────────────────

        private static byte[] Emit(MutableIffNode root)
        {
            return IffWriter.Write(new MutableIffDocument(root));
        }
    }
}
