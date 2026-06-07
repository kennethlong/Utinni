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

namespace Utinni.Mcp.Tools;

/// <summary>
/// The ONE host-writable path boundary OUTSIDE <c>resolvedRoot</c> (T-14-13, REVIEWS Consensus #6).
/// <c>get_template_schema</c> wraps <c>compile-definition --out &lt;hostTemp&gt; --skip-native</c>; the
/// CLI insists on writing the schema artifact to <c>--out</c>, so the host allocates a unique file
/// under <see cref="Path.GetTempPath"/> (explicitly OUTSIDE the pinned root — a documented writable
/// boundary), hands its path to the CLI, and DELETES it on <see cref="Dispose"/> regardless of
/// outcome. The host PARSES NOTHING from the temp file; the CLI already emits a
/// <c>{schemaPath,classCount,nativeStatus}</c> envelope on stdout that the mapper passes through.
/// </summary>
public sealed class TempSchemaOutput : IDisposable
{
    /// <summary>The absolute path of the allocated temp file under the OS temp directory.</summary>
    public string Path { get; }

    private bool _disposed;

    private TempSchemaOutput(string path)
    {
        Path = path;
    }

    /// <summary>
    /// Allocates a unique, empty temp file under <see cref="System.IO.Path.GetTempPath"/> (the
    /// documented writable boundary outside resolvedRoot) and returns the disposable handle. The
    /// caller passes <see cref="Path"/> as the CLI <c>--out</c> and lets <c>using</c> clean it up.
    /// </summary>
    public static TempSchemaOutput Create()
    {
        string dir = System.IO.Path.GetTempPath();
        string name = "utinni-mcp-schema-" + Guid.NewGuid().ToString("N") + ".json";
        string full = System.IO.Path.Combine(dir, name);
        return new TempSchemaOutput(full);
    }

    /// <summary>Best-effort read of the temp file's raw text (empty string if it does not exist).</summary>
    public string ReadAllTextOrEmpty()
    {
        try
        {
            return File.Exists(Path) ? File.ReadAllText(Path) : string.Empty;
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    /// <summary>Deletes the temp file (best-effort) — the writable boundary is reclaimed.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (File.Exists(Path)) File.Delete(Path);
        }
        catch (IOException)
        {
            // Best-effort cleanup; a leaked temp file is benign and never holds agent-visible state.
        }
    }
}
