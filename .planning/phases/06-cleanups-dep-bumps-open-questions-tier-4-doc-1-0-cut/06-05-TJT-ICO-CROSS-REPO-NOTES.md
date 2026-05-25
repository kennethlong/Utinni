# 06-05 TJT.ico Cross-Repo Ejection Notes

**Decision:** D-15 (Phase 6 06-CONTEXT) — eject The Jawa Toolbox's icon from the Utinni
framework so the framework no longer ships plugin branding as its default.
**Authority:** [[feedback-utinniplugins-authority]] — standing authority for paired
cross-repo commits to `kennethlong/UtinniPlugins`; no human-action checkpoint required for
code commits (only the live-SWG smoke needs one; that lands in 06-06).
**Precedent:** Phase 02 D-09 cross-repo paired-commit posture.

## What changed and why

`UtinniCoreDotNet/UI/Forms/UtinniForm.cs` — the framework base form every plugin's editor
windows inherit from — set `this.Icon = Resources.TJT` (TJT.ico). That meant *every* plugin's
window, not just The Jawa Toolbox's, displayed the Jawa Toolbox icon in the OS chrome. That is
plugin branding leaking into the framework.

After this change:
- **Utinni framework** ships a neutral gear icon (`utinni.ico`) as the `UtinniForm` default.
- **The Jawa Toolbox** owns `TJT.ico` (and `TJT.png`) in its own repo and sets its own window
  icon explicitly in `FormObjectBrowser`.

## Paired commits

| Repo | Commit | Summary |
|------|--------|---------|
| `kennethlong/Utinni` | `a3093be08d31f1befe60713e823b6f213c6f96af` | `feat(06-05): replace TJT.ico with neutral utinni.ico in UtinniForm default` — adds `UtinniCoreDotNet/Resources/utinni.ico`, removes `TJT.ico` + `TJT.png` and their resx/designer/csproj entries, repoints `UtinniForm.Icon` to `Resources.Utinni`. |
| `kennethlong/UtinniPlugins` | `c9cfa9d01417bea772142136b69ec333dd30fa3f` | `feat(tjt): receive TJT.ico from Utinni framework (paired with utinni#a3093be)` — adds `TheJawaToolboxDotNet/Resources/TJT.ico` + `TJT.png` (copied next to the plugin DLL), and `FormObjectBrowser` loads `TJT.ico` as its window icon at construction (guarded `File.Exists`, mirroring how `Plugin.cs` resolves `settings.ini` from the plugin dir). |

## Neutral framework icon

`utinni.ico` is an original 64×64 gear glyph (U+2699) on a slate rounded-square, generated via
`System.Drawing` — "gear" per the D-15 planner-discretion list (gear/wrench/lambda/generic).
A gear reads as "tooling/framework", which is the intended neutral framework identity.

## Verification

- Utinni side: `grep TJT.ico` under `UtinniCoreDotNet/` returns zero; `Resources.Utinni` (Icon)
  resolves; Release|x86 solution build is green; covered by CI on master.
- TJT side: the icon load is `File.Exists`-guarded, so a missing asset degrades gracefully to
  the framework default rather than throwing. The Jawa Toolbox build (and the bundled-plugin
  load) is exercised in 06-06 (MSI packaging) and the live-SWG Tier-4 smoke; it is not part of
  Utinni's CI lane, which builds `Utinni.sln` only.
