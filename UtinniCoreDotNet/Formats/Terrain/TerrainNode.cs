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
// Terrain (.trn / FORM TGEN) node model understood by reading swg-client-v2
// .../sharedTerrain/.../generator/{TerrainGeneratorLoader,AffectorHeight,Boundary,Filter}.{cpp,h}
// (SOE/Bootprint, All Rights Reserved) and the EA-IFF-85 public standard. Only the on-disk layout was
// studied — no code, comments, identifier names, or test fixtures copied from any reference source.
// Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;
using System.Linq;

namespace UtinniCoreDotNet.Formats.Terrain
{
    /// <summary>One decoded field of a typed terrain leaf: a name, its decoded value rendered to a string,
    /// and the display type that produced it (so the navigable envelope can label it).</summary>
    public sealed class TerrainField
    {
        /// <summary>Field name (matches the <see cref="TgenFieldDescriptor.Name"/> it was decoded from).</summary>
        public string Name { get; }

        /// <summary>The decoded value, rendered to a string (float / int / enum / familyId).</summary>
        public string Value { get; }

        /// <summary>The display classification this value was decoded under.</summary>
        public TgenDisplayType DisplayType { get; }

        /// <summary>True iff this field may be edited by Plan 03's <c>apply-save-trn</c>.</summary>
        public bool Editable { get; }

        /// <summary>Constructs a decoded field row.</summary>
        public TerrainField(string name, string value, TgenDisplayType displayType, bool editable)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value;
            DisplayType = displayType;
            Editable = editable;
        }
    }

    /// <summary>
    /// A single boundary / filter / affector node decoded from a layer. EXACTLY ONE of three mutually
    /// exclusive states (review concern #4/#8):
    /// <list type="bullet">
    ///   <item><b>Typed</b> — a complete descriptor existed AND consumed-length == payload-length; carries
    ///         named <see cref="TypedFields"/>; <see cref="IsEditable"/> is <c>true</c>.</item>
    ///   <item><b>Raw-preserved</b> (<see cref="IsRawPreserved"/>) — unknown FourCC, unrecognized version,
    ///         truncated, or trailing-byte mismatch; carries <see cref="RawHex"/>; non-editable.</item>
    ///   <item><b>Dead-skipped</b> (<see cref="IsDeadSkipped"/>) — a recognized obsolete tag (D-03);
    ///         display-only ("obsolete, ignored"); non-editable.</item>
    /// </list>
    /// Every node carries its physical <see cref="StableIdPath"/> derived from the DOM walk INCLUDING
    /// raw/dead siblings, so ordinals never drift across decode modes (concern #14).
    /// </summary>
    public sealed class TerrainNode
    {
        /// <summary>The 4-char FourCC tag of this node (e.g. "AHCN", "BALL", "ZZZZ").</summary>
        public string Tag { get; }

        /// <summary>The FORM version string this node was decoded at (e.g. "0000"), or null when unknown.</summary>
        public string Version { get; }

        /// <summary>The named typed fields (non-empty only in the Typed state).</summary>
        public IReadOnlyList<TerrainField> TypedFields { get; }

        /// <summary>True iff this node is in the raw-preserved state (unknown / unrecognized / truncated).</summary>
        public bool IsRawPreserved { get; }

        /// <summary>True iff this node is a recognized-and-skipped DEAD/obsolete tag (display-only).</summary>
        public bool IsDeadSkipped { get; }

        /// <summary>A lowercase hex rendering of the raw payload (non-null only in the raw-preserved state).</summary>
        public string RawHex { get; }

        /// <summary>The physical-path stable id of this node (concern #14 — includes raw/dead siblings).</summary>
        public string StableIdPath { get; }

        /// <summary>True iff this node carries editable typed fields. False for raw-preserved AND dead-skipped.</summary>
        public bool IsEditable => !IsRawPreserved && !IsDeadSkipped;

        private TerrainNode(
            string tag,
            string version,
            IReadOnlyList<TerrainField> typedFields,
            bool isRawPreserved,
            bool isDeadSkipped,
            string rawHex,
            string stableIdPath)
        {
            Tag = tag;
            Version = version;
            TypedFields = typedFields ?? new List<TerrainField>().AsReadOnly();
            IsRawPreserved = isRawPreserved;
            IsDeadSkipped = isDeadSkipped;
            RawHex = rawHex;
            StableIdPath = stableIdPath;
        }

        /// <summary>Builds a TYPED node (complete descriptor, consumed == payload). Editable.</summary>
        public static TerrainNode Typed(string tag, string version, IReadOnlyList<TerrainField> fields, string stableIdPath)
        {
            if (tag == null) throw new ArgumentNullException(nameof(tag));
            if (fields == null) throw new ArgumentNullException(nameof(fields));
            return new TerrainNode(tag, version, fields, false, false, null, stableIdPath);
        }

        /// <summary>Builds a RAW-PRESERVED node (unknown / unrecognized version / truncated). Non-editable.</summary>
        public static TerrainNode RawPreserved(string tag, string version, byte[] rawBytes, string stableIdPath)
        {
            if (tag == null) throw new ArgumentNullException(nameof(tag));
            return new TerrainNode(tag, version, null, true, false, ToHex(rawBytes), stableIdPath);
        }

        /// <summary>Builds a DEAD-SKIPPED node (recognized obsolete tag, display-only). Non-editable.</summary>
        public static TerrainNode DeadSkipped(string tag, string version, string stableIdPath)
        {
            if (tag == null) throw new ArgumentNullException(nameof(tag));
            return new TerrainNode(tag, version, null, false, true, null, stableIdPath);
        }

        private static string ToHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return string.Empty;
            return string.Concat(bytes.Select(b => b.ToString("x2")));
        }
    }
}
