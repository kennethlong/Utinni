// ======================================================================
//
// utinni_engine_hookpoints.h -- Utinni engine entry-point advertisement
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
// wave so the two repos cannot drift -- see UTINNI_HOOKPOINTS_VERSION note).
// It therefore carries ONLY the structs + version + the X-macro name list
// (.inc). It MUST NOT carry the provider-side exported GetEngineHookPoints()
// declaration -- a consumer repo must never import a dll-exported symbol. The
// export declaration+definition lives ONLY in the SwgClient-only TU
// utinni_advertise.cpp.
//
// Keep this header EXE-LOCAL: it must not be added to any shared header the
// gl0X renderer plugins compile (AGENTS.md shared-header ABI cascade trap).
// It pulls in no engine library header by design.
//
// See .planning/handoff/2026-06-20-utinni-engine-entrypoint-advertisement-spec.md.
// ======================================================================

#ifndef INCLUDED_utinni_engine_hookpoints_H
#define INCLUDED_utinni_engine_hookpoints_H

// ----------------------------------------------------------------------
// Contract version. PINNED at 1 for all waves: lockstep-by-shared-header
// makes a per-wave bump cosmetic (the .h/.inc are re-copied into
// D:/Code/Utinni at each wave so the consumer always sees the matching
// version). Do NOT bump per wave -- the required action is the explicit
// .inc + .h re-copy into D:/Code/Utinni (tasked in 37-02/37-03).
// ----------------------------------------------------------------------
#define UTINNI_HOOKPOINTS_VERSION 1

// ----------------------------------------------------------------------
// One row per advertised endpoint: a stable contract name + the borrowed
// address of the engine function/global (or a thunk wrapping it).
// ----------------------------------------------------------------------
struct UtinniEngineHookPoint
{
	const char * name;   // stable contract identity, e.g. "config::loadOverrideConfig"
	void *       addr;   // &EngineSymbol (or thunk) -- borrowed, process-lifetime
};

// ----------------------------------------------------------------------
// The advertised table. Returned by GetEngineHookPoints() as a pointer to
// a process-lifetime static; Utinni only reads it. No NUL-name sentinel --
// count is sizeof/sizeof of the row array (see utinni_advertise.cpp).
// ----------------------------------------------------------------------
struct UtinniEngineHookPoints
{
	unsigned int                 version;   // == UTINNI_HOOKPOINTS_VERSION at build time
	unsigned int                 count;     // number of rows in entries[]
	const UtinniEngineHookPoint * entries;  // static array of `count` rows
};

#endif // INCLUDED_utinni_engine_hookpoints_H
