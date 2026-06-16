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
// Terrain (.trn / FORM TGEN) palette model understood by reading swg-client-v2
// .../sharedTerrain/.../generator/{TerrainGenerator,ShaderGroup,FloraGroup,RadialGroup,EnvironmentGroup,
// FractalGroup,BitmapGroup}.{cpp,h} (SOE/Bootprint, All Rights Reserved): the six palettes load in a
// FIXED order (shader/flora/radial/environment/fractal/bitmap), each an OPTIONAL form, and FractalGroup +
// BitmapGroup BOTH use the MGRP tag — disambiguated by load-order POSITION, not by tag (Pitfall 4). Only
// the on-disk layout was studied — no code, comments, identifier names, or test fixtures copied from any
// reference source. Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;

namespace UtinniCoreDotNet.Formats.Terrain
{
    /// <summary>
    /// The six shared read-only palettes in their FIXED positional load order (D-04). Each slot is
    /// OPTIONAL (Pitfall 3) — an absent slot has <see cref="TerrainPalette.Present"/> = <c>false</c> and
    /// does NOT shift later slots. Because <c>FractalGroup</c> and <c>BitmapGroup</c> share the <c>MGRP</c>
    /// tag, when only ONE MGRP is present the bound slot is marked <see cref="TerrainPalette.Ambiguous"/>
    /// rather than guessing fractal-vs-bitmap (concern #3). familyIds are preserved verbatim — never
    /// renumbered on save.
    /// </summary>
    public sealed class TerrainPalettes
    {
        /// <summary>Slot 1 — Shader palette (<c>SGRP</c>), referenced by ASCN/ASRP via familyId.</summary>
        public TerrainPalette Shader { get; }

        /// <summary>Slot 2 — Flora palette (<c>FGRP</c>).</summary>
        public TerrainPalette Flora { get; }

        /// <summary>Slot 3 — Radial (flora) palette (<c>RGRP</c>).</summary>
        public TerrainPalette Radial { get; }

        /// <summary>Slot 4 — Environment palette (<c>EGRP</c>), referenced by AENV.</summary>
        public TerrainPalette Environment { get; }

        /// <summary>Slot 5 — Fractal palette (<c>MGRP</c>, first by load order).</summary>
        public TerrainPalette Fractal { get; }

        /// <summary>Slot 6 — Bitmap palette (<c>MGRP</c>, second by load order).</summary>
        public TerrainPalette Bitmap { get; }

        /// <summary>The six slots in fixed load order (shader, flora, radial, environment, fractal, bitmap).</summary>
        public IReadOnlyList<TerrainPalette> InLoadOrder { get; }

        /// <summary>Constructs the positional palette set.</summary>
        public TerrainPalettes(
            TerrainPalette shader,
            TerrainPalette flora,
            TerrainPalette radial,
            TerrainPalette environment,
            TerrainPalette fractal,
            TerrainPalette bitmap)
        {
            Shader = shader ?? throw new ArgumentNullException(nameof(shader));
            Flora = flora ?? throw new ArgumentNullException(nameof(flora));
            Radial = radial ?? throw new ArgumentNullException(nameof(radial));
            Environment = environment ?? throw new ArgumentNullException(nameof(environment));
            Fractal = fractal ?? throw new ArgumentNullException(nameof(fractal));
            Bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
            InLoadOrder = new List<TerrainPalette> { Shader, Flora, Radial, Environment, Fractal, Bitmap }.AsReadOnly();
        }

        /// <summary>An all-absent palette set (every slot Present=false) — the minimal-TGEN result.</summary>
        public static TerrainPalettes AllAbsent()
        {
            return new TerrainPalettes(
                TerrainPalette.Absent("Shader"),
                TerrainPalette.Absent("Flora"),
                TerrainPalette.Absent("Radial"),
                TerrainPalette.Absent("Environment"),
                TerrainPalette.Absent("Fractal"),
                TerrainPalette.Absent("Bitmap"));
        }
    }

    /// <summary>
    /// One read-only palette slot: its fixed-load-order role name, a Present flag, an Ambiguous flag
    /// (set when a single MGRP could be fractal OR bitmap — concern #3), and the familyId → name entries
    /// (verbatim familyIds, never renumbered).
    /// </summary>
    public sealed class TerrainPalette
    {
        /// <summary>The fixed-load-order role name ("Shader" / "Flora" / "Radial" / "Environment" / "Fractal" / "Bitmap").</summary>
        public string Role { get; }

        /// <summary>True iff this slot's optional form was present in the file.</summary>
        public bool Present { get; }

        /// <summary>True iff this slot's identity (fractal-vs-bitmap) is ambiguous (single-MGRP case — concern #3).</summary>
        public bool Ambiguous { get; }

        /// <summary>familyId → name entries, in file order. familyIds are verbatim — never renumbered (D-04).</summary>
        public IReadOnlyList<TerrainPaletteFamily> Families { get; }

        /// <summary>Constructs a palette slot.</summary>
        public TerrainPalette(string role, bool present, bool ambiguous, IReadOnlyList<TerrainPaletteFamily> families)
        {
            Role = role ?? throw new ArgumentNullException(nameof(role));
            Present = present;
            Ambiguous = ambiguous;
            Families = families ?? new List<TerrainPaletteFamily>().AsReadOnly();
        }

        /// <summary>An absent slot for the given role (Present=false, no families).</summary>
        public static TerrainPalette Absent(string role)
            => new TerrainPalette(role, present: false, ambiguous: false, families: new List<TerrainPaletteFamily>().AsReadOnly());

        /// <summary>A present slot for the given role with the given families and ambiguity flag.</summary>
        public static TerrainPalette Of(string role, IReadOnlyList<TerrainPaletteFamily> families, bool ambiguous = false)
            => new TerrainPalette(role, present: true, ambiguous: ambiguous, families: families);
    }

    /// <summary>One palette entry: a verbatim familyId and its human name.</summary>
    public sealed class TerrainPaletteFamily
    {
        /// <summary>The verbatim familyId (never renumbered on save — D-04).</summary>
        public int FamilyId { get; }

        /// <summary>The family's human name, or empty when none was stored.</summary>
        public string Name { get; }

        /// <summary>Constructs a palette family entry.</summary>
        public TerrainPaletteFamily(int familyId, string name)
        {
            FamilyId = familyId;
            Name = name ?? string.Empty;
        }
    }
}
