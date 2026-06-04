// ======================================================================
// Utinni 13-01 (AUTH-06) — dead-alias redirect shim.
//
// The lifted item exporters (ArmorExporterTool / CoreWeaponExporterTool)
// #include "serverGame/ServerObjectTemplate.h" and read a handful of
// COMPILE-TIME enum constants from it (ServerObjectTemplate::ArmorCategory_Last,
// ArmorLevel_Last, XP_crafting, XP_craftingClothingArmor, CT_weapon, CT_lightsaber).
//
// The pinned lift-source corpus (@5fce7bb8) has NO server tree — the original
// `src/engine/server/library/serverGame` include root the exporter vcxproj
// pointed at does not exist in the client-only repo. The ServerObjectTemplate
// class that physically carries those enums lives in the Phase-12-lifted
// sharedTemplate. This shim redirects the dead serverGame alias there so the
// enum references resolve without re-introducing a server-side closure (the
// exporters use enum values only — no serverGame link symbol is referenced).
//
// This is an include-path redirect (the Phase-12 shim pattern), NOT a source
// edit to the lifted exporter .cpp. Recorded as an AUTH-06 revival delta in
// tools/DEPENDENCY-MANIFEST.md.
// ======================================================================
#ifndef UTINNI_COMPAT_SERVERGAME_SERVEROBJECTTEMPLATE_H
#define UTINNI_COMPAT_SERVERGAME_SERVEROBJECTTEMPLATE_H

#include "sharedTemplate/ServerObjectTemplate.h"

#endif // UTINNI_COMPAT_SERVERGAME_SERVEROBJECTTEMPLATE_H
