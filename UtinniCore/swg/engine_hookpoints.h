// ======================================================================
//
// engine_hookpoints.h -- Utinni engine entry-point advertisement
// contract (handoff 2026-06-20). The exe-side game-logic twin of the
// shipped graphics gl11_r.dll!GetHookPoints (Direct3d11.cpp:856-888).
//
// Defines the name->pointer table structs an injected modding overlay
// (Utinni) reads via GetProcAddress(hExe, "GetEngineHookPoints") to detour
// / call / read engine functions and globals by NAME rather than by a
// hardcoded SWGEmu RVA. Pure read-only contract: each row is { name, addr },
// every address taken at compile time by &EngineSymbol so it is correct by
// construction and survives every rebuild. The client stays Utinni-agnostic;
// if Utinni is not injected, nothing reads this table and it is inert.
//
// This header is SHARED VERBATIM with D:/Code/Utinni (copied at each catalog
// wave so the two repos cannot drift -- see ENGINE_HOOKPOINTS_VERSION note).
// It therefore carries ONLY the structs + version + the X-macro name list
// (.inc). It MUST NOT carry the provider-side exported GetEngineHookPoints()
// declaration -- a consumer repo must never import a dll-exported symbol. The
// export declaration+definition lives ONLY in the SwgClient-only TU
// engine_advertise.cpp.
//
// Keep this header EXE-LOCAL: it must not be added to any shared header the
// gl0X renderer plugins compile (AGENTS.md shared-header ABI cascade trap).
// It pulls in no engine library header by design.
//
// See .planning/handoff/2026-06-20-utinni-engine-entrypoint-advertisement-spec.md.
// ======================================================================

#ifndef INCLUDED_engine_hookpoints_H
#define INCLUDED_engine_hookpoints_H

// ----------------------------------------------------------------------
// Contract version. POLICY (D-03, Phase 38 -- overrides the 37-era "pinned at
// 1, do NOT bump per wave" note): any NAME ADD/REMOVE to the .inc bumps this
// version. 38-05 EXTENDS the policy: an ADDRESS-CORRECTNESS change behind an
// UNCHANGED name set ALSO bumps the version, because a consumer that has already
// bound an endpoint must know the address behind the name moved (here: 4 detoured
// rows re-pointed from a call-through forwarder to the real engine entry). The
// version is advisory -- the contract is name-keyed, so a consumer resolves
// endpoints by name regardless -- but the bump is the explicit "what's behind a
// name changed" signal. Lockstep is still enforced byte-exactly: the .h + .inc are
// re-copied verbatim into D:/Code/Utinni/UtinniCore/swg/ at each wave so the
// consumer always sees the matching set.
//
// Bumped 1 -> 2 in 38-03: covered ALL the Phase-38 catalog GROWTH (38-01
// groundScene::* x8, 38-02 client/config x4, 38-03 cuiChatWindow::* x4) -- 94 names.
// Bumped 2 -> 3 in 38-05: ADDRESS-CORRECTNESS only -- the SAME 94-name set, but the
// 4 DETOURED rows (groundScene::{update,handleInputMapEvent}, cuiChatWindow::
// {enableTextInput,chatEnterHandler}) now advertise the REAL engine entry instead of
// a call-through forwarder thunk (a detour on a forwarder is silently dead). The
// consumer's required-set is UNAFFECTED -- only the addresses behind four names moved.
// Bumped 3 -> 4 in 24-§4 (MISC/INPUT editor-unlock batch): 3 NAME ADDs --
// game::g_mainLoopCounter (new out-of-line Game::getMainLoopCount accessor for ms_loops),
// treeFile::searchTree (&TreeFile::addSearchTree -- resolves the open/searchTree collision),
// cuiChatWindow::createNewWindow (&SwgCuiChatWindow::createNewWindow -- the sole ctor funnel;
// the requested raw-ctor real-entry is infeasible, you cannot address a ctor in C++). 97 names.
// Also an ADDRESS re-point under an UNCHANGED name: game::mainLoop now points at the per-frame
// Game::runGameLoopOnce (was the once-per-process Game::run).
// Bumped 4 -> 5 in 24-ENUM-A (editor file-pickers): 1 NAME ADD -- treeFile::enumerateFiles
// (&TreeFile::enumerateFiles -- new static that walks ms_searchNodes and yields every
// SearchTree/SearchTOC filename via a callback, so the consumer Repository can populate the
// editor pickers). 98 names. (The companion in-world CUI reflow fix is behavior-only -- no row.)
// Bumped 5 -> 6 in 24-SCENE-LOAD (editor "Load scene"): 1 NAME ADD -- game::loadScene (a
// __cdecl(terrain,player) thunk over Game::setScene(true,terrain,player,nullptr), the full
// SceneCreator lifecycle). game::setupScene -> _setScene(Scene*) only sets ms_scene and leaves
// a pre-built GroundScene half-integrated (next-frame throw); loadScene lets the engine build
// + integrate from filenames. 99 names. (game::setupScene stays mapped as the low-level
// set-pre-built-scene primitive -- unchanged.)
// Bumped 6 -> 7 in 24-§2.B (Bucket B, Effects editor live preview): 5 NAME ADDs --
// skeletalAppearance::getDisplayLodSkeleton (non-virtual LOD read, bit_cast PMF),
// renderWorld::addObjectNotifications (static &fn), bloom::preSceneRender +
// bloom::postSceneRender (static &fn), particlePreview::retrigger (friend free fn
// utinni_retriggerClientEffect over ClientEffectManager::m_particleSystems -- the
// cooperative particle hot-reload entry). 104 names. The requested-but-NOT-advertisable
// rows are OMITTED/SKIPPED (documented in engine_advertise.cpp + the HANDBACK): the
// VIRTUAL render methods particleEffectAppearance::render + skeletalAppearance::render
// (consumer vtable-resolves, handoff §2.C), the un-addressable particleEffectAppearance
// ctor (no &Class::Class, no construction funnel), the NONEXISTENT skeletalAppearance::
// addShaderPrimitives + renderWorld::render, and the wildcard shaderPrimitiveSorter::*
// (no concrete method named). Render globals: consumer drives the draw via the already-
// advertised graphics::* statics (handoff's preferred shape) -- no raw-global rows added.
// Bumped 7 -> 8 in Bucket B-2 (live .cef RE-PLAY): 1 NAME ADD --
// particlePreview::replayClientEffect (free fn utinni_replayClientEffect; re-plays a .cef
// FRESH on Game::getPlayer() via the public ClientEffectManager::playClientEffect, the
// transient muzzle/hit/explosion case the retrigger's restart() cannot cover). Constant
// &fn, public APIs only (not a friend). 105 names.
// ----------------------------------------------------------------------
#define ENGINE_HOOKPOINTS_VERSION 8

// ----------------------------------------------------------------------
// One row per advertised endpoint: a stable contract name + the borrowed
// address of the engine function/global (or a thunk wrapping it).
// ----------------------------------------------------------------------
struct EngineHookPoint
{
	const char * name;   // stable contract identity, e.g. "config::loadOverrideConfig"
	void *       addr;   // &EngineSymbol (or thunk) -- borrowed, process-lifetime
};

// ----------------------------------------------------------------------
// The advertised table. Returned by GetEngineHookPoints() as a pointer to
// a process-lifetime static; Utinni only reads it. No NUL-name sentinel --
// count is sizeof/sizeof of the row array (see engine_advertise.cpp).
// ----------------------------------------------------------------------
struct EngineHookPoints
{
	unsigned int                 version;   // == ENGINE_HOOKPOINTS_VERSION at build time
	unsigned int                 count;     // number of rows in entries[]
	const EngineHookPoint * entries;  // static array of `count` rows
};

#endif // INCLUDED_engine_hookpoints_H
