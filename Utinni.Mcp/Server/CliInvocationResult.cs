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

namespace Utinni.Mcp.Server;

/// <summary>
/// Captured outcome of a single <c>utinni-cli.exe</c> spawn (see <see cref="CliDispatcher"/>).
/// The MCP host maps <see cref="ExeFound"/>=false and <see cref="TimedOut"/>=true to hard MCP
/// errors; otherwise <see cref="Stdout"/> carries the sorted-key JSON envelope (success or
/// in-band error) and <see cref="ExitCode"/> follows the Phase-13 contract
/// (0 ok / 1 usage / 2 domain / 3 notfound).
///
/// <para>This is a PUBLISHED contract — the Wave-2/3 tool plans depend on these members and the
/// factory shapes (<see cref="ExeMissing"/>/<see cref="TimedOutResult"/>/<see cref="Completed"/>).</para>
/// </summary>
public sealed class CliInvocationResult
{
    /// <summary>False when the resolved exe does not exist — host maps to a hard MCP error.</summary>
    public bool ExeFound { get; init; }

    /// <summary>True when the child exceeded the dispatcher timeout and was killed — hard MCP error.</summary>
    public bool TimedOut { get; init; }

    /// <summary>Child process exit code (0 ok / 1 usage / 2 domain / 3 notfound). Meaningful only
    /// when <see cref="ExeFound"/> is true and <see cref="TimedOut"/> is false.</summary>
    public int ExitCode { get; init; }

    /// <summary>Captured stdout — the sorted-key JSON envelope on success / in-band error.</summary>
    public string Stdout { get; init; } = string.Empty;

    /// <summary>Captured stderr.</summary>
    public string Stderr { get; init; } = string.Empty;

    /// <summary>The resolved exe path was not found on disk (no spawn attempted).</summary>
    public static CliInvocationResult ExeMissing(string path) => new()
    {
        ExeFound = false,
        Stderr = "utinni-cli executable not found: " + path
    };

    /// <summary>The child exceeded the timeout and was killed.</summary>
    public static CliInvocationResult TimedOutResult(System.TimeSpan timeout) => new()
    {
        ExeFound = true,
        TimedOut = true,
        Stderr = "utinni-cli did not exit within " + timeout.TotalSeconds + "s (killed)."
    };

    /// <summary>The child ran to completion with the captured exit/streams.</summary>
    public static CliInvocationResult Completed(int exitCode, string stdout, string stderr) => new()
    {
        ExeFound = true,
        TimedOut = false,
        ExitCode = exitCode,
        Stdout = stdout ?? string.Empty,
        Stderr = stderr ?? string.Empty
    };
}
