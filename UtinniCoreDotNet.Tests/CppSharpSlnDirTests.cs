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
using UtinniCoreDotNetGen;
using Xunit;

namespace UtinniCoreDotNet.Tests
{
    /// <summary>
    /// C-15 regression tests for UtinniCoreDotNetGen.Program.ResolveSlnDir().
    /// Three resolution modes: args[0]/$(SolutionDir), walk-up to Utinni.sln, UTINNI_SLN_DIR env var.
    /// </summary>
    public class CppSharpSlnDirTests : IDisposable
    {
        // Track env var state so we can restore it after tests that mutate it
        private readonly string _originalEnvVar;

        public CppSharpSlnDirTests()
        {
            _originalEnvVar = Environment.GetEnvironmentVariable("UTINNI_SLN_DIR");
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("UTINNI_SLN_DIR", _originalEnvVar);
        }

        [Fact]
        public void ResolveSlnDir_ArgZeroProvided_UsesIt()
        {
            // Arrange: create a real temp directory to pass as args[0]
            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            try
            {
                string someWorkingDir = Path.GetTempPath();

                // Act
                string result = Program.ResolveSlnDir(someWorkingDir, new[] { tempDir });

                // Assert: returned path is args[0] with trailing backslash
                Assert.Equal(tempDir.TrimEnd('\\') + "\\", result);
            }
            finally
            {
                Directory.Delete(tempDir);
            }
        }

        [Fact]
        public void ResolveSlnDir_NoArgsButWalkUpFindsUtinniSln_UsesIt()
        {
            // Arrange: create a temp hierarchy with Utinni.sln at the root
            // temp/
            //   sln/
            //     Utinni.sln  (empty file)
            //     deep/
            //       nested/
            //         dir/     <-- workingDir
            string tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string slnDir = Path.Combine(tempRoot, "sln");
            string workingDir = Path.Combine(slnDir, "deep", "nested", "dir");

            Directory.CreateDirectory(workingDir);
            File.WriteAllText(Path.Combine(slnDir, "Utinni.sln"), ""); // empty sln file

            try
            {
                Environment.SetEnvironmentVariable("UTINNI_SLN_DIR", null);

                // Act
                string result = Program.ResolveSlnDir(workingDir, new string[0]);

                // Assert: walk-up should find slnDir
                Assert.Equal(slnDir.TrimEnd('\\') + "\\", result);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Fact]
        public void ResolveSlnDir_NoArgsNoWalkupHit_NoEnvVar_Throws()
        {
            // Arrange: use temp path as workingDir — no Utinni.sln anywhere in ancestor hierarchy
            // (temp path is typically C:\Users\...\AppData\Local\Temp — no Utinni.sln there)
            string workingDir = Path.GetTempPath();
            Environment.SetEnvironmentVariable("UTINNI_SLN_DIR", null);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(
                () => Program.ResolveSlnDir(workingDir, new string[0]));
        }

        [Fact]
        public void ResolveSlnDir_NoArgsNoWalkupHit_HasEnvVar_UsesEnv()
        {
            // Arrange: set env var to a real temp directory
            string envDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(envDir);
            try
            {
                Environment.SetEnvironmentVariable("UTINNI_SLN_DIR", envDir);

                string workingDir = Path.GetTempPath();

                // Act
                string result = Program.ResolveSlnDir(workingDir, new string[0]);

                // Assert: env var value used
                Assert.Equal(envDir.TrimEnd('\\') + "\\", result);
            }
            finally
            {
                Directory.Delete(envDir);
            }
        }
    }
}
