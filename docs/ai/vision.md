# Vision: a one-stop shop for SWG modders

> **Status:** Strategic direction. Captured 2026-05-16 in conversation with
> the maintainer. This is the lens we use to prioritize work going forward.

## The problem

A Star Wars Galaxies modder today juggles ~30 separate tools to make a
non-trivial change. The exact list varies by what you're doing, but a
typical creature-or-quest mod touches some subset of:

| Concern                        | Today's tool                                                  |
| ------------------------------ | ------------------------------------------------------------- |
| Browse `.tre` archives         | `TreeFileExtractor.exe`, `TreeFileBuilder.exe`                |
| Inspect an IFF                 | `IFFEditor`, hex editor                                       |
| Look at object templates       | Hand-edit `.iff`, `IFFEditor`, or `SwgObjectEditor`-class tools |
| Edit a mesh / appearance       | Maya / 3ds Max + SOE exporter plugins (`plugin/win32/*`)      |
| Edit a skeletal animation      | Maya + exporter                                               |
| Edit a particle system         | `ParticleEditor`                                              |
| Edit / preview a creature      | `SwgCreatureEditor`-class tools                                |
| Edit a conversation tree       | `SwgConversationEditor.exe`                                    |
| Edit a quest                   | `SwgSpaceQuestEditor.exe` / quest-data tables                 |
| Edit a datatable               | `SwgDataTableTool` / `DataTableEditor`                        |
| Tune drop tables, AI profiles  | Hand-edit `.tab` files in a text editor                       |
| Edit terrain                   | `SwgTerrainEditor`                                            |
| Edit world snapshots           | **Utinni / Jawa Toolbox** — this is the only one we own today |
| Edit a buildout                | Hand-edit `.iff`, no friendly tool                            |
| Write or change server scripts | A text editor + `bison`/`flex` toolchain + `ScriptCompiler`   |
| Edit string tables (localisation) | `SwgStringEditor` / hand-edit `.stf`                        |
| Edit a UI page                 | Hand-edit `.inc` + reload                                     |
| Tweak shaders                  | DirectX 9 FX editor                                            |
| Package & ship                 | `TreeFileBuilder` + manual `.tre` ordering + manifest hand-care |
| Distribute                     | Modding forums, ad-hoc archives, hand-rolled installers       |

That's a *huge* surface area, much of it powered by 15+ year old SOE tools
that won't run cleanly on modern Windows, or by community one-offs in
varying states of maintenance. New modders bounce off this. Experienced
modders run a fragile zoo of VMs / tool installs / batch scripts.

## The goal

**Utinni becomes the single tool a modder opens.** From inside it they can:

- **See** anything the running client can load — every `.tre`, every IFF,
  every datatable, every template, every UI page, every shader, every
  string-table entry.
- **Edit** anything they could realistically edit by hand: snapshots,
  templates, datatables, conversations, quests, particles, UI pages,
  strings, shader uniforms.
- **Preview** edits live — the client is already running inside the editor;
  changes apply in-place (with the existing reload paths and a few new
  ones).
- **Author** new content: drop in new meshes (via plugin exporters), new
  scripts, new quests; mark them as belonging to a named mod.
- **Package** their mod into a single archive (e.g. one `.tre` + a manifest)
  with one button.
- **Share** to a community hub the same way game launchers share user-made
  content today.

The promise to a modder is: *download Utinni, install once, do everything.*

## Why Utinni is the right foundation

Utinni is uniquely positioned for this because of how it works. **The client
is already loaded inside the tool.** Everything the live game can read,
Utinni's plugins can read. Everything the live game can render, Utinni can
display, with a gizmo on it. This is a deeper integration than any
standalone editor SOE shipped — those editors had to re-implement the
rendering pipeline. Utinni doesn't.

Concretely:

- The `TreeFile` hook records every filename the game loads — so the
  asset browser is *free* (the Jawa Toolbox already exploits this).
- DirectX is hooked — so any "preview" feature can draw on top of the live
  scene without extra glue.
- The dearImgui + ImGuizmo overlay gives us in-world manipulators with
  basically no per-feature cost.
- The .NET host gives us WinForms (and could give us WPF or Avalonia in
  future) for any side-panel editor we want.
- The plugin model means feature areas are isolable and shippable
  independently — we don't have to land all 30 tools in one PR.

The framework already covers ~10–15% of the surface area. The rest is
plugins, not framework rewrites.

## What that means for prioritisation

This vision reshapes the assessment ([assessment.md](assessment.md)) in two
ways:

1. **Foundations matter more than features.** Before piling new plugins on,
   we need the framework to be reliable. The critical issues in the
   assessment (loader-lock, silent plugin failures, threading hazards,
   broken Debug path) hurt every future plugin. Fix the base, then build.
2. **Plugin authoring must be effortless.** If every new editor inside
   Utinni costs the same as building a standalone tool, we never close the
   gap. The strategic reworks in the assessment (symmetric `Add/Remove`,
   plugin lifecycle, single-source RVAs, CI, modernised templates,
   `[CallerMemberName]` logging) all reduce per-plugin friction.

Concretely, the order is:

1. **Stabilise the framework** (the assessment's Weeks 1–4) — bug fixes,
   CI, dead-code removal.
2. **Polish plugin authoring** (assessment's Weeks 5–8) — reworks A–H,
   modernised templates, packaging script.
3. **Then start the plugin pipeline** — feature-by-feature, each backed by
   `IEditorPlugin` and shippable independently.

## What plugins we'd want, in rough order

This is a long-range list, not a commitment. Each represents months of
work; we'd ship them as we go. Ordering reflects (impact × leverage on
existing Utinni capabilities).

### Wave 1 — round out what we have

| Plugin                             | Replaces                              | Notes                                                 |
| ---------------------------------- | ------------------------------------- | ----------------------------------------------------- |
| **TRE Browser** (read-only)        | `TreeFileExtractor`                   | Use the existing `treefile::getAllFilenames` hook + a tree-view. Hex/preview pane. Drag to disk. |
| **IFF Editor** (read + write)      | `IFFEditor`, hex editor                | Generic IFF tree viewer; field-level edit for known forms. |
| **Datatable Editor** (`.tab`)      | `SwgDataTableTool`                    | The `.tab` format is well-understood; spreadsheet-style edit.   |
| **String-table Editor** (`.stf`)   | `SwgStringEditor`                      | Localisation rows + reload-strings hook in client.    |
| **Object Template Editor**         | `IFFEditor` against shared templates  | Field-driven UI for `SharedObjectTemplate`, etc.      |

### Wave 2 — content authoring

| Plugin                             | Replaces                                | Notes                                               |
| ---------------------------------- | ----------------------------------------- | --------------------------------------------------- |
| **Conversation Tree Editor**       | `SwgConversationEditor.exe`              | Node-graph UI, live preview against an NPC in-world. |
| **Quest Editor**                   | `SwgSpaceQuestEditor.exe`, quest tables  | Steps, triggers, rewards, task graphs.              |
| **Buildout / World Editor**        | Manual `.iff` edits + Jawa Toolbox       | Promote snapshot editing into full buildout editing. |
| **Particle Editor**                | `ParticleEditor`                          | Live-preview on top of the gizmo'd target.          |
| **UI Page Editor**                 | Hand-edit `.inc`                          | Re-uses the CUI reload hook.                        |
| **Shader Inspector / Editor**      | DirectX9 FX editor                        | Read shaders, hot-reload via existing graphics hook. |

### Wave 3 — workflow

| Plugin                             | Replaces                                          | Notes                                               |
| ---------------------------------- | --------------------------------------------------- | --------------------------------------------------- |
| **Mod Manager**                    | Manual `.tre` ordering, hand-rolled installers     | Define a "mod" as a labelled bundle. Enable/disable. |
| **Mod Packager / Builder**         | `TreeFileBuilder`                                   | Build a `.tre` from a mod's tracked changes.        |
| **Community Hub Browser**          | Modding forums, ad-hoc archive sites               | Optional. Subscribe to / publish mod bundles.       |
| **Asset Diff / Compare**           | (no equivalent today)                              | Compare two `.tre`s or two snapshots — useful for upstream/fork.  |

### Wave 4 — maybe-someday

| Plugin                             | Replaces                                          | Notes                                               |
| ---------------------------------- | --------------------------------------------------- | --------------------------------------------------- |
| **Mesh viewer** (read-only)        | Maya for inspection                                 | Use the live `Appearance` system. Write would require a full exporter chain. |
| **Animation previewer**            | Maya for inspection                                 | Likewise.                                           |
| **Script editor** (server-side)    | Text editor + `bison`/`flex`/`ScriptCompiler`       | Out of scope for the *client* tool; would need server-side cooperation. |
| **Texture authoring**              | Photoshop + texture conversion CLI                  | Out of scope; commit to importing from PNG instead.  |

The Wave 4 items are realistically the territory of separate tools that
Utinni can hand off to, not absorb. Mesh and texture authoring need
specialised editing UX that DCC tools have spent decades on; we plug into
the export pipelines but don't reinvent them.

## Anti-goals (what we explicitly are NOT)

- **Not a server-side mod manager.** SWG-Source / swg-main handle server
  scripting and data. We integrate with their conventions but don't manage
  the server.
- **Not a launcher / patcher.** There are good launchers already
  (SWGEmu's, community ones). Utinni is the *editor*; the launcher is
  what users run day-to-day to play.
- **Not Maya / 3ds Max.** We hand off to DCCs for mesh/animation/texture
  authoring; we don't try to be one.
- **Not a multiplayer-cheat enabler.** All editing is local-asset / offline-
  scene work. Live shards can detect and reject modified clients; that's
  fine — modders edit, then publish a mod, then play unmodded.

## Strategic position

Both `Utinni` and `UtinniPlugins` are MIT-licensed forks of
[`ptklatt/Utinni`](https://github.com/ptklatt/Utinni) and
[`ptklatt/UtinniPlugins`](https://github.com/ptklatt/UtinniPlugins). The
upstreams appear dormant. Our approach:

1. **Build a sovereign fork** that advances independently.
2. **Offer fixes upstream** where they're clean PRs (so the community
   benefits even if our broader direction differs).
3. **Cooperate with SWG-Source** for any server-side touch points — they
   maintain `client-tools/` and `swg-main/`; the more our work composes
   with theirs the better.

We don't need to be the only modding tool — there will always be specialised
editors. But we want to be the *default* tool. New modders should be told
"download Utinni" and be productive in an afternoon.

## How this doc evolves

This is a strategic statement, not a roadmap. Update it when:

- The vision shifts (e.g. we decide to scope down or expand).
- A wave-item gets re-prioritised (move it between waves or out entirely).
- An anti-goal is contested.

Tactical plans live in [`assessment.md`](assessment.md) (current health) and
will eventually live in a roadmap doc (concrete sequencing). GitHub issues
should reference back to one of these for context.

## See also

- [Assessment](assessment.md) — the code-quality audit driving the
  near-term work.
- [Architecture](architecture.md) — how the framework is shaped today.
- [Plugin framework](plugin-framework.md) — the SPI every future plugin
  uses.
- [The Jawa Toolbox](../../../UtinniPlugins/docs/ai/jawa-toolbox.md) — the
  prototype of what a Wave-1+ plugin looks like.
