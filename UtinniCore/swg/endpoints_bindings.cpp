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
// Plan 02 / D-01: the FULL catalog -- every utinni_engine_hookpoints.inc name
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

// resolveFromExe() uses GetModuleHandleA/GetProcAddress -- the only Windows reach.
#include <Windows.h>

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
} // namespace swg::worldsnapshot

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
} // namespace swg::camera

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
} // namespace swg::treefile

// -- report (misc/swg_misc.cpp:29-31) ---------------------------------------
namespace swg::report
{
using pPrint = void(__cdecl*)(const char* msg);
extern pPrint print;
} // namespace swg::report

namespace swg::endpoints
{
// ----------------------------------------------------------------------
// FULL CATALOG (Plan 02 / D-01): every utinni_engine_hookpoints.inc name MINUS the
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
#define UTINNI_HOOKPOINT(group, name) || ceStrEq(n, #group "::" #name)
#include "swg/utinni_engine_hookpoints.inc"
#undef UTINNI_HOOKPOINT
        ;
}

// Compile-time count of the .inc rows (the contract size) -- the s_bindings[] count
// MUST be exactly this minus the one D-02 carve-out (77 of 78). A drifted .inc or a
// dropped/duplicated binding row trips this BUILD-time gate (EPA-04 / Pitfall 5).
constexpr size_t kIncCount = 0
#define UTINNI_HOOKPOINT(group, name) +1
#include "swg/utinni_engine_hookpoints.inc"
#undef UTINNI_HOOKPOINT
    ;

constexpr size_t kBindingCount = sizeof(s_bindings) / sizeof(s_bindings[0]);

static_assert(kIncCount == 78, "contract .inc size drifted from the expected 78 names");
static_assert(kBindingCount == 77, "s_bindings[] must bind 77 of 78 (.inc minus the D-02 carve-out)");
static_assert(kBindingCount == kIncCount - 1, "exactly one .inc name (consoleHelper::sendInput) is the carve-out");

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
    using pGetEngineHookPoints = const UtinniEngineHookPoints*(__cdecl*)();

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

    const UtinniEngineHookPoints* table = pGet();
    resolve(table, s_bindings, sizeof(s_bindings) / sizeof(s_bindings[0]));
    return true;
}
} // namespace swg::endpoints
