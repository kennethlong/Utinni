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

namespace UtinniCoreDotNetGen
{
    /// <summary>
    /// C-15: Resolves the Utinni solution root directory using three resolution modes.
    /// Lives in its own class (no CppSharp using-directives) so that consumers — notably
    /// CppSharpSlnDirTests in UtinniCoreDotNet.Tests — can reference it without the CLR
    /// being forced to load CppSharp.AST / CppSharp.Generators just to JIT the class.
    /// </summary>
    public static class SlnDirResolver
    {
        /// <summary>
        /// Resolves the Utinni solution root directory using three resolution modes:
        /// 1. args[0] / $(SolutionDir) — explicit arg passed by the post-build event.
        /// 2. Walk-up from workingDir until a directory containing Utinni.sln is found.
        /// 3. UTINNI_SLN_DIR environment variable fallback.
        /// Throws InvalidOperationException if none of the modes succeed.
        /// </summary>
        public static string Resolve(string workingDir, string[] args)
        {
            // Preference 1: explicit arg (e.g. $(SolutionDir) from post-build event)
            if (args != null && args.Length > 0 && Directory.Exists(args[0]))
            {
                return args[0].TrimEnd('\\', '/') + "\\";
            }

            // Preference 2: walk up from workingDir looking for Utinni.sln
            var dir = new DirectoryInfo(workingDir);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Utinni.sln")))
                {
                    return dir.FullName.TrimEnd('\\', '/') + "\\";
                }
                dir = dir.Parent;
            }

            // Preference 3: UTINNI_SLN_DIR environment variable
            string envDir = Environment.GetEnvironmentVariable("UTINNI_SLN_DIR");
            if (!string.IsNullOrEmpty(envDir) && Directory.Exists(envDir))
            {
                return envDir.TrimEnd('\\', '/') + "\\";
            }

            throw new InvalidOperationException(
                "Could not resolve Utinni solution directory. " +
                "Pass $(SolutionDir) as args[0], run from a build output directory inside " +
                "the solution tree, or set UTINNI_SLN_DIR.");
        }
    }
}
