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

namespace UtinniCoreDotNet.Formats.ClientEffect
{
    /// <summary>
    /// The LOCKED default field values used when ADDING a new command to a ClientEffect (REVIEWS MEDIUM #5).
    /// These constants are the SINGLE source of truth shared by every "add command" call site — the
    /// <c>apply-save-effect</c> CLI verb (Plan 02), <c>ClefFixtureBuilder</c> (Plan 01 tests), and the
    /// in-app "Add command" Form path (Plan 04) — so goldens are deterministic and the editor and CLI emit
    /// byte-identical new commands. A new command is emitted at the FILE'S EXISTING CLEF version (D-03); the
    /// version-conditional CPAP fields below are emitted only when the source version permits them.
    /// </summary>
    public static class ClefCommandDefaults
    {
        // CPAP (CreateAppearance) — version-conditional.
        public const string CpapAppearanceTemplateName = "appearance/default.prt";
        public const float CpapTimeInSeconds = 1.0f;
        public const bool CpapSoftParticleTerminate = false;          // v0002+
        public const float CpapMinScale = 1.0f;                       // v0003
        public const float CpapMaxScale = 1.0f;                       // v0003
        public const float CpapMinPlaybackRate = 1.0f;                // v0003
        public const float CpapMaxPlaybackRate = 1.0f;                // v0003

        // PSND (PlaySound).
        public const string PsndSoundTemplateName = "sound/default.snd";

        // CLGT (CreateLight).
        public const byte ClgtR = 255;
        public const byte ClgtG = 255;
        public const byte ClgtB = 255;
        public const float ClgtTimeInSeconds = 1.0f;
        public const float ClgtConstantAttenuation = 1.0f;
        public const float ClgtLinearAttenuation = 0.0f;
        public const float ClgtQuadraticAttenuation = 0.0f;
        public const float ClgtRange = 1.0f;

        // CAMS (CameraShake).
        public const float CamsMagnitude = 1.0f;
        public const float CamsFrequency = 1.0f;
        public const float CamsTime = 1.0f;
        public const float CamsFalloffRadius = 1.0f;

        // FFBK (ForceFeedback).
        public const string FfbkForceFeedbackFile = "forcefeedback/default.ffe";
        public const int FfbkIterations = 1;
        public const float FfbkRange = 1.0f;

        /// <summary>
        /// Builds the LOCKED-default command payload for the given tag at the supplied CLEF version. CPAP is
        /// version-conditional (emits only the fields the source version defines — D-03); the other four are
        /// version-stable. Throws <see cref="ClientEffectParseException"/> for an unknown tag.
        /// </summary>
        public static byte[] BuildDefaultPayload(string tag, string version)
        {
            switch (tag)
            {
                case "CPAP":
                    {
                        int v = ParseVersion(version);
                        bool? soft = v >= 2 ? (bool?)CpapSoftParticleTerminate : null;
                        float? minScale = v >= 3 ? (float?)CpapMinScale : null;
                        float? maxScale = v >= 3 ? (float?)CpapMaxScale : null;
                        float? minRate = v >= 3 ? (float?)CpapMinPlaybackRate : null;
                        float? maxRate = v >= 3 ? (float?)CpapMaxPlaybackRate : null;
                        return ClefFieldCodec.EncodeCpap(version, CpapAppearanceTemplateName, CpapTimeInSeconds,
                            soft, minScale, maxScale, minRate, maxRate);
                    }
                case "PSND":
                    return ClefFieldCodec.EncodePsnd(PsndSoundTemplateName);
                case "CLGT":
                    return ClefFieldCodec.EncodeClgt(ClgtR, ClgtG, ClgtB, ClgtTimeInSeconds,
                        ClgtConstantAttenuation, ClgtLinearAttenuation, ClgtQuadraticAttenuation, ClgtRange);
                case "CAMS":
                    return ClefFieldCodec.EncodeCams(CamsMagnitude, CamsFrequency, CamsTime, CamsFalloffRadius);
                case "FFBK":
                    return ClefFieldCodec.EncodeFfbk(FfbkForceFeedbackFile, FfbkIterations, FfbkRange);
                default:
                    throw new ClientEffectParseException(ClientEffectParseError.UnexpectedForm,
                        "Unknown CLEF command tag '" + tag + "' — cannot build a default payload.");
            }
        }

        private static int ParseVersion(string version)
        {
            int n;
            if (version == null || !int.TryParse(version, out n))
            {
                throw new ClientEffectParseException(ClientEffectParseError.VersionFieldMismatch,
                    "A numeric CLEF version tag (e.g. \"0001\") is required to build a default payload; got '"
                    + version + "'.");
            }
            return n;
        }
    }
}
