// ======================================================================
//
// engine_hookpoints.inc -- canonical X-macro name list for the
// Utinni engine entry-point advertisement contract. SHARED VERBATIM with
// D:/Code/Utinni (re-copied at each catalog wave so the two repos cannot
// drift). This file is the single source of truth for the set of advertised
// endpoint NAMES.
//
// Format: ENGINE_HOOKPOINT(group, name) -- the contract name is "group::name".
// The includer #defines ENGINE_HOOKPOINT before #include-ing this file and
// #undef's it after, expanding it to (a) the table rows on the provider side,
// (b) the required-name set + the coverage gate on the consumer side, and
// (c) the s_requiredNames[] / count self-check in engine_advertise.cpp.
//
// 37-01 spike: the 3 representative rows below prove the full mechanism --
// a crash-fixer thunk (config), a static fn (game), and a non-virtual member
// PMF (commandParser). 37-02/37-03 EXTEND this file with the MVP / full
// catalog. The table BODY in engine_advertise.cpp stays hand-written (rows
// are heterogeneous: static fn / bit_cast PMF / ctor thunk / accessor) and
// the coverage self-check bridges this name list to that body.
//
// See .planning/handoff/2026-06-20-utinni-engine-entrypoint-advertisement-spec.md.
// ======================================================================

// ----------------------------------------------------------------------
// 37-02 MVP required set (~boot -> render-overlay -> scene-change repro path).
// Groups: config, client, game, graphics, scene::groundScene(clean rows only),
// cui::manager, cui::io, consoleHelper, commandParser. Each name here has a
// source-confirmed, signature-checked row in engine_advertise.cpp (the table
// BODY stays hand-written: rows are heterogeneous -- static fn / bit_cast PMF /
// accessor). The runtime utinni_verifyNoNullNoDup() name-set-equality check
// bridges this list to that body (EPA-04). Re-copied verbatim into D:/Code/Utinni
// at wave close. MISMATCH corrections (RESEARCH "Spec best-guesses that are WRONG")
// are baked into the table rows, NOT here -- this list carries CONTRACT names.
//
// OMITTED / DEFERRED-to-37-03 MVP names (recorded in 37-02-SUMMARY, not advertised):
//   config::setModalChat/getModalChat (unresolved symbol), game::g_mainLoopCounter
//   (only an INLINE accessor Game::getLoopCount -> no ODR-emitted address, Pitfall 2),
//   client::{wndProc,writeMiniDump,writeCrashLog,setupStartDataInstall} (file-local
//   exe statics -> each needs an external-linkage shim, deferred), cui::manager::
//   findObjectUnderCursor (not on CuiManager), ALL groundScene::* rows (the class
//   is MULTIPLE-INHERITANCE via NetworkScene -> inflated PMFs fail the sizeof guard;
//   even reloadTerrain/getCurrentCamera/setView need a __thiscall thunk, 37-03),
//   cui::io::processEvent
//   (VIRTUAL -> Utinni resolves off the live vtable), cui::chatWindow::* +
//   chatWindow::ctor (MULTIPLE-INHERITANCE class -> inflated PMF fails the
//   sizeof(PMF)==sizeof(void*) guard -> needs __thiscall thunks, 37-03),
//   commandParser::{ctor1,ctor2}(thunk) + createDelegateCommands(PROTECTED).
// ----------------------------------------------------------------------

// -- config (sharedFoundation ConfigFile statics + 38-02 CuiPreferences statics) --
// setModalChat/getModalChat are the 37-02 CORRECTION: they exist as PUBLIC
// CuiPreferences statics (NOT config/ConfigFile) -- the row comments in
// engine_advertise.cpp carry the class MISMATCH; this list keeps the CONTRACT
// names.
ENGINE_HOOKPOINT(config,        loadOverrideConfig)
ENGINE_HOOKPOINT(config,        loadConfigFileBuffer)
ENGINE_HOOKPOINT(config,        loadConfigFileString)
ENGINE_HOOKPOINT(config,        setModalChat)
ENGINE_HOOKPOINT(config,        getModalChat)

// -- client (exe entry + win32 exe-statics; ClientMain.h / Os.cpp / DebugHelp.cpp) --
// wndProc/writeMiniDump (38-02) are external-linkage shims over the PRIVATE
// Os::WindowProc (__stdcall) and DebugHelp::writeMiniDump (win32-private header).
// writeCrashLog + setupStartDataInstall are NONEXISTENT in this tree (grep = 0
// hits) -> OMITTED here + in the .cpp, flagged for the EPA-08 handback.
ENGINE_HOOKPOINT(client,        clientMain)
ENGINE_HOOKPOINT(client,        wndProc)
ENGINE_HOOKPOINT(client,        writeMiniDump)

// -- game (clientGame, all static; Game.h) ----------------------------------
ENGINE_HOOKPOINT(game,          install)
ENGINE_HOOKPOINT(game,          quit)
ENGINE_HOOKPOINT(game,          mainLoop)
ENGINE_HOOKPOINT(game,          setupScene)
ENGINE_HOOKPOINT(game,          loadScene)
ENGINE_HOOKPOINT(game,          cleanupScene)
ENGINE_HOOKPOINT(game,          getPlayer)
ENGINE_HOOKPOINT(game,          getPlayerCreatureObject)
ENGINE_HOOKPOINT(game,          getCamera)
ENGINE_HOOKPOINT(game,          getConstCamera)
ENGINE_HOOKPOINT(game,          isViewFirstPerson)
ENGINE_HOOKPOINT(game,          isHudSceneTypeSpace)
ENGINE_HOOKPOINT(game,          g_runningFlags)
ENGINE_HOOKPOINT(game,          g_mainLoopCounter)

// -- graphics (clientGraphics facade, all static; Graphics.h) ---------------
ENGINE_HOOKPOINT(graphics,      install)
ENGINE_HOOKPOINT(graphics,      update)
ENGINE_HOOKPOINT(graphics,      beginScene)
ENGINE_HOOKPOINT(graphics,      endScene)
ENGINE_HOOKPOINT(graphics,      present)
ENGINE_HOOKPOINT(graphics,      presentWindow)
ENGINE_HOOKPOINT(graphics,      resize)
ENGINE_HOOKPOINT(graphics,      flushResources)
ENGINE_HOOKPOINT(graphics,      screenshot)
ENGINE_HOOKPOINT(graphics,      useHardwareCursor)
ENGINE_HOOKPOINT(graphics,      showMouseCursor)
ENGINE_HOOKPOINT(graphics,      setSystemMouseCursorPosition)
ENGINE_HOOKPOINT(graphics,      setStaticShader)
ENGINE_HOOKPOINT(graphics,      g_renderTargetWidth)
ENGINE_HOOKPOINT(graphics,      g_renderTargetHeight)

// -- scene::groundScene (clientGame; GroundScene.h) -- 38-01, EPA-05 ---------
// GroundScene is MULTIPLE-INHERITANCE (NetworkScene : public Scene, public
// MessageDispatch::Receiver) -> every PMF inflated, so each row is a __fastcall
// thunk (public methods + ctor in engine_advertise.cpp; PRIVATE init/update/
// handleInputMap* as in-TU GroundScene.cpp forwarders). draw is VIRTUAL (SKIP,
// vtable-resolved) and g_instance is inline-getter/private (OMIT) -> both kept
// OUT of this required set (see the .cpp SKIP/OMIT notes).
ENGINE_HOOKPOINT(groundScene,   ctor)
ENGINE_HOOKPOINT(groundScene,   init)
ENGINE_HOOKPOINT(groundScene,   reloadTerrain)
ENGINE_HOOKPOINT(groundScene,   changeCamera)
ENGINE_HOOKPOINT(groundScene,   getCurrentCamera)
ENGINE_HOOKPOINT(groundScene,   update)
ENGINE_HOOKPOINT(groundScene,   handleInputMapUpdate)
ENGINE_HOOKPOINT(groundScene,   handleInputMapEvent)

// -- cui::chatWindow (swgClientUserInterface; SwgCuiChatWindow.h) -- 38-03, EPA-05 --
// SwgCuiChatWindow is TRIPLE-INHERITANCE (SwgCuiLockableMediator, UINotification,
// MessageDispatch::Receiver [SwgCuiChatWindow.h:58-61]) -> every PMF inflated, so
// each row is a __fastcall thunk in engine_advertise.cpp (all 4 target PUBLIC
// non-virtual methods -- no friend decl needed). Contract names here are the SPEC
// names; the in-tree method NAME MISMATCH (acceptTextInput/appendToAllTabs/
// appendTextToCurrentTab/performEnterKey) is baked into the .cpp row comments.
// ctor is an MI ctor requiring a live UIPage& arg (37-03 already deferred) -> OMIT
// here (DEFER); chatEnterHandler advertises the CLEAN-entry performEnterKey only,
// the Issue #11 mid-function NOP is a separate SWGEmu-only joint decision (.cpp note).
ENGINE_HOOKPOINT(cuiChatWindow, enableTextInput)
ENGINE_HOOKPOINT(cuiChatWindow, writeToAllTabs)
ENGINE_HOOKPOINT(cuiChatWindow, writeToCurrentTab)
ENGINE_HOOKPOINT(cuiChatWindow, chatEnterHandler)
ENGINE_HOOKPOINT(cuiChatWindow, createNewWindow)

// -- cui::manager (clientUserInterface, all static; CuiManager.h) ------------
ENGINE_HOOKPOINT(cuiManager,    render)
ENGINE_HOOKPOINT(cuiManager,    setSize)
ENGINE_HOOKPOINT(cuiManager,    togglePointer)
ENGINE_HOOKPOINT(cuiManager,    restartMusic)
ENGINE_HOOKPOINT(cuiManager,    g_instance)

// -- cui::io (clientUserInterface; CuiIoWin.h) -------------------------------
ENGINE_HOOKPOINT(cuiIo,         setKeyboardInputActive)
ENGINE_HOOKPOINT(cuiIo,         requestKeyboard)
ENGINE_HOOKPOINT(cuiIo,         g_instance)

// -- consoleHelper (clientUserInterface; CuiConsoleHelper.h) -----------------
ENGINE_HOOKPOINT(consoleHelper, sendInput)

// -- commandParser (sharedCommandParser; CommandParser.h) --------------------
ENGINE_HOOKPOINT(commandParser, addSubCommand)

// ======================================================================
// 37-03 full-catalog required set. Extends the 37-02 MVP. Every name here
// has a source-confirmed, signature-checked row in engine_advertise.cpp.
// Symbols that the plan named but that do NOT exist in this tree (or are
// virtual / inline / multiple-inheritance-inflated) are OMITTED here and
// documented in the .cpp + 37-03-SUMMARY (graceful degradation, EPA-04:
// "a wrong & is worse than a missing row"). Re-copied verbatim into
// D:/Code/Utinni at wave close.
// ======================================================================

// -- extent (sharedCollision; BaseExtent.h) ---------------------------------
// §8 #2: the NON-virtual BaseExtent::intersect overload (collapses the
// UtinniCore retail/SWGEmu RVA split). Extent::intersect does NOT exist;
// Extent::realIntersect is protected-virtual (skipped).
ENGINE_HOOKPOINT(extent,        intersect)

// -- object (sharedObject; Object.h) -- NON-VIRTUAL getters/setters only -----
// (addToWorld/removeFromWorld/setParentCell are VIRTUAL -> skipped in .cpp;
//  move_o is INLINE Object.h:1216 -> OMITTED; no plain getType/move symbol.)
ENGINE_HOOKPOINT(object,        getObjectType)
ENGINE_HOOKPOINT(object,        getObjectTemplate)
ENGINE_HOOKPOINT(object,        getObjectTemplateName)
ENGINE_HOOKPOINT(object,        getNetworkId)
ENGINE_HOOKPOINT(object,        getParentCell)
ENGINE_HOOKPOINT(object,        getTransform_o2w)
ENGINE_HOOKPOINT(object,        setTransform_o2w)
ENGINE_HOOKPOINT(object,        getPosition_w)
ENGINE_HOOKPOINT(object,        setPosition_w)
ENGINE_HOOKPOINT(object,        getAppearance)
ENGINE_HOOKPOINT(object,        setAppearance)
ENGINE_HOOKPOINT(object,        move_p)

// -- objectTemplate (sharedObject/sharedGame) -------------------------------
// Base ObjectTemplate is virtual-heavy; the filename getters live on the
// NON-virtual SharedObjectTemplate (single-inheritance chain -> clean PMF).
ENGINE_HOOKPOINT(objectTemplate, createObject)
ENGINE_HOOKPOINT(objectTemplate, getAppearanceFilename)
ENGINE_HOOKPOINT(objectTemplate, getPortalLayoutFilename)
ENGINE_HOOKPOINT(objectTemplate, getClientDataFile)

// -- worldSnapshot (clientGame; WorldSnapshot.h) -- ALL STATIC in this tree --
// (the plan's instance methods openFile/saveFile/getNodeByIndex/... are the
//  Utinni OFFLINE WorldSnapshot, not our runtime scene loader; OMITTED.)
ENGINE_HOOKPOINT(worldSnapshot, load)
ENGINE_HOOKPOINT(worldSnapshot, addObject)
ENGINE_HOOKPOINT(worldSnapshot, removeObject)
ENGINE_HOOKPOINT(worldSnapshot, moveObject)
ENGINE_HOOKPOINT(worldSnapshot, getLoadingPercent)
ENGINE_HOOKPOINT(worldSnapshot, detailLevelChanged)

// -- camera (clientGraphics; Camera.h) -- NON-VIRTUAL setters (non-inline) ---
ENGINE_HOOKPOINT(camera,        setViewport)
ENGINE_HOOKPOINT(camera,        setNearPlane)
ENGINE_HOOKPOINT(camera,        setFarPlane)
ENGINE_HOOKPOINT(camera,        setHorizontalFieldOfView)
ENGINE_HOOKPOINT(camera,        reverseProjectInViewportSpace)

// -- misc statics (memory/audio/file/report) --------------------------------
ENGINE_HOOKPOINT(memory,        allocate)
ENGINE_HOOKPOINT(memory,        free)
ENGINE_HOOKPOINT(audio,         setMasterVolume)
ENGINE_HOOKPOINT(audio,         getMasterVolume)
ENGINE_HOOKPOINT(treeFile,      open)
ENGINE_HOOKPOINT(treeFile,      searchTree)
ENGINE_HOOKPOINT(treeFile,      enumerateFiles)
ENGINE_HOOKPOINT(report,        print)

// -- commandParser ctors (free-function __thiscall thunks) ------------------
ENGINE_HOOKPOINT(commandParser, ctor1)
ENGINE_HOOKPOINT(commandParser, ctor2)

// -- globals (§8 #3) -- accessor-style; raw &g where genuinely accessible ----
ENGINE_HOOKPOINT(graphics,      g_frameNumber)
