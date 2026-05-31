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
// (SOE/Bootprint, All Rights Reserved): every typed accessor falls through to the base template when
// the local param chunk is not loaded (if(!isLoaded()) return base->getXxx()). Only the on-disk layout
// and that runtime fallback rule were studied — no code, comments, identifier names, or test fixtures
// copied from any reference source. Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;

namespace UtinniCoreDotNet.Formats.ObjectTemplate
{
    /// <summary>
    /// Discriminates where an <see cref="EffectiveField"/>'s effective value came from after the
    /// DERV-chain effective-merge (D-01):
    /// <list type="bullet">
    ///   <item><see cref="LocalOverride"/> — the open template has its own local param chunk for this
    ///         field (the C++ <c>isLoaded()</c>-true case).</item>
    ///   <item><see cref="Inherited"/> — the nearest RESOLVED ancestor that supplies the field is a
    ///         base template; <see cref="EffectiveField.OriginAncestorName"/> names it.</item>
    ///   <item><see cref="UnresolvedBase"/> — the field is supplied (or would be supplied) only by a
    ///         base that could not be resolved (enumerate-only/V6000/missing); the value is unknown
    ///         and <see cref="EffectiveField.OriginAncestorName"/> names the unresolved base.</item>
    /// </list>
    /// </summary>
    public enum EffectiveFieldOriginKind
    {
        /// <summary>The open template's own local param chunk supplies the value.</summary>
        LocalOverride,
        /// <summary>A resolved ancestor template supplies the value (inherited).</summary>
        Inherited,
        /// <summary>The supplying base could not be resolved — value is unknown, never invented.</summary>
        UnresolvedBase
    }

    /// <summary>
    /// One resolved row in the effective-merged object-template view (D-01): a field name, the
    /// effective value from the nearest ancestor (including the open template) that has a local param
    /// chunk for it, and an origin marker naming that ancestor / declaring the field unresolved.
    ///
    /// <para><b>Effective semantics:</b> the value is the C# equivalent of the verified client
    /// <c>if(!field.isLoaded()) return base-&gt;getXxx()</c> per-field fallback — the value from the
    /// nearest level (the open template itself first) that physically carries the param chunk.</para>
    ///
    /// <para><b>Unresolved degradation (D-01 LOCKED):</b> an <see cref="EffectiveFieldOriginKind.UnresolvedBase"/>
    /// row carries a null <see cref="EffectiveValue"/> — the resolver never invents a value for a field
    /// that no resolvable ancestor supplies; it only records that the supplying base is unresolved.</para>
    /// </summary>
    public sealed class EffectiveField
    {
        /// <summary>The field's NUL-terminated name.</summary>
        public string FieldName { get; }

        /// <summary>
        /// The effective typed value from the nearest ancestor (incl. the open template) that has a
        /// local param chunk. Null ONLY for an <see cref="EffectiveFieldOriginKind.UnresolvedBase"/>
        /// row (the value is unknown because its supplying base could not be resolved).
        /// </summary>
        public ObjectTemplateParamValue EffectiveValue { get; }

        /// <summary>Where the effective value came from (local override / inherited / unresolved base).</summary>
        public EffectiveFieldOriginKind Origin { get; }

        /// <summary>
        /// The name of the ancestor that supplies the value: null for <see cref="EffectiveFieldOriginKind.LocalOverride"/>,
        /// the resolving base name for <see cref="EffectiveFieldOriginKind.Inherited"/>, and the
        /// unresolved base name for <see cref="EffectiveFieldOriginKind.UnresolvedBase"/>.
        /// </summary>
        public string OriginAncestorName { get; }

        internal EffectiveField(string fieldName, ObjectTemplateParamValue effectiveValue,
            EffectiveFieldOriginKind origin, string originAncestorName)
        {
            if (fieldName == null) throw new ArgumentNullException("fieldName");
            FieldName = fieldName;
            EffectiveValue = effectiveValue;
            Origin = origin;
            OriginAncestorName = originAncestorName;
        }
    }

    /// <summary>
    /// One segment of the ancestor breadcrumb (root → … → this) produced by the resolver. Each segment
    /// names a template in the DERV chain and records whether the walk could resolve it.
    /// </summary>
    public sealed class BreadcrumbSegment
    {
        /// <summary>
        /// The template's name. For the open template this is null/empty (it has no self-name in the
        /// DERV chain — it is identified by its <see cref="IsOpenTemplate"/> flag); for each ancestor
        /// it is the DERV base-template name that referenced it.
        /// </summary>
        public string Name { get; }

        /// <summary>True for the open template segment (the tail of root→this); false for ancestors.</summary>
        public bool IsOpenTemplate { get; }

        /// <summary>
        /// True when this segment's bytes were resolved + parsed; false when the base could not be
        /// resolved (enumerate-only/V6000/missing) or the depth/cycle guard stopped the walk here.
        /// </summary>
        public bool Resolved { get; }

        internal BreadcrumbSegment(string name, bool isOpenTemplate, bool resolved)
        {
            Name = name;
            IsOpenTemplate = isOpenTemplate;
            Resolved = resolved;
        }
    }

    /// <summary>
    /// The full effective-merged object-template view (D-01): one <see cref="EffectiveField"/> row per
    /// field across the resolved DERV chain, the ancestor breadcrumb (root → … → this) with each
    /// segment's resolution state, and whether the walk terminated early via the depth/cycle guard.
    /// </summary>
    public sealed class EffectiveTemplateView
    {
        private readonly List<EffectiveField> _fields;
        private readonly List<BreadcrumbSegment> _breadcrumb;

        internal EffectiveTemplateView(string rootType, string version,
            List<EffectiveField> fields, List<BreadcrumbSegment> breadcrumb, bool guardTripped)
        {
            RootType = rootType;
            Version = version;
            _fields = fields;
            _breadcrumb = breadcrumb;
            GuardTripped = guardTripped;
        }

        /// <summary>The open template's root type tag (e.g. "SHOT").</summary>
        public string RootType { get; }

        /// <summary>The open template's digit-tagged version (e.g. "0000").</summary>
        public string Version { get; }

        /// <summary>One row per field, in open-template-first then inherited discovery order.</summary>
        public IReadOnlyList<EffectiveField> Fields { get { return _fields.AsReadOnly(); } }

        /// <summary>
        /// The ancestor chain root→this. Element 0 is the most-derived OPEN template; later elements are
        /// successively-deeper ancestors. The final reachable ancestor whose base could not be resolved
        /// carries an <see cref="BreadcrumbSegment.Resolved"/>-false trailing segment.
        /// </summary>
        public IReadOnlyList<BreadcrumbSegment> Breadcrumb { get { return _breadcrumb.AsReadOnly(); } }

        /// <summary>
        /// True when the depth/cycle guard terminated the chain walk early (a cyclic DERV chain or a
        /// chain deeper than the hard cap). The remaining chain is surfaced as unresolved rather than
        /// stack-overflowing.
        /// </summary>
        public bool GuardTripped { get; }
    }
}
