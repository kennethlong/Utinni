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
// Particle (.prt / FORM PEFT) field-value union. Layout understood by reading swg-client-v2
// .../sharedMath/.../WaveForm.cpp (control-point waveform) + .../clientParticle/.../ColorRamp.cpp
// (color-over-life ramp) + the emitter scalar/enum/bool chunks in ParticleEmitterDescription.cpp
// (SOE/Bootprint, All Rights Reserved). Only the on-disk layout was studied — no code, comments,
// identifier names, or test fixtures copied from any reference source.
// Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;

namespace UtinniCoreDotNet.Formats.Particle
{
    /// <summary>
    /// Discriminates a <see cref="ParticleFieldValue"/> concrete variant. The editor's per-field
    /// value widget switches on this; the codec switches on it to re-encode.
    /// </summary>
    public enum ParticleFieldKind
    {
        /// <summary>A typed <see cref="float"/> scalar.</summary>
        Float,
        /// <summary>A typed <see cref="int"/> scalar.</summary>
        Int,
        /// <summary>A typed <see cref="bool"/> (stored as a single byte / int on disk).</summary>
        Bool,
        /// <summary>A typed enum code (an int32 the engine reads as an enum, e.g. an interpolation type).</summary>
        Enum,
        /// <summary>A control-point waveform (sharedMath WaveForm).</summary>
        WaveForm,
        /// <summary>A color-over-life ramp (clientParticle ColorRamp).</summary>
        ColorRamp,
        /// <summary>
        /// Verbatim bytes the codec did not type — an unrecognized FORM version sub-tree, an
        /// over-length / truncated leaf, or any field the V1 typed path declines. Round-trips
        /// byte-for-byte (D-05); never mis-typed.
        /// </summary>
        RawBytesHexFallback
    }

    /// <summary>
    /// A single control point of a <see cref="ParticleFieldKind.WaveForm"/>: the four floats every
    /// known WaveForm version carries per control point. Field order matches the on-disk record;
    /// the codec preserves it verbatim.
    /// </summary>
    public sealed class WaveFormControlPoint
    {
        /// <summary>Position along the waveform (0..1 on disk).</summary>
        public float Percent { get; }
        /// <summary>The control-point value.</summary>
        public float Value { get; }
        /// <summary>The random-min delta band.</summary>
        public float RandomMin { get; }
        /// <summary>The random-max delta band.</summary>
        public float RandomMax { get; }

        /// <summary>Constructs a control point from its four floats.</summary>
        public WaveFormControlPoint(float percent, float value, float randomMin, float randomMax)
        {
            Percent = percent;
            Value = value;
            RandomMin = randomMin;
            RandomMax = randomMax;
        }
    }

    /// <summary>A typed waveform: the version-tag preserved verbatim + the per-version header scalars
    /// + the ordered control points. The codec re-emits the SAME version + scalars + points it read so
    /// a no-edit round-trip is byte-exact.</summary>
    public sealed class ParticleWaveForm
    {
        /// <summary>The 4-char version tag the waveform decoded from (e.g. "0002").</summary>
        public string Version { get; }
        /// <summary>The interpolation-type int32 (all versions).</summary>
        public int InterpolationType { get; }
        /// <summary>The sample-type int32 (versions 0001/0002; null for 0000).</summary>
        public int? SampleType { get; }
        /// <summary>The value-min float (version 0002 only; null otherwise).</summary>
        public float? ValueMin { get; }
        /// <summary>The value-max float (version 0002 only; null otherwise).</summary>
        public float? ValueMax { get; }
        /// <summary>The ordered control points, in on-disk order.</summary>
        public IReadOnlyList<WaveFormControlPoint> ControlPoints { get; }

        /// <summary>Constructs a typed waveform.</summary>
        public ParticleWaveForm(string version, int interpolationType, int? sampleType,
            float? valueMin, float? valueMax, IList<WaveFormControlPoint> controlPoints)
        {
            if (version == null) throw new ArgumentNullException("version");
            if (controlPoints == null) throw new ArgumentNullException("controlPoints");
            Version = version;
            InterpolationType = interpolationType;
            SampleType = sampleType;
            ValueMin = valueMin;
            ValueMax = valueMax;
            ControlPoints = new List<WaveFormControlPoint>(controlPoints).AsReadOnly();
        }
    }

    /// <summary>A single control point of a <see cref="ParticleFieldKind.ColorRamp"/>: percent + RGB.</summary>
    public sealed class ColorRampControlPoint
    {
        /// <summary>Position along the ramp (0..1 on disk).</summary>
        public float Percent { get; }
        /// <summary>Red component (0..1 on disk).</summary>
        public float Red { get; }
        /// <summary>Green component (0..1 on disk).</summary>
        public float Green { get; }
        /// <summary>Blue component (0..1 on disk).</summary>
        public float Blue { get; }

        /// <summary>Constructs a color-ramp control point.</summary>
        public ColorRampControlPoint(float percent, float red, float green, float blue)
        {
            Percent = percent;
            Red = red;
            Green = green;
            Blue = blue;
        }
    }

    /// <summary>A typed color ramp: version + header scalars + ordered control points (byte-exact re-emit).</summary>
    public sealed class ParticleColorRamp
    {
        /// <summary>The 4-char version tag the ramp decoded from (e.g. "0001").</summary>
        public string Version { get; }
        /// <summary>The interpolation-type uint32 (all versions).</summary>
        public long InterpolationType { get; }
        /// <summary>The sample-type uint32 (version 0001 only; null for 0000).</summary>
        public long? SampleType { get; }
        /// <summary>The ordered control points, in on-disk order.</summary>
        public IReadOnlyList<ColorRampControlPoint> ControlPoints { get; }

        /// <summary>Constructs a typed color ramp.</summary>
        public ParticleColorRamp(string version, long interpolationType, long? sampleType,
            IList<ColorRampControlPoint> controlPoints)
        {
            if (version == null) throw new ArgumentNullException("version");
            if (controlPoints == null) throw new ArgumentNullException("controlPoints");
            Version = version;
            InterpolationType = interpolationType;
            SampleType = sampleType;
            ControlPoints = new List<ColorRampControlPoint>(controlPoints).AsReadOnly();
        }
    }

    /// <summary>
    /// Discriminated value carrier for a single particle field — the managed analog of the typed
    /// fields the engine loaders read out of an EMTR / leaf chunk. The closed variant set is
    /// <see cref="ParticleFieldKind.Float"/> / <see cref="ParticleFieldKind.Int"/> /
    /// <see cref="ParticleFieldKind.Bool"/> / <see cref="ParticleFieldKind.Enum"/> /
    /// <see cref="ParticleFieldKind.WaveForm"/> / <see cref="ParticleFieldKind.ColorRamp"/> /
    /// <see cref="ParticleFieldKind.RawBytesHexFallback"/>.
    ///
    /// <para><b>Degrade-don't-abort (D-05):</b> any field the V1 typed path cannot safely interpret —
    /// most importantly an unrecognized FORM version sub-tree, but also an over-length / truncated leaf
    /// — is carried as <see cref="ParticleFieldKind.RawBytesHexFallback"/> via
    /// <see cref="FromRawBytes"/>, which captures the EXACT original bytes so they round-trip
    /// byte-for-byte. The codec never FATALs the way the SOE reference does.</para>
    /// </summary>
    public sealed class ParticleFieldValue
    {
        private readonly float _floatValue;
        private readonly int _intValue;
        private readonly bool _boolValue;
        private readonly int _enumValue;
        private readonly ParticleWaveForm _waveForm;
        private readonly ParticleColorRamp _colorRamp;
        private readonly byte[] _rawBytes;

        private ParticleFieldValue(
            ParticleFieldKind kind,
            float floatValue,
            int intValue,
            bool boolValue,
            int enumValue,
            ParticleWaveForm waveForm,
            ParticleColorRamp colorRamp,
            byte[] rawBytes)
        {
            Kind = kind;
            _floatValue = floatValue;
            _intValue = intValue;
            _boolValue = boolValue;
            _enumValue = enumValue;
            _waveForm = waveForm;
            _colorRamp = colorRamp;
            _rawBytes = rawBytes;
        }

        /// <summary>The concrete variant discriminator.</summary>
        public ParticleFieldKind Kind { get; }

        /// <summary>Creates a typed float value.</summary>
        public static ParticleFieldValue FromFloat(float value)
        {
            return new ParticleFieldValue(ParticleFieldKind.Float, value, 0, false, 0, null, null, null);
        }

        /// <summary>Creates a typed int value.</summary>
        public static ParticleFieldValue FromInt(int value)
        {
            return new ParticleFieldValue(ParticleFieldKind.Int, 0f, value, false, 0, null, null, null);
        }

        /// <summary>Creates a typed bool value.</summary>
        public static ParticleFieldValue FromBool(bool value)
        {
            return new ParticleFieldValue(ParticleFieldKind.Bool, 0f, 0, value, 0, null, null, null);
        }

        /// <summary>Creates a typed enum-code value (an int32 the engine reads as an enum).</summary>
        public static ParticleFieldValue FromEnum(int code)
        {
            return new ParticleFieldValue(ParticleFieldKind.Enum, 0f, 0, false, code, null, null, null);
        }

        /// <summary>Creates a typed waveform value.</summary>
        public static ParticleFieldValue FromWaveForm(ParticleWaveForm waveForm)
        {
            if (waveForm == null) throw new ArgumentNullException("waveForm");
            return new ParticleFieldValue(ParticleFieldKind.WaveForm, 0f, 0, false, 0, waveForm, null, null);
        }

        /// <summary>Creates a typed color-ramp value.</summary>
        public static ParticleFieldValue FromColorRamp(ParticleColorRamp colorRamp)
        {
            if (colorRamp == null) throw new ArgumentNullException("colorRamp");
            return new ParticleFieldValue(ParticleFieldKind.ColorRamp, 0f, 0, false, 0, null, colorRamp, null);
        }

        /// <summary>
        /// Creates a raw-bytes hex-fallback value carrying the verbatim bytes (the load-bearing D-05
        /// path). The bytes are defensively copied and round-trip byte-for-byte on encode.
        /// </summary>
        public static ParticleFieldValue FromRawBytes(byte[] bytes)
        {
            byte[] copy = (bytes == null) ? new byte[0] : (byte[])bytes.Clone();
            return new ParticleFieldValue(ParticleFieldKind.RawBytesHexFallback, 0f, 0, false, 0, null, null, copy);
        }

        /// <summary>The wrapped float (Float variant only).</summary>
        public float FloatValue { get { RequireKind(ParticleFieldKind.Float); return _floatValue; } }

        /// <summary>The wrapped int (Int variant only).</summary>
        public int IntValue { get { RequireKind(ParticleFieldKind.Int); return _intValue; } }

        /// <summary>The wrapped bool (Bool variant only).</summary>
        public bool BoolValue { get { RequireKind(ParticleFieldKind.Bool); return _boolValue; } }

        /// <summary>The wrapped enum code (Enum variant only).</summary>
        public int EnumValue { get { RequireKind(ParticleFieldKind.Enum); return _enumValue; } }

        /// <summary>The wrapped typed waveform (WaveForm variant only).</summary>
        public ParticleWaveForm WaveForm { get { RequireKind(ParticleFieldKind.WaveForm); return _waveForm; } }

        /// <summary>The wrapped typed color ramp (ColorRamp variant only).</summary>
        public ParticleColorRamp ColorRamp { get { RequireKind(ParticleFieldKind.ColorRamp); return _colorRamp; } }

        /// <summary>Returns a defensive copy of the verbatim bytes (RawBytesHexFallback only).</summary>
        public byte[] GetRawBytesCopy()
        {
            RequireKind(ParticleFieldKind.RawBytesHexFallback);
            return (byte[])_rawBytes.Clone();
        }

        /// <summary>The UI Type-column label for the per-field value widget.</summary>
        public string FieldTypeLabel
        {
            get
            {
                switch (Kind)
                {
                    case ParticleFieldKind.Float: return "float";
                    case ParticleFieldKind.Int: return "int";
                    case ParticleFieldKind.Bool: return "bool";
                    case ParticleFieldKind.Enum: return "enum";
                    case ParticleFieldKind.WaveForm: return "waveform";
                    case ParticleFieldKind.ColorRamp: return "color ramp";
                    case ParticleFieldKind.RawBytesHexFallback: return "raw bytes (hex)";
                    default: return "(unknown)";
                }
            }
        }

        private void RequireKind(ParticleFieldKind expected)
        {
            if (Kind != expected)
            {
                throw new InvalidOperationException(
                    "Particle field value is a " + Kind + " variant, not " + expected + ".");
            }
        }
    }
}
