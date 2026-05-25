# Utinni

[![CI](https://github.com/kennethlong/Utinni/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/kennethlong/Utinni/actions/workflows/ci.yml)

Utinni is a client plugin and injection framework which aims to provide an easier access to client and content development for Pre-CU Star Wars Galaxies and more specifically [SWGEmu](https://github.com/swgemu).

Official plugins can be found [here](https://github.com/ptklatt/UtinniPlugins).

> **Documentation:** an interactive doc set lives in [`docs/index.html`](./docs/index.html) — open it locally for the navigable HTML site (architecture, injection, plugin framework, callbacks, UI, hotkeys, undo/redo, SDK, build, tutorial, internals, regenerating bindings, glossary). The same content is available as plain markdown for AI/grep tooling under [`docs/ai/`](./docs/ai/). The plugin reference docs live in the sibling repo at [`../UtinniPlugins/docs/`](../UtinniPlugins/docs/index.html).

**Features**
* Gizmo implementation via ImGuizmo
* C# Plugin Framework
* C++ Plugin Framework
* Undo/Redo Framework in C#
* Hotkey Framework with rebindable hotkeys and a hotkey editor
* Settings handled via .ini
* Editor mode built in C# with Winforms, that natively hosts the Star Wars Galaxy client inside of a WinForms panel
* Custom WinForms form and control library
* Offline scene mode
* Game config override .cfg file
* Cmd passthrough for the launcher, enabling game settings being set via Windows shortcut
* FreeCam, including the ability to hide the player model (Only works in freecam)

**Planned key features**
* WinForm color themes (Almost done)
* Settings editor
* Game CUI Framework which allows the modification of existing UI elements and the creation of new
* Expanded FreeCam controls
* Setting to automatically attach Visual Studio on injection (Partly working)
* Combined mouse and keyboard hotkeys (Currently keyboard only)


**Preview**
[![Utinni - The Jawa Toolbox Preview](https://i.imgur.com/v7aSgWv.png)](https://www.youtube.com/watch?v=QVe-oY_Sx1Y)



**Third party libraries used:**

Please see licenses.txt for license information on libraries used.

* [CppSharp](https://github.com/mono/CppSharp)
* DetourXS
* [dearImgui](https://github.com/ocornut/imgui)
* [ImGuizmo](https://github.com/CedricGuillemet/ImGuizmo)
* [spdlog](https://github.com/gabime/spdlog)

**Credits**
* [James Webb (Sytner)](https://github.com/jdswebb) -- Pushing me to release and being a helping hand when I get absolutely stuck
* [Borrie BoBaka](https://modthegalaxy.com/index.php?members/helmedraven.3396/) -- Being there and supporting me from the very start of the development, invaluable testing, bug hunting and quality of life suggestions helped forge Utinni into what it now is
* [mezzanine](https://modthegalaxy.com/index.php?members/dsrules.896/) -- Being there and supporting me from the very start of the development, testing and experimenting with plugin development
