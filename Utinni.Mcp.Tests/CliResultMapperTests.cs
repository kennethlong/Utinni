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

using System.Linq;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Utinni.Mcp.Server;
using Utinni.Mcp.Tools;
using Xunit;

namespace Utinni.Mcp.Tests;

/// <summary>
/// <see cref="CliResultMapper"/> taxonomy tests (14-02 Task 1, REVIEWS Consensus #7).
/// The mapper turns a <see cref="CliInvocationResult"/> into an MCP <see cref="CallToolResult"/>,
/// passing the CLI's sorted-key JSON envelope through as text + StructuredContent, validating the
/// envelope SHAPE (schemaVersion + command + (result XOR error)), and applying the full
/// exit-code -> MCP error taxonomy:
/// <list type="bullet">
///   <item>exit 0 + valid result envelope -> isError=false, text + structuredContent.</item>
///   <item>exit 1/2/3 + valid error envelope -> isError=true (agent self-corrects).</item>
///   <item>exe-missing / timeout / non-JSON / malformed-shape / out-of-range exit /
///         exit-0-with-error-envelope / empty stdout -> HARD McpException (no tool result).</item>
///   <item>stderr pollution alongside a valid stdout envelope -> still mapped on stdout.</item>
///   <item>SEMANTIC pass-through: structuredContent deep-equals the original envelope (no rewrite).</item>
/// </list>
/// </summary>
public class CliResultMapperTests
{
    private const string SuccessEnvelope =
        "{\"command\":\"parse-tre\",\"result\":{\"recordCount\":3,\"source\":\"a.tre\"},\"schemaVersion\":1}";

    private const string ErrorEnvelope =
        "{\"command\":\"parse-tre\",\"error\":{\"kind\":\"FileNotFound\",\"message\":\"nope\"},\"schemaVersion\":1}";

    private static string GetText(CallToolResult r) =>
        r.Content.OfType<TextContentBlock>().Single().Text;

    // ── Success path ────────────────────────────────────────────────────────

    [Fact]
    public void Exit0_ValidResultEnvelope_MapsToSuccessWithTextAndStructuredContent()
    {
        var result = CliInvocationResult.Completed(0, SuccessEnvelope, string.Empty);

        CallToolResult mapped = CliResultMapper.ToCallToolResult(result);

        Assert.False(mapped.IsError);
        Assert.Equal(SuccessEnvelope, GetText(mapped));
        Assert.NotNull(mapped.StructuredContent);
    }

    [Fact]
    public void Exit0_StructuredContent_DeepEqualsTheOriginalEnvelope_NoFieldRewrite()
    {
        var result = CliInvocationResult.Completed(0, SuccessEnvelope, string.Empty);

        CallToolResult mapped = CliResultMapper.ToCallToolResult(result);

        // SEMANTIC equality (Cursor MEDIUM): re-serialize structuredContent + the original,
        // re-parse both, assert JsonNode deep-equality — NOT string compare.
        JsonNode original = JsonNode.Parse(SuccessEnvelope)!;
        JsonNode roundTripped = JsonNode.Parse(mapped.StructuredContent!.Value.GetRawText())!;
        Assert.True(JsonNode.DeepEquals(original, roundTripped));
    }

    // ── In-band domain error path ───────────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Exit123_ValidErrorEnvelope_MapsToIsErrorTrue(int exitCode)
    {
        var result = CliInvocationResult.Completed(exitCode, ErrorEnvelope, string.Empty);

        CallToolResult mapped = CliResultMapper.ToCallToolResult(result);

        Assert.True(mapped.IsError);
        Assert.Equal(ErrorEnvelope, GetText(mapped));
        Assert.NotNull(mapped.StructuredContent);
    }

    // ── Hard transport / execution errors ───────────────────────────────────

    [Fact]
    public void ExeMissing_IsHardMcpError()
    {
        var result = CliInvocationResult.ExeMissing(@"C:\nope\utinni-cli.exe");
        Assert.Throws<McpException>(() => CliResultMapper.ToCallToolResult(result));
    }

    [Fact]
    public void TimedOut_IsHardMcpError()
    {
        var result = CliInvocationResult.TimedOutResult(System.TimeSpan.FromSeconds(60));
        Assert.Throws<McpException>(() => CliResultMapper.ToCallToolResult(result));
    }

    // ── Shape / taxonomy hard errors ────────────────────────────────────────

    [Fact]
    public void Exit0_NonJsonStdout_IsHardMcpError()
    {
        var result = CliInvocationResult.Completed(0, "this is not json", string.Empty);
        Assert.Throws<McpException>(() => CliResultMapper.ToCallToolResult(result));
    }

    [Fact]
    public void Exit0_EmptyStdout_IsHardMcpError()
    {
        var result = CliInvocationResult.Completed(0, string.Empty, string.Empty);
        Assert.Throws<McpException>(() => CliResultMapper.ToCallToolResult(result));
    }

    [Fact]
    public void Exit0_MissingSchemaVersion_IsHardMcpError()
    {
        var result = CliInvocationResult.Completed(0,
            "{\"command\":\"parse-tre\",\"result\":{}}", string.Empty);
        Assert.Throws<McpException>(() => CliResultMapper.ToCallToolResult(result));
    }

    [Fact]
    public void Exit0_MissingCommand_IsHardMcpError()
    {
        var result = CliInvocationResult.Completed(0,
            "{\"result\":{},\"schemaVersion\":1}", string.Empty);
        Assert.Throws<McpException>(() => CliResultMapper.ToCallToolResult(result));
    }

    [Fact]
    public void Exit0_BothResultAndError_IsHardMcpError()
    {
        var result = CliInvocationResult.Completed(0,
            "{\"command\":\"x\",\"result\":{},\"error\":{\"kind\":\"k\",\"message\":\"m\"},\"schemaVersion\":1}",
            string.Empty);
        Assert.Throws<McpException>(() => CliResultMapper.ToCallToolResult(result));
    }

    [Fact]
    public void Exit0_NeitherResultNorError_IsHardMcpError()
    {
        var result = CliInvocationResult.Completed(0,
            "{\"command\":\"x\",\"schemaVersion\":1}", string.Empty);
        Assert.Throws<McpException>(() => CliResultMapper.ToCallToolResult(result));
    }

    [Fact]
    public void Exit0_WithErrorEnvelope_IsHardMcpError_Contradiction()
    {
        // exit 0 says "success" but the envelope carries an error object — contradiction.
        var result = CliInvocationResult.Completed(0, ErrorEnvelope, string.Empty);
        Assert.Throws<McpException>(() => CliResultMapper.ToCallToolResult(result));
    }

    [Fact]
    public void ExitOutsideTaxonomy_IsHardMcpError_EvenWithJsonStdout()
    {
        // Child crash exit 134 with JSON-looking stdout must NOT become an in-band domain answer.
        var result = CliInvocationResult.Completed(134, SuccessEnvelope, string.Empty);
        Assert.Throws<McpException>(() => CliResultMapper.ToCallToolResult(result));
    }

    [Fact]
    public void Exit123_WithResultEnvelopeInsteadOfError_IsHardMcpError()
    {
        // A non-zero exit must carry an ERROR envelope; a result envelope here is a contradiction.
        var result = CliInvocationResult.Completed(2, SuccessEnvelope, string.Empty);
        Assert.Throws<McpException>(() => CliResultMapper.ToCallToolResult(result));
    }

    // ── stderr pollution does not affect mapping ────────────────────────────

    [Fact]
    public void StderrPollution_AlongsideValidStdout_StillMappedOnStdout()
    {
        var result = CliInvocationResult.Completed(0, SuccessEnvelope, "WARNING: noisy diagnostic\n");

        CallToolResult mapped = CliResultMapper.ToCallToolResult(result);

        Assert.False(mapped.IsError);
        Assert.Equal(SuccessEnvelope, GetText(mapped));
    }

    // ── DryRunNotice helper (for Plan 03 repack) ────────────────────────────

    [Fact]
    public void DryRunNotice_ReturnsNonErrorResultMentioningTheAbsolutePath()
    {
        const string abs = @"C:\swg\client\test.tre";

        CallToolResult notice = CliResultMapper.DryRunNotice(abs);

        Assert.False(notice.IsError);
        Assert.Contains(abs, GetText(notice));
    }
}
