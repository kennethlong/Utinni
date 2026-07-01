# CLAUDE.md

Claude-specific guidance for this repo. The tool-agnostic project runbook (build/test/toolchain/CI/
invariants/format-reality) lives in **AGENTS.md** — read it first.

@AGENTS.md

Everything below is Claude-specific: the auto-memory system, the "phone a friend" peer-review mechanics,
the GSD workflow, standing authorities, and working preferences. Durable project facts belong in
AGENTS.md; hard-won engineering lessons belong in `docs/ai/lessons.md`; keep this file thin.

## Auto-memory

A persistent file-based memory lives at `~/.claude/projects/D--Code-Utinni/memory/` (~50+ files; one
fact per file with frontmatter). `MEMORY.md` is the index loaded each session — one line per memory.
Memories are **machine-local** (not in the repo, invisible to Codex/Cursor/contributors), so anything
that should be *shared* belongs in AGENTS.md / `docs/ai/` instead. Recalled memories reflect what was
true when written — verify a named file/flag still exists before acting on it. Save user facts,
feedback, project context, and references; don't duplicate what AGENTS.md / the repo already records.

## Phone a friend (cross-AI peer review)

Two external reviewers run headless on this machine for second opinions on plans, diffs, and tricky RE:

- **Codex** (ChatGPT-authed): `codex exec --skip-git-repo-check -` — pipe the prompt on stdin. Invoke
  directly for a second opinion; or draft a paste-prompt as fallback.
- **Cursor** (`cursor-agent`, NOT `cursor`): at `C:\Users\kenne\AppData\Local\cursor-agent\`; invoke via
  PowerShell `& cursor-agent.cmd -p --mode ask --trust`. Add `--model sonnet-5` for the Sonnet-5 reviewer
  (confirm the exact model name via `cursor-agent --list-models`). Note the GSD `/gsd:review` workflow
  assumes a `cursor agent` subcommand that is wrong on Windows — use `cursor-agent.cmd`.

Plus the **in-harness crew** (Agent tool, model override): spawn a Claude reviewer for a fast second
opinion without leaving the session. **The Sonnet tier = `claude-sonnet-5`** (Sonnet 5); use Opus for the
heaviest reviews. This is the same Sonnet the GSD model catalog resolves the `sonnet` tier to
(`~/.claude/get-shit-done/bin/shared/model-catalog.json`, `runtimeTierDefaults.claude.sonnet`).

Use these for adversarial review of foundation-phase plans and any change where being wrong is expensive
(injection, detours, binding regen, byte-exact codecs).

## GSD workflow

This project runs Get-Shit-Done (`.planning/`). Phases → plans → atomic commits. `gsd-sdk` is on PATH.
Worktrees are OFF (see AGENTS.md) — run build waves inline. **Grep-gate hygiene:** plan acceptance
"grep X returns zero matches" is literal — reword source comments to avoid the gated token (keep the
historical name only in non-gated docs).

## Standing authorities

- **Push:** pre-authorized `git push origin <branch>` for this repo during CI iteration. Still confirm
  destructive pushes (`--force`, branch delete).
- **UtinniPlugins:** standing authority to edit/commit/push the sibling `kennethlong/UtinniPlugins` repo
  at `D:/Code/UtinniPlugins`. Cross-repo paired commits do NOT need a human checkpoint — only the
  live-SWG smoke does (see AGENTS.md).
- **Commit trailer:** end commit messages with the configured Co-Authored-By line.

## Working preferences

- **Max-harness verification:** for fixes that aren't unit-testable out of the box, default to *inventing*
  a harness (P/Invoke, fixtures, schema asserts, process-isolated helpers) over manual smoke. Propose
  harness shapes first; let the user opt down to manual only if the cost is genuinely prohibitive.
- **Keep WIP scaffolding:** during in-progress feature iteration, don't pre-emptively delete exports/
  plumbing/atomics from a failed approach — patterns often transfer to the next strategy. Clean up only
  when the feature fully works.
- **D3D9-hook diagnosis first:** when ImGui doesn't render in an injected session, test the d3d9.dll
  pattern-scan FIRST (30 sec) before assuming SWG-side RVA drift (multi-day investigation).
- **Live-SWG repro reality:** the user triggers scene changes via TJT's chat-command parser; landing
  naked after a scene change is the baseline, NOT a regression. Don't disable TJT in a bisect that needs
  the scene-change repro path.

See `docs/ai/lessons.md` for the full distilled engineering-lesson set these preferences draw on.
