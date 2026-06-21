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
// RED-via-Skip (Wave 0, plan 23-01): the apply-save-template fail-closed gate. Mirrors the proven
// ApplySaveIffCommand.TestPerturbSerialized seam (a Func<byte[],byte[]> that corrupts the serialized
// bytes after the mutation so the untouched-leaf verify must fail-closed: exit 2, NOTHING written).
// Method names embed the VALIDATION.md --filter tokens (ApplySaveTemplate, FailClosed).

using Xunit;

namespace Utinni.Cli.Tests.Template
{
    /// <summary>
    /// <c>apply-save-template</c> fail-closed gate: with the serialized bytes perturbed on an UNTOUCHED
    /// leaf, the verb's verify-before-commit must reject (exit 2) and write nothing. Filter tokens:
    /// ApplySaveTemplate, FailClosed. RED-via-Skip pending the Wave 1/2 apply-save-template verb.
    /// </summary>
    public sealed class ApplySaveTemplateTests
    {
        private const string RedFailClosed = "RED — Wave 1/2 ships apply-save-template + the TestPerturbSerialized seam; tracking PROD-IFFT-02";

        // filter "ApplySaveTemplate&FailClosed"
        [Fact(Skip = RedFailClosed)]
        [Trait("Category", "ApplySaveTemplateFailClosed")]
        public void ApplySaveTemplate_FailClosed_PerturbedUntouchedLeaf_Exit2_NothingWritten()
        {
            // Seed a loose-override .iff; install TestPerturbSerialized to corrupt an untouched leaf in
            // the serialized output; run apply-save-template with one template-field mutation; assert
            // exit code 2 and the on-disk file is byte-identical to the seeded original (no write).
            Assert.True(false, "pending Wave 1/2 apply-save-template fail-closed verify");
        }
    }
}
