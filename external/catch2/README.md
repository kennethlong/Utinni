# Catch2 — Vendored Dependency

**Version:** v3.15.0
**Source URL:** https://github.com/catchorg/Catch2/releases/tag/v3.15.0
**Amalgamated files:**
- https://github.com/catchorg/Catch2/releases/download/v3.15.0/catch_amalgamated.hpp
- https://github.com/catchorg/Catch2/releases/download/v3.15.0/catch_amalgamated.cpp

**License:** Boost Software License 1.0 (BSL-1.0) — see `LICENSE.txt`
**Vendored:** 2026-05-23
**SHA-256 (catch_amalgamated.hpp):** DDF4E42976DEA2BBBE8E7464AD5AB156E7061CC8CCEF290E6E406477283483EE
**SHA-256 (catch_amalgamated.cpp):** 2AB441B2FA0051A547E88AF4AD98151C1CE1F2FBE3D5E9AD9367CFC2FD44DBF8
**Why vendored, not vcpkg:** matches existing `external/` posture (CppSharp, DetourXS, ImGuizmo, LeksysINI, imgui, nvapi, spdlog all vendored); vcpkg adoption deferred to Phase 6 STAB-03 per 05-CONTEXT.md D-02.

## Integrity Verification

To re-verify the vendored amalgamated files against the upstream tag:

```powershell
Get-FileHash external/catch2/catch_amalgamated.hpp -Algorithm SHA256
Get-FileHash external/catch2/catch_amalgamated.cpp -Algorithm SHA256
```

Compare against the SHA-256 values above. Mismatch indicates tampering or accidental modification.
