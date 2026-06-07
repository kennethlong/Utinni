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

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Utinni.Mcp.Server;

namespace Utinni.Mcp.Tools;

/// <summary>
/// The shared pass-through that turns a <see cref="CliInvocationResult"/> into an MCP
/// <see cref="CallToolResult"/> (14-02 Task 1). Every read tool — and Plan 03's write tools —
/// route their dispatch result through here. It is deliberately the ONLY place the CLI's stdout is
/// parsed, and it parses the envelope as OPAQUE JSON: it validates the envelope SHAPE and applies
/// the exit-code taxonomy, but NEVER rewrites a field (forbidden business logic — T-14-07).
///
/// <para><b>Envelope SHAPE (Utinni.Cli/Output/JsonOutput.cs, REVIEWS Consensus #7):</b> a valid
/// envelope has <c>schemaVersion</c> AND <c>command</c> AND EXACTLY ONE of (<c>result</c>,
/// <c>error</c>). Anything else is a HARD MCP error, not an agent-correctable tool result.</para>
///
/// <para><b>Exit-code taxonomy (Phase-13 contract):</b>
/// <list type="bullet">
///   <item>exit 0 + valid <c>result</c> envelope -> tool result, <see cref="CallToolResult.IsError"/>=false.</item>
///   <item>exit 1/2/3 + valid <c>error</c> envelope -> tool result, IsError=true (agent self-corrects).</item>
///   <item>exe-missing / timeout / non-JSON / empty / malformed-shape / exit outside {0,1,2,3} /
///         exit-0-with-error-envelope / non-zero-with-result-envelope -> HARD <see cref="McpException"/>.</item>
/// </list>
/// <c>stderr</c> is captured for diagnostics but NEVER parsed; a polluted stderr alongside a valid
/// stdout envelope still maps on stdout.</para>
/// </summary>
public static class CliResultMapper
{
    /// <summary>
    /// Maps a completed/failed CLI invocation to an MCP tool result, throwing
    /// <see cref="McpException"/> for every hard transport / execution / shape failure.
    /// </summary>
    public static CallToolResult ToCallToolResult(CliInvocationResult r)
    {
        // (1) Hard transport failures: no envelope to interpret.
        if (!r.ExeFound)
        {
            throw new McpException(
                "utinni-cli executable was not found. " + Trim(r.Stderr));
        }

        if (r.TimedOut)
        {
            throw new McpException(
                "utinni-cli did not exit within the configured timeout (killed). " + Trim(r.Stderr));
        }

        // (2) Exit code outside the Phase-13 taxonomy {0,1,2,3} => a child crash / unexpected
        //     outcome, even if stdout happens to look like JSON. Never an in-band domain answer.
        if (r.ExitCode is < 0 or > 3)
        {
            throw new McpException(
                "utinni-cli exited with an unexpected code " + r.ExitCode +
                " (outside the 0..3 contract). " + Trim(r.Stderr));
        }

        // (3) Parse stdout as opaque JSON. Empty or non-JSON => hard error.
        if (string.IsNullOrWhiteSpace(r.Stdout))
        {
            throw new McpException("utinni-cli produced no stdout to interpret as a JSON envelope.");
        }

        JsonNode? envelope;
        try
        {
            envelope = JsonNode.Parse(r.Stdout);
        }
        catch (JsonException ex)
        {
            throw new McpException("utinni-cli stdout is not valid JSON: " + ex.Message);
        }

        if (envelope is not JsonObject obj)
        {
            throw new McpException("utinni-cli stdout is not a JSON object envelope.");
        }

        // (4) Validate the envelope SHAPE: schemaVersion + command + EXACTLY ONE of (result, error).
        bool hasSchemaVersion = obj.ContainsKey("schemaVersion");
        bool hasCommand = obj.ContainsKey("command");
        bool hasResult = obj.ContainsKey("result");
        bool hasError = obj.ContainsKey("error");

        if (!hasSchemaVersion || !hasCommand || (hasResult == hasError))
        {
            throw new McpException(
                "utinni-cli stdout is not a valid envelope shape " +
                "(requires schemaVersion + command + exactly one of result/error).");
        }

        // (5) Cross-check the exit code against the envelope discriminator.
        //     exit 0 MUST carry a result; exit 1/2/3 MUST carry an error. A mismatch
        //     (exit-0-with-error, or non-zero-with-result) is a contradiction => hard error.
        if (r.ExitCode == 0 && !hasResult)
        {
            throw new McpException(
                "utinni-cli exited 0 but the envelope carries an error object (contradiction).");
        }

        if (r.ExitCode != 0 && !hasError)
        {
            throw new McpException(
                "utinni-cli exited " + r.ExitCode +
                " but the envelope carries a result object (contradiction).");
        }

        // (6) Valid in-band outcome. Pass the envelope through VERBATIM: the raw stdout as a text
        //     mirror + the parsed node as StructuredContent. NEVER rewrite a field.
        bool isError = r.ExitCode != 0; // 1/2/3 => agent-correctable in-band error.

        // StructuredContent is a JsonElement: re-parse the validated raw stdout (not a rewrite —
        // the bytes are identical to what the CLI emitted) so the envelope is carried verbatim.
        using JsonDocument doc = JsonDocument.Parse(r.Stdout);
        JsonElement structured = doc.RootElement.Clone();

        return new CallToolResult
        {
            IsError = isError,
            Content = new List<ContentBlock>
            {
                new TextContentBlock { Text = r.Stdout }
            },
            StructuredContent = structured
        };
    }

    /// <summary>Returns a trimmed diagnostic string (empty when the source is null/blank).</summary>
    private static string Trim(string? s) => string.IsNullOrWhiteSpace(s) ? string.Empty : s.Trim();

    /// <summary>
    /// Builds a non-error tool result describing what a DESTRUCTIVE operation WOULD do, without
    /// spawning the CLI (Plan 03's <c>repack_tre</c> dry-run path). The <paramref name="absolutePath"/>
    /// is an absolute, root-resolved path: this is ACCEPTED info-disclosure to the trusted local
    /// agent (T-14-12, documented in MCP-SECURITY.md). This helper performs NO file op and NO CLI
    /// dispatch — it only narrates intent.
    /// </summary>
    public static CallToolResult DryRunNotice(string absolutePath)
    {
        string text =
            "Dry run (no write performed). The destructive operation would target: " + absolutePath +
            ". Re-invoke with dry_run=false to actually perform it.";

        return new CallToolResult
        {
            IsError = false,
            Content = new List<ContentBlock>
            {
                new TextContentBlock { Text = text }
            }
        };
    }
}
