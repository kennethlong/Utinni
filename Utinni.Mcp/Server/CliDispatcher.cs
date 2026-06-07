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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Utinni.Mcp.Server;

/// <summary>
/// The lone subprocess seam in the net10 MCP host — spawns the net472/x86 <c>utinni-cli.exe</c>
/// one layer up from the Phase-13 <c>NativeToolRunner</c> (which spawns the SOE build CLIs from
/// inside the CLI). Copies that hang-proofing discipline:
/// <list type="bullet">
///   <item><c>UseShellExecute=false</c> (no shell, no metacharacter injection) +
///         <see cref="ProcessStartInfo.ArgumentList"/> per-arg (net10 — no manual quoting; the
///         65-line CommandLineToArgvW algorithm the net472 runner needed is unnecessary here).</item>
///   <item>Close stdin after start so a child <c>getchar()</c> on a usage/error path gets EOF.</item>
///   <item>Start BOTH stdout/stderr async reads BEFORE awaiting exit (Cursor pipe-deadlock fix —
///         a child filling one pipe buffer cannot block exit while we wait on the other).</item>
///   <item>Injectable timeout (Codex): a <see cref="TimeSpan"/> ctor overload lets tests use a
///         sub-second timeout; production defaults to 60s. On timeout, <c>Kill(true)</c> the whole
///         tree + drain, returning <see cref="CliInvocationResult.TimedOutResult"/> (no hang).</item>
///   <item>Missing exe returns <see cref="CliInvocationResult.ExeMissing"/> WITHOUT throwing.</item>
/// </list>
/// </summary>
public sealed class CliDispatcher
{
    /// <summary>Production default wall-clock backstop for a single CLI invocation.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    private readonly string _cliExePath;
    private readonly TimeSpan _timeout;

    /// <summary>Constructs a dispatcher with the production default 60s timeout.</summary>
    public CliDispatcher(string cliExePath) : this(cliExePath, DefaultTimeout)
    {
    }

    /// <summary>Constructs a dispatcher with an INJECTABLE timeout (tests use a sub-second value).</summary>
    public CliDispatcher(string cliExePath, TimeSpan timeout)
    {
        _cliExePath = cliExePath ?? throw new ArgumentNullException(nameof(cliExePath));
        _timeout = timeout;
    }

    /// <summary>
    /// Spawns the CLI with <paramref name="verb"/> as the first argument followed by
    /// <paramref name="args"/>, captures stdout/stderr/exit, and returns a
    /// <see cref="CliInvocationResult"/>. Never throws on the missing-exe or timeout paths — those
    /// become hard-error result objects the host maps to MCP errors.
    /// </summary>
    public async Task<CliInvocationResult> RunAsync(string verb, IReadOnlyList<string> args)
    {
        if (verb == null) throw new ArgumentNullException(nameof(verb));
        args ??= Array.Empty<string>();

        if (!File.Exists(_cliExePath))
        {
            return CliInvocationResult.ExeMissing(_cliExePath);
        }

        var psi = new ProcessStartInfo
        {
            FileName = _cliExePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // net10 ArgumentList: each element is one argv entry — the runtime quotes per the
        // CommandLineToArgvW rules. No manual concatenation, no shell.
        psi.ArgumentList.Add(verb);
        for (int i = 0; i < args.Count; i++)
        {
            psi.ArgumentList.Add(args[i] ?? string.Empty);
        }

        using var process = new Process { StartInfo = psi };
        process.Start();

        // Close stdin so a child blocking on stdin gets EOF (never hangs waiting for input).
        try { process.StandardInput.Close(); } catch { /* child may have already exited */ }

        // START both async reads BEFORE awaiting exit (Cursor pipe-deadlock fix): a child that
        // fills one pipe's buffer while we block on WaitForExit would otherwise deadlock.
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(_timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Timeout: kill the whole tree, then drain so the read tasks complete and pipes close.
            try { process.Kill(entireProcessTree: true); } catch { /* already exiting */ }
            try { await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); } catch { /* ignore */ }
            try { await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false); } catch { /* ignore */ }
            return CliInvocationResult.TimedOutResult(_timeout);
        }

        // Process exited within the timeout — both streams have hit EOF; await the captures.
        string stdout = await stdoutTask.ConfigureAwait(false);
        string stderr = await stderrTask.ConfigureAwait(false);
        return CliInvocationResult.Completed(process.ExitCode, stdout, stderr);
    }
}
