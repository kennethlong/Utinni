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
/// Small, tested command-line parser for the MCP host's launch flags (Codex review: specify +
/// test the arg-parsing edge cases rather than inlining ad-hoc parsing in <c>Program.cs</c>).
///
/// <para>Supports both the space form (<c>--root C:\x</c>) and the equals form (<c>--root=C:\x</c>)
/// for each known flag. A flag with no following value is TOLERATED (leaves the property null —
/// never crashes). On duplicates the FIRST occurrence wins (documented). Quoted values are
/// already de-quoted by the OS argv splitter before reaching <see cref="Parse"/>, so no quote
/// handling is needed here.</para>
///
/// <para>Unknown args are ignored — this parser only extracts the flags it knows; the rest of the
/// process startup (and the MCP SDK's own host-builder arg handling) is unaffected.</para>
/// </summary>
public sealed class ServerArgs
{
    /// <summary>The <c>--root</c> / <c>--root=</c> value (first occurrence wins); null if absent.</summary>
    public string? Root { get; private set; }

    /// <summary>The <c>--cli-path</c> / <c>--cli-path=</c> value (first occurrence wins); null if absent.</summary>
    public string? CliPath { get; private set; }

    private const string RootFlag = "--root";
    private const string CliPathFlag = "--cli-path";

    /// <summary>
    /// Parses the known flags out of <paramref name="args"/>. Tolerant: a flag with a missing
    /// value leaves its property null; duplicates after the first are ignored; the equals form is
    /// accepted; unknown tokens are skipped.
    /// </summary>
    public static ServerArgs Parse(string[]? args)
    {
        var result = new ServerArgs();
        if (args == null)
        {
            return result;
        }

        for (int i = 0; i < args.Length; i++)
        {
            string token = args[i] ?? string.Empty;

            // Equals form: --flag=value
            if (TryEquals(token, RootFlag, out string? rootEq))
            {
                if (result.Root == null) result.Root = rootEq;
                continue;
            }
            if (TryEquals(token, CliPathFlag, out string? cliEq))
            {
                if (result.CliPath == null) result.CliPath = cliEq;
                continue;
            }

            // Space form: --flag value  (value is the NEXT token, if any)
            if (token == RootFlag)
            {
                string? value = NextValue(args, ref i);
                if (result.Root == null && value != null) result.Root = value;
                continue;
            }
            if (token == CliPathFlag)
            {
                string? value = NextValue(args, ref i);
                if (result.CliPath == null && value != null) result.CliPath = value;
                continue;
            }
        }

        return result;
    }

    // Matches "--flag=anything" and emits the part after the '='. The empty form ("--root=")
    // yields an empty-string value, which is treated as present-but-empty (still tolerated).
    private static bool TryEquals(string token, string flag, out string? value)
    {
        string prefix = flag + "=";
        if (token.StartsWith(prefix, System.StringComparison.Ordinal))
        {
            value = token.Substring(prefix.Length);
            return true;
        }
        value = null;
        return false;
    }

    // Consumes the next token as the value for a space-form flag. If the next token is itself a
    // flag (starts with "--") or there is no next token, the value is MISSING (returns null and
    // does NOT advance past a following flag) — tolerated per the contract.
    private static string? NextValue(string[] args, ref int i)
    {
        if (i + 1 >= args.Length)
        {
            return null; // trailing flag with no value
        }

        string next = args[i + 1] ?? string.Empty;
        if (next.StartsWith("--", System.StringComparison.Ordinal))
        {
            return null; // next token is another flag, not this flag's value
        }

        i++; // consume the value token
        return next;
    }
}
