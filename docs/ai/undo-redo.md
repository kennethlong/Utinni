# Undo / Redo

> Audience: .NET editor plugin authors.

Utinni provides a small, event-driven undo/redo system. Plugins **raise an
event** to push a command; they never directly touch the stacks. The host
manages the stacks, the merge logic, the redo invalidation, and the
scene-cleanup reset.

## Surface

`UndoRedo/IUndoCommand.cs`:

```csharp
public interface IUndoCommand
{
    string GetText();                         // user-visible label in history list
    void   Execute();                          // redo — apply
    void   Undo();                             // reverse
    bool   AllowMerge();                       // can next command absorb into this?
    bool   Merge(IUndoCommand newCommand);     // returns true if absorbed
}
```

`UndoRedo/UndoRedoManager.cs`:

```csharp
public class UndoRedoManager
{
    public Stack<IUndoCommand> UndoCommands;
    public Stack<IUndoCommand> RedoCommands;

    public UndoRedoManager(Action onUpdate, Action undoCb, Action redoCb);

    public void AddUndoCommand(IEditorPlugin editorPlugin);
    public void Undo(int count = 1);
    public void Redo(int count = 1);
}
```

`PluginFramework/IEditorPlugin.cs`:

```csharp
public class AddUndoCommandEventArgs : EventArgs
{
    public IUndoCommand UndoCommand;
    public AddUndoCommandEventArgs(IUndoCommand cmd) { UndoCommand = cmd; }
}

public interface IEditorPlugin : IPlugin
{
    EventHandler<AddUndoCommandEventArgs> AddUndoCommand { get; set; }
    // ...
}
```

## Lifecycle

```mermaid
sequenceDiagram
  participant Plugin
  participant Mgr as UndoRedoManager
  participant FormMain
  participant Host as GameCallbacks

  FormMain->>Mgr: new UndoRedoManager(onUpdate=RefreshDropdown, undoCb, redoCb)
  Mgr->>Host: GameCallbacks.AddCleanupSceneCall(() => clear stacks)
  loop for each IEditorPlugin
    FormMain->>Mgr: Mgr.AddUndoCommand(plugin)
    Note over Mgr,Plugin: plugin.AddUndoCommand += Mgr.HandleAdd
  end

  Plugin->>Plugin: state changes (gizmo, button, etc.)
  Plugin->>Mgr: AddUndoCommand?.Invoke(plugin, new AddUndoCommandEventArgs(cmd))
  Mgr->>Mgr: RedoStack.Clear()
  alt UndoStack non-empty and top.AllowMerge() and top.Merge(cmd)
    Note over Mgr: absorbed; no push
  else
    Mgr->>Mgr: UndoStack.Push(cmd)
  end
  Mgr->>FormMain: onUpdate() — refresh title-bar dropdown
```

## Pushing a command (the only thing plugins write)

```csharp
AddUndoCommand?.Invoke(this,
    new AddUndoCommandEventArgs(
        new WorldSnapshotNodePositionChangedCommand(
            node, oldTransform, newTransform)));
```

This event invocation is the **entire** plugin-side API. `FormMain` wires
your `AddUndoCommand` event to `UndoRedoManager.HandleAdd` once, during
init. You raise; the manager handles.

## Built-in commands

`Commands/WorldSnapshotCommands.cs`:

| Command                                          | What it captures                                                         | Merge?    |
| ------------------------------------------------ | ------------------------------------------------------------------------ | --------- |
| `AddWorldSnapshotNodeCommand(node)`              | A node about to be added.                                                | No        |
| `RemoveWorldSnapshotNodeCommand(node)`           | A node about to be removed (stores a deep copy for re-add).              | No        |
| `WorldSnapshotNodePositionChangedCommand(node, before, after)` | Two `Transform`s.                                                      | No        |
| `WorldSnapshotNodeRotationChangedCommand(node, before, after)` | Two `Transform`s.                                                      | No        |

All four use `GroundSceneCallbacks.AddUpdateLoopCall` inside `Execute()` /
`Undo()` so the actual native mutation happens on the game thread — you can
fire the event from a UI handler without breaking thread discipline.

## Writing your own `IUndoCommand`

```csharp
public class SetTimeOfDayCommand : IUndoCommand
{
    private readonly float before;
    private readonly float after;

    public SetTimeOfDayCommand(float before, float after)
    {
        this.before = before;
        this.after  = after;
    }

    public string GetText() => $"Set time-of-day to {after:0.00}";

    public void Execute() =>
        GameCallbacks.AddMainLoopCall(() => Terrain.Get().SetTimeOfDay(after));

    public void Undo() =>
        GameCallbacks.AddMainLoopCall(() => Terrain.Get().SetTimeOfDay(before));

    public bool AllowMerge() => true;

    public bool Merge(IUndoCommand newCommand)
    {
        // Merge consecutive "Set time-of-day" actions so a slider drag becomes one entry.
        if (newCommand is SetTimeOfDayCommand other)
        {
            // Keep our "before", adopt their "after".
            // (immutable fields → we'd need to re-design slightly; this is illustrative)
            return false;
        }
        return false;
    }
}
```

### Merge semantics

`UndoRedoManager.HandleAdd`:

```
if UndoStack non-empty and UndoStack.Peek().AllowMerge():
    if UndoStack.Peek().Merge(newCommand):
        return    // absorbed — do not push
UndoStack.Push(newCommand)
```

So merging is **destination-eats-source**: the existing top-of-stack command
gets to adjust itself based on the incoming one. If it returns `true`, the
new command is dropped on the floor.

Typical merge use cases:

- Slider drag — collapse N "set value to X" commands into one "set value to
  final-X" command.
- Mouse-drag gizmo — collapse a stream of position deltas into a single
  before/after move.

If merging is fiddly to get right, return `AllowMerge() => false` and push
distinct commands. Users have an editor option to undo/redo multiple steps at
once.

## Undo / Redo invocation

The editor wires three sources to the manager:

| Source                                          | Calls                                                                  |
| ----------------------------------------------- | ---------------------------------------------------------------------- |
| Title-bar Undo button click                     | `manager.Undo(1)`                                                       |
| Title-bar Redo button click                     | `manager.Redo(1)`                                                       |
| `UndoRedoListDropDown` "undo through this row"  | `manager.Undo(N)` / `manager.Redo(N)`                                   |
| (Optional) plugin-defined hotkey                | `manager.Undo()` / `manager.Redo()` — but plugins normally don't reach into the manager directly; rebind the title-bar buttons via the editor's standard `Ctrl+Z` if needed. |

## Scene-cleanup reset

`UndoRedoManager`'s constructor registers
`GameCallbacks.AddCleanupSceneCall(() => { UndoCommands.Clear(); RedoCommands.Clear(); ... })`.

This is intentional: world-snapshot identities don't survive a scene unload,
and resurrecting them on undo would either crash or silently no-op. **Plugin
authors should not expect undo history to persist across scene changes.**

## Patterns

### Capture before + after at the right moment

For continuous edits (gizmo, slider) the natural pattern is:

```csharp
// 1. On "start of edit" (gizmo enabled, slider focus, etc.):
beforeT = obj.GetTransform();

// 2. On every change:
obj.SetTransform(newT);   // already happens via the gizmo / slider

// 3. On "end of edit" (gizmo disabled, slider blur, mouse-up):
AddUndoCommand?.Invoke(this, new AddUndoCommandEventArgs(
    new WorldSnapshotNodePositionChangedCommand(node, beforeT, obj.GetTransform())));
```

Capturing *before* at the start and pushing once at the end gives you a clean
single-step undo without merge gymnastics.

### Always update via the same path that Execute uses

Bad:

```csharp
// Live edit
obj.SetTransform(newT);

// Undo command
public void Execute() { obj.SetTransform(after); }
public void Undo()    { obj.SetTransform(before); }
```

— the live edit isn't routed through `Execute`, so any side-effects
`Execute` does (refresh detail level, notify renderer) won't happen on the
initial change. **Push the live edit through the same `Execute()` path,**
or factor that into a shared helper.

### Compose multi-step edits as a single command

If a single user action results in multiple object mutations, capture them
all in one `IUndoCommand` with internal lists:

```csharp
public class BatchMoveCommand : IUndoCommand
{
    private readonly List<(Object obj, Transform before, Transform after)> moves;

    public void Execute() { foreach (var m in moves) m.obj.SetTransform(m.after); }
    public void Undo()    { foreach (var m in moves) m.obj.SetTransform(m.before); }
    public string GetText() => $"Move {moves.Count} object(s)";
    public bool AllowMerge() => false;
    public bool Merge(IUndoCommand newCommand) => false;
}
```

## Things to watch out for

- **Stale references.** If you store a `Node` and the user later deletes it
  by other means, your `Undo()` will operate on a freed pointer. The
  built-in `RemoveWorldSnapshotNodeCommand` works around this by storing a
  *copy* of the node, not a reference.
- **Redo invalidation.** Any push clears the redo stack. Plugins that emit
  many tiny commands "behind the scenes" (e.g. an auto-correct routine) can
  unintentionally trash the user's redo history. Either merge aggressively
  or don't emit commands for non-user-initiated edits.
- **Threading.** The event invocation can happen on either thread; the
  manager doesn't lock. Pushing from two threads simultaneously is
  undefined.

## See also

- [Plugin framework](plugin-framework.md) — `AddUndoCommand` event.
- [Callbacks reference](callbacks.md) — `AddCleanupSceneCall` is what
  clears the stack.
- [Bridge — Commands/WorldSnapshotCommands](bridge.md#built-in-commands) —
  the four built-in implementations.
