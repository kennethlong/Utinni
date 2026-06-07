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

using System.ComponentModel;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Utinni.Mcp.Server;

namespace Utinni.Mcp.Tools;

/// <summary>
/// The DISTINCT, off-by-default destructive tool (14-03, T-14-03). <c>repack_tre</c> wraps the
/// <c>repack-tre</c> CLI verb (full-rebuild, in-place overwrite). It is deliberately NOT a
/// <c>save_*</c> tool and is UNREACHABLE from any of them — the apply-save-* verbs reject .tre
/// inputs, so the only path to a destructive repack is THIS tool.
///
/// <para><b>Host-side dry_run gate (D-04):</b> the <c>repack-tre</c> verb has NO <c>--dry-run</c>
/// flag (confirmed in <c>RepackTreCommand.cs</c>). The gate is therefore HOST-SIDE and DEFAULTS to
/// plan-only: <c>dry_run=true</c> returns <see cref="CliResultMapper.DryRunNotice"/> WITHOUT
/// spawning the verb. The notice is PLAN-ONLY — it narrates what WOULD happen but does NOT validate
/// the lock / support / backup and does NOT take a backup (Cursor LOW: never claim a backup
/// occurred). Only <c>dry_run=false</c> spawns the verb, which routes through
/// <c>TreBackupPath</c> (timestamped backup before overwrite) + <c>TreRepackLock</c> (refuse a
/// live-client-held archive) and refuses V6000/encrypted (exit 2).</para>
///
/// <para><b>Destructive annotation (T-14-02):</b> the tool is marked Destructive as an ADVISORY
/// hint. The real safety is the dry_run-default + the verb's backup/lock/refuse-encrypted — never
/// the annotation or any elicitation prompt (which a headless agent may auto-accept).</para>
///
/// <para><b>Concurrency (T-14-16):</b> the actual run honors the same per-resolved-path
/// serialization as the <c>save_*</c> tools.</para>
/// </summary>
[McpServerToolType]
public static class RepackTool
{
    [McpServerTool(Name = "repack_tre", Destructive = true, Idempotent = false, ReadOnly = false)]
    [Description("DESTRUCTIVE: repack a .tre archive in place (full rebuild). dry_run DEFAULTS TO true and is " +
                 "PLAN-ONLY — it reports the target path WITHOUT spawning the repack, WITHOUT validating lock/support, " +
                 "and WITHOUT taking a backup. Re-invoke with dry_run=false to actually repack; the real run takes a " +
                 "timestamped backup first and refuses archives held open by another process or V6000/encrypted. " +
                 "This is the ONLY path to a destructive .tre rebuild — the save_* tools never repack.")]
    public static Task<CallToolResult> RepackTre(
        ResolvedRoot root,
        CliDispatcher cli,
        [Description("Path to the .tre archive relative to the configured client root. Never an absolute path.")]
        string relativePath,
        [Description("Default true (plan-only, no write, no backup). Set false to actually repack.")]
        bool dry_run = true)
    {
        // Boundary path-escape gate FIRST — throws on escape, NO CLI spawned and NO dry-run notice.
        string abs = root.Resolve(relativePath);

        // Host-side dry_run gate: the verb has no --dry-run flag, so plan-only is enforced HERE.
        // No spawn, no backup claim — just narrate the intended target.
        if (dry_run)
        {
            return Task.FromResult(CliResultMapper.DryRunNotice(abs));
        }

        // Actual destructive run — serialized per resolved path, same as the save_* surface.
        return SaveVerb.RunSerializedAsync(abs, async () =>
        {
            // repack-tre takes a positional path (no --root); the resolved absolute path is contained.
            CliInvocationResult r = await cli.RunAsync("repack-tre", new[] { abs }).ConfigureAwait(false);
            return CliResultMapper.ToCallToolResult(r);
        });
    }
}
