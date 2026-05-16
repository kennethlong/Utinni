# Tutorial: your first editor plugin

> Audience: new .NET plugin authors. Reading time: ~30 minutes; with VS open,
> you'll have a working plugin in about that long.

We'll build **`HelloEditorPlugin`** — a one-`SubPanel`, one-hotkey, one-undo
plugin that:

1. Dock a panel on the editor right rail.
2. Show the current player position, updated each frame.
3. Add a button that teleports the player to the world origin **with undo**.
4. Bind <kbd>Ctrl</kbd>+<kbd>Home</kbd> as a hotkey for the same teleport.
5. Log a system message when the player targets something.

By the end you'll have used every major Utinni system: `IEditorPlugin`,
`SubPanel`, `GameCallbacks`, `ObjectCallbacks`, `GroundSceneCallbacks`,
`HotkeyManager`, `IUndoCommand`, and `Log`.

## Step 0 — prerequisites

- Utinni built and installed (see [Build & run](build.md)).
- `Plugins/` directory next to `Launcher.exe`.
- VS 2019 with the [VSIX templates](sdk.md#install) installed *or* a manual
  project ready (see [SDK — without the VSIX](sdk.md#net-plugin-without-the-vsix)).
- Verified that **TheJawaToolbox** (or any working plugin) loads — gives you
  a known-good baseline.

## Step 1 — create the project

**File → New → Project → UtinniDotNetEditorPlugin**.

Project name: `HelloEditorPlugin`.

The wizard creates `Plugin.cs`:

```csharp
using System;
using System.Collections.Generic;
using UtinniCore.Utinni;
using UtinniCoreDotNet.Hotkeys;
using UtinniCoreDotNet.PluginFramework;
using UtinniCoreDotNet.UI.Controls;
using UtinniCoreDotNet.UI.Forms;
using UtinniCoreDotNet.Utility;

public class HelloEditorPlugin : IEditorPlugin
{
    public PluginInformation Information { get; }
    public EventHandler<AddUndoCommandEventArgs> AddUndoCommand { get; set; }

    public HelloEditorPlugin()
    {
        Information = new PluginInformation("HelloEditorPlugin",
                                            "First-plugin tutorial",
                                            "Your Name");
    }

    public UtINI GetConfig() => null;
    public HotkeyManager GetHotkeyManager() => null;
    public List<IEditorForm> GetForms() => null;
    public List<SubPanelContainer> GetStandalonePanels() => null;
    public List<SubPanel> GetSubPanels() => null;
}
```

Build it once and copy the DLL to `Plugins/HelloEditorPlugin/`. Add to
`ut.ini`:

```ini
[Plugins]
plugin_0 = true,HelloEditorPlugin
```

Run `Launcher.exe`. The editor should open — your plugin is loaded but
invisible (no UI yet). Check `logs/utinni.log` for "1 .NET Plugin(s) loaded"
(or however many you have).

## Step 2 — add a SubPanel

Create `HelloSubPanel.cs`:

```csharp
using System;
using System.Windows.Forms;
using UtinniCore.Utinni;
using UtinniCoreDotNet.Callbacks;
using UtinniCoreDotNet.UI.Controls;
using UtinniCoreDotNet.Utility;

public class HelloSubPanel : SubPanel
{
    private readonly UtinniLabel  lblPos;
    private readonly UtinniButton btnTeleport;

    public HelloSubPanel(HelloEditorPlugin owner) : base("Hello", true)
    {
        lblPos = new UtinniLabel
        {
            Text = "player: (-)",
            Dock = DockStyle.Top,
            Height = 24
        };
        btnTeleport = new UtinniButton
        {
            Text = "Teleport to origin",
            Dock = DockStyle.Top,
            Height = 28,
            Enabled = false       // turn on when a scene is loaded
        };

        Controls.Add(btnTeleport);
        Controls.Add(lblPos);

        GameCallbacks.AddSetupSceneCall(() =>
            this.Invoke((MethodInvoker)(() => btnTeleport.Enabled = true)));
        GameCallbacks.AddCleanupSceneCall(() =>
            this.Invoke((MethodInvoker)(() => btnTeleport.Enabled = false)));
    }
}
```

Update `Plugin.cs`:

```csharp
public override List<SubPanel> GetSubPanels()
    => new List<SubPanel> { new HelloSubPanel(this) };
```

Rebuild, re-deploy, re-launch. You should see a "Hello" panel on the right
rail. The button is disabled until you load a scene.

## Step 3 — update the position label every frame

Inside `HelloSubPanel`'s constructor, after the existing handlers, add:

```csharp
GroundSceneCallbacks.AddUpdateLoopCall(UpdateLoop);

// re-arm after each fire — the queue-style API is one-shot
void Rearm() => GroundSceneCallbacks.AddUpdateLoopCall(UpdateLoop);

void UpdateLoop()
{
    try
    {
        var player = Game.GetPlayerCreatureObject();
        if (player == null) return;
        var pos = player.GetTransform().Position;

        // Marshal to UI.
        if (!IsDisposed)
            this.BeginInvoke((MethodInvoker)(() =>
                lblPos.Text = $"player: ({pos.x:0.0}, {pos.y:0.0}, {pos.z:0.0})"));
    }
    finally
    {
        // re-enqueue so we fire again next frame
        if (!IsDisposed) Rearm();
    }
}
```

Why re-enqueue? `AddUpdateLoopCall` is the **one-shot** queued pattern — fires
once then drops the callback. Re-enqueueing each call turns it into a
persistent per-frame hook. (You could also use the persistent-Action pattern
some other callback hubs offer — `Add*Callback` — but the ground-scene
update loop is queue-based.)

Build, deploy, run, load a scene (e.g. `naboo.trn` with the avatar default).
The label should update every frame with the player's position.

## Step 4 — teleport, with undo

Add an `IUndoCommand`:

```csharp
public class TeleportCommand : IUndoCommand
{
    private readonly Object       playerObj;
    private readonly Transform    before;
    private readonly Transform    after;

    public TeleportCommand(Object player, Transform before, Transform after)
    {
        this.playerObj = player;
        this.before    = before;
        this.after     = after;
    }

    public string GetText() => $"Teleport to ({after.Position.x:0}, {after.Position.y:0}, {after.Position.z:0})";

    public void Execute() => GroundSceneCallbacks.AddUpdateLoopCall(
        () => playerObj.SetTransform(after));

    public void Undo()    => GroundSceneCallbacks.AddUpdateLoopCall(
        () => playerObj.SetTransform(before));

    public bool AllowMerge() => false;
    public bool Merge(IUndoCommand newCommand) => false;
}
```

Wire the button:

```csharp
btnTeleport.Click += (s, e) =>
{
    GroundSceneCallbacks.AddUpdateLoopCall(() =>
    {
        var player = Game.GetPlayerCreatureObject();
        var before = player.GetTransform();
        var after  = new Transform(new Vector(0, before.Position.y, 0),
                                   before.Rotation);

        var cmd = new TeleportCommand(player, before, after);
        // Execute the change immediately
        player.SetTransform(after);
        // Push to undo stack
        owner.AddUndoCommand?.Invoke(owner, new AddUndoCommandEventArgs(cmd));
    });
};
```

`owner` is the `HelloEditorPlugin` instance you passed into the SubPanel
constructor. Raising `AddUndoCommand` on it puts the command on the editor's
undo stack — `FormMain` has already wired the event for you.

Build, deploy, run, click **Teleport to origin**. The player teleports.
Click the editor's title-bar Undo button (or use the history dropdown) —
the player teleports back. Redo — forward again.

## Step 5 — bind a hotkey

In `Plugin.cs`, add a hotkey manager:

```csharp
private readonly HotkeyManager hotkeys;

public HelloEditorPlugin()
{
    Information = new PluginInformation(...);

    hotkeys = new HotkeyManager(onGameFocusOnly: false);

    hotkeys.Add(new Hotkey
    {
        Name             = "HelloPlugin.TeleportHome",
        Text             = "Teleport to origin",
        ModifierKeys     = Keys.Control,
        Key              = Keys.Home,
        OnDownCallback   = TriggerTeleport,
        OverrideGameInput = false,
        Enabled          = true,
        OnGameFocusOnly  = true
    });

    hotkeys.CreateSettings();
}

public override HotkeyManager GetHotkeyManager() => hotkeys;

private void TriggerTeleport()
{
    GroundSceneCallbacks.AddUpdateLoopCall(() =>
    {
        var player = Game.GetPlayerCreatureObject();
        if (player == null) return;
        var before = player.GetTransform();
        var after  = new Transform(new Vector(0, before.Position.y, 0), before.Rotation);
        var cmd    = new TeleportCommand(player, before, after);
        player.SetTransform(after);
        AddUndoCommand?.Invoke(this, new AddUndoCommandEventArgs(cmd));
    });
}
```

Refactor the SubPanel button to call `owner.TriggerTeleport()` instead of
duplicating logic:

```csharp
btnTeleport.Click += (s, e) => owner.TriggerTeleport();
// (TriggerTeleport must be public or internal so the SubPanel can call it)
```

Build, deploy, run. <kbd>Ctrl</kbd>+<kbd>Home</kbd> in the game window now
teleports too — and goes through the same undo path.

Users can rebind in `Plugins/HelloEditorPlugin/input.ini`:

```ini
[Hotkeys]
HelloPlugin.TeleportHome = Control + Home
```

…or via the in-editor settings UI.

## Step 6 — react to target changes

Make the SubPanel log a system message when the player targets something:

```csharp
public HelloSubPanel(HelloEditorPlugin owner) : base("Hello", true)
{
    // ...

    ObjectCallbacks.AddOnTargetCallback(() =>
    {
        var t = Game.GetPlayerLookAtTargetObject();
        var name = t?.GetTemplateName() ?? "(none)";
        Log.Info($"target → {name}");
        SystemMessageManager.SendMessage($"target: {name}");
    });
}
```

`Log.Info` writes to `utinni.log`. `SystemMessageManager.SendMessage` pops
the message into the game's chat-style system-message stream.

## Step 7 — package and ship

Final layout:

```
Plugins/HelloEditorPlugin/
├── HelloEditorPlugin.dll
└── input.ini          (created on first run with your default binding)
```

`ut.ini`:

```ini
[Plugins]
plugin_0 = true,HelloEditorPlugin
```

That's it. You've built a working editor plugin with UI, per-frame state,
callbacks, hotkeys, and undo.

## What to learn next

- **More callbacks** — graphics begin/end-scene, post-processing
  (`utinni::postProcessing::addPreSceneRenderCallback` ↔
  `UtinniCore.Swg.Misc.postProcessing` via P/Invoke for advanced rendering work).
- **Drag-drop from a `Form`** — see the Jawa Toolbox's `FormObjectBrowser`.
- **Custom undo merge** — collapse continuous slider drags into one
  undo entry via `Merge()`.
- **Gizmo integration** — call `imgui_impl.Enable(targetObject)` and
  subscribe to `ImGuiCallbacks` for translate/rotate.
- **Your own command parser** — add a C++ half to your plugin (see
  `ExampleCppPlugin`) and expose `/hello` slash commands in chat.

## Common mistakes

1. **Touching WinForms from a callback** without `Invoke` →
   `InvalidOperationException`.
2. **Calling `Game.LoadScene()` from a button click** without queueing →
   crash or hang. Always wrap in `GameCallbacks.AddMainLoopCall`.
3. **Saving plugin state in static fields** → state persists across plugin
   reloads via debug-reattach. Use instance fields tied to your plugin object.
4. **Naming hotkeys without a prefix** → collisions with other plugins.
   Use `HelloPlugin.SomeBinding`.
5. **Calling `Log.Info` from inside a `Form.Paint`** → log sink callbacks
   fire on whatever thread the log came from; if a UI paint logs and the
   sink updates UI, you can recurse into paint. Log sparingly from UI hot
   paths.

## See also

- [Plugin framework](plugin-framework.md)
- [Callbacks reference](callbacks.md)
- [UI framework](ui-framework.md)
- [Hotkeys](hotkeys.md), [Undo / Redo](undo-redo.md)
- The Jawa Toolbox plugin source — the most complete worked example.
