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

using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace UtinniCoreDotNet.Tests.PreservationAudit
{
    /// <summary>
    /// STAB-04 preservation audit (Phase 6 / Plan 06-05 Task 4 / D-18). One fail-on-violation
    /// Fact per load-bearing foundation enumerated in .planning/intel/constraints.md
    /// (CON-N-01..09 native, CON-M-01..09 managed, CON-T-01..05 process/tooling). Each Fact
    /// greps the live source tree (build output / vendored trees excluded) and FAILS if the
    /// foundation has been refactored away -- "warn-only is the wrong shape" (Phase 4 D-11).
    /// Companion narrative + per-item dispositions live in
    /// .planning/phases/06-.../06-AUDIT.md.
    /// </summary>
    public class PreservationAuditTests
    {
        // ---------------------------------------------------------------- Native (CON-N-01..09)

        [Fact] // CON-N-01: swg::<subsystem> detour-table pattern (pX x = (pX)0xRVA)
        public void CON_N_01_DetourTablePattern_Present()
        {
            int files = RepoRoot.CountFilesMatching(
                "UtinniCore/swg", @"\(p[A-Z][A-Za-z0-9_]*\)0x[0-9A-Fa-f]{5,}", ".cpp", ".h");
            Assert.True(files >= 30,
                "CON-N-01 violated: expected >=30 swg files using the (pX)0xRVA detour-table pattern, found " + files);
        }

        [Fact] // CON-N-02: utinni:: thin-wrapper firewall over swg::*
        public void CON_N_02_ThinWrapperFirewall_Intact()
        {
            Assert.False(RepoRoot.FileMatches("UtinniCore/utinni.h", "#include \"swg/"),
                "CON-N-02 violated: the public facade utinni.h must not #include swg/* headers (firewall leak).");
            Assert.True(RepoRoot.FileMatches("UtinniCore/swg/object/object.cpp", @"namespace swg")
                     && RepoRoot.FileMatches("UtinniCore/swg/object/object.cpp", @"namespace utinni"),
                "CON-N-02 violated: object.cpp must keep the dual swg::/utinni:: wrapper separation.");
        }

        [Fact] // CON-N-03: mid-function naked trampolines with pushad/popad
        public void CON_N_03_NakedMidTrampolines_Present()
        {
            int files = RepoRoot.CountFilesMatching(
                "UtinniCore", @"__declspec\(naked\)|midPopCell|midCrashLogWrite|midCtor", ".cpp", ".h");
            Assert.True(files >= 1,
                "CON-N-03 violated: no naked mid-function trampolines found in UtinniCore.");
        }

        [Fact] // CON-N-04: memory::copy / createJMP bracket writes with VirtualProtect save/restore
        public void CON_N_04_VirtualProtectBracket_Present()
        {
            int n = RepoRoot.MatchCount("UtinniCore/utility/memory.cpp", "VirtualProtect");
            Assert.True(n >= 2,
                "CON-N-04 violated: expected >=2 VirtualProtect calls (save+restore) in utility/memory.cpp, found " + n);
        }

        [Fact] // CON-N-05: Launcher suspended-process + EP-park (EB FE) + CreateRemoteThread(LoadLibraryA) + OEP-restore
        public void CON_N_05_LauncherInjectionFlow_Intact()
        {
            Assert.True(RepoRoot.FileMatches("Launcher/main.cpp", "CreateRemoteThread"),
                "CON-N-05 violated: Launcher must use CreateRemoteThread for injection.");
            Assert.True(RepoRoot.FileMatches("Launcher/main.cpp", "LoadLibraryA"),
                "CON-N-05 violated: Launcher must inject via LoadLibraryA.");
            Assert.True(RepoRoot.FileMatches("Launcher/main.cpp", @"0xEB,\s*0xFE"),
                "CON-N-05 violated: Launcher must EP-park the target with the EB FE (jmp $) patch.");
        }

        [Fact] // CON-N-06: imgui_impl device-loss handling (isSetup guard)
        public void CON_N_06_ImguiIsSetupGuard_Present()
        {
            Assert.True(RepoRoot.FileMatches("UtinniCore/swg/ui/imgui_impl.cpp", "isSetup"),
                "CON-N-06 violated: imgui_impl.cpp must keep the isSetup device-loss guard.");
        }

        [Fact] // CON-N-07: Game::loadScene two-frame state machine (loadNewScene + sceneCleaned)
        public void CON_N_07_LoadSceneStateMachine_Intact()
        {
            Assert.True(RepoRoot.FileMatches("UtinniCore/swg/game/game.cpp", "loadNewScene")
                     && RepoRoot.FileMatches("UtinniCore/swg/game/game.cpp", "sceneCleaned"),
                "CON-N-07 violated: game.cpp must keep the loadNewScene/sceneCleaned two-frame state machine.");
        }

        [Fact] // CON-N-08: pImpl idiom keeps STL out of the DLL boundary (PluginManager + UtINI)
        public void CON_N_08_PimplIdiom_Present()
        {
            foreach (var h in new[] { "UtinniCore/plugin_framework/plugin_manager.h", "UtINI/utini.h" })
            {
                Assert.True(RepoRoot.FileMatches(h, @"struct Impl;") && RepoRoot.FileMatches(h, @"Impl\* pImpl"),
                    "CON-N-08 violated: " + h + " must keep the forward-declared struct Impl; + Impl* pImpl idiom.");
            }
        }

        [Fact] // CON-N-09: spdlog base_sink<std::mutex> for the OutputSink
        public void CON_N_09_OutputSinkBaseSink_Present()
        {
            Assert.True(RepoRoot.FileMatches("UtinniCore/utility/log.cpp", @"base_sink<std::mutex>"),
                "CON-N-09 violated: the OutputSink must derive from spdlog::sinks::base_sink<std::mutex>.");
        }

        // --------------------------------------------------------------- Managed (CON-M-01..09)

        [Fact] // CON-M-01: IPlugin / IEditorPlugin minimal SPI
        public void CON_M_01_PluginSpiShape_Intact()
        {
            Assert.True(RepoRoot.FileMatches("UtinniCoreDotNet/PluginFramework/IPlugin.cs", @"interface IPlugin")
                     && RepoRoot.FileMatches("UtinniCoreDotNet/PluginFramework/IPlugin.cs", "GetConfig"),
                "CON-M-01 violated: IPlugin must declare the GetConfig SPI.");
            Assert.True(RepoRoot.FileMatches("UtinniCoreDotNet/PluginFramework/IEditorPlugin.cs", "GetForms")
                     && RepoRoot.FileMatches("UtinniCoreDotNet/PluginFramework/IEditorPlugin.cs", "GetSubPanels"),
                "CON-M-01 violated: IEditorPlugin must declare GetForms + GetSubPanels.");
        }

        [Fact] // CON-M-02: [InheritedExport] for MEF discovery
        public void CON_M_02_InheritedExport_Present()
        {
            Assert.True(RepoRoot.FileMatches("UtinniCoreDotNet/PluginFramework/IPlugin.cs", @"InheritedExport")
                     || RepoRoot.FileMatches("UtinniCoreDotNet/PluginFramework/IEditorPlugin.cs", @"InheritedExport"),
                "CON-M-02 violated: the plugin interfaces must carry [InheritedExport] for MEF discovery.");
        }

        [Fact] // CON-M-03: WorldSnapshotCommands copy-on-construct
        public void CON_M_03_WorldSnapshotCopyOnConstruct_Present()
        {
            Assert.True(RepoRoot.FileMatches("UtinniCoreDotNet/Commands/WorldSnapshotCommands.cs",
                    @"new WorldSnapshotReaderWriter\.Node\("),
                "CON-M-03 violated: WorldSnapshotCommands must copy the node at command-creation time.");
        }

        [Fact] // CON-M-04: HotkeyManager CreateSettings / Load / Save triplet
        public void CON_M_04_HotkeyManagerTriplet_Present()
        {
            const string f = "UtinniCoreDotNet/Hotkeys/HotkeyManager.cs";
            Assert.True(RepoRoot.FileMatches(f, "CreateSettings")
                     && RepoRoot.FileMatches(f, @"\bLoad\b")
                     && RepoRoot.FileMatches(f, @"\bSave\b"),
                "CON-M-04 violated: HotkeyManager must keep the CreateSettings/Load/Save triplet.");
        }

        [Fact] // CON-M-05: UndoRedoManager.OnCleanupCallback clears both stacks on scene-cleanup
        public void CON_M_05_UndoRedoCleanupCallback_Present()
        {
            Assert.True(RepoRoot.FileMatches("UtinniCoreDotNet/UndoRedo/UndoRedoManager.cs", "OnCleanupCallback"),
                "CON-M-05 violated: UndoRedoManager must keep the OnCleanupCallback scene-cleanup hook.");
        }

        [Fact] // CON-M-06: UtinniForm custom title bar via OnPaint + WM_NCHITTEST
        public void CON_M_06_UtinniFormCustomTitlebar_Intact()
        {
            const string f = "UtinniCoreDotNet/UI/Forms/UtinniForm.cs";
            Assert.True(RepoRoot.FileMatches(f, "WM_NCHITTEST") && RepoRoot.FileMatches(f, "OnPaint"),
                "CON-M-06 violated: UtinniForm must keep the custom title bar (OnPaint + WM_NCHITTEST).");
        }

        [Fact] // CON-M-07: Log.AddOutputSinkCallback fanout
        public void CON_M_07_LogOutputSinkCallback_Present()
        {
            Assert.True(RepoRoot.FileMatches("UtinniCoreDotNet/Utility/Log.cs", "AddOutputSinkCallback"),
                "CON-M-07 violated: Log must keep the AddOutputSinkCallback fanout pattern.");
        }

        [Fact] // CON-M-08: PanelGame ties SWG-window placement to the panel (evolved: Issue #9/#10 reparent)
        public void CON_M_08_PanelGameOwnsSwgWindow_Intact()
        {
            const string f = "UtinniCoreDotNet/UI/Controls/PanelGame.cs";
            // The original SetHwnd-on-every-layout was superseded by owned-popup reparenting
            // (Issue #9/#10); the foundation -- PanelGame owns SWG-window placement -- persists.
            Assert.True(RepoRoot.FileMatches(f, "ReparentSwgWindow") && RepoRoot.FileMatches(f, "PanelGame_Layout"),
                "CON-M-08 violated: PanelGame must keep ownership of SWG-window placement (reparent + layout handler).");
        }

        [Fact] // CON-M-09: drag-drop ray-cast primitive (cui_hud) underpinning FormObjectBrowser orchestration
        public void CON_M_09_DragDropRaycastPrimitive_Present()
        {
            int n = RepoRoot.CountFilesMatching("UtinniCore/swg/ui", "(?i)collideCursorWithWorld", ".cpp", ".h");
            Assert.True(n >= 1,
                "CON-M-09 violated: the cui_hud CollideCursorWithWorld ray-cast primitive (drag-drop orchestration) is missing.");
        }

        // ------------------------------------------------------- Process / tooling (CON-T-01..05)

        [Fact] // CON-T-01: UtinniCore.vcxproj post-build chain (xcopy data + UtinniCoreDotNetGen.exe)
        public void CON_T_01_PostBuildChain_Intact()
        {
            const string f = "UtinniCore/UtinniCore.vcxproj";
            Assert.True(RepoRoot.FileMatches(f, "PostBuildEvent")
                     && RepoRoot.FileMatches(f, "xcopy")
                     && RepoRoot.FileMatches(f, "UtinniCoreDotNetGen.exe"),
                "CON-T-01 violated: UtinniCore post-build must xcopy data/ then run UtinniCoreDotNetGen.exe.");
        }

        [Fact] // CON-T-02: RelWithDbgInfo triple-config across the five core projects + the C++ example
        public void CON_T_02_TripleConfig_Intact()
        {
            // CON-T-02 covers the five core native projects + the C++ example ("five projects +
            // templates + examples"). The Phase-3 R-B test fixtures (CrtMatchPlugin, LegacyPlugin)
            // and the Phase-6 LoaderLockHarness are test-only Debug+Release projects -- not in
            // scope -- and the C++ plugin TEMPLATE uses tokenised VS config placeholders rather
            // than literal ProjectConfiguration entries (see 06-AUDIT.md CON-T-02).
            var inScope = new[]
            {
                "UtinniCore/UtinniCore.vcxproj",
                "UtINI/UtINI.vcxproj",
                "Launcher/Launcher.vcxproj",
                "UtinniCore-Symbols/UtinniCore-Symbols.vcxproj",
                "UtinniCore.Tests/UtinniCore.Tests.vcxproj",
                "sdk/examples/ExampleCppPlugin/ExampleCppPlugin.vcxproj",
            };
            foreach (var rel in inScope)
            {
                var full = Path.Combine(RepoRoot.Dir, rel.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(full), "CON-T-02: expected in-scope project is missing: " + rel);
                var text = File.ReadAllText(full);
                int configs = Regex.Matches(text, "ProjectConfiguration Include").Count;
                Assert.True(configs >= 3 && text.Contains("RelWithDbgInfo"),
                    "CON-T-02 violated: " + rel + " must keep Debug + Release + RelWithDbgInfo (found " + configs + " configs).");
            }
        }

        [Fact] // CON-T-03: two-language template parity (C++ + .NET)
        public void CON_T_03_TwoLanguageTemplateParity_Present()
        {
            Assert.True(RepoRoot.Exists("sdk/UtinniCppPluginTemplate"),
                "CON-T-03 violated: the C++ plugin template is missing.");
            Assert.True(RepoRoot.Exists("sdk/UtinniPluginTemplates/DotNetPluginTemplate"),
                "CON-T-03 violated: the .NET plugin template is missing.");
        }

        [Fact] // CON-T-04: Props.cs factoring (centralised plugin MSBuild boilerplate)
        public void CON_T_04_PropsCsFactoring_Present()
        {
            Assert.True(RepoRoot.Exists("sdk/UtinniPluginTemplates/Vsix/Utility/Props.cs"),
                "CON-T-04 violated: the centralised Props.cs MSBuild-boilerplate factoring is missing.");
        }

        [Fact] // CON-T-05: Jawa Toolbox *Impl separation (cross-repo; soft check + document-only)
        public void CON_T_05_JawaToolboxImplSeparation_Documented()
        {
            // CON-T-05 lives in the sibling kennethlong/UtinniPlugins repo (TheJawaToolbox), which
            // is not checked out on the CI runner. When the sibling IS present (local dev box), assert
            // the *Impl separation pattern exists; otherwise the audit is document-only (see 06-AUDIT.md).
            var sibling = Path.GetFullPath(Path.Combine(RepoRoot.Dir, "..", "UtinniPlugins"));
            if (Directory.Exists(sibling))
            {
                var tjt = Path.Combine(sibling, "The Jawa Toolbox");
                int implFiles = Directory.Exists(tjt)
                    ? Directory.EnumerateFiles(tjt, "*Impl.cs", SearchOption.AllDirectories).Count()
                    : 0;
                Assert.True(implFiles >= 1,
                    "CON-T-05 violated: TheJawaToolbox must keep its *Impl.cs separation pattern (found " + implFiles + ").");
            }
            else
            {
                // Sibling repo absent (e.g., CI): cross-repo invariant is recorded in 06-AUDIT.md.
                Assert.True(true);
            }
        }
    }
}
