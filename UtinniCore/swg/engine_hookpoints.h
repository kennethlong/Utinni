// ======================================================================
//
// engine_hookpoints.h -- engine entry-point advertisement
// contract (handoff 2026-06-20). The exe-side game-logic twin of the
// shipped graphics gl11_r.dll!GetHookPoints (Direct3d11.cpp:856-888).
//
// Defines the name->pointer table structs an injected modding overlay
// reads via GetProcAddress(hExe, "GetEngineHookPoints") to detour
// / call / read engine functions and globals by NAME rather than by a
// hardcoded SWGEmu RVA. Pure read-only contract: each row is { name, addr },
// every address taken at compile time by &EngineSymbol so it is correct by
// construction and survives every rebuild. The client stays consumer-agnostic;
// if the consumer is not injected, nothing reads this table and it is inert.
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
// engine_retriggerClientEffect over ClientEffectManager::m_particleSystems -- the
// cooperative particle hot-reload entry). 104 names. The requested-but-NOT-advertisable
// rows are OMITTED/SKIPPED (documented in engine_advertise.cpp + the HANDBACK): the
// VIRTUAL render methods particleEffectAppearance::render + skeletalAppearance::render
// (consumer vtable-resolves, handoff §2.C), the un-addressable particleEffectAppearance
// ctor (no &Class::Class, no construction funnel), the NONEXISTENT skeletalAppearance::
// addShaderPrimitives + renderWorld::render, and the wildcard shaderPrimitiveSorter::*
// (no concrete method named). Render globals: consumer drives the draw via the already-
// advertised graphics::* statics (handoff's preferred shape) -- no raw-global rows added.
// Bumped 7 -> 8 in Bucket B-2 (live .cef RE-PLAY): 1 NAME ADD --
// particlePreview::replayClientEffect (free fn engine_replayClientEffect; re-plays a .cef
// FRESH on Game::getPlayer() via the public ClientEffectManager::playClientEffect, the
// transient muzzle/hit/explosion case the retrigger's restart() cannot cover). Constant
// &fn, public APIs only (not a friend). 105 names.
// Bumped 8 -> 9 in Bucket A (per-editor real-entry detour rows, ledger §2.A): 6 NAME ADDs --
// cuiRadialMenuManager::update + cuiMenu::infoTypesFindDefaultCursor (free fn) +
// systemMessageManager::receiveMessage (static &fn) + creatureObject::setTarget (pmfRealEntry,
// MI) + messageQueue::appendMessage + messageQueue::appendMessageData (pmfToVoid). 111 names.
// The other 8 of the 14 requested §2.A rows are OMITTED (un-addressable ctors cuiChatWindow/
// cuiLoginScreen -- chat's createNewWindow funnel already covers it; nonexistent
// cuiManager::findObjectUnderCursor + cuiHud::getTarget) or SKIPPED (virtual: cuiHud::
// actionPerformAction, cuiHud::update, cuiLoginScreen::activate, debugCamera::alter ->
// consumer vtable-resolves) -- documented in engine_advertise.cpp + the handback, NOT in
// this required-name set.
// Bumped 9 -> 10 in Bucket A-2 (world-pick / HUD-target): 2 NAME ADDs --
// cuiHud::getTarget (was a Bucket A OMIT; now the real entry of SwgCuiHud::
// getLastSelectedObject() const via a __fastcall MI thunk -- the world-picked object) +
// cuiHud::g_instance (static SwgCuiHudFactory::findMediatorForCurrentHud -> the LIVE
// ground/space hud the consumer calls getTarget on, mirroring cuiIo::g_instance). 113 names.
// Bumped 10 -> 11 in Bucket A-2.1 (crash revert): 1 NAME REMOVE --
// systemMessageManager::receiveMessage (Bucket A v9) is REVERTED. It was mapped to the static
// CuiSystemMessageManager::receiveSystemMessage(const ChatSystemMessage&), but the consumer's
// hkReceiveMessage detour targets the MessageDispatch::Receiver::receiveMessage(emitter,message)
// byte-stream receiver -- pointing that 2-arg network hook at the 1-arg static CRASHED world-load
// (c0000005 via sendFakeSystemMessage->receiveSystemMessage->hkReceiveMessage on region-enter). The
// real receiver is the file-local Listener::receiveMessage (no external symbol) -> un-advertisable;
// OMIT (a wrong & is worse than a missing row). 112 names.
// Bumped 11 -> 12 in Bucket A-3 (target-change resolver): 1 NAME ADD --
// network::getObjectById -> static NetworkIdManager::getObjectById(const NetworkId&) [the
// NetworkId->Object* lookup]. Unblocks the creatureObject::setTarget callback: the consumer's
// hkSetTarget resolves the new target id->Object* via Network::getObjectById, which on the
// advertised client hit a stale hardcoded RVA and crashed. constant &fn (true static). 113 names.
// Bumped 12 -> 13 in the FREE-CAM editor-unlock wave (2026-06-29): 6 NAME ADDs, all CALLED
// accessors that encapsulate fragile NGE struct byte-offsets (the particlePreview::retrigger /
// config::setModalChat shim pattern) so the consumer stops hardcoding offsets that drift:
//   groundScene::isFreeCameraActive               -- bool, currentView == CI_debugPortal (==5==consumer cm_Free)
//   groundScene::getDebugPortalCameraMessageQueue -- the MQ at m_debugPortalCameraInputMap (was read at InputMap+0xC)
//   gameCamera::getMessageQueue                   -- the camera's movement MQ via getController()->getMessageQueue() (was GameCamera+0x248)
//   messageQueue::getCount                        -- MISMATCH: real entry MessageQueue::getNumberOfMessages
//   messageQueue::getMessage                      -- the 4-arg (int,int*,float*,uint32*) overload (read side; appendMessage already advertised)
//   object::isActive                              -- bool; NON-virtual but INLINE -> external-linkage shim (no ODR address)
// All constant &thunk / &fn or pmfToVoid rows (no detours). 119 names.
// Bumped 13 -> 14 in the sysmsg-SEND provider request (2026-07-02): 1 NAME ADD --
// systemMessageManager::sendMessage -> &CuiSystemMessageManager::sendFakeSystemMessage (all-static class;
// plain constant &fn, no MI/virtual/thunk). The INJECT half of the sysmsg pair: a CALLED endpoint (the consumer
// injects a system message into the chat/system feed), byte-exact ABI match to the consumer typedef
// void(__cdecl*)(const Unicode::String&, bool). SEND ONLY -- the RECEIVE half stays OMIT (the reverted Bucket
// A-2.1 receiveMessage that AV'd world-load; the real inbound receiver is a file-local anonymous
// MessageDispatch::Receiver Listener, un-advertisable -- receiveSystemMessage next to the static is NOT it). 120 names.
// Bumped 14 -> 15 in sysmsg-SEND rev-2 (2026-07-03): 1 NAME REPLACE -- systemMessageManager::sendMessage
// REMOVED, systemMessageManager::sendMessageUtf8 ADDED -> extern "C" void __cdecl
// utinni_sendFakeSystemMessage(const char* utf8Msg, bool chatBoxOnly), a provider-side utf8 shim that widens
// and calls sendFakeSystemMessage. The v14 row CRASHED live smoke (WRITE-AV): "byte-exact ABI match" was true
// of the signature only -- the const Unicode::String& PARAMETER crossed the boundary as a C++ object, and the
// consumer's swg::WString models the 2002 SWGEmu 3-pointer layout while ours is a v145 SSO basic_string ->
// the engine read _Mysize past the consumer's 12-byte object. ABI RULE adopted by both repos: only primitives
// and pointers cross the advertised boundary on CALLED endpoints; class-type params (strings above all) need
// an extern "C" shim (the utinni_replayClientEffect precedent). Name-REPLACE, not re-point, so version-skewed
// pairings miss by name and degrade instead of mis-calling. Companion rev-2 AUDIT flagged the other CALLED
// rows with class-type params/returns as ABI-UNSAFE TO CALL until shimmed (row comments in
// engine_advertise.cpp): cuiChatWindow::writeToAllTabs + writeToCurrentTab and consoleHelper::sendInput
// (const Unicode::String& params), worldSnapshot::addObject (CrcString const& + std::string const&),
// objectTemplate::get{AppearanceFilename,PortalLayoutFilename,ClientDataFile} (return our std::string by
// const&). DETOURED rows are unaffected (they read args our own code constructed). Still 120 names.
// Bumped 15 -> 16 in the lookAtTarget-accessor request (2026-07-09): 1 NAME ADD --
// game::getPlayerLookAtTargetId -> extern "C" __int64 __cdecl utinni_getPlayerLookAtTargetId(void)
// (defined in CreatureObject.cpp), the READ twin of creatureObject::setTarget: returns the PLAYER's
// lookAt/selection-target NetworkId VALUE (full 64 bits, cluster-id bits included; 0 = no player /
// no target -- NetworkId::cms_invalid is NetworkId(0)). Strictly the lookAt target (m_lookAtTarget),
// NOT the NGE intended/combat target (getIntendedTarget -- a future separate request, never a
// re-point). Shim per the rev-2 ABI RULE: CreatureObject::getLookAtTarget() is INLINE (no ODR
// address) and returns const CachedNetworkId& (embeds a mutable Watcher<Object> the consumer does
// not model) -- the shim collapses it to a primitive in EDX:EAX. Unblocks the consumer's
// Game::getPlayerLookAtTargetObject() reroute through the v12 network::getObjectById row ->
// target-aware editor affordances on the advertised client. 121 names.
// ----------------------------------------------------------------------
#define ENGINE_HOOKPOINTS_VERSION 16

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
// a process-lifetime static; the consumer only reads it. No NUL-name sentinel --
// count is sizeof/sizeof of the row array (see engine_advertise.cpp).
// ----------------------------------------------------------------------
struct EngineHookPoints
{
	unsigned int                 version;   // == ENGINE_HOOKPOINTS_VERSION at build time
	unsigned int                 count;     // number of rows in entries[]
	const EngineHookPoint * entries;  // static array of `count` rows
};

#endif // INCLUDED_engine_hookpoints_H
