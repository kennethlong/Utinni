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

using System.Reflection;
using System.Runtime.CompilerServices;

// REVIEWS MEDIUM-15: validate-plugin contract version marker.
// Read via reflection on the BUILT assembly (not source-file walk from BaseDirectory).
// Bumps in lockstep with the schemaVersion: 1 contract.
[assembly: AssemblyMetadata("validate-plugin-version", "1")]

// Iter-3 HIGH-1: allows PluginInspectionTests.Test10 to call
// PluginInspectionFilters.FilterNativeLoadErrors (internal static).
// Iter-4 MED-3 verified: this file is in the EXE assembly (Utinni.Cli/Properties/AssemblyInfo.cs),
// distinct from the TEST assembly's AssemblyInfo.cs (Utinni.Cli.Tests/Properties/AssemblyInfo.cs).
[assembly: InternalsVisibleTo("Utinni.Cli.Tests")]
