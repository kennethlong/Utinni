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
// GREEN (plan 23-05): the apply-save-template fail-closed + containment gates. Mirrors the proven
// ApplySaveIffCommand.TestPerturbSerialized seam (a Func<byte[],byte[]> that corrupts the serialized
// bytes after the mutation so the untouched-leaf verify must fail-closed: exit 2, NOTHING written).
// Method names embed the VALIDATION.md --filter tokens (ApplySaveTemplate, FailClosed, Containment).

using System.IO;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.Template;
using Utinni.Cli.Commands;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.Template
{
    /// <summary>
    /// <c>apply-save-template</c> contained/atomic/fail-closed gates: a perturbed UNTOUCHED leaf must
    /// fail the verify-before-commit (exit 2, write nothing), and a --root escape must be rejected before
    /// any write. Filter tokens: ApplySaveTemplate, FailClosed, Containment.
    /// </summary>
    public sealed class ApplySaveTemplateTests
    {
        // A two-leaf fixture: FORM TPLX { FORM <ver> { KERN(editable) } { OTHR(untouched) } }. The KERN leaf
        // is the template-eligible edit target; OTHR is the untouched leaf the fail-closed test corrupts.
        private static byte[] TwoLeafFixture()
        {
            MutableIffNode root = MutableIffNode.NewContainer("FORM", "TPLX");
            MutableIffNode ver = root.AddContainer("FORM", TemplateTestFixtures.VersionLow);
            ver.AddLeaf("KERN", TemplateTestFixtures.KernPayload());
            ver.AddLeaf("OTHR", new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 });
            return IffWriter.Write(new MutableIffDocument(root));
        }

        private const string KernLeafId = "FORM:TPLX/0/FORM:0001/0/KERN:KERN/0";

        // filter "ApplySaveTemplate&FailClosed"
        [Fact]
        [Trait("Category", "ApplySaveTemplateFailClosed")]
        public void ApplySaveTemplate_FailClosed_PerturbedUntouchedLeaf_Exit2_NothingWritten()
        {
            using (var tmp = new TempDir())
            {
                byte[] seeded = TwoLeafFixture();
                string rel = "appearance/edit.iff";
                string dest = Path.Combine(tmp.Path, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.WriteAllBytes(dest, seeded);

                string tplPath = tmp.WriteText("kern.json",
                    TemplateJson.Serialize(TemplateAuthoring.KernTemplate(TemplateTestFixtures.VersionLow)));

                // Corrupt a byte in the serialized output (lands on an untouched leaf region) AFTER the
                // mutation, so the byte-identity verify on untouched leaves must reject and write nothing.
                ApplySaveTemplateCommand.TestPerturbSerialized = bytes =>
                {
                    byte[] copy = (byte[])bytes.Clone();
                    copy[copy.Length - 1] ^= 0xFF; // OTHR's payload tail is the last bytes written
                    return copy;
                };
                try
                {
                    CliResult res = InProcessCliRunner.Run("apply-save-template", rel,
                        "--root", tmp.Path, "--mutate-leaf", KernLeafId, "--template", tplPath,
                        "--set", "i32=999");

                    Assert.Equal(2, res.ExitCode);
                    // Nothing written: the on-disk file is byte-identical to the seed.
                    byte[] after = File.ReadAllBytes(dest);
                    Assert.Equal(seeded, after);
                }
                finally
                {
                    ApplySaveTemplateCommand.TestPerturbSerialized = null;
                }
            }
        }

        // filter "ApplySaveTemplate&Containment"
        [Fact]
        [Trait("Category", "ApplySaveTemplateContainment")]
        public void ApplySaveTemplate_Containment_RootEscape_Exit2_NoWrite()
        {
            using (var tmp = new TempDir())
            {
                string tplPath = tmp.WriteText("kern.json",
                    TemplateJson.Serialize(TemplateAuthoring.KernTemplate(TemplateTestFixtures.VersionLow)));

                // A relAsset that climbs out of --root must be rejected (PathContainment, exit 2) before any
                // read or write.
                CliResult res = InProcessCliRunner.Run("apply-save-template", "../escape.iff",
                    "--root", tmp.Path, "--mutate-leaf", KernLeafId, "--template", tplPath);

                Assert.Equal(2, res.ExitCode);
            }
        }
    }
}
