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

using System.IO;
using System.Linq;
using CommandLine;
using UtinniCoreDotNet.Formats.Tre;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("parse-tre", HelpText = "Parse a .tre archive and emit sorted-key JSON to stdout.")]
    public class ParseTreOptions
    {
        [Value(0, MetaName = "path", Required = true, HelpText = "Path to the .tre file.")]
        public string Path { get; set; }
    }

    public static class ParseTreCommand
    {
        public static int Run(ParseTreOptions o)
        {
            // FileNotFound check: exit 3 (D-02).
            if (!File.Exists(o.Path))
            {
                return JsonOutput.EmitError("parse-tre", "FileNotFound",
                    "TRE file not found: " + o.Path, exitCode: 3);
            }

            try
            {
                var tre = TreFile.Open(o.Path);
                return JsonOutput.EmitSuccess("parse-tre", BuildResult(tre, o.Path));
            }
            catch (TreParseException ex)
            {
                // TreParseException.Kind.ToString() yields the LOCKED error.kind string.
                return JsonOutput.EmitError("parse-tre", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                // OS-level read failures (permissions, device errors, etc.).
                return JsonOutput.EmitError("parse-tre", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception is intentionally NOT caught here — unexpected
            // exception types should bubble to the top level for diagnosis.
        }

        /// <summary>
        /// Builds the LOCKED result object (REVIEWS HIGH-7 — CONTRACT-LOCKED field names).
        /// The caller (JsonOutput.EmitSuccess) wraps this in the root envelope:
        ///   { "command": "parse-tre", "result": <below>, "schemaVersion": 1 }
        /// The result object itself NEVER pre-adds schemaVersion or command (REVIEWS HIGH-6).
        /// </summary>
        private static object BuildResult(TreFile tre, string sourcePath)
        {
            return new
            {
                source = sourcePath,
                header = new
                {
                    version            = tre.Header.Version,
                    recordCount        = tre.Header.RecordCount,            // REVIEWS HIGH-7: NOT resourceCount
                    infoOffset         = tre.Header.InfoOffset,
                    infoCompression    = tre.Header.InfoCompression,
                    infoCompressedSize = tre.Header.InfoCompressedSize,
                    nameCompression    = tre.Header.NameCompression,
                    nameCompressedSize = tre.Header.NameCompressedSize,
                    nameSize           = tre.Header.NameSize
                },
                records = tre.Records.Select((r, ordinal) => new
                {
                    id               = "tre:" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture),  // REVIEWS HIGH-7: stable id
                    name             = r.Name,
                    offset           = r.Offset,                            // REVIEWS HIGH-7: NOT dataOffset
                    compressedSize   = r.CompressedSize,                    // REVIEWS HIGH-7: NOT dataCompressedSize
                    uncompressedSize = r.UncompressedSize,                  // REVIEWS HIGH-7: NOT dataSize
                    compressionKind  = r.CompressionKind,                   // REVIEWS HIGH-7: enum string "none"|"deflate" NOT int
                    checksum         = r.Checksum                           // Research §A2: reported as raw int32, not verified
                }).ToList()
            };
        }
    }
}
