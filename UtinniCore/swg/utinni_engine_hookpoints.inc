// ======================================================================
//
// utinni_engine_hookpoints.inc -- canonical X-macro name list for the
// Utinni engine entry-point advertisement contract. SHARED VERBATIM with
// D:/Code/Utinni (re-copied at each catalog wave so the two repos cannot
// drift). This file is the single source of truth for the set of advertised
// endpoint NAMES.
//
// Format: UTINNI_HOOKPOINT(group, name) -- the contract name is "group::name".
// The includer #defines UTINNI_HOOKPOINT before #include-ing this file and
// #undef's it after, expanding it to (a) the table rows on the provider side,
// (b) the required-name set + the coverage gate on the consumer side, and
// (c) the s_requiredNames[] / count self-check in utinni_advertise.cpp.
//
// 37-01 spike: the 3 representative rows below prove the full mechanism --
// a crash-fixer thunk (config), a static fn (game), and a non-virtual member
// PMF (commandParser). 37-02/37-03 EXTEND this file with the MVP / full
// catalog. The table BODY in utinni_advertise.cpp stays hand-written (rows
// are heterogeneous: static fn / bit_cast PMF / ctor thunk / accessor) and
// the coverage self-check bridges this name list to that body.
//
// See .planning/handoff/2026-06-20-utinni-engine-entrypoint-advertisement-spec.md.
// ======================================================================

// ----------------------------------------------------------------------
// 37-02 MVP required set (~boot -> render-overlay -> scene-change repro path).
// Groups: config, client, game, graphics, scene::groundScene(clean rows only),
// cui::manager, cui::io, consoleHelper, commandParser. Each name here has a
// source-confirmed, signature-checked row in utinni_advertise.cpp (the table
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
// utinni_advertise.cpp carry the class MISMATCH; this list keeps the CONTRACT
// names.
UTINNI_HOOKPOINT(config,        loadOverrideConfig)
UTINNI_HOOKPOINT(config,        loadConfigFileBuffer)
UTINNI_HOOKPOINT(config,        loadConfigFileString)
UTINNI_HOOKPOINT(config,        setModalChat)
UTINNI_HOOKPOINT(config,        getModalChat)

// -- client (exe entry + win32 exe-statics; ClientMain.h / Os.cpp / DebugHelp.cpp) --
// wndProc/writeMiniDump (38-02) are external-linkage shims over the PRIVATE
// Os::WindowProc (__stdcall) and DebugHelp::writeMiniDump (win32-private header).
// writeCrashLog + setupStartDataInstall are NONEXISTENT in this tree (grep = 0
// hits) -> OMITTED here + in the .cpp, flagged for the EPA-08 handback.
UTINNI_HOOKPOINT(client,        clientMain)
UTINNI_HOOKPOINT(client,        wndProc)
UTINNI_HOOKPOINT(client,        writeMiniDump)

// -- game (clientGame, all static; Game.h) ----------------------------------
UTINNI_HOOKPOINT(game,          install)
UTINNI_HOOKPOINT(game,          quit)
UTINNI_HOOKPOINT(game,          mainLoop)
UTINNI_HOOKPOINT(game,          setupScene)
UTINNI_HOOKPOINT(game,          cleanupScene)
UTINNI_HOOKPOINT(game,          getPlayer)
UTINNI_HOOKPOINT(game,          getPlayerCreatureObject)
UTINNI_HOOKPOINT(game,          getCamera)
UTINNI_HOOKPOINT(game,          getConstCamera)
UTINNI_HOOKPOINT(game,          isViewFirstPerson)
UTINNI_HOOKPOINT(game,          isHudSceneTypeSpace)
UTINNI_HOOKPOINT(game,          g_runningFlags)

// -- graphics (clientGraphics facade, all static; Graphics.h) ---------------
UTINNI_HOOKPOINT(graphics,      install)
UTINNI_HOOKPOINT(graphics,      update)
UTINNI_HOOKPOINT(graphics,      beginScene)
UTINNI_HOOKPOINT(graphics,      endScene)
UTINNI_HOOKPOINT(graphics,      present)
UTINNI_HOOKPOINT(graphics,      presentWindow)
UTINNI_HOOKPOINT(graphics,      resize)
UTINNI_HOOKPOINT(graphics,      flushResources)
UTINNI_HOOKPOINT(graphics,      screenshot)
UTINNI_HOOKPOINT(graphics,      useHardwareCursor)
UTINNI_HOOKPOINT(graphics,      showMouseCursor)
UTINNI_HOOKPOINT(graphics,      setSystemMouseCursorPosition)
UTINNI_HOOKPOINT(graphics,      setStaticShader)
UTINNI_HOOKPOINT(graphics,      g_renderTargetWidth)
UTINNI_HOOKPOINT(graphics,      g_renderTargetHeight)

// -- scene::groundScene (clientGame; GroundScene.h) -- 38-01, EPA-05 ---------
// GroundScene is MULTIPLE-INHERITANCE (NetworkScene : public Scene, public
// MessageDispatch::Receiver) -> every PMF inflated, so each row is a __fastcall
// thunk (public methods + ctor in utinni_advertise.cpp; PRIVATE init/update/
// handleInputMap* as in-TU GroundScene.cpp forwarders). draw is VIRTUAL (SKIP,
// vtable-resolved) and g_instance is inline-getter/private (OMIT) -> both kept
// OUT of this required set (see the .cpp SKIP/OMIT notes).
UTINNI_HOOKPOINT(groundScene,   ctor)
UTINNI_HOOKPOINT(groundScene,   init)
UTINNI_HOOKPOINT(groundScene,   reloadTerrain)
UTINNI_HOOKPOINT(groundScene,   changeCamera)
UTINNI_HOOKPOINT(groundScene,   getCurrentCamera)
UTINNI_HOOKPOINT(groundScene,   update)
UTINNI_HOOKPOINT(groundScene,   handleInputMapUpdate)
UTINNI_HOOKPOINT(groundScene,   handleInputMapEvent)

// -- cui::chatWindow (swgClientUserInterface; SwgCuiChatWindow.h) -- 38-03, EPA-05 --
// SwgCuiChatWindow is TRIPLE-INHERITANCE (SwgCuiLockableMediator, UINotification,
// MessageDispatch::Receiver [SwgCuiChatWindow.h:58-61]) -> every PMF inflated, so
// each row is a __fastcall thunk in utinni_advertise.cpp (all 4 target PUBLIC
// non-virtual methods -- no friend decl needed). Contract names here are the SPEC
// names; the in-tree method NAME MISMATCH (acceptTextInput/appendToAllTabs/
// appendTextToCurrentTab/performEnterKey) is baked into the .cpp row comments.
// ctor is an MI ctor requiring a live UIPage& arg (37-03 already deferred) -> OMIT
// here (DEFER); chatEnterHandler advertises the CLEAN-entry performEnterKey only,
// the Issue #11 mid-function NOP is a separate SWGEmu-only joint decision (.cpp note).
UTINNI_HOOKPOINT(cuiChatWindow, enableTextInput)
UTINNI_HOOKPOINT(cuiChatWindow, writeToAllTabs)
UTINNI_HOOKPOINT(cuiChatWindow, writeToCurrentTab)
UTINNI_HOOKPOINT(cuiChatWindow, chatEnterHandler)

// -- cui::manager (clientUserInterface, all static; CuiManager.h) ------------
UTINNI_HOOKPOINT(cuiManager,    render)
UTINNI_HOOKPOINT(cuiManager,    setSize)
UTINNI_HOOKPOINT(cuiManager,    togglePointer)
UTINNI_HOOKPOINT(cuiManager,    restartMusic)
UTINNI_HOOKPOINT(cuiManager,    g_instance)

// -- cui::io (clientUserInterface; CuiIoWin.h) -------------------------------
UTINNI_HOOKPOINT(cuiIo,         setKeyboardInputActive)
UTINNI_HOOKPOINT(cuiIo,         requestKeyboard)
UTINNI_HOOKPOINT(cuiIo,         g_instance)

// -- consoleHelper (clientUserInterface; CuiConsoleHelper.h) -----------------
UTINNI_HOOKPOINT(consoleHelper, sendInput)

// -- commandParser (sharedCommandParser; CommandParser.h) --------------------
UTINNI_HOOKPOINT(commandParser, addSubCommand)

// ======================================================================
// 37-03 full-catalog required set. Extends the 37-02 MVP. Every name here
// has a source-confirmed, signature-checked row in utinni_advertise.cpp.
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
UTINNI_HOOKPOINT(extent,        intersect)

// -- object (sharedObject; Object.h) -- NON-VIRTUAL getters/setters only -----
// (addToWorld/removeFromWorld/setParentCell are VIRTUAL -> skipped in .cpp;
//  move_o is INLINE Object.h:1216 -> OMITTED; no plain getType/move symbol.)
UTINNI_HOOKPOINT(object,        getObjectType)
UTINNI_HOOKPOINT(object,        getObjectTemplate)
UTINNI_HOOKPOINT(object,        getObjectTemplateName)
UTINNI_HOOKPOINT(object,        getNetworkId)
UTINNI_HOOKPOINT(object,        getParentCell)
UTINNI_HOOKPOINT(object,        getTransform_o2w)
UTINNI_HOOKPOINT(object,        setTransform_o2w)
UTINNI_HOOKPOINT(object,        getPosition_w)
UTINNI_HOOKPOINT(object,        setPosition_w)
UTINNI_HOOKPOINT(object,        getAppearance)
UTINNI_HOOKPOINT(object,        setAppearance)
UTINNI_HOOKPOINT(object,        move_p)

// -- objectTemplate (sharedObject/sharedGame) -------------------------------
// Base ObjectTemplate is virtual-heavy; the filename getters live on the
// NON-virtual SharedObjectTemplate (single-inheritance chain -> clean PMF).
UTINNI_HOOKPOINT(objectTemplate, createObject)
UTINNI_HOOKPOINT(objectTemplate, getAppearanceFilename)
UTINNI_HOOKPOINT(objectTemplate, getPortalLayoutFilename)
UTINNI_HOOKPOINT(objectTemplate, getClientDataFile)

// -- worldSnapshot (clientGame; WorldSnapshot.h) -- ALL STATIC in this tree --
// (the plan's instance methods openFile/saveFile/getNodeByIndex/... are the
//  Utinni OFFLINE WorldSnapshot, not our runtime scene loader; OMITTED.)
UTINNI_HOOKPOINT(worldSnapshot, load)
UTINNI_HOOKPOINT(worldSnapshot, addObject)
UTINNI_HOOKPOINT(worldSnapshot, removeObject)
UTINNI_HOOKPOINT(worldSnapshot, moveObject)
UTINNI_HOOKPOINT(worldSnapshot, getLoadingPercent)
UTINNI_HOOKPOINT(worldSnapshot, detailLevelChanged)

// -- camera (clientGraphics; Camera.h) -- NON-VIRTUAL setters (non-inline) ---
UTINNI_HOOKPOINT(camera,        setViewport)
UTINNI_HOOKPOINT(camera,        setNearPlane)
UTINNI_HOOKPOINT(camera,        setFarPlane)
UTINNI_HOOKPOINT(camera,        setHorizontalFieldOfView)
UTINNI_HOOKPOINT(camera,        reverseProjectInViewportSpace)

// -- misc statics (memory/audio/file/report) --------------------------------
UTINNI_HOOKPOINT(memory,        allocate)
UTINNI_HOOKPOINT(memory,        free)
UTINNI_HOOKPOINT(audio,         setMasterVolume)
UTINNI_HOOKPOINT(audio,         getMasterVolume)
UTINNI_HOOKPOINT(treeFile,      open)
UTINNI_HOOKPOINT(report,        print)

// -- commandParser ctors (free-function __thiscall thunks) ------------------
UTINNI_HOOKPOINT(commandParser, ctor1)
UTINNI_HOOKPOINT(commandParser, ctor2)

// -- globals (§8 #3) -- accessor-style; raw &g where genuinely accessible ----
UTINNI_HOOKPOINT(graphics,      g_frameNumber)
