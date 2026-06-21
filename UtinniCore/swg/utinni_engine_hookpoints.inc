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

// -- config (sharedFoundation, all static; ConfigFile.h) --------------------
UTINNI_HOOKPOINT(config,        loadOverrideConfig)
UTINNI_HOOKPOINT(config,        loadConfigFileBuffer)
UTINNI_HOOKPOINT(config,        loadConfigFileString)

// -- client (exe entry; ClientMain.h) ---------------------------------------
UTINNI_HOOKPOINT(client,        clientMain)

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

// -- scene::groundScene -- ALL DEFERRED to 37-03: GroundScene is a MULTIPLE-
//    INHERITANCE class (via NetworkScene : public Scene, public MessageDispatch::
//    Receiver) -> inflated member PMFs fail the pmfToVoid sizeof guard. Even the
//    "clean" reloadTerrain/getCurrentCamera/setView rows need a __thiscall thunk
//    (37-03 tier). Not advertised this wave (kept out of the required set).

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
