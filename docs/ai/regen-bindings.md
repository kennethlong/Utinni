# Regenerating bindings

> Audience: core contributors. Plugin authors don't run this — they consume
> the already-generated bindings via `UtinniCoreDotNet.dll`.

`UtinniCoreDotNet/Generated/UtinniCore.cs` is produced by **CppSharp** —
a Clang-frontend / C# code-emitter that parses C++ headers and writes
managed wrappers around them. The driver lives in `UtinniCoreDotNetGen/`.

You need to regenerate when:

1. You **add a new public function or class** to a header in `UtinniCore/`
   that you want callable from managed code.
2. You **change a signature** of an existing public function.
3. You **add a new header** (and `Program.cs` has been updated to point to
   it).
4. You **upgrade CppSharp** itself.

You do *not* need to regenerate for:

- Pure-implementation changes inside `.cpp` files.
- Adding a new `*_callback` or `add*Callback` if its delegate signature was
  already there.
- Internal headers that nobody outside `UtinniCore` includes.

## The generator

`UtinniCoreDotNetGen/Program.cs` defines a `Gen : ILibrary` class that
CppSharp's `ConsoleDriver` invokes in four phases:

### Setup

```csharp
public void Setup(Driver driver)
{
    string workingDir = AppDomain.CurrentDomain.BaseDirectory;
    string slnDir     = /* walk up from bin/<Config>/ */;

    var opts = driver.Options;
    opts.TargetTriple    = "i686-pc-win32-msvc";
    opts.UnityBuild      = true;
    opts.EnableRTTI      = true;
    opts.GeneratorKind   = GeneratorKind.CSharp;
    opts.OutputDir       = slnDir + "UtinniCoreDotNet\\Generated\\";
    opts.SymbolsLibraryName = "UtinniCore-Symbols";

    var module = opts.AddModule("UtinniCore");
    module.LibraryName = "UtinniCore";
    module.LibraryDirs.Add(slnDir + "bin\\Release\\");

    module.IncludeDirs.Add(slnDir);
    module.IncludeDirs.Add(slnDir + "external");
    module.IncludeDirs.Add(slnDir + "UtinniCore");

    module.Headers.Add("utinni.h");
    module.Headers.Add("utility/log.h");
    module.Headers.Add("plugin_framework/plugin_manager.h");
    module.Headers.Add("swg/appearance/skeleton.h");
    module.Headers.Add("swg/client/client.h");
    module.Headers.Add("swg/camera/debug_camera.h");
    module.Headers.Add("swg/game/game.h");
    module.Headers.Add("swg/graphics/directx9.h");
    module.Headers.Add("swg/graphics/graphics.h");
    module.Headers.Add("swg/misc/config.h");
    module.Headers.Add("swg/misc/network.h");
    module.Headers.Add("swg/misc/swg_math.h");
    module.Headers.Add("swg/object/client_object.h");
    module.Headers.Add("swg/object/creature_object.h");
    module.Headers.Add("swg/object/player_object.h");
    module.Headers.Add("swg/scene/ground_scene.h");
    module.Headers.Add("swg/scene/terrain.h");
    module.Headers.Add("swg/scene/render_world.h");
    module.Headers.Add("swg/scene/world_snapshot.h");
    module.Headers.Add("swg/ui/cui_chat_window.h");
    module.Headers.Add("swg/ui/cui_hud.h");
    module.Headers.Add("swg/ui/cui_io.h");
    module.Headers.Add("swg/ui/cui_manager.h");
    module.Headers.Add("swg/ui/cui_misc.h");
    module.Headers.Add("swg/ui/imgui_impl.h");

    opts.Defines.Add("SPDLOG_NO_EXCEPTIONS");
    opts.Defines.Add("FMT_EXCEPTIONS=0");
}
```

Things to know:

- **Target triple `i686-pc-win32-msvc`** — Clang has to mimic MSVC's x86 ABI
  for `__thiscall` / `__stdcall` / name mangling to come out right.
- **`SymbolsLibraryName = "UtinniCore-Symbols"`** — CppSharp will P/Invoke
  against mangled symbols that live in this lib. The `UtinniCore-Symbols`
  project exists solely so the symbols are exported in a form CppSharp can
  reference.
- **Headers list is explicit.** Anything not listed here is invisible to
  the generator. If you add a new public header, add it.

### SetupPasses / Postprocess

Both empty in current source — no custom AST transformations are run. If
you ever need to rename a generated symbol or strip a class wholesale, add
a CppSharp `TranslationUnitPass` here.

### Preprocess

```csharp
public void Preprocess(Driver driver, ASTContext ctx)
{
    ctx.IgnoreHeadersWithName("spdlog");
    ctx.IgnoreHeadersWithName("detourxs");
    ctx.IgnoreHeadersWithName("ADE32");
}
```

Filters out third-party headers we don't want to expose to managed code.
Add to this list if a new vendor lib leaks into the public surface.

## Running the generator

### From Visual Studio

1. Build `UtinniCore` in **`Release`** (the generator looks under
   `bin\Release\` for `UtinniCore.dll`).
2. Set `UtinniCoreDotNetGen` as the startup project.
3. **Debug → Start Without Debugging** (Ctrl+F5).

The generator prints CppSharp's parse progress, then writes
`UtinniCoreDotNet/Generated/UtinniCore.cs`.

### From the command line

```
cd UtinniCoreDotNetGen\bin\Release
UtinniCoreDotNetGen.exe
```

The post-build event copies CppSharp's lib folder next to the exe so this
works after a clean build.

### As a `UtinniCore` post-build step

`UtinniCore.vcxproj`'s post-build step runs the generator automatically when
configured. The hand-edited `StdEdited.cs` is **not** touched.

## Output

After a successful run, `UtinniCoreDotNet/Generated/UtinniCore.cs` is
overwritten. The file is ~5000+ lines of:

- One C# `namespace` per native namespace (e.g. `UtinniCore.Utinni`,
  `UtinniCore.Swg.Math`, `UtinniCore.ImguiGizmo`).
- One `public unsafe partial class <Name> : IDisposable` per native class
  exposed.
- One `internal partial struct __Internal { /* P/Invokes */ }` inside each
  class.
- A `ConcurrentDictionary<IntPtr, T>` named `NativeToManagedMap` for
  identity-preserving wrappers.
- Marshaled call sites that build/teardown native instances around each
  method.

The generator marks the file with the standard `<auto-generated>` comment
and a CppSharp version line — don't edit by hand.

## The hand-edited `StdEdited.cs`

`UtinniCoreDotNet/Generated/StdEdited.cs` is *not* regenerated. It contains
~800 lines of hand-tuned wrappers around `std::basic_string`,
`std::allocator`, `std::char_traits`, etc. CppSharp's auto-generated STL
wrappers don't match MSVC's STL layout precisely enough to round-trip
strings safely, so this file overrides them.

Top-of-file directive:

```csharp
[assembly: InternalsVisibleTo("UtinniCore")]
```

…and then the specialized:

```csharp
public unsafe partial class BasicString<_Elem, _Traits, _Alloc> : IDisposable
{
    // hand-written __CopyValue, constructor specialization checks, Dispose...
}
```

If you upgrade CppSharp or MSVC's STL layout shifts, you may need to
re-derive these — but **don't regenerate them automatically**. They're
maintained alongside CppSharp upgrades.

## Validating a regen

1. **Rebuild `UtinniCoreDotNet`** — the easiest smoke test. Compile errors
   typically point at:
   - Mismatched generics on STL types — likely `StdEdited.cs` is stale.
   - Missing entry points — symbol exports in `UtinniCore-Symbols` need
     refreshing.
   - Renamed types — hand-written managed code that calls into the bindings
     needs to update.
2. **Diff the file.** A clean regen against unchanged headers should be
   identical (modulo version strings). A semantic regen should show only
   the lines for the symbols you added/changed.
3. **Launch the editor with at least one known-good plugin loaded.** If
   plugins now fail to compose, you regressed a public type.
4. **Run a smoke scenario.** Load a scene → free-cam toggle → move an
   object via gizmo → undo. Hits the bulk of the bindings.

## Adding a new function (worked example)

You want to expose `utinni::GroundScene::setTimeOfDay(float t)` so .NET can
call it.

1. **Declare in the header**:

   ```cpp
   // swg/scene/ground_scene.h
   UTINNI_API void setTimeOfDay(float timeOfDay);
   ```

2. **Implement in the .cpp**:

   ```cpp
   // swg/scene/ground_scene.cpp
   void setTimeOfDay(float t) {
       swg::groundScene::setTimeOfDay(get(), t);   // call into SWG
   }
   ```

3. **(No header list change needed)** because `ground_scene.h` is already
   in `Program.cs`'s `module.Headers`.
4. **Build `UtinniCore` Release**.
5. **Run `UtinniCoreDotNetGen.exe`** — verify `Generated/UtinniCore.cs` now
   has `UtinniCore.Utinni.GroundScene.SetTimeOfDay(float)`.
6. **Rebuild `UtinniCoreDotNet`** — verify it compiles.
7. **Use it from C#**:

   ```csharp
   GroundScene.Get().SetTimeOfDay(0.5f);
   ```

## Adding a new header

1. Place the header somewhere under `UtinniCore/` and `#include` it from
   anything that needs it on the C++ side.
2. Add to `Program.cs`:

   ```csharp
   module.Headers.Add("my_subsystem/my_header.h");
   ```

3. Regenerate.

## Common regen failures

| Symptom                                              | Cause / fix                                                                                  |
| ---------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| "Couldn't find UtinniCore.dll" at generator runtime  | You haven't built `UtinniCore` Release. Build it first.                                      |
| Generator crashes on a header                        | Clang couldn't parse it — usually a missing include path or an MSVC-only construct. Wrap in `#ifndef CPPSHARP` if necessary. |
| Generated file has `Type<UNRESOLVED>` placeholders   | Forward declarations CppSharp couldn't resolve. Either include the defining header or add a `ctx.IgnoreClassWithName(...)` in `Preprocess`. |
| `UtinniCoreDotNet` compile errors after regen        | Most often a renamed type / removed function. Fix the hand-written managed call sites.        |
| Strings round-trip as garbage                        | `StdEdited.cs` is stale. Manually re-derive against the current STL.                          |

## See also

- [Bridge](bridge.md) — what the generator output looks like in use.
- [Native core](core.md) — what's exposed in the C++ headers.
- [Internals](internals.md) — the RVAs that ultimately back each generated
  P/Invoke.
- [CppSharp project](https://github.com/mono/CppSharp) — upstream docs.
