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

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Utinni.Cli.Commands
{
    /// <summary>
    /// Probes a native PE file for exported symbol names WITHOUT loading the DLL
    /// into the process address space (REVIEWS HIGH-1 fix path A).
    ///
    /// Implementation: opens the file with a FileStream, wraps it in a PEReader from
    /// System.Reflection.Metadata, and walks the PE export name table manually via
    /// the IMAGE_EXPORT_DIRECTORY structure.
    ///
    /// NO LoadLibraryExW / GetProcAddress / FreeLibrary calls. Native DLL code is
    /// never mapped into the address space — strongest possible T-04-EoP mitigation.
    ///
    /// Alternative documented for future phases: LoadLibraryExW(path, NULL,
    /// DONT_RESOLVE_DLL_REFERENCES) (flag 0x1) — DllMain not called, GetProcAddress
    /// works per MSDN. Could replace PE-parsing if a future phase needs better
    /// compatibility with packed/obfuscated DLLs.
    /// </summary>
    public static class NativeExportProbe
    {
        /// <summary>
        /// Returns true if the PE file at <paramref name="dllPath"/> exports a
        /// symbol named <paramref name="symbolName"/> (case-sensitive, ASCII).
        /// Returns false on any error (file not found, invalid PE, etc.).
        /// Never throws.
        /// </summary>
        public static bool HasExport(string dllPath, string symbolName)
        {
            if (string.IsNullOrEmpty(dllPath) || !File.Exists(dllPath))
            {
                return false;
            }

            try
            {
                using (var fs = new FileStream(dllPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var peReader = new PEReader(fs, PEStreamOptions.PrefetchEntireImage))
                {
                    return PeReaderHasExport(peReader, symbolName);
                }
            }
            catch
            {
                // Malformed PE, access denied, or any other exception — safe fallback.
                return false;
            }
        }

        private static bool PeReaderHasExport(PEReader peReader, string symbolName)
        {
            var peHeaders = peReader.PEHeaders;
            if (peHeaders == null || peHeaders.PEHeader == null)
            {
                return false;
            }

            var exportDir = peHeaders.PEHeader.ExportTableDirectory;
            if (exportDir.Size == 0)
            {
                return false;
            }

            // CR-01/WR-05: resolve each RVA against the section that actually contains
            // it, rather than caching a single section for the whole function. The PE
            // spec guarantees the IMAGE_EXPORT_DIRECTORY itself lives in one section, but
            // the AddressOfNames array and the individual name strings it points at may
            // live in a different section (e.g. AddressOfNames in .edata, name strings
            // in .rdata for larger MSVC-built DLLs). Using a single section's
            // PointerToRawData/VirtualAddress for all three would yield a wrong file
            // offset whenever those pointers cross a section boundary.
            int exportRva = exportDir.RelativeVirtualAddress;
            int exportOffset = RvaToFileOffset(peHeaders, exportRva);
            if (exportOffset < 0)
            {
                return false;
            }

            // Get the entire image bytes. PEReader.GetEntireImage() is available since 1.1.
            // It returns the full PE image as a PEMemoryBlock.
            var imageBlock = peReader.GetEntireImage();
            var imageReader = imageBlock.GetReader();
            int imageLength = imageBlock.Length;

            // Read IMAGE_EXPORT_DIRECTORY (40 bytes) starting at exportOffset.
            // IMAGE_EXPORT_DIRECTORY layout:
            //   offset  0: Characteristics (4 bytes)
            //   offset  4: TimeDateStamp (4 bytes)
            //   offset  8: MajorVersion (2 bytes) + MinorVersion (2 bytes)
            //   offset 12: Name RVA (4 bytes)
            //   offset 16: Base (4 bytes)
            //   offset 20: NumberOfFunctions (4 bytes)
            //   offset 24: NumberOfNames (4 bytes)
            //   offset 28: AddressOfFunctions RVA (4 bytes)
            //   offset 32: AddressOfNames RVA (4 bytes)
            //   offset 36: AddressOfNameOrdinals RVA (4 bytes)
            if (exportOffset + 40 > imageLength)
            {
                return false;
            }

            imageReader.Offset = exportOffset;
            imageReader.ReadUInt32(); // Characteristics
            imageReader.ReadUInt32(); // TimeDateStamp
            imageReader.ReadUInt16(); // MajorVersion
            imageReader.ReadUInt16(); // MinorVersion
            imageReader.ReadUInt32(); // Name RVA
            imageReader.ReadUInt32(); // Base
            uint numberOfFunctions = imageReader.ReadUInt32();
            uint numberOfNames = imageReader.ReadUInt32();
            imageReader.ReadUInt32(); // AddressOfFunctions RVA
            uint addressOfNamesRva = imageReader.ReadUInt32();
            // Skip AddressOfNameOrdinals

            if (numberOfNames == 0 || addressOfNamesRva == 0)
            {
                return false;
            }

            // CR-01/WR-05: per-RVA section lookup for the AddressOfNames array.
            int namesFileOffset = RvaToFileOffset(peHeaders, (int)addressOfNamesRva);
            if (namesFileOffset < 0 || namesFileOffset + (long)numberOfNames * 4 > imageLength)
            {
                return false;
            }

            // Iterate the name-pointer array.
            for (uint i = 0; i < numberOfNames; i++)
            {
                int entryOffset = namesFileOffset + (int)(i * 4);
                if (entryOffset + 4 > imageLength)
                {
                    break;
                }

                imageReader.Offset = entryOffset;
                uint nameRva = imageReader.ReadUInt32();
                // CR-01: per-RVA section lookup for each name string — the strings
                // may live in a different section than the names array itself.
                int nameFileOffset = RvaToFileOffset(peHeaders, (int)nameRva);
                if (nameFileOffset < 0 || nameFileOffset >= imageLength)
                {
                    continue;
                }

                // Read null-terminated ASCII string.
                imageReader.Offset = nameFileOffset;
                string name = ReadNullTerminatedAscii(imageReader, imageLength);
                if (name == symbolName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// CR-01/WR-05: resolves an image-relative virtual address (RVA) to a file
        /// offset by looking up the section that actually contains the RVA. Returns
        /// -1 if no section contains the RVA (which indicates a malformed PE or an
        /// RVA pointing at uninitialised image bytes such as a BSS-style section).
        /// </summary>
        private static int RvaToFileOffset(PEHeaders peHeaders, int rva)
        {
            int idx = peHeaders.GetContainingSectionIndex(rva);
            if (idx < 0)
            {
                return -1;
            }

            var sec = peHeaders.SectionHeaders[idx];
            return sec.PointerToRawData + (rva - sec.VirtualAddress);
        }

        private static string ReadNullTerminatedAscii(BlobReader reader, int maxEnd)
        {
            var bytes = new List<byte>(32);
            while (reader.Offset < maxEnd && reader.RemainingBytes > 0)
            {
                byte b = reader.ReadByte();
                if (b == 0)
                {
                    break;
                }

                bytes.Add(b);
            }

            return Encoding.ASCII.GetString(bytes.ToArray());
        }
    }
}
