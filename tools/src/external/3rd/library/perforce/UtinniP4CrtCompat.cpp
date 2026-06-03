// UtinniP4CrtCompat.cpp
//
// Utinni 12-02 CRT-compat shim for the 2002-era Perforce ClientAPI support lib
// (libsupp.lib) linked against the modern Universal CRT under PlatformToolset v145.
//
// libsupp references two legacy CRT symbols that the UCRT no longer provides the
// way the old import expects:
//
//   * _fscanf  -> the UCRT made the scanf family inline, so the standalone export
//                 is gone. Re-supplied by legacy_stdio_definitions.lib (added to the
//                 link globally in tools/Directory.Build.props).
//
//   * __tzname -> the legacy timezone-name DATA export. The UCRT exposes timezone
//                 names only through _tzset()/_get_tzname(); the raw data symbol is
//                 no longer exported. libsupp's DateTime::FmtTz reads it solely to
//                 format human-readable timezone suffixes in log timestamps -- a path
//                 Utinni's headless, byte-exact `-compile` verb never exercises.
//
// We provide a benign definition so the link resolves. The C identifier `_tzname`
// decorates to the linker symbol `__tzname` on x86, matching the unresolved external.
// This is a separate datum from the UCRT's internal timezone state (which is reached
// via _get_tzname); the only observable effect is that any Perforce-side log timestamp
// would read "GMT" rather than the host TZ. Utinni never triggers that path.
//
// Scope: compiled only into the two template-tool EXEs (TemplateCompiler,
// TemplateDefinitionCompiler) that link the Perforce libs, with PCH disabled.

extern "C" char *_tzname[2] = { const_cast<char *>("GMT"), const_cast<char *>("GMT") };
