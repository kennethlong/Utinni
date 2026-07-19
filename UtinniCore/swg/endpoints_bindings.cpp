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

// Phase 24 / EPA-02 -- the SYMBOL-BEARING half of the swg::endpoints resolver
// (Option-A split, mirroring render_backend_dx9.cpp vs render_backend.cpp).
//
// This TU is compiled ONLY into UtinniCore.dll -- NEVER into UtinniCore.Tests --
// because it references the per-subsystem swg::* pFn literals, which are
// namespace-scope definitions with external linkage in the subsystem TUs but are
// NOT exported (no UTINNI_API) and so are absent from UtinniCore.lib's import
// surface. Dragging them into the test link is the LNK2001 the split avoids. The
// injection-free pure resolve()/lookupByName() + the subset static_assert live in
// endpoints.cpp and ARE compiled into the test.
//
// Plan 02 / D-01: the FULL catalog -- every engine_hookpoints.inc name
// (78 total) MINUS the single D-02 carve-out (consoleHelper::sendInput) -> 77
// bound rows. Each row's slot is the storage cell of the consumer's existing
// per-subsystem pFn literal; the contract NAME is the resolution key (so the
// name-mismatch rows -- where the contract name differs from the consumer literal
// name -- still resolve). The slot's typedef must be CALL-COMPATIBLE with the
// provider's real symbol (utinni_advertise.cpp); name mismatches are fine but a
// signature mismatch would corrupt the call (see VERIFY comments per group).
//
// Accessor-style globals (D-04) bind to dedicated pFn ACCESSOR slots (null on the
// SWGEmu path -- there is no RVA literal; the read-site memory::read's the global).
// Task 2 adapts the read-sites read->call. Here we only add the binding entries.

#include "endpoints.h"

#include "utinni.h"                // byte/swgptr + the build's common prelude
#include "swg/misc/swg_math.h"     // swg::math::{Transform, Vector}
#include "swg/ui/command_parser.h" // utinni::CommandParser (+ nested CommandData)
#include "swg/camera/camera.h"     // v3 (38-01): utinni::Camera::Modes (nested enum, not fwd-declarable) for groundScene::changeCamera
#include "swg/misc/swg_string.h"   // v3 (38-03): swg::WString for cuiChatWindow::writeTo{All,Current}Tab

// resolveFromExe() uses GetModuleHandleA/GetProcAddress -- the only Windows reach.
#include <Windows.h>

#include <cstdio> // WS-3 drift-log formatting (std::snprintf)

// ----------------------------------------------------------------------
// extern re-declarations of every consumer pFn literal bound this plan.
//
// CRITICAL ABI NOTE: MSVC encodes a namespace-scope variable's full POINTEE type in
// its mangled symbol (?name@ns@@3P6A...@ZA), so these externs MUST use the EXACT same
// function-pointer typedef as the originating TU or the linker reports LNK2001. The
// engine class types appear ONLY behind pointers/refs in those typedefs, so a forward
// declaration is sufficient -- we do NOT need the full engine type graph (only
// CommandParser::CommandData, a nested type, needs the real header). Each typedef is
// copied verbatim from its cited originating TU; a drift would fail to link (a useful
// guard in itself). The resolver only stores the table's void* into the cell; the
// call-site ABI lives in the originating TU's literal + the Task 2 read->call sites.
// ----------------------------------------------------------------------

// Forward declarations of the engine types referenced (by pointer/ref) below.
namespace utinni
{
class Object;
class Camera;
class Appearance;
class CellProperty;
class ExtentBase;
class GroundScene;
class SharedObjectTemplate;
struct IoEvent;     // v3 (38-01): groundScene::handleInputMapEvent param (by pointer; struct, not class -- MSVC mangles the tag)
class ClientObject; // v9 (Bucket A): cuiMenu::infoTypesFindDefaultCursor param (by pointer)
class MessageQueue; // v9 (Bucket A): messageQueue::append* this-ptr (by pointer)
} // namespace utinni

// -- config (misc/config.cpp:30-39) -----------------------------------------
namespace swg::config
{
using pLoadConfigFileBuffer = bool(__cdecl*)(byte* buffer, int bufferLength);
using pLoadConfigFileString = bool(__cdecl*)(const char* filename);
using pLoadOverrideConfig = int(__cdecl*)();

extern pLoadConfigFileBuffer loadConfigFileBuffer;
extern pLoadConfigFileString loadConfigFileString;
extern pLoadOverrideConfig loadOverrideConfig;
} // namespace swg::config

// -- client (client/client.cpp:33,41) --------------------------------------
namespace swg::client
{
using pMainLoop = int(__cdecl*)(HINSTANCE hInstance, int a2, int a3);
extern pMainLoop clientMain;
} // namespace swg::client

// -- game (game/game.cpp:40-70 + D-04 g_runningFlags) -----------------------
namespace swg::game
{
using pInstall = void(__cdecl*)(int applicationType);
using pQuit = void(__cdecl*)();
using pMainLoop = void(__cdecl*)(bool presentToWindow, HWND hwnd, int width, int height);
using pSetupScene = void(__cdecl*)(utinni::GroundScene* newScene);
using pCleanupScene = void(__cdecl*)();
using pGetPlayer = utinni::Object*(__cdecl*)();
using pGetPlayerCreatureObject = utinni::Object*(__cdecl*)();
using pGetCamera = utinni::Camera*(__cdecl*)();
using pGetConstCamera = const utinni::Camera*(__cdecl*)();
using pIsViewFirstPerson = bool(__cdecl*)();
using pIsHudSceneTypeSpace = bool(__cdecl*)();
using pIsOver = bool(__cdecl*)();
using pMainLoopCount = int(__cdecl*)(); // v4 (24): accessor for the private ms_loops counter

extern pInstall install;
extern pQuit quit;
extern pMainLoop mainLoop;
extern pSetupScene setupScene;
extern pCleanupScene cleanupScene;
extern pGetPlayer getPlayer;
extern pGetPlayerCreatureObject getPlayerCreatureObject;
extern pGetCamera getCamera;
extern pGetConstCamera getConstCamera;
extern pIsViewFirstPerson isViewFirstPerson;
extern pIsHudSceneTypeSpace isHudSceneTypeSpace;
extern pIsOver g_runningFlags;
extern pMainLoopCount g_mainLoopCounter; // v4 (24/4b): &Game::getMainLoopCount accessor
// v6 (24): full SceneCreator string-based scene load (advertised-only; null on SWGEmu).
using pLoadScene = void(__cdecl*)(const char* terrain, const char* player);
extern pLoadScene loadScene;
// v16 (24 / Goal A+): player lookAt-target id READ shim (advertised-only; null on SWGEmu).
using pGetPlayerLookAtTargetId = int64_t(__cdecl*)();
extern pGetPlayerLookAtTargetId getPlayerLookAtTargetId;
} // namespace swg::game

// -- graphics (graphics/graphics.cpp:37-83 + D-04 accessors) ----------------
namespace swg::graphics
{
using pInstall = bool(__cdecl*)();
using pUpdate = void(__cdecl*)(float elapsedTime);
using pBeginScene = void(__cdecl*)();
using pEndScene = void(__cdecl*)();
using pPresentWindow = int(__cdecl*)(HWND hwnd, int width, int height);
using pPresent = void(__cdecl*)();
using pUseHardwareCursor = bool(__cdecl*)(bool value);
using pShowMouseCursor = bool(__cdecl*)(bool isShown);
using pSetSystemMouseCursorPosition = void(__cdecl*)(int X, int Y);
using pResize = void(__cdecl*)(int width, int height);
using pFlushResources = void(__cdecl*)(bool reset);
using pSetStaticShader = void(__cdecl*)(swgptr staticShader, int pass);
using pScreenshot = bool(__cdecl*)(const char* filename);
using pRenderTargetAccessor = int(__cdecl*)();
using pFrameNumberAccessor = int(__cdecl*)();

extern pInstall install;
extern pUpdate update;
extern pBeginScene beginScene;
extern pEndScene endScene;
extern pPresentWindow presentWindow;
extern pPresent present;
extern pUseHardwareCursor useHardwareCursor;
extern pShowMouseCursor showMouseCursor;
extern pSetSystemMouseCursorPosition setSystemMouseCursorPosition;
extern pResize resize;
extern pFlushResources flushResources;
extern pSetStaticShader setStaticShader;
extern pScreenshot screenshot;
extern pRenderTargetAccessor g_renderTargetWidth;
extern pRenderTargetAccessor g_renderTargetHeight;
extern pFrameNumberAccessor g_frameNumber;
} // namespace swg::graphics

// -- cuiManager (ui/cui_manager.cpp:34-48 + D-04 g_instance) ----------------
namespace swg::cuiManager
{
using pRender = void(__thiscall*)(swgptr pThis);
using pSetSize = void(__cdecl*)(int width, int height);
using pTogglePointer = void(__cdecl*)(bool isOn);
using pRestartMusic = void(__cdecl*)(bool notPlaying);
using pGetIoWin = swgptr(__cdecl*)();

extern pRender render;
extern pSetSize setSize;
extern pTogglePointer togglePointer;
extern pRestartMusic restartMusic;
extern pGetIoWin g_instance;
} // namespace swg::cuiManager

// -- cuiIo (ui/cui_io.cpp:33-41 + D-04 g_instance) --------------------------
namespace swg::cuiIo
{
using pSetKeyboardInputActive = swgptr(__thiscall*)(swgptr pThis, bool value);
using pRequestKeyboard = swgptr(__thiscall*)(swgptr pThis, bool value);
using pGetIoWin = swgptr(__cdecl*)();

extern pSetKeyboardInputActive setKeyboardInputActive;
extern pRequestKeyboard requestKeyboard;
extern pGetIoWin g_instance;
} // namespace swg::cuiIo

// -- commandParser (ui/command_parser.cpp:29-39) ----------------------------
namespace swg::commandParser
{
using pCtor1 = utinni::CommandParser*(__thiscall*)(utinni::CommandParser * pThis, const char* command, size_t argCount, const char* args, const char* helpInfo, utinni::CommandParser* delegate);
using pCtor2 = utinni::CommandParser*(__thiscall*)(utinni::CommandParser * pThis, const utinni::CommandParser::CommandData& commandData, utinni::CommandParser* delegate);
using pAddSubCommand = utinni::CommandParser*(__thiscall*)(utinni::CommandParser * pThis, utinni::CommandParser* subCommand);

extern pCtor1 ctor1;
extern pCtor2 ctor2;
extern pAddSubCommand addSubCommand;
} // namespace swg::commandParser

// -- extent (appearance/extent.cpp:29-32; contract extent::intersect ->
//    consumer swg::baseExtent::intersect, name mismatch) ---------------------
namespace swg::baseExtent
{
using pIntersect = bool(__thiscall*)(utinni::ExtentBase* pThis, swg::math::Vector* worldStart, swg::math::Vector* worldEnd, swg::math::Vector* normal, float* time);
extern pIntersect intersect;
} // namespace swg::baseExtent

// -- object (object/object.cpp:84-188; name mismatches + 3 new D-01 slots) --
namespace swg::object
{
using pGetType = unsigned int(__thiscall*)(utinni::Object* pThis);
using pGetParentCell = utinni::CellProperty*(__thiscall*)(utinni::Object * pThis);
using pGetTransform_o2w = swg::math::Transform*(__thiscall*)(utinni::Object * pThis);
using pSetTransform_o2w = void(__thiscall*)(utinni::Object* pThis, swg::math::Transform& objectToWorld);
using pGetPosition = swg::math::Vector*(__thiscall*)(utinni::Object * pThis);
using pSetPosition = void(__thiscall*)(utinni::Object* pThis, swg::math::Vector& position);
using pGetAppearance = utinni::Appearance*(__thiscall*)(utinni::Object * pThis);
using pSetAppearance = void(__thiscall*)(utinni::Object* pThis, utinni::Appearance* appearance);
using pMove = void(__thiscall*)(utinni::Object* pThis, const swg::math::Vector& vector);
using pGetObjectTemplate = swgptr(__thiscall*)(utinni::Object* pThis);
using pGetObjectTemplateName = const char*(__thiscall*)(utinni::Object * pThis);
using pGetNetworkId = swgptr(__thiscall*)(utinni::Object* pThis);
using pIsActive = bool(__thiscall*)(utinni::Object* pThis); // v13 (free-cam): hkAlter guard

extern pGetType getType;
extern pGetParentCell getParentCell;
extern pGetTransform_o2w getTransform_o2w;
extern pSetTransform_o2w setTransform_o2w;
extern pGetPosition getPosition;
extern pSetPosition setPosition;
extern pGetAppearance getAppearance;
extern pSetAppearance setAppearance;
extern pMove move;
extern pGetObjectTemplate getObjectTemplate;
extern pGetObjectTemplateName getObjectTemplateName;
extern pGetNetworkId getNetworkId;
extern pIsActive isActive;
} // namespace swg::object

// -- objectTemplate (object/object.cpp:55-70) -------------------------------
namespace swg::objectTemplate
{
using pCreateObject = utinni::Object*(__cdecl*)(const char* filename);
extern pCreateObject createObject;
} // namespace swg::objectTemplate

namespace swg::sharedObjectTemplate
{
using pGetAppearancetFilename = const char**(__thiscall*)(utinni::SharedObjectTemplate * pThis, bool unk);
using pGetPortalLayoutFilename = const char**(__thiscall*)(utinni::SharedObjectTemplate * pThis, bool unk);
using pGetClientDataFilename = const char**(__thiscall*)(utinni::SharedObjectTemplate * pThis, bool unk);

extern pGetAppearancetFilename getAppearancetFilename;
extern pGetPortalLayoutFilename getPortalLayoutFilename;
extern pGetClientDataFilename getClientDataFilename;
} // namespace swg::sharedObjectTemplate

// -- worldSnapshot (scene/world_snapshot.cpp:83-103 + 3 new D-01 static slots;
//    contract worldSnapshot::* -> consumer swg::worldsnapshot::*) ------------
namespace swg::worldsnapshot
{
using pLoad = void(__cdecl*)(const char* name);
using pAddObject = void(__cdecl*)(swgptr object, swgptr node);
using pDetailLevelChanged = void(__cdecl*)();
using pRemoveObject = void(__cdecl*)(swgptr networkId);
using pMoveObject = void(__cdecl*)(swgptr networkId, const swg::math::Transform& transform);
using pGetLoadingPercent = int(__cdecl*)();

extern pLoad load;
extern pAddObject addObject;
extern pDetailLevelChanged detailLevelChanged;
extern pRemoveObject removeObject;
extern pMoveObject moveObject;
extern pGetLoadingPercent getLoadingPercent;

// v17 (Goal B Wave 1): the 7 id-keyed READ shims (rev-3 frozen row table). extern "C"
// __cdecl provider symbols (utinni_ws*); primitives + the size-first UtinniWsNodeInfo
// POD only cross the boundary (the sysmsg rev-2 ABI rule). Slots start null -- no
// SWGEmu RVA exists; the consumer's WorldSnapshotLive facade null-checks every call.
using pWsGetNodeCount = int(__cdecl*)();
using pWsGetTopNodeIdAt = int64_t(__cdecl*)(int index);
using pWsGetChildCount = int(__cdecl*)(int64_t id);
using pWsGetChildIdAt = int64_t(__cdecl*)(int64_t id, int index);
using pWsGetNodeInfo = int(__cdecl*)(int64_t id, UtinniWsNodeInfo* out);
using pWsGetNodeTemplateName = int(__cdecl*)(int64_t id, char* buf, int cap);
using pWsGetGeneration = int(__cdecl*)();

extern pWsGetNodeCount wsGetNodeCount;
extern pWsGetTopNodeIdAt wsGetTopNodeIdAt;
extern pWsGetChildCount wsGetChildCount;
extern pWsGetChildIdAt wsGetChildIdAt;
extern pWsGetNodeInfo wsGetNodeInfo;
extern pWsGetNodeTemplateName wsGetNodeTemplateName;
extern pWsGetGeneration wsGetGeneration;

// v18 (Goal B Wave 2): the 5 LIVE-ONLY mutation shims (frozen 2026-07-18 row table).
// wsRemoveNode is tri-state (1 removed / 0 miss / -1 occupied). Typedefs copied
// verbatim from world_snapshot.cpp (LNK2001 guard).
using pWsAddObject = int64_t(__cdecl*)(const char* sharedTemplateFilename, const float* transform12, int64_t containedById);
using pWsAddNodeAt = int(__cdecl*)(int64_t explicitId, int64_t containedById, const char* templateFilename, int cellIndex, const float* transform12, float radius, unsigned int portalLayoutCrc);
using pWsRemoveNode = int(__cdecl*)(int64_t id);
using pWsSetNodeRadius = int(__cdecl*)(int64_t id, float radius);
using pWsConfigureIdAllocator = int(__cdecl*)(int64_t floor, int64_t ceiling);

extern pWsAddObject wsAddObject;
extern pWsAddNodeAt wsAddNodeAt;
extern pWsRemoveNode wsRemoveNode;
extern pWsSetNodeRadius wsSetNodeRadius;
extern pWsConfigureIdAllocator wsConfigureIdAllocator;

// v19 (Goal B Wave 3): the 3 PERSISTENCE shims (save typed-result / save-root copy-out / unload).
using pWsSaveSnapshot = int(__cdecl*)();
using pWsGetSavePath = int(__cdecl*)(char* buf, int cap);
using pWsUnloadSnapshot = void(__cdecl*)();
extern pWsSaveSnapshot wsSaveSnapshot;
extern pWsGetSavePath wsGetSavePath;
extern pWsUnloadSnapshot wsUnloadSnapshot;
} // namespace swg::worldsnapshot

// v19 (Goal B Wave 3 / rider 4B): NGE targeting filter (cui_hud.cpp). Advertised-only.
namespace swg::cuiPreferences
{
using pSetAllowTargetAnything = void(__cdecl*)(bool value);
using pGetAllowTargetAnything = bool(__cdecl*)();
extern pSetAllowTargetAnything setAllowTargetAnything;
extern pGetAllowTargetAnything getAllowTargetAnything;
} // namespace swg::cuiPreferences

// -- camera (camera/camera.cpp:33-54; reverseProjectInViewportSpace ->
//    consumer reverseProjectInViewportSpaceInt, name mismatch) ---------------
namespace swg::camera
{
using pSetViewport = void(__thiscall*)(utinni::Camera* pThis, int x0, int y0, int width, int height);
using pSetNearPlane = void(__thiscall*)(utinni::Camera* pThis, float nearPlane);
using pSetFarPlane = void(__thiscall*)(utinni::Camera* pThis, float farPlane);
using pSetHorizontalFieldOfView = void(__thiscall*)(utinni::Camera* pThis, float fieldOfView);
using pReverseProjectInViewportSpaceInt = swg::math::Vector*(__thiscall*)(utinni::Camera * pThis, swg::math::Vector& result, int x, int y);

extern pSetViewport setViewport;
extern pSetNearPlane setNearPlane;
extern pSetFarPlane setFarPlane;
extern pSetHorizontalFieldOfView setHorizontalFieldOfView;
extern pReverseProjectInViewportSpaceInt reverseProjectInViewportSpaceInt;
// v19 (Goal B Wave 3 / rider 4C): live-camera matrix accessors for the gizmo. Advertised-only.
using pGetProjectionMatrix = int(__cdecl*)(float* out16);
using pGetTransformO2W = int(__cdecl*)(float* out12);
extern pGetProjectionMatrix getProjectionMatrix;
extern pGetTransformO2W getTransformO2W;
} // namespace swg::camera

// -- gameCamera (camera/camera.cpp:66-; v13 free-cam: movement MQ accessor) ----
namespace swg::gameCamera
{
using pGetMessageQueue = utinni::MessageQueue*(__thiscall*)(utinni::GameCamera * pThis);
extern pGetMessageQueue getMessageQueue;
} // namespace swg::gameCamera

// -- memory (misc/swg_memory.cpp:29-39; memory::free -> swg::memory::deallocate) --
namespace swg::memory
{
using pAllocate = void*(__cdecl*)(size_t size);
using pDeallocate = void(__cdecl*)(void* address, size_t size);

extern pAllocate allocate;
extern pDeallocate deallocate;
} // namespace swg::memory

// -- audio (misc/audio.cpp:29-33) -------------------------------------------
namespace swg::audio
{
using pSetMasterVolume = void(__cdecl*)(float volume);
using pGetMasterVolume = float(__cdecl*)();

extern pSetMasterVolume setMasterVolume;
extern pGetMasterVolume getMasterVolume;
} // namespace swg::audio

// -- treeFile (misc/tree_file.cpp:30-32; treeFile::open -> swg::treefile::searchTree) --
namespace swg::treefile
{
using pSearchTree = swgptr(__thiscall*)(swgptr pThis, int priority, const char* treeFilename);
extern pSearchTree searchTree;
// v4 (24/4c): collision fix -- the real search-path registration is a STATIC __cdecl(fileName,
// priority) (reversed args, no pThis). Advertised-only slot; the SWGEmu path stays on searchTree.
using pAddSearchTree = void(__cdecl*)(const char* fileName, int priority);
extern pAddSearchTree addSearchTree;
// v5 (24): the full TRE/TOC file enumeration the Repository needs (callback per filename).
using pEnumerateFiles = void(__cdecl*)(void(__cdecl* cb)(const char* fileName, void* ctx), void* ctx);
extern pEnumerateFiles enumerateFiles;
} // namespace swg::treefile

// -- report (misc/swg_misc.cpp:29-31) ---------------------------------------
namespace swg::report
{
using pPrint = void(__cdecl*)(const char* msg);
extern pPrint print;
} // namespace swg::report

// ===== v3 (Phase 38) additions: 16 new advertised endpoints. GLOBAL scope (NOT inside
// swg::endpoints) so the names mangle as ::swg::<sub>::* and match the originating TUs.
// Typedefs copied verbatim from those TUs (LNK2001 guard, per the ABI note above). =====

// -- config setModalChat/getModalChat (misc/config.cpp:34-42) --
namespace swg::config
{
using pSetModalChat = void(__cdecl*)(bool value);
using pGetModalChat = bool(__cdecl*)();
extern pSetModalChat setModalChat;
extern pGetModalChat getModalChat;
} // namespace swg::config

// -- client wndProc/writeMiniDump (client/client.cpp:36-47) --
namespace swg::client
{
using pWndProc = int(__stdcall*)(HWND Hwnd, UINT Message, WPARAM wParam, LPARAM lParam);
using pWriteMiniDump = bool(__cdecl*)(const char* filename, swgptr unk);
extern pWndProc wndProc;
extern pWriteMiniDump writeMiniDump;
} // namespace swg::client

// -- groundScene (scene/ground_scene.cpp:45-69) -- 38-01 MI thunks/forwarders --
namespace swg::groundScene
{
using pCtor = utinni::GroundScene*(__thiscall*)(void* pThis, const char* terrainFilename, const char* avatarObjectFilename, swgptr customPlayer);
using pReloadTerrain = void(__thiscall*)(utinni::GroundScene* pThis);
using pChangeCamera = int(__thiscall*)(utinni::GroundScene* pThis, utinni::Camera::Modes cameraMode, float);
using pGetCurrentCamera = utinni::Camera*(__thiscall*)(utinni::GroundScene * pThis);
using pUpdate = void(__thiscall*)(utinni::GroundScene* pThis, float time);
using pHandleInputMapUpdate = void(__thiscall*)(utinni::GroundScene* pThis);
using pHandleInputMapEvent = void(__thiscall*)(utinni::GroundScene* pThis, utinni::IoEvent* ioEvent);
using pInit = void(__thiscall*)(utinni::GroundScene* pThis, const char* terrain, utinni::Object* playerObj, float time);
using pIsFreeCameraActive = bool(__thiscall*)(utinni::GroundScene* pThis);                                 // v13 (free-cam)
using pGetDebugPortalCameraMessageQueue = utinni::MessageQueue*(__thiscall*)(utinni::GroundScene * pThis); // v13 (free-cam)
extern pCtor ctor;
extern pReloadTerrain reloadTerrain;
extern pChangeCamera changeCamera;
extern pGetCurrentCamera getCurrentCamera;
extern pUpdate update;
extern pHandleInputMapUpdate handleInputMapUpdate;
extern pHandleInputMapEvent handleInputMapEvent;
extern pInit init;
extern pIsFreeCameraActive isFreeCameraActive;                             // v13 (free-cam)
extern pGetDebugPortalCameraMessageQueue getDebugPortalCameraMessageQueue; // v13 (free-cam)
} // namespace swg::groundScene

// -- cuiChatWindow (ui/cui_chat_window.cpp:40-55) -- 38-03 MI thunks/real-entry --
namespace swg::cuiChatWindow
{
using pEnableTextInput = void(__thiscall*)(swgptr pThis, bool value, bool setKeyboardInput, bool unfocus);
using pWriteToTab = swgptr(__thiscall*)(swgptr pThis, const WString& str);
using pChatEnterHandler = void(__thiscall*)(swgptr pThis);
extern pEnableTextInput enableTextInput;
extern pWriteToTab writeToAllTabs;
extern pWriteToTab writeToCurrentTab;
extern pChatEnterHandler chatEnterHandler;
// v4 (24/4d): ctor is unaddressable -> provider advertises the sole construction funnel
// (static factory __cdecl(UIPage&, Game::SceneType, std::string const&)). Advertised-only.
using pCreateNewWindow = swgptr(__cdecl*)(swgptr uiPage, int sceneType, swgptr stdString);
extern pCreateNewWindow createNewWindow;
} // namespace swg::cuiChatWindow

// ===== v7 (Phase 24 / 24-§2.B Bucket B -- Effects editor live preview): 5 new endpoints.
// Typedefs copied VERBATIM from the originating consumer TUs (LNK2001 guard, per the ABI
// note above). The 4 render rows bind to EXISTING per-subsystem literals (render_world.cpp /
// post_processing.cpp / skeleton.cpp) and are behavior-neutral this wave -- their consuming
// detours stay gated (skeletal/shaderSorter target unadvertised RVAs) or are no-ops (renderWorld);
// only particlePreview::retrigger is actively consumed (utinni::ParticlePreview seam). =====

// -- renderWorld::addObjectNotifications (scene/render_world.cpp:31) --
namespace swg::renderWorld
{
using pAddObjectNotifications = void(__cdecl*)(utinni::Object* obj);
extern pAddObjectNotifications addObjectNotifications;
} // namespace swg::renderWorld

// -- bloom::preSceneRender/postSceneRender (graphics/post_processing.cpp:33-34) --
namespace swg::bloom
{
using pPreSceneRender = void(__cdecl*)();
using pPostSceneRender = void(__cdecl*)();
extern pPreSceneRender preSceneRender;
extern pPostSceneRender postSceneRender;
} // namespace swg::bloom

// -- skeletalAppearance::getDisplayLodSkeleton (appearance/skeleton.cpp:38) --
// Provider row is a bit_cast PMF of the non-virtual const overload; the consumer literal's
// __thiscall(swgptr)->swgptr typedef is call-compatible (opaque this + opaque return).
namespace swg::skeletalAppearance
{
using pGetDisplayLodSkeleton = swgptr(__thiscall*)(swgptr pThis);
extern pGetDisplayLodSkeleton getDisplayLodSkeleton;
} // namespace swg::skeletalAppearance

// -- particlePreview::retrigger (scene/particle_preview.cpp) -- NEW advertised-only slot.
// Provider symbol: void utinni_retriggerClientEffect(char const*) (friend free fn over
// ClientEffectManager::m_particleSystems). Null on SWGEmu (no RVA literal -- accessor-style,
// D-04 class); the consumer null-checks before calling (the degraded/NotReachable path).
namespace swg::particlePreview
{
using pRetrigger = void(__cdecl*)(const char* logicalName);
extern pRetrigger retrigger;
// v8 (Bucket B-2): live .cef RE-PLAY -> provider bool utinni_replayClientEffect(char const*)
// over the public ClientEffectManager::playClientEffect. Null on SWGEmu (advertised-only).
using pReplay = bool(__cdecl*)(const char* clientEffectName);
extern pReplay replay;
} // namespace swg::particlePreview

// ===== v9 (Phase 24 / Bucket A -- per-editor real-entry detour rows, ledger §2.A): 6 new
// endpoints. Typedefs copied VERBATIM from the originating consumer TUs (LNK2001 guard). All
// bind to EXISTING per-subsystem literals whose detours are still wholesale-gated `!advertised`
// in utinni.cpp -- so binding is behavior-neutral this wave (the resolver fills the slot; the
// per-editor un-gate lands per-subsystem behind individual maintainer smokes, the v4/v5 idiom). =====

// -- cuiRadialMenuManager::update (ui/cui_radial_menu.cpp:31) --
namespace swg::cuiRadialMenuManager
{
using pUpdate = void(__cdecl*)();
using pClear = void(__cdecl*)();
extern pUpdate update;
extern pClear clear;
} // namespace swg::cuiRadialMenuManager

// -- clientWorld (ui/cui_hud.cpp; v20 Live World Editor ray-pick) --
namespace swg::clientWorld
{
using pCollideScreenRay = int(__cdecl*)(int screenX, int screenY, int objectsOnly, __int64* outHitObjectId, float* outPoint3);
extern pCollideScreenRay collideScreenRay;
} // namespace swg::clientWorld

// -- cuiMenu::infoTypesFindDefaultCursor (ui/cui_menu.cpp:32) --
namespace swg::cuiMenu
{
using pInfoTypesFindDefaultCursor = swgptr(__cdecl*)(utinni::ClientObject* obj);
extern pInfoTypesFindDefaultCursor infoTypesFindDefaultCursor;
} // namespace swg::cuiMenu

// -- cuiHud::g_instance + getTarget (ui/cui_manager.cpp; Bucket A-2 v10 world-pick) --
// (systemMessageManager::receiveMessage was bound in v9 but REMOVED v10->v11/A-2.1: a wrong-& --
//  the consumer hkReceiveMessage is a 2-arg MessageDispatch::Receiver byte-stream receiver, the
//  provider advertised a 1-arg static -> world-load crash. OMIT'd provider-side; binding dropped here.)
namespace swg::cuiHud
{
using pGetInstance = swgptr(__cdecl*)();
using pGetTarget = swgptr(__thiscall*)(swgptr pHud);
extern pGetInstance g_instance;
extern pGetTarget getTarget;
} // namespace swg::cuiHud

// -- systemMessageManager::sendMessageUtf8 (ui/cui_manager.cpp; v15 rev-2 -- the SEND/inject half;
//    the RECEIVE half stays OMIT per the A-2.1 note above. Provider extern "C"
//    utinni_sendFakeSystemMessage(const char* utf8Msg, bool chatBoxOnly) shim: the v14 direct
//    const Unicode::String& row CRASHED (WString models the 2002 layout, not v145 basic_string) --
//    primitives/pointers only across the boundary; the widen happens provider-side. Slot starts
//    null (advertised-only); the wrapper null-checks) --
namespace swg::systemMessageManager
{
using pSendMessageUtf8 = void(__cdecl*)(const char* utf8Msg, bool chatBoxOnly);
extern pSendMessageUtf8 sendMessageUtf8;
} // namespace swg::systemMessageManager

// -- network::getObjectById (misc/network.cpp:31; Bucket A-3 v12 -- unblocks the target-change
//    callback's Object* resolve. Contract network::getObjectById -> consumer idManagerGetObjectById,
//    name mismatch; provider NetworkIdManager::getObjectById(const NetworkId&) static, exact ABI match) --
namespace swg::network
{
using pIdManagerGetObjectById = utinni::Object*(__cdecl*)(const int64_t& id);
extern pIdManagerGetObjectById idManagerGetObjectById;
} // namespace swg::network

// -- creatureObject::setTarget (object/creature_object.cpp:35; provider MISMATCH setLookAtTarget(NetworkId&),
//    real entry via utinni_creatureSetTargetRealEntry() -- semantic verified at the un-gate smoke) --
namespace swg::creatureObject
{
using pSetTarget = void(__thiscall*)(swgptr pThis, const int64_t& id);
extern pSetTarget setTarget;
} // namespace swg::creatureObject

// -- messageQueue::appendMessage / appendMessageData (misc/io_win.cpp:43-44) --
namespace swg::messageQueue
{
using pAppendMessage = void(__thiscall*)(utinni::MessageQueue* pThis, int msg, float value, uint32_t* flags);
using pAppendMessageData = void(__thiscall*)(utinni::MessageQueue* pThis, int msg, float value, swgptr data, uint32_t* flags);
using pGetCount = int(__thiscall*)(utinni::MessageQueue* pThis);                                                        // v13 (free-cam): MISMATCH -> getNumberOfMessages
using pGetMessage = void(__thiscall*)(utinni::MessageQueue* pThis, int index, int* msg, float* value, uint32_t* flags); // v13 (free-cam): 4-arg overload
extern pAppendMessage appendMessage;
extern pAppendMessageData appendMessageData;
extern pGetCount getCount;
extern pGetMessage getMessage;
} // namespace swg::messageQueue

namespace swg::endpoints
{
// ----------------------------------------------------------------------
// FULL CATALOG (Plan 02 / D-01): every engine_hookpoints.inc name MINUS the
// single D-02 carve-out (consoleHelper::sendInput) -> 77 rows. The contract NAME is
// the resolution key; the slot is the storage cell of the consumer literal that
// serves that engine function (so the name-mismatch rows -- listed inline -- resolve
// by name). consoleHelper::sendInput is deliberately ABSENT (WR-05 / D-02): it stays
// on its RVA literal and is allow-listed out of the coverage gate (see endpoints.cpp).
//
// VERIFIED against D:/Code/swg-client-v2/.../win32/utinni_advertise.cpp:154-288 (the
// provider's real &symbol per contract name). Signature concerns recorded in the
// 24-02-SUMMARY. The order below matches the .inc declaration order.
// ----------------------------------------------------------------------

static const Binding s_bindings[] = {
    // -- config --
    {"config::loadOverrideConfig", (void**)&swg::config::loadOverrideConfig},
    {"config::loadConfigFileBuffer", (void**)&swg::config::loadConfigFileBuffer},
    {"config::loadConfigFileString", (void**)&swg::config::loadConfigFileString}, // MISMATCH: provider ConfigFile::loadFile

    // -- client --
    {"client::clientMain", (void**)&swg::client::clientMain},

    // -- game --
    {"game::install", (void**)&swg::game::install},
    {"game::quit", (void**)&swg::game::quit},
    {"game::mainLoop", (void**)&swg::game::mainLoop}, // MISMATCH: provider Game::run
    {"game::setupScene", (void**)&swg::game::setupScene},
    {"game::cleanupScene", (void**)&swg::game::cleanupScene},
    {"game::getPlayer", (void**)&swg::game::getPlayer},
    {"game::getPlayerCreatureObject", (void**)&swg::game::getPlayerCreatureObject}, // MISMATCH: provider Game::getPlayerCreature
    {"game::getCamera", (void**)&swg::game::getCamera},
    {"game::getConstCamera", (void**)&swg::game::getConstCamera},
    {"game::isViewFirstPerson", (void**)&swg::game::isViewFirstPerson},
    {"game::isHudSceneTypeSpace", (void**)&swg::game::isHudSceneTypeSpace},
    {"game::g_runningFlags", (void**)&swg::game::g_runningFlags}, // D-04 ACCESSOR: provider &Game::isOver (call-not-read)

    // -- graphics --
    {"graphics::install", (void**)&swg::graphics::install},
    {"graphics::update", (void**)&swg::graphics::update},
    {"graphics::beginScene", (void**)&swg::graphics::beginScene},
    {"graphics::endScene", (void**)&swg::graphics::endScene},
    {"graphics::present", (void**)&swg::graphics::present},
    {"graphics::presentWindow", (void**)&swg::graphics::presentWindow},
    {"graphics::resize", (void**)&swg::graphics::resize},
    {"graphics::flushResources", (void**)&swg::graphics::flushResources},
    {"graphics::screenshot", (void**)&swg::graphics::screenshot},               // MISMATCH: provider Graphics::screenShot
    {"graphics::useHardwareCursor", (void**)&swg::graphics::useHardwareCursor}, // MISMATCH: provider setHardwareMouseCursorEnabled
    {"graphics::showMouseCursor", (void**)&swg::graphics::showMouseCursor},
    {"graphics::setSystemMouseCursorPosition", (void**)&swg::graphics::setSystemMouseCursorPosition},
    {"graphics::setStaticShader", (void**)&swg::graphics::setStaticShader},
    {"graphics::g_renderTargetWidth", (void**)&swg::graphics::g_renderTargetWidth},   // D-04 ACCESSOR: provider &Graphics::getCurrentRenderTargetWidth
    {"graphics::g_renderTargetHeight", (void**)&swg::graphics::g_renderTargetHeight}, // D-04 ACCESSOR: provider &Graphics::getCurrentRenderTargetHeight

    // -- cuiManager --
    {"cuiManager::render", (void**)&swg::cuiManager::render},
    {"cuiManager::setSize", (void**)&swg::cuiManager::setSize},
    {"cuiManager::togglePointer", (void**)&swg::cuiManager::togglePointer}, // MISMATCH: provider CuiManager::setPointerToggledOn
    {"cuiManager::restartMusic", (void**)&swg::cuiManager::restartMusic},
    {"cuiManager::g_instance", (void**)&swg::cuiManager::g_instance}, // D-04 ACCESSOR: provider &CuiManager::getIoWin

    // -- cuiIo --
    {"cuiIo::setKeyboardInputActive", (void**)&swg::cuiIo::setKeyboardInputActive},
    {"cuiIo::requestKeyboard", (void**)&swg::cuiIo::requestKeyboard},
    {"cuiIo::g_instance", (void**)&swg::cuiIo::g_instance}, // D-04 ACCESSOR: provider &CuiManager::getIoWin

    // -- consoleHelper::sendInput -- D-02 / WR-05 CARVE-OUT: intentionally NOT bound.

    // -- commandParser --
    {"commandParser::addSubCommand", (void**)&swg::commandParser::addSubCommand},

    // -- extent (name mismatch: contract extent::intersect -> swg::baseExtent::intersect) --
    {"extent::intersect", (void**)&swg::baseExtent::intersect},

    // -- object (name mismatches: contract object::getObjectType -> swg::object::getType,
    //    getPosition_w -> getPosition, setPosition_w -> setPosition, move_p -> move) --
    {"object::getObjectType", (void**)&swg::object::getType},                       // MISMATCH name
    {"object::getObjectTemplate", (void**)&swg::object::getObjectTemplate},         // D-01 new slot
    {"object::getObjectTemplateName", (void**)&swg::object::getObjectTemplateName}, // D-01 new slot
    {"object::getNetworkId", (void**)&swg::object::getNetworkId},                   // D-01 new slot
    {"object::getParentCell", (void**)&swg::object::getParentCell},
    {"object::getTransform_o2w", (void**)&swg::object::getTransform_o2w},
    {"object::setTransform_o2w", (void**)&swg::object::setTransform_o2w},
    {"object::getPosition_w", (void**)&swg::object::getPosition}, // MISMATCH name
    {"object::setPosition_w", (void**)&swg::object::setPosition}, // MISMATCH name
    {"object::getAppearance", (void**)&swg::object::getAppearance},
    {"object::setAppearance", (void**)&swg::object::setAppearance},
    {"object::move_p", (void**)&swg::object::move}, // MISMATCH name

    // -- objectTemplate (getClientDataFile -> swg::sharedObjectTemplate::getClientDataFilename) --
    {"objectTemplate::createObject", (void**)&swg::objectTemplate::createObject},
    {"objectTemplate::getAppearanceFilename", (void**)&swg::sharedObjectTemplate::getAppearancetFilename}, // MISMATCH name
    {"objectTemplate::getPortalLayoutFilename", (void**)&swg::sharedObjectTemplate::getPortalLayoutFilename},
    {"objectTemplate::getClientDataFile", (void**)&swg::sharedObjectTemplate::getClientDataFilename}, // MISMATCH name

    // -- worldSnapshot (contract worldSnapshot::* -> swg::worldsnapshot::*) --
    {"worldSnapshot::load", (void**)&swg::worldsnapshot::load},
    {"worldSnapshot::addObject", (void**)&swg::worldsnapshot::addObject},
    {"worldSnapshot::removeObject", (void**)&swg::worldsnapshot::removeObject},           // D-01 new slot
    {"worldSnapshot::moveObject", (void**)&swg::worldsnapshot::moveObject},               // D-01 new slot
    {"worldSnapshot::getLoadingPercent", (void**)&swg::worldsnapshot::getLoadingPercent}, // D-01 new slot
    {"worldSnapshot::detailLevelChanged", (void**)&swg::worldsnapshot::detailLevelChanged},

    // -- camera (reverseProjectInViewportSpace -> swg::camera::reverseProjectInViewportSpaceInt) --
    {"camera::setViewport", (void**)&swg::camera::setViewport},
    {"camera::setNearPlane", (void**)&swg::camera::setNearPlane},
    {"camera::setFarPlane", (void**)&swg::camera::setFarPlane},
    {"camera::setHorizontalFieldOfView", (void**)&swg::camera::setHorizontalFieldOfView},
    {"camera::reverseProjectInViewportSpace", (void**)&swg::camera::reverseProjectInViewportSpaceInt}, // MISMATCH name (Int overload)

    // -- memory (memory::free -> swg::memory::deallocate) --
    {"memory::allocate", (void**)&swg::memory::allocate},
    {"memory::free", (void**)&swg::memory::deallocate}, // MISMATCH name

    // -- audio --
    {"audio::setMasterVolume", (void**)&swg::audio::setMasterVolume},
    {"audio::getMasterVolume", (void**)&swg::audio::getMasterVolume},

    // -- treeFile (treeFile::open -> swg::treefile::searchTree) --
    {"treeFile::open", (void**)&swg::treefile::searchTree}, // MISMATCH name

    // -- report --
    {"report::print", (void**)&swg::report::print},

    // -- commandParser ctors --
    {"commandParser::ctor1", (void**)&swg::commandParser::ctor1},
    {"commandParser::ctor2", (void**)&swg::commandParser::ctor2},

    // -- graphics::g_frameNumber (globals tail) --
    {"graphics::g_frameNumber", (void**)&swg::graphics::g_frameNumber}, // D-04 ACCESSOR: provider &Graphics::getFrameNumber

    // ===== v3 (Phase 38) additions: 16 new endpoints (name -> consumer slot) =====
    // -- config (38-02; provider CuiPreferences::set/getModalChat) --
    {"config::setModalChat", (void**)&swg::config::setModalChat},
    {"config::getModalChat", (void**)&swg::config::getModalChat},
    // -- client (38-02; provider DebugHelp::writeMiniDump) --
    // client::wndProc is a 2nd CARVE-OUT (intentionally NOT bound): binding it forwards the
    // TJT panel's WM_SIZE to the embedded DX11 client -> ResizeBuffers to the wrong size ->
    // corrupted render. Allow-listed in endpoints.cpp; embed-resize is RNDR-04 follow-on.
    {"client::writeMiniDump", (void**)&swg::client::writeMiniDump},
    // -- groundScene (38-01; MI thunks/forwarders; update+handleInputMapEvent are REAL-ENTRY in v3) --
    {"groundScene::ctor", (void**)&swg::groundScene::ctor},
    {"groundScene::init", (void**)&swg::groundScene::init},
    {"groundScene::reloadTerrain", (void**)&swg::groundScene::reloadTerrain},
    {"groundScene::changeCamera", (void**)&swg::groundScene::changeCamera}, // contract changeCamera -> provider setView
    {"groundScene::getCurrentCamera", (void**)&swg::groundScene::getCurrentCamera},
    {"groundScene::update", (void**)&swg::groundScene::update},
    {"groundScene::handleInputMapUpdate", (void**)&swg::groundScene::handleInputMapUpdate},
    {"groundScene::handleInputMapEvent", (void**)&swg::groundScene::handleInputMapEvent},
    // -- cuiChatWindow (38-03; enableTextInput+chatEnterHandler are REAL-ENTRY in v3) --
    {"cuiChatWindow::enableTextInput", (void**)&swg::cuiChatWindow::enableTextInput},
    {"cuiChatWindow::writeToAllTabs", (void**)&swg::cuiChatWindow::writeToAllTabs},
    {"cuiChatWindow::writeToCurrentTab", (void**)&swg::cuiChatWindow::writeToCurrentTab},
    {"cuiChatWindow::chatEnterHandler", (void**)&swg::cuiChatWindow::chatEnterHandler},

    // ===== v4 (Phase 24 MISC/INPUT editor-unlock) additions: 3 new endpoints =====
    // Bound now so the contract resolves at v4/97; the detours that CONSUME these slots land
    // per-subsystem behind maintainer live-smokes (the slots resolve but no active hook uses
    // them yet -> behavior-neutral on the advertised client, which stays RENDER-only).
    {"game::g_mainLoopCounter", (void**)&swg::game::g_mainLoopCounter},               // 4b ACCESSOR: provider &Game::getMainLoopCount (call-not-read)
    {"treeFile::searchTree", (void**)&swg::treefile::addSearchTree},                  // 4c: provider &TreeFile::addSearchTree (static __cdecl, reversed args vs SWGEmu searchTree)
    {"cuiChatWindow::createNewWindow", (void**)&swg::cuiChatWindow::createNewWindow}, // 4d: sole construction funnel (C++ ctor is unaddressable)

    // ===== v5 (Phase 24): TreeFile file enumeration -- populates the Repository on the advertised client =====
    {"treeFile::enumerateFiles", (void**)&swg::treefile::enumerateFiles}, // provider &TreeFile::enumerateFiles (callback per filename)

    // ===== v6 (Phase 24): full SceneCreator string-based scene load (editor "Load scene") =====
    {"game::loadScene", (void**)&swg::game::loadScene}, // provider &utinni_gameLoadScene -> Game::setScene(true, terrain, player, nullptr)

    // ===== v7 (Phase 24 / 24-§2.B Bucket B -- Effects editor live preview): 5 new endpoints =====
    // The 4 render rows bind to existing per-subsystem literals (behavior-neutral this wave: their
    // detours stay gated or are no-ops); particlePreview::retrigger is the live-preview value.
    {"skeletalAppearance::getDisplayLodSkeleton", (void**)&swg::skeletalAppearance::getDisplayLodSkeleton}, // provider bit_cast PMF (non-virtual const overload)
    {"renderWorld::addObjectNotifications", (void**)&swg::renderWorld::addObjectNotifications},             // provider static &RenderWorld::addObjectNotifications(Object&)
    {"bloom::preSceneRender", (void**)&swg::bloom::preSceneRender},                                         // provider static &Bloom::preSceneRender
    {"bloom::postSceneRender", (void**)&swg::bloom::postSceneRender},                                       // provider static &Bloom::postSceneRender
    {"particlePreview::retrigger", (void**)&swg::particlePreview::retrigger},                               // provider &utinni_retriggerClientEffect (friend free fn; null on SWGEmu)

    // ===== v8 (Phase 24 / 24-§2.B-2 Bucket B-2 -- live .cef RE-PLAY): 1 new endpoint =====
    {"particlePreview::replayClientEffect", (void**)&swg::particlePreview::replay}, // provider &utinni_replayClientEffect (re-play .cef on the player; null on SWGEmu)

    // ===== v9 (Phase 24 / Bucket A -- per-editor real-entry detour rows §2.A): 6 new endpoints =====
    // Bound now so the contract resolves at v9/111 + WS-3 drift telemetry covers them; the detours
    // that CONSUME these slots stay wholesale-gated `!advertised` in utinni.cpp -> behavior-neutral.
    // Per-editor un-gate lands per-subsystem behind individual maintainer smokes (the v4/v5 idiom).
    {"cuiRadialMenuManager::update", (void**)&swg::cuiRadialMenuManager::update},               // static &CuiRadialMenuManager::update
    {"cuiMenu::infoTypesFindDefaultCursor", (void**)&swg::cuiMenu::infoTypesFindDefaultCursor}, // free fn Cui::MenuInfoTypes::findDefaultCursor
    {"creatureObject::setTarget", (void**)&swg::creatureObject::setTarget},                     // MISMATCH: provider CreatureObject::setLookAtTarget (MI real entry)
    {"messageQueue::appendMessage", (void**)&swg::messageQueue::appendMessage},                 // MessageQueue::appendMessage(int,float,uint32)
    {"messageQueue::appendMessageData", (void**)&swg::messageQueue::appendMessageData},         // MISMATCH: provider appendMessage(int,float,Data*,uint32)

    // ===== v10 (Phase 24 / Bucket A-2 -- world-pick / HUD-target): 2 new endpoints (getter rows, not detoured) =====
    {"cuiHud::getTarget", (void**)&swg::cuiHud::getTarget},   // __fastcall thunk -> SwgCuiHud::getLastSelectedObject() const (the picked Object*)
    {"cuiHud::g_instance", (void**)&swg::cuiHud::g_instance}, // static SwgCuiHudFactory::findMediatorForCurrentHud() (the live SwgCuiHud*)

    // ===== v12 (Phase 24 / Bucket A-3 -- network id->Object resolver, unblocks target-change): 1 new endpoint =====
    {"network::getObjectById", (void**)&swg::network::idManagerGetObjectById}, // MISMATCH name: provider NetworkIdManager::getObjectById (static)

    // ===== v13 (Phase 24 / free-cam editor unlock -- accessors replacing fragile NGE struct offsets): 6 new endpoints =====
    // CALLED accessors (NOT detoured). Binding now resolves them at v13/117 -- behavior-neutral: the slots
    // fill on the advertised client but the free-cam path stays gated until the consumer wiring waves
    // (processIoEvent latch-route, alter vtable-resolve, handleInputMapEvent un-skip, FreeCamImpl).
    {"groundScene::isFreeCameraActive", (void**)&swg::groundScene::isFreeCameraActive},                             // __fastcall thunk getCurrentView()==CI_debugPortal (null on SWGEmu)
    {"groundScene::getDebugPortalCameraMessageQueue", (void**)&swg::groundScene::getDebugPortalCameraMessageQueue}, // friend forwarder -> input MQ (replaces debugPortalCameraInputMap+0xC)
    {"gameCamera::getMessageQueue", (void**)&swg::gameCamera::getMessageQueue},                                     // __fastcall thunk getController()->getMessageQueue() (replaces camera+0x248; aliases the input MQ)
    {"messageQueue::getCount", (void**)&swg::messageQueue::getCount},                                               // MISMATCH: provider MessageQueue::getNumberOfMessages
    {"messageQueue::getMessage", (void**)&swg::messageQueue::getMessage},                                           // 4-arg overload getMessage(int,int*,float*,uint32*)
    {"object::isActive", (void**)&swg::object::isActive},                                                           // external-linkage shim (non-virtual but inline -> no PMF on the provider side)

    // -- v15 (Phase 24 / sysmsg SEND rev-2): the INJECT half only, via the provider's extern "C"
    //    utf8 shim (the v14 direct Unicode::String& row crashed -- string-layout ABI; name-REPLACED).
    //    Slot starts null (advertised-only); the SWGEmu WString literal stays a separate, unbound path.
    {"systemMessageManager::sendMessageUtf8", (void**)&swg::systemMessageManager::sendMessageUtf8}, // CALLED (inject); receive half stays OMIT

    // ===== v16 (Phase 24 / Goal A+ -- player lookAt-target id READ): 1 new endpoint =====
    // Provider extern "C" utinni_getPlayerLookAtTargetId: the player's lookAt/selection-target
    // NetworkId VALUE (full int64; 0 = no player/no target) -- the READ twin of the v9
    // creatureObject::setTarget row (same m_lookAtTarget slot; NOT NGE intended/combat target).
    // Primitive-only shim per the sysmsg rev-2 ABI rule. Consumer resolves the id via the v12
    // network::getObjectById row in Game::getPlayerLookAtTargetObject()'s advertised branch.
    {"game::getPlayerLookAtTargetId", (void**)&swg::game::getPlayerLookAtTargetId}, // CALLED, game-thread, on-demand

    // ===== v17 (Phase 24 / Goal B Wave 1 -- snapshot-editor id-keyed READ): 7 new endpoints =====
    // rev-3 frozen row table (24-PROVIDER-REQUEST-goalB-wave1-rows.md). All CALLED accessors
    // (never detoured), game-thread-only, miss-safe on unknown ids. Enumeration is live +
    // AUTHORED-ONLY (tombstones and buildout-provenance rows never appear -- counts are smaller
    // than SWGEmu raw walks by contract). Consumed via the utinni::WorldSnapshotLive facade.
    {"worldSnapshot::wsGetNodeCount", (void**)&swg::worldsnapshot::wsGetNodeCount},
    {"worldSnapshot::wsGetTopNodeIdAt", (void**)&swg::worldsnapshot::wsGetTopNodeIdAt},
    {"worldSnapshot::wsGetChildCount", (void**)&swg::worldsnapshot::wsGetChildCount},
    {"worldSnapshot::wsGetChildIdAt", (void**)&swg::worldsnapshot::wsGetChildIdAt},
    {"worldSnapshot::wsGetNodeInfo", (void**)&swg::worldsnapshot::wsGetNodeInfo},                 // size-first UtinniWsNodeInfo POD-out
    {"worldSnapshot::wsGetNodeTemplateName", (void**)&swg::worldsnapshot::wsGetNodeTemplateName}, // returns needed length INCLUDING NUL
    {"worldSnapshot::wsGetGeneration", (void**)&swg::worldsnapshot::wsGetGeneration},             // pure counter (no parse force); compare !=, never +1

    // ===== v18 (Phase 24 / Goal B Wave 2 -- snapshot-editor LIVE-ONLY mutation): 5 new endpoints =====
    // Frozen 2026-07-18 row table. CALLED (never detoured), game-thread-only, every op fail-closed
    // with full pre-validation BEFORE any reader mutation. Nothing persists (Wave 3). Consumed via
    // the utinni::WorldSnapshotLive mutation veneer.
    {"worldSnapshot::wsAddObject", (void**)&swg::worldsnapshot::wsAddObject},                       // provider mints id (+POB cells); returns new top id, 0 fail-closed
    {"worldSnapshot::wsAddNodeAt", (void**)&swg::worldsnapshot::wsAddNodeAt},                       // undo-replay data re-add at EXPLICIT id; one-batch subtree contract
    {"worldSnapshot::wsRemoveNode", (void**)&swg::worldsnapshot::wsRemoveNode},                     // TRI-STATE: 1 removed / 0 miss / -1 occupied
    {"worldSnapshot::wsSetNodeRadius", (void**)&swg::worldsnapshot::wsSetNodeRadius},               // re-seats the sphere-tree extent
    {"worldSnapshot::wsConfigureIdAllocator", (void**)&swg::worldsnapshot::wsConfigureIdAllocator}, // install-scan floor / ceiling band

    // ===== v19 (Phase 24 / Goal B Wave 3 -- persistence + riders): 7 new endpoints =====
    // CALLED (never detoured), game-thread-only. Persistence writes the current scene's authored .ws;
    // targeting + camera riders unlock in-world static selection + the live gizmo. Consumed via the
    // WorldSnapshotLive persistence methods, cuiHud targeting, and imgui_impl gizmo matrices.
    {"worldSnapshot::wsSaveSnapshot", (void**)&swg::worldsnapshot::wsSaveSnapshot},                   // typed result enum (0 ok; 1..6 errors)
    {"worldSnapshot::wsGetSavePath", (void**)&swg::worldsnapshot::wsGetSavePath},                     // save-root copy-out; 0 = no loose SearchPath
    {"worldSnapshot::wsUnloadSnapshot", (void**)&swg::worldsnapshot::wsUnloadSnapshot},               // unload + ms_sceneName reset (reload prerequisite)
    {"cuiPreferences::setAllowTargetAnything", (void**)&swg::cuiPreferences::setAllowTargetAnything}, // NGE targeting filter (rider 4B)
    {"cuiPreferences::getAllowTargetAnything", (void**)&swg::cuiPreferences::getAllowTargetAnything}, // restore-on-close read
    {"camera::getProjectionMatrix", (void**)&swg::camera::getProjectionMatrix},                       // gizmo projection (rider 4C; row-major float[4][4])
    {"camera::getTransformO2W", (void**)&swg::camera::getTransformO2W},                               // gizmo camera o2w (row-major float[3][4])

    // ===== v20 (Live World Editor ray-pick + pre-approved radial clear): 2 new endpoints =====
    // CALLED (never detoured), game-thread-only. collideScreenRay is the engine-side copy-out
    // cursor ray-cast (hud pick semantics, player excluded; terrain hit -> ret 1 + id 0 + valid
    // point). clear is the public static CuiRadialMenuManager::clear (editor-teardown reset).
    {"clientWorld::collideScreenRay", (void**)&swg::clientWorld::collideScreenRay}, // 1 hit / 0 miss; objectsOnly=1 drops terrain/terrainFlora/interiorGeometry
    {"cuiRadialMenuManager::clear", (void**)&swg::cuiRadialMenuManager::clear},     // static &CuiRadialMenuManager::clear (v20 rider)
};

// ----------------------------------------------------------------------
// D-03a (this side): the X-macro subset assert, re-applied against the s_bindings[]
// names that live HERE. A bogus binding name in s_bindings[] fails the BUILD here,
// not silently at runtime (EPA-04 layer a / T-24-04). Kept duplicated next to the
// table it guards (the predicate has no external deps).
// ----------------------------------------------------------------------
namespace
{
constexpr bool ceStrEq(const char* a, const char* b)
{
    while (*a && (*a == *b))
    {
        ++a;
        ++b;
    }
    return *a == *b;
}

constexpr bool isInHookpointInc(const char* n)
{
    return false
#define ENGINE_HOOKPOINT(group, name) || ceStrEq(n, #group "::" #name)
#include "swg/engine_hookpoints.inc"
#undef ENGINE_HOOKPOINT
        ;
}

// Compile-time count of the .inc rows (the contract size) -- the s_bindings[] count
// MUST be exactly this minus the one D-02 carve-out (77 of 78). A drifted .inc or a
// dropped/duplicated binding row trips this BUILD-time gate (EPA-04 / Pitfall 5).
constexpr size_t kIncCount = 0
#define ENGINE_HOOKPOINT(group, name) +1
#include "swg/engine_hookpoints.inc"
#undef ENGINE_HOOKPOINT
    ;

constexpr size_t kBindingCount = sizeof(s_bindings) / sizeof(s_bindings[0]);

static_assert(kIncCount == 142, "contract .inc size drifted from the expected 142 names (v20 / Live World Editor ray-pick + radial clear: 2 name-adds)");
static_assert(kBindingCount == 140, "s_bindings[] must bind 140 of 142 (.inc minus the TWO carve-outs)");
static_assert(kBindingCount == kIncCount - 2,
              "exactly two .inc names are carve-outs: consoleHelper::sendInput (D-02) + "
              "client::wndProc (embed-resize regression; RNDR-04 follow-on)");

// Every s_bindings[] name MUST be a member of the .inc catalog (subset invariant).
// s_bindings[] itself is NOT constexpr (its .slot initializers take the address of
// extern objects -- not a constant expression), so the subset check iterates a
// PARALLEL constexpr array of just the binding NAMES (string literals -> constexpr).
// This array MUST stay in lockstep with s_bindings[] (same names, same order); the
// count static_assert below ties them together so a drift between the two trips here.
constexpr const char* kBindingNames[] = {
    "config::loadOverrideConfig",
    "config::loadConfigFileBuffer",
    "config::loadConfigFileString",
    "client::clientMain",
    "game::install",
    "game::quit",
    "game::mainLoop",
    "game::setupScene",
    "game::cleanupScene",
    "game::getPlayer",
    "game::getPlayerCreatureObject",
    "game::getCamera",
    "game::getConstCamera",
    "game::isViewFirstPerson",
    "game::isHudSceneTypeSpace",
    "game::g_runningFlags",
    "graphics::install",
    "graphics::update",
    "graphics::beginScene",
    "graphics::endScene",
    "graphics::present",
    "graphics::presentWindow",
    "graphics::resize",
    "graphics::flushResources",
    "graphics::screenshot",
    "graphics::useHardwareCursor",
    "graphics::showMouseCursor",
    "graphics::setSystemMouseCursorPosition",
    "graphics::setStaticShader",
    "graphics::g_renderTargetWidth",
    "graphics::g_renderTargetHeight",
    "cuiManager::render",
    "cuiManager::setSize",
    "cuiManager::togglePointer",
    "cuiManager::restartMusic",
    "cuiManager::g_instance",
    "cuiIo::setKeyboardInputActive",
    "cuiIo::requestKeyboard",
    "cuiIo::g_instance",
    "commandParser::addSubCommand",
    "extent::intersect",
    "object::getObjectType",
    "object::getObjectTemplate",
    "object::getObjectTemplateName",
    "object::getNetworkId",
    "object::getParentCell",
    "object::getTransform_o2w",
    "object::setTransform_o2w",
    "object::getPosition_w",
    "object::setPosition_w",
    "object::getAppearance",
    "object::setAppearance",
    "object::move_p",
    "objectTemplate::createObject",
    "objectTemplate::getAppearanceFilename",
    "objectTemplate::getPortalLayoutFilename",
    "objectTemplate::getClientDataFile",
    "worldSnapshot::load",
    "worldSnapshot::addObject",
    "worldSnapshot::removeObject",
    "worldSnapshot::moveObject",
    "worldSnapshot::getLoadingPercent",
    "worldSnapshot::detailLevelChanged",
    "camera::setViewport",
    "camera::setNearPlane",
    "camera::setFarPlane",
    "camera::setHorizontalFieldOfView",
    "camera::reverseProjectInViewportSpace",
    "memory::allocate",
    "memory::free",
    "audio::setMasterVolume",
    "audio::getMasterVolume",
    "treeFile::open",
    "report::print",
    "commandParser::ctor1",
    "commandParser::ctor2",
    "graphics::g_frameNumber",
    // ===== v3 (Phase 38) additions — lockstep with s_bindings[] above =====
    "config::setModalChat",
    "config::getModalChat",
    "client::writeMiniDump",
    "groundScene::ctor",
    "groundScene::init",
    "groundScene::reloadTerrain",
    "groundScene::changeCamera",
    "groundScene::getCurrentCamera",
    "groundScene::update",
    "groundScene::handleInputMapUpdate",
    "groundScene::handleInputMapEvent",
    "cuiChatWindow::enableTextInput",
    "cuiChatWindow::writeToAllTabs",
    "cuiChatWindow::writeToCurrentTab",
    "cuiChatWindow::chatEnterHandler",
    // ===== v4 (Phase 24) additions — lockstep with s_bindings[] above =====
    "game::g_mainLoopCounter",
    "treeFile::searchTree",
    "cuiChatWindow::createNewWindow",
    // ===== v5 (Phase 24) addition — lockstep with s_bindings[] above =====
    "treeFile::enumerateFiles",
    // ===== v6 (Phase 24) addition — lockstep with s_bindings[] above =====
    "game::loadScene",
    // ===== v7 (Phase 24 / Bucket B) additions — lockstep with s_bindings[] above =====
    "skeletalAppearance::getDisplayLodSkeleton",
    "renderWorld::addObjectNotifications",
    "bloom::preSceneRender",
    "bloom::postSceneRender",
    "particlePreview::retrigger",
    // ===== v8 (Phase 24 / Bucket B-2) addition — lockstep with s_bindings[] above =====
    "particlePreview::replayClientEffect",
    // ===== v9 (Phase 24 / Bucket A) additions — lockstep with s_bindings[] above =====
    "cuiRadialMenuManager::update",
    "cuiMenu::infoTypesFindDefaultCursor",
    "creatureObject::setTarget",
    "messageQueue::appendMessage",
    "messageQueue::appendMessageData",
    // ===== v10 (Phase 24 / Bucket A-2 world-pick) additions — lockstep with s_bindings[] above =====
    "cuiHud::getTarget",
    "cuiHud::g_instance",
    // ===== v12 (Phase 24 / Bucket A-3 network id-resolver) addition — lockstep with s_bindings[] above =====
    "network::getObjectById",
    // ===== v13 (Phase 24 / free-cam accessors) additions — lockstep with s_bindings[] above =====
    "groundScene::isFreeCameraActive",
    "groundScene::getDebugPortalCameraMessageQueue",
    "gameCamera::getMessageQueue",
    "messageQueue::getCount",
    "messageQueue::getMessage",
    "object::isActive",
    // ===== v15 (Phase 24 / sysmsg SEND rev-2, name-replaced the v14 row) — lockstep with s_bindings[] above =====
    "systemMessageManager::sendMessageUtf8",
    // ===== v16 (Phase 24 / Goal A+ lookAt-target id READ) addition — lockstep with s_bindings[] above =====
    "game::getPlayerLookAtTargetId",
    // ===== v17 (Phase 24 / Goal B Wave 1 snapshot READ) additions — lockstep with s_bindings[] above =====
    "worldSnapshot::wsGetNodeCount",
    "worldSnapshot::wsGetTopNodeIdAt",
    "worldSnapshot::wsGetChildCount",
    "worldSnapshot::wsGetChildIdAt",
    "worldSnapshot::wsGetNodeInfo",
    "worldSnapshot::wsGetNodeTemplateName",
    "worldSnapshot::wsGetGeneration",
    // ===== v18 (Phase 24 / Goal B Wave 2 snapshot mutation) additions — lockstep with s_bindings[] above =====
    "worldSnapshot::wsAddObject",
    "worldSnapshot::wsAddNodeAt",
    "worldSnapshot::wsRemoveNode",
    "worldSnapshot::wsSetNodeRadius",
    "worldSnapshot::wsConfigureIdAllocator",
    // ===== v19 (Phase 24 / Goal B Wave 3) additions — lockstep with s_bindings[] above =====
    "worldSnapshot::wsSaveSnapshot",
    "worldSnapshot::wsGetSavePath",
    "worldSnapshot::wsUnloadSnapshot",
    "cuiPreferences::setAllowTargetAnything",
    "cuiPreferences::getAllowTargetAnything",
    "camera::getProjectionMatrix",
    "camera::getTransformO2W",
    // ===== v20 additions — lockstep with s_bindings[] above =====
    "clientWorld::collideScreenRay",
    "cuiRadialMenuManager::clear",
};

constexpr bool allNamesInInc()
{
    for (const char* n : kBindingNames)
    {
        if (!isInHookpointInc(n))
        {
            return false;
        }
    }
    return true;
}

// The parallel name array MUST be exactly as long as s_bindings[] (lockstep) ...
static_assert(sizeof(kBindingNames) / sizeof(kBindingNames[0]) == kBindingCount,
              "kBindingNames[] drifted from s_bindings[] -- keep them in lockstep");
// ... and every name MUST be advertised in the .inc (subset invariant / drift gate).
static_assert(allNamesInInc(), "an s_bindings[] name is not advertised in the .inc (drift)");
// The carve-out must NOT appear among the bound names (D-02).
static_assert([]() constexpr
              {
    for (const char* n : kBindingNames)
    {
        if (ceStrEq(n, "consoleHelper::sendInput"))
        {
            return false;
        }
    }
    return true; }(), "consoleHelper::sendInput must remain the unbound D-02 carve-out");
} // namespace

// Phase 24: set true by resolveFromExe() when the GetEngineHookPoints export is present.
// Drives installable() below so createDetours()/createPatches() can skip wholly-unresolved
// subsystems on the advertised client while staying a strict no-op on SWGEmu.
static bool s_advertisedClient = false;

// WS-3 init telemetry: per-binding provenance filled by resolveFromExe() on the advertised path
// (lockstep with s_bindings[]). Default Unresolved; consulted ONLY for the one-shot drift log in
// resolveFromExe() -- NOT a runtime safety gate (crew review 2026-06-25). See Source doc in endpoints.h.
static Source s_bindingSource[sizeof(s_bindings) / sizeof(s_bindings[0])] = {};

bool isAdvertisedClient()
{
    return s_advertisedClient;
}

// Committed + executable check (same predicate as the Detour::Create / memory:: guards).
static bool isCommittedExecutable(const void* addr)
{
    if (addr == nullptr)
        return false;

    MEMORY_BASIC_INFORMATION mbi;
    if (VirtualQuery(addr, &mbi, sizeof(mbi)) == 0)
        return false;
    if (mbi.State != MEM_COMMIT)
        return false;
    if (mbi.Protect & (PAGE_GUARD | PAGE_NOACCESS))
        return false;

    const DWORD prot = mbi.Protect & 0xFF; // strip PAGE_GUARD/NOCACHE/WRITECOMBINE modifiers
    return prot == PAGE_EXECUTE || prot == PAGE_EXECUTE_READ ||
           prot == PAGE_EXECUTE_READWRITE || prot == PAGE_EXECUTE_WRITECOPY;
}

bool installable(const void* target)
{
    // SWGEmu (export absent): always install -- every literal is a valid mapped address,
    // so the full hook set installs unchanged (D-00). Advertised client: install only when
    // the subsystem's primary target resolved to committed + executable memory.
    return !s_advertisedClient || isCommittedExecutable(target);
}

bool resolveFromExe()
{
    using pGetEngineHookPoints = const EngineHookPoints*(__cdecl*)();

    HMODULE hExe = GetModuleHandleA(nullptr); // the injected SWG client exe
    auto pGet = reinterpret_cast<pGetEngineHookPoints>(
        GetProcAddress(hExe, "GetEngineHookPoints"));
    if (pGet == nullptr)
    {
        // SWGEmu Pre-CU: no export -> STRICT NO-OP. Every swg::* RVA literal is
        // left exactly as-is so the existing D3D9 path is byte-for-byte unchanged
        // (D-00 / criterion 3 / T-24-03).
        utinni::log::info("endpoints: no GetEngineHookPoints export -- RVA path (SWGEmu Pre-CU)");
        return false;
    }

    // Advertised client: latch the dual-path flag BEFORE resolving so the per-subsystem
    // install gate (installable()) is armed for createDetours()/createPatches().
    s_advertisedClient = true;

    const EngineHookPoints* table = pGet();
    const size_t count = sizeof(s_bindings) / sizeof(s_bindings[0]);
    resolve(table, s_bindings, count, s_bindingSource);

    // WS-3 init telemetry: surface any BOUND name that ended SwgemuRva -- a miss whose slot still
    // holds its hardcoded SWGEmu literal (drift / version skew) -> garbage on the advertised client.
    // Diagnostic ONLY: the read-sites still degrade via the per-subsystem isAdvertisedClient() guards;
    // this just catches a dropped/renamed contract name at init instead of at the eventual crash. The
    // provider's lazy-fill GetEngineHookPoints() makes init-time tagging authoritative (see endpoints.h).
    int drift = 0;
    for (size_t i = 0; i < count; ++i)
    {
        if (s_bindingSource[i] == Source::SwgemuRva)
        {
            char m[176];
            std::snprintf(m, sizeof(m),
                          "endpoints WS-3: bound name '%s' MISSED on advertised -> still SWGEmu RVA (drift/skew)",
                          s_bindings[i].name);
            utinni::log::warning(m);
            ++drift;
        }
    }
    if (drift == 0)
    {
        utinni::log::info("endpoints WS-3: every bound name resolved on advertised (no SwgemuRva drift)");
    }
    return true;
}

int countResolvableNow()
{
    using pGetEngineHookPoints = const EngineHookPoints*(__cdecl*)();
    HMODULE hExe = GetModuleHandleA(nullptr);
    auto pGet = reinterpret_cast<pGetEngineHookPoints>(GetProcAddress(hExe, "GetEngineHookPoints"));
    if (pGet == nullptr)
    {
        return -1; // SWGEmu / no export
    }

    const EngineHookPoints* table = pGet();
    const size_t count = sizeof(s_bindings) / sizeof(s_bindings[0]);
    int now = 0;
    for (size_t i = 0; i < count; ++i)
    {
        if (lookupByName(table, s_bindings[i].name) != nullptr)
        {
            ++now;
        }
    }

    char msg[224];
    std::snprintf(msg, sizeof(msg),
                  "endpoints DIAG: re-read post-init -> %d/%zu resolvable NOW (enumerateFiles=%s, "
                  "cuiManager::setSize=%s, groundScene::update=%s, object::getObjectType=%s)",
                  now, count, lookupByName(table, "treeFile::enumerateFiles") ? "OK" : "null",
                  lookupByName(table, "cuiManager::setSize") ? "OK" : "null",
                  lookupByName(table, "groundScene::update") ? "OK" : "null",
                  lookupByName(table, "object::getObjectType") ? "OK" : "null");
    utinni::log::info(msg);
    return now;
}
} // namespace swg::endpoints

// Phase 24 embed-startup mitigation: C-linkage export so PanelGame.cs can branch on the
// advertised-client state (the reparent-deferral gate only applies to the advertised client;
// SWGEmu reparents as before). Returns C++ bool -> Native.cs marshals it as UnmanagedType.I1.
extern "C" __declspec(dllexport) bool __cdecl isAdvertisedClientExport()
{
    return swg::endpoints::isAdvertisedClient();
}
