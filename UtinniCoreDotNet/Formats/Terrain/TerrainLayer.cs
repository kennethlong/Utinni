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
// Terrain (.trn / FORM TGEN) layer model understood by reading swg-client-v2
// .../sharedTerrain/.../generator/TerrainGenerator.cpp (LayerItem::load active flag + name at the IHDR
// leaf; Layer::load recursion over child boundaries/filters/affectors/sub-layers) (SOE/Bootprint, All
// Rights Reserved) and the EA-IFF-85 public standard. Only the on-disk layout was studied — no code,
// comments, identifier names, or test fixtures copied from any reference source. Implementation original
// to Utinni under MIT.

using System;
using System.Collections.Generic;

namespace UtinniCoreDotNet.Formats.Terrain
{
    /// <summary>
    /// A decoded terrain layer (FORM <c>LAYR</c>): its name + active flag (read from the <c>IHDR</c>
    /// layer-item header leaf — concern #10 read↔write parity), its directly-contained boundary/filter/
    /// affector <see cref="Nodes"/>, and any nested sub-layers (<see cref="SubLayers"/>, recursion). A
    /// sealed, constructor-injected, get-only DTO (the immutable-view shape).
    ///
    /// <para><b>v1 envelope scope (TRN-01):</b> the navigable layer exposes only <see cref="Name"/> +
    /// <see cref="Active"/> at the layer level — the richer LAYR fields (invertFilters/expanded/notes that
    /// higher versions add) are not surfaced in the v1 envelope.</para>
    /// </summary>
    public sealed class TerrainLayer
    {
        /// <summary>The layer name (from the IHDR leaf), or empty string when no name was stored.</summary>
        public string Name { get; }

        /// <summary>The layer active flag (from the IHDR int32 — the SAME leaf Plan 03 <c>--field active</c> mutates).</summary>
        public bool Active { get; }

        /// <summary>The physical-path stable id of this layer's FORM node (concern #14).</summary>
        public string StableIdPath { get; }

        /// <summary>This layer's directly-contained boundary/filter/affector nodes (typed / raw / dead).</summary>
        public IReadOnlyList<TerrainNode> Nodes { get; }

        /// <summary>Nested sub-layers (the recursion — a LAYR may contain child LAYR forms).</summary>
        public IReadOnlyList<TerrainLayer> SubLayers { get; }

        /// <summary>Constructs an immutable layer view.</summary>
        public TerrainLayer(
            string name,
            bool active,
            string stableIdPath,
            IReadOnlyList<TerrainNode> nodes,
            IReadOnlyList<TerrainLayer> subLayers)
        {
            Name = name ?? string.Empty;
            Active = active;
            StableIdPath = stableIdPath;
            Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            SubLayers = subLayers ?? throw new ArgumentNullException(nameof(subLayers));
        }
    }
}
