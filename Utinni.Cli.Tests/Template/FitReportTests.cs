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
//
// Wave 1 (plan 23-03) Task 3 GREEN: the FitReport PURE function (D-17.2) + the TypePlausibility predicate
// library (D-17.3). Filter tokens: Template, Fit, Partial, Plausibility.

using System.Collections.Generic;
using UtinniCoreDotNet.Formats.Template;
using Xunit;

namespace Utinni.Cli.Tests.Template
{
    /// <summary>
    /// Goldens for the pure fit-check + plausibility predicate library (plan 23-03). Filter tokens live
    /// in the method names: Template, Fit, Partial, Plausibility.
    /// </summary>
    public sealed class FitReportTests
    {
        // ── FitReport consumed-exactly (D-17.2) — filter "Template&Fit" ──

        [Fact]
        [Trait("Category", "TemplateFit")]
        public void Template_Fit_FullConsumeTemplate_ReportsConsumedExactly()
        {
            byte[] payload = TemplateTestFixtures.PresetPayload();
            TemplateModel template = BuildVectorThenRemainderTemplate(consumeAll: true);

            FitReport report = FitChecker.FitCheck(template, payload);

            Assert.True(report.ConsumedExactly);
            Assert.Equal(report.BytesTotal, report.BytesConsumed);
            Assert.Equal(payload.Length, report.BytesTotal);
            Assert.NotNull(report.PerField);
        }

        // ── FitReport partial-consume — filter "Template&Fit&Partial" ──

        [Fact]
        [Trait("Category", "TemplateFitPartial")]
        public void Template_Fit_Partial_TrailingBytes_ReportsNotConsumedWithResidual()
        {
            byte[] payload = TemplateTestFixtures.PresetPayload(); // 79 bytes
            // A template that reads only the leading vector (3 x f32 = 12 bytes), leaving a large residual.
            TemplateModel template = BuildVectorOnlyTemplate();

            FitReport report = FitChecker.FitCheck(template, payload);

            Assert.False(report.ConsumedExactly);
            Assert.Equal(12, report.BytesConsumed);
            Assert.Equal(payload.Length, report.BytesTotal);
            Assert.True(report.BytesTotal - report.BytesConsumed > 0); // residual count
        }

        // ── TypePlausibility predicates (D-17.3) — filter "Template&Plausibility" ──

        [Fact]
        [Trait("Category", "TemplatePlausibility")]
        public void Template_Plausibility_LooksLikeFloat_AcceptsPlausibleRejectsGarbage()
        {
            Assert.True(TypePlausibility.LooksLikeFloat(1.5f));
            Assert.True(TypePlausibility.LooksLikeFloat(0f));
            Assert.True(TypePlausibility.LooksLikeFloat(-42.25f));

            Assert.False(TypePlausibility.LooksLikeFloat(float.NaN));
            Assert.False(TypePlausibility.LooksLikeFloat(float.PositiveInfinity));
            Assert.False(TypePlausibility.LooksLikeFloat(1e30f)); // absurd magnitude
        }

        [Fact]
        [Trait("Category", "TemplatePlausibility")]
        public void Template_Plausibility_LooksLikeCStringRun_DetectsAsciiThenNul()
        {
            Assert.True(TypePlausibility.LooksLikeCStringRun(new byte[] { 0x41, 0x42, 0x43, 0x00 })); // "ABC\0"
            Assert.True(TypePlausibility.LooksLikeCStringRun(new byte[] { 0x00 })); // empty string

            Assert.False(TypePlausibility.LooksLikeCStringRun(new byte[] { 0x41, 0x42, 0x43 })); // no NUL
            Assert.False(TypePlausibility.LooksLikeCStringRun(new byte[] { 0x01, 0x02, 0x00 })); // control bytes
            Assert.False(TypePlausibility.LooksLikeCStringRun(null));
        }

        [Fact]
        [Trait("Category", "TemplatePlausibility")]
        public void Template_Plausibility_LooksLikeCount_AcceptsSmallNonNegative()
        {
            Assert.True(TypePlausibility.LooksLikeCount(0));
            Assert.True(TypePlausibility.LooksLikeCount(3));
            Assert.True(TypePlausibility.LooksLikeCount(1000));

            Assert.False(TypePlausibility.LooksLikeCount(-1));
            Assert.False(TypePlausibility.LooksLikeCount(long.MaxValue)); // absurd cap
        }

        // ── builders ──

        private static TemplateModel BuildVectorOnlyTemplate()
        {
            return new TemplateModel
            {
                Version = 1,
                MatchLeafTag = "PRST",
                Fields = new List<FieldRecord>
                {
                    new FieldRecord { Name = "x", Type = KernelType.F32 },
                    new FieldRecord { Name = "y", Type = KernelType.F32 },
                    new FieldRecord { Name = "z", Type = KernelType.F32 }
                }
            };
        }

        // A template that consumes the whole PresetPayload (79 bytes): the leading vector then a
        // trailing-remainder of raw bytes (UntilEnd of u8) so the fit-check consumes exactly.
        private static TemplateModel BuildVectorThenRemainderTemplate(bool consumeAll)
        {
            return new TemplateModel
            {
                Version = 1,
                MatchLeafTag = "PRST",
                Fields = new List<FieldRecord>
                {
                    new FieldRecord { Name = "x", Type = KernelType.F32 },
                    new FieldRecord { Name = "y", Type = KernelType.F32 },
                    new FieldRecord { Name = "z", Type = KernelType.F32 },
                    new FieldRecord
                    {
                        Name = "rest",
                        Type = KernelType.Array,
                        Repeat = new RepeatSpec { Kind = RepeatKind.UntilEnd },
                        StructFields = new List<FieldRecord> { new FieldRecord { Name = "b", Type = KernelType.U8 } }
                    }
                }
            };
        }
    }
}
