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
using System.Linq;
using System.Text.RegularExpressions;

namespace UtinniCoreDotNet.Tests.PreservationAudit
{
    /// <summary>
    /// Phase 6 (Plan 06-05 / D-18): repo-root anchor + grep helpers for the STAB-04
    /// preservation-audit tests. Walks up from the test assembly's base directory until
    /// it finds Utinni.sln, so the same checks work locally (bin\Release\net472\) and on
    /// the self-hosted CI runner. Asserts use AT LEAST thresholds so an off-by-one path
    /// bug surfaces as a test failure rather than a false pass (T-06-05-04 mitigation).
    /// </summary>
    internal static class RepoRoot
    {
        private static readonly Lazy<string> _dir = new Lazy<string>(Find);

        /// <summary>Absolute path to the repository root (the directory containing Utinni.sln).</summary>
        public static string Dir => _dir.Value;

        // Build output / vendored trees are never part of a preservation grep.
        private static readonly string[] ExcludedDirs =
        {
            "external", "bin", "obj", "Debug", "Release", "RelWithDbgInfo",
            "x86", "x64", ".vs", ".git", "vcpkg_installed", "packages", "TestResults"
        };

        private static string Find()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Utinni.sln")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            throw new InvalidOperationException(
                "Could not locate the repository root (Utinni.sln) walking up from " +
                AppDomain.CurrentDomain.BaseDirectory);
        }

        private static bool IsExcluded(string fullPath)
        {
            var rel = fullPath.Substring(Dir.Length);
            var sep = Path.DirectorySeparatorChar;
            return ExcludedDirs.Any(d =>
                rel.IndexOf(sep + d + sep, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>Enumerate source files under a repo-relative subdir, excluding build output.</summary>
        public static IEnumerable<string> SourceFiles(string relSubdir, params string[] extensions)
        {
            var baseDir = Path.Combine(Dir, relSubdir.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(baseDir)) yield break;
            foreach (var f in Directory.EnumerateFiles(baseDir, "*.*", SearchOption.AllDirectories))
            {
                if (IsExcluded(f)) continue;
                var ext = Path.GetExtension(f);
                if (extensions.Length == 0 ||
                    extensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase)))
                {
                    yield return f;
                }
            }
        }

        /// <summary>Count source files under <paramref name="relSubdir"/> whose text matches <paramref name="pattern"/>.</summary>
        public static int CountFilesMatching(string relSubdir, string pattern, params string[] extensions)
        {
            var rx = new Regex(pattern, RegexOptions.Multiline);
            return SourceFiles(relSubdir, extensions).Count(f => rx.IsMatch(File.ReadAllText(f)));
        }

        /// <summary>True if the repo-relative file exists and its text matches <paramref name="pattern"/>.</summary>
        public static bool FileMatches(string relPath, string pattern)
        {
            var full = Path.Combine(Dir, relPath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(full) && Regex.IsMatch(File.ReadAllText(full), pattern, RegexOptions.Multiline);
        }

        /// <summary>Number of regex matches of <paramref name="pattern"/> in the repo-relative file (0 if missing).</summary>
        public static int MatchCount(string relPath, string pattern)
        {
            var full = Path.Combine(Dir, relPath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(full) ? Regex.Matches(File.ReadAllText(full), pattern, RegexOptions.Multiline).Count : 0;
        }

        /// <summary>True if a repo-relative file or directory exists.</summary>
        public static bool Exists(string relPath)
        {
            var full = Path.Combine(Dir, relPath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(full) || Directory.Exists(full);
        }
    }
}
