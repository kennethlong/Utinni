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

using System;
using System.Collections.Generic;
using System.IO;
using CommandLine;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Formats.Tre;
using UtinniCoreDotNet.Saving;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("repack-tre", HelpText = "Destructively repack a .tre archive (full rebuild), taking a timestamped backup first. SEPARATE from save (D-10).")]
    public class RepackTreOptions
    {
        [Value(0, MetaName = "path", Required = true, HelpText = "Path to the .tre archive to repack in place.")]
        public string Path { get; set; }
    }

    /// <summary>
    /// Plan 13-03 (D-10): the destructive <c>.tre</c> repack as its OWN explicit verb — NEVER reachable
    /// from <c>save</c>, so the default write surface stays safe-by-construction. Takes a timestamped
    /// backup (<see cref="TreBackupPath.NextAvailable"/>) before overwriting, probes the
    /// <see cref="TreRepackLock"/> so a live-client-held archive is refused rather than corrupted, and
    /// refuses V6000/encrypted (enumerate-only) archives (<see cref="TreWriter.Repack"/> throws
    /// <see cref="NotSupportedException"/>) → exit 2. The result envelope is the same locked shape as
    /// <c>save</c>, with <c>backupPath</c> POPULATED.
    ///
    /// <para>Exit codes: 0 ok; 2 parse/lock/not-supported error; 3 file-not-found.</para>
    /// </summary>
    public static class RepackTreCommand
    {
        public static int Run(RepackTreOptions o)
        {
            if (!File.Exists(o.Path))
            {
                return JsonOutput.EmitError("repack-tre", "FileNotFound", ".tre archive not found: " + o.Path, exitCode: 3);
            }

            try
            {
                TreFile original = TreFile.Open(o.Path);

                // Full-rebuild repack with NO edits (pure round-trip). V6000/encrypted archives throw
                // NotSupportedException BEFORE any filesystem write — refuse, never corrupt.
                byte[] repacked = TreWriter.Repack(original, new Dictionary<int, byte[]>());

                // Refuse if the archive is held open by another process (the live client) — never
                // overwrite a file in use (08-REVIEWS MEDIUM-10).
                TreRepackLock.ProbeResult probe = TreRepackLock.Probe(o.Path);
                if (probe == TreRepackLock.ProbeResult.SharingViolation)
                {
                    return JsonOutput.EmitError("repack-tre", "FileInUse",
                        "archive is held open by another process (the live client?); save as a loose override instead: " + o.Path,
                        exitCode: 2);
                }
                if (probe == TreRepackLock.ProbeResult.OtherIoError)
                {
                    return JsonOutput.EmitError("repack-tre", "IoError",
                        "could not acquire exclusive access to: " + o.Path, exitCode: 2);
                }

                // Backup BEFORE the destructive overwrite (repack already succeeded into memory).
                string backupPath = TreBackupPath.NextAvailable(o.Path, DateTime.Now);
                File.Copy(o.Path, backupPath, overwrite: false);

                WriteAtomic(o.Path, repacked);

                bool validated = Reparses(repacked, original.Records.Count);

                var result = new JObject
                {
                    ["backupPath"] = backupPath,
                    ["bytesWritten"] = repacked.Length,
                    ["path"] = o.Path,
                    ["validated"] = validated,
                    ["written"] = true
                };
                return JsonOutput.EmitSuccess("repack-tre", result);
            }
            catch (NotSupportedException ex)
            {
                return JsonOutput.EmitError("repack-tre", "NotSupported", ex.Message, exitCode: 2);
            }
            catch (TreParseException ex)
            {
                return JsonOutput.EmitError("repack-tre", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("repack-tre", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception is intentionally NOT caught.
        }

        private static bool Reparses(byte[] bytes, int expectedRecordCount)
        {
            try
            {
                using (var ms = new MemoryStream(bytes, writable: false))
                {
                    TreFile rt = TreFile.Open(ms);
                    return rt.Records.Count == expectedRecordCount;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void WriteAtomic(string path, byte[] bytes)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true);
            }
        }
    }
}
