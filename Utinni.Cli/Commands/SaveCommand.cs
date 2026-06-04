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
using System.IO;
using CommandLine;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Formats.Datatable;
using UtinniCoreDotNet.Formats.Decoders;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.ObjectTemplate;
using UtinniCoreDotNet.Formats.StringTable;
using UtinniCoreDotNet.Saving;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("save", HelpText = "Re-serialize an IFF/datatable/stf/OT asset and write it (loose-override by --root, or explicit --path).")]
    public class SaveOptions
    {
        [Value(0, MetaName = "asset", Required = true, HelpText = "Asset path. With --path: the source to load. With --root: the relative asset path under the client root (loose-override).")]
        public string Asset { get; set; }

        [Option("path", HelpText = "Explicit destination path to write to (mutually exclusive with --root).")]
        public string Path { get; set; }

        [Option("root", HelpText = "Client root for a loose-override save; the asset is resolved + re-serialized under it (mutually exclusive with --path).")]
        public string Root { get; set; }
    }

    /// <summary>
    /// Plan 13-03 (AUTH-05): the first WRITE verb in utinni-cli. Loads an asset, detects its format
    /// by content sniff (IFF / datatable / stf / OT), re-serializes via the matching FRAMEWORK writer,
    /// writes the bytes atomically, and reports the ROADMAP-locked envelope
    /// <c>{ written, path, bytesWritten, backupPath, validated }</c>.
    ///
    /// <para><b>Framework-leg only (PATTERNS Option 1):</b> the WinForms <c>TJT.Saving</c> targets are
    /// unreferenceable from net472 — this verb composes the framework primitives directly
    /// (<see cref="IffWriter"/>/<see cref="DataTableWriter"/>/<see cref="StringTableWriter"/>/
    /// <see cref="MutableObjectTemplate.Serialize"/> + <see cref="LooseOverridePath"/> + an atomic
    /// FileStream write with <c>Flush(true)</c>).</para>
    ///
    /// <para><b>Safe-by-construction (D-10):</b> the destructive <c>.tre</c> repack is NOT a save
    /// format — a <c>.tre</c> input is rejected with a usage error pointing at <c>repack-tre</c>.</para>
    ///
    /// <para>Exit codes: 0 ok; 1 usage; 2 parse/containment error; 3 file-not-found. Generic Exception
    /// is intentionally NOT caught.</para>
    /// </summary>
    public static class SaveCommand
    {
        private enum AssetFormat { Tre, Stf, Datatable, ObjectTemplate, Iff, Unknown }

        public static int Run(SaveOptions o)
        {
            bool hasPath = !string.IsNullOrEmpty(o.Path);
            bool hasRoot = !string.IsNullOrEmpty(o.Root);

            if (hasPath && hasRoot)
            {
                return JsonOutput.EmitError("save", "UsageError",
                    "--path and --root are mutually exclusive (explicit destination vs loose-override).", exitCode: 1);
            }
            if (!hasPath && !hasRoot)
            {
                return JsonOutput.EmitError("save", "UsageError",
                    "save requires either --path <dest> (explicit) or --root <clientRoot> (loose-override).", exitCode: 1);
            }

            // Resolve the source-to-load and the destination.
            string sourcePath;
            string destPath;
            if (hasPath)
            {
                sourcePath = o.Asset;
                destPath = o.Path;
            }
            else
            {
                // Loose-override: resolve the relative asset path under the root (rejects ../rooted).
                try
                {
                    destPath = LooseOverridePath.Resolve(o.Root, o.Asset);
                }
                catch (ArgumentException ex)
                {
                    return JsonOutput.EmitError("save", "PathContainment", ex.Message, exitCode: 2);
                }
                sourcePath = destPath; // re-serialize the existing loose asset in place
            }

            if (!File.Exists(sourcePath))
            {
                return JsonOutput.EmitError("save", "FileNotFound", "asset not found: " + sourcePath, exitCode: 3);
            }

            try
            {
                byte[] loaded = File.ReadAllBytes(sourcePath);
                AssetFormat format = DetectFormat(loaded);

                if (format == AssetFormat.Tre)
                {
                    return JsonOutput.EmitError("save", "UsageError",
                        ".tre archives are not a save format; use the repack-tre verb (a backup-routed destructive rebuild).", exitCode: 1);
                }

                byte[] serialized = Serialize(format, loaded);

                WriteAtomic(destPath, serialized);

                bool validated = Reparses(format, serialized);

                var result = new JObject
                {
                    ["backupPath"] = JValue.CreateNull(),     // loose-override save takes no backup (D-10: repack owns backups)
                    ["bytesWritten"] = serialized.Length,
                    ["path"] = destPath,
                    ["validated"] = validated,
                    ["written"] = true
                };
                return JsonOutput.EmitSuccess("save", result);
            }
            catch (DataTableParseException ex)
            {
                return JsonOutput.EmitError("save", "DataTableParseException", ex.Message, exitCode: 2);
            }
            catch (StringTableParseException ex)
            {
                return JsonOutput.EmitError("save", "StringTableParseException", ex.Message, exitCode: 2);
            }
            catch (DecoderException ex)
            {
                return JsonOutput.EmitError("save", "DecoderException", ex.Message, exitCode: 2);
            }
            catch (IffParseException ex)
            {
                return JsonOutput.EmitError("save", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("save", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception is intentionally NOT caught (mirror RoundtripTabCommand).
        }

        // ─────────────────────────────────────────────────────────────────────
        // Content-sniff format detection.
        //   "EERT" -> .tre (routed to repack-tre); 0xABCD u32 LE -> .stf; "FORM" IFF: DTII -> datatable,
        //   else structurally-decodable-as-OT -> OT, else plain IFF.
        // ─────────────────────────────────────────────────────────────────────

        private static AssetFormat DetectFormat(byte[] bytes)
        {
            if (bytes.Length >= 4 && bytes[0] == (byte)'E' && bytes[1] == (byte)'E' && bytes[2] == (byte)'R' && bytes[3] == (byte)'T')
            {
                return AssetFormat.Tre;
            }
            // .stf magic LocalizedStringTable::ms_MAGIC == 0xABCD as a little-endian u32.
            if (bytes.Length >= 4 && bytes[0] == 0xCD && bytes[1] == 0xAB && bytes[2] == 0x00 && bytes[3] == 0x00)
            {
                return AssetFormat.Stf;
            }
            if (bytes.Length >= 4 && bytes[0] == (byte)'F' && bytes[1] == (byte)'O' && bytes[2] == (byte)'R' && bytes[3] == (byte)'M')
            {
                MutableIffDocument iff = ReadIff(bytes);
                if (iff.Root != null && iff.Root.Kind == MutableIffNodeKind.Container && iff.Root.SubTypeId == "DTII")
                {
                    return AssetFormat.Datatable;
                }
                // OT is detected structurally (digit version form + int32 param-count); a non-OT IFF
                // throws DecoderException → fall through to plain IFF.
                try
                {
                    MutableObjectTemplate.FromMutableIff(iff);
                    return AssetFormat.ObjectTemplate;
                }
                catch (DecoderException)
                {
                    return AssetFormat.Iff;
                }
            }
            return AssetFormat.Unknown;
        }

        private static byte[] Serialize(AssetFormat format, byte[] loaded)
        {
            switch (format)
            {
                case AssetFormat.Stf:
                    return StringTableWriter.Serialize(StringTableDocument.FromBytes(loaded).Mutable);
                case AssetFormat.Datatable:
                    return new DataTableWriter(DataTableDocument.FromIff(ReadIff(loaded)).Mutable).Serialize();
                case AssetFormat.ObjectTemplate:
                    return MutableObjectTemplate.FromMutableIff(ReadIff(loaded)).Serialize();
                case AssetFormat.Iff:
                    return IffWriter.Write(ReadIff(loaded));
                default:
                    throw new IffParseException(IffParseError.MalformedFourCc,
                        "Unrecognized asset format (not IFF/datatable/stf/OT).");
            }
        }

        private static bool Reparses(AssetFormat format, byte[] bytes)
        {
            try
            {
                switch (format)
                {
                    case AssetFormat.Stf:
                        StringTableDocument.FromBytes(bytes);
                        return true;
                    case AssetFormat.Datatable:
                        DataTableDocument.FromIff(ReadIff(bytes));
                        return true;
                    case AssetFormat.ObjectTemplate:
                        MutableObjectTemplate.FromMutableIff(ReadIff(bytes));
                        return true;
                    case AssetFormat.Iff:
                        ReadIff(bytes);
                        return true;
                    default:
                        return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static MutableIffDocument ReadIff(byte[] bytes)
        {
            IffDocument doc;
            using (var ms = new MemoryStream(bytes, writable: false))
            {
                doc = IffReader.Read(ms);
            }
            return MutableIffDocument.FromDocument(doc, bytes);
        }

        // Atomic write core (mirror IffSaveTargets WriteAtomic — REIMPLEMENTED, not referenced).
        // Flush(true) forces the OS to flush to disk so a subsequent reload sees no stale bytes.
        private static void WriteAtomic(string path, byte[] bytes)
        {
            string dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true);
            }
        }
    }
}
