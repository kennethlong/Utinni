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
using System.IO;

namespace Utinni.Cli.Tests.Infrastructure
{
    public static class FixturePath
    {
        /// <summary>
        /// Resolves a fixture path relative to the test assembly's base directory.
        /// Handles both files and directories.
        /// Throws FileNotFoundException with a clear message if the path does not exist.
        /// </summary>
        public static string Resolve(string relativePath)
        {
            var candidate = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "Fixtures", relativePath));

            if (File.Exists(candidate) || Directory.Exists(candidate))
            {
                return candidate;
            }

            throw new FileNotFoundException(
                "Fixture not found: " + candidate + " (BaseDirectory: " + AppContext.BaseDirectory + ")",
                candidate);
        }
    }
}
