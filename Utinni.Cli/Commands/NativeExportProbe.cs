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

            // Locate the section containing the export directory.
            int exportRva = exportDir.RelativeVirtualAddress;
            int sectionIndex = peHeaders.GetContainingSectionIndex(exportRva);
            if (sectionIndex < 0)
            {
                return false;
            }

            var section = peHeaders.SectionHeaders[sectionIndex];

            // Get the entire image bytes. PEReader.GetEntireImage() is available since 1.1.
            // It returns the full PE image as a PEMemoryBlock.
            var imageBlock = peReader.GetEntireImage();

            // Convert to a byte array using the BlobReader approach.
            // BlobReader.ReadBytes is available but we need the section offset within the image.
            // Safer: use the file pointer (PointerToRawData) to locate the section in the image.
            int sectionFileOffset = section.PointerToRawData;
            int exportOffset = sectionFileOffset + (exportRva - section.VirtualAddress);

            // imageBlock.GetReader() returns a BlobReader over the entire image.
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
            if (exportOffset < 0 || exportOffset + 40 > imageLength)
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

            // Convert AddressOfNames RVA to file offset.
            int namesFileOffset = sectionFileOffset + ((int)addressOfNamesRva - section.VirtualAddress);
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
                int nameFileOffset = sectionFileOffset + ((int)nameRva - section.VirtualAddress);
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
