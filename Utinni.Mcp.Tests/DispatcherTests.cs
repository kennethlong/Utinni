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
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Utinni.Mcp.Server;
using Xunit;

namespace Utinni.Mcp.Tests;

/// <summary>
/// <see cref="CliDispatcher"/> behavior tests. The dispatcher passes (verb, args) as the child's
/// argv, so these tests point it at cmd.exe / powershell as STUB children:
/// <list type="bullet">
///   <item>exe-missing -> ExeFound=false WITHOUT throwing.</item>
///   <item>non-zero exit (`cmd /c exit 7`) -> ExitCode=7, TimedOut=false.</item>
///   <item>injected sub-second timeout against a deterministic long-runner
///         (`powershell ... Start-Sleep 30`) -> TimedOut=true, child killed, well under 30s.</item>
///   <item>large stdout (>100 KB) completes without deadlock (both streams read async).</item>
/// </list>
/// The deterministic long-runner is `powershell -NoProfile -Command Start-Sleep` — NOT
/// `%ComSpec% /c pause` which terminates immediately on the dispatcher's closed stdin (Codex+Cursor).
/// The real-60s-default path is gated <c>[Trait("Category","Slow")]</c> and excluded from the
/// fast suite.
/// </summary>
public class DispatcherTests
{
    private static string ComSpec =>
        Environment.GetEnvironmentVariable("ComSpec") ?? @"C:\Windows\System32\cmd.exe";

    private static string PowerShell =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            @"WindowsPowerShell\v1.0\powershell.exe");

    [Fact]
    public async Task ExeMissing_ReturnsExeFoundFalse_WithoutThrowing()
    {
        string missing = Path.Combine(Path.GetTempPath(), "no-such-cli-" + Guid.NewGuid().ToString("N") + ".exe");
        var dispatcher = new CliDispatcher(missing, TimeSpan.FromSeconds(5));

        CliInvocationResult result = await dispatcher.RunAsync("anything", Array.Empty<string>());

        Assert.False(result.ExeFound);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task NonZeroExit_CapturesExitCode()
    {
        // cmd.exe /c exit 7  -> exit code 7.
        var dispatcher = new CliDispatcher(ComSpec, TimeSpan.FromSeconds(10));

        CliInvocationResult result = await dispatcher.RunAsync("/c", new[] { "exit", "7" });

        Assert.True(result.ExeFound);
        Assert.False(result.TimedOut);
        Assert.Equal(7, result.ExitCode);
    }

    [Fact]
    public async Task InjectedTimeout_KillsLongRunner_WellUnderRunnerDuration()
    {
        // powershell -NoProfile -Command Start-Sleep -Seconds 30 -> a deterministic 30s hang.
        // With a 500ms injected timeout the dispatcher must kill it and return promptly.
        Assert.True(File.Exists(PowerShell), "powershell.exe not found at the expected path.");
        var dispatcher = new CliDispatcher(PowerShell, TimeSpan.FromMilliseconds(500));

        var sw = Stopwatch.StartNew();
        CliInvocationResult result = await dispatcher.RunAsync(
            "-NoProfile", new[] { "-Command", "Start-Sleep -Seconds 30" });
        sw.Stop();

        Assert.True(result.ExeFound);
        Assert.True(result.TimedOut, "Dispatcher should report TimedOut for the long-runner.");
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20),
            $"Timeout-kill took {sw.Elapsed.TotalSeconds:F1}s — should be far under the 30s runner duration.");
    }

    [Fact]
    public async Task LargeStdout_NoDeadlock()
    {
        // Emit > 100 KB to stdout. If the dispatcher waited for exit BEFORE reading stdout, the
        // child would block on a full pipe buffer and this would deadlock. Reading both streams
        // async before WaitForExit prevents that.
        Assert.True(File.Exists(PowerShell), "powershell.exe not found at the expected path.");
        var dispatcher = new CliDispatcher(PowerShell, TimeSpan.FromSeconds(30));

        // 200 KB of 'x' written to stdout in one shot.
        string command = "[Console]::Out.Write('x' * 200000)";
        CliInvocationResult result = await dispatcher.RunAsync(
            "-NoProfile", new[] { "-Command", command });

        Assert.True(result.ExeFound);
        Assert.False(result.TimedOut);
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Stdout.Length >= 100_000,
            $"Expected >= 100 KB of stdout, got {result.Stdout.Length} chars.");
    }

    [Fact]
    [Trait("Category", "Slow")]
    public async Task DefaultTimeout_60s_KillsLongRunner()
    {
        // Gated off the per-push fast suite (Cursor LOW): default-ctor dispatcher (60s) against the
        // 30s+... actually 90s long-runner to ensure the 60s default fires. Run manually/nightly.
        Assert.True(File.Exists(PowerShell), "powershell.exe not found at the expected path.");
        var dispatcher = new CliDispatcher(PowerShell); // default 60s

        var sw = Stopwatch.StartNew();
        CliInvocationResult result = await dispatcher.RunAsync(
            "-NoProfile", new[] { "-Command", "Start-Sleep -Seconds 120" });
        sw.Stop();

        Assert.True(result.TimedOut);
        Assert.True(sw.Elapsed >= TimeSpan.FromSeconds(55) && sw.Elapsed < TimeSpan.FromSeconds(90),
            $"Default-timeout kill landed at {sw.Elapsed.TotalSeconds:F0}s — expected near 60s.");
    }
}
