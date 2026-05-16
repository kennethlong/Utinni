---
phase: 01
slug: ci-tier-1-c-scaffold
status: verified
threats_total: 12
threats_closed: 12
threats_open: 0
threats_accepted: 8
threats_mitigated: 4
asvs_level: 1
block_on: critical
created: 2026-05-16
audited: 2026-05-16
register_authored_at_plan_time: true
---

# Phase 01 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.
> Phase 01 ships CI/test infrastructure only — no production attack surface, no user data, no network listeners. Threat model focuses on supply-chain (NuGet, GitHub Actions, legacy DXSDK installer) and CI privilege scoping. ASVS level 1 is the appropriate baseline; bump to L2 if a future phase expands scope to handle untrusted input.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| developer workstation → NuGet registry (`api.nuget.org`) | `msbuild /restore` pulls `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` over TLS. Compromise vectors: typosquatted package, transitive dep drift, registry MITM. | NuGet packages (.nupkg, signed) |
| developer workstation → committed lockfile | `UtinniCoreDotNet.Tests/packages.lock.json` is committed to the repo and consumed by every CI run; tampering changes resolved versions silently. | Pinned NuGet graph (text) |
| GitHub Actions runner (`windows-2022`) → external CDNs | Runner downloads `actions/checkout@v4`, `actions/cache@v4`, `microsoft/setup-msbuild@v2`, `actions/upload-artifact@v4` from GitHub's action registry, NuGet packages from `api.nuget.org`, and the DirectX SDK June 2010 installer (~600 MB) from `download.microsoft.com/download/a/e/7/ae743f1f-632b-4809-87a9-aa1bb3458e31/DXSDK_Jun10.exe`. | Action tarballs, NuGet packages, MSI installer |
| Workflow trigger source → runner | `push: branches: [master]` and `pull_request: branches: [master]` are the only trigger surfaces. PRs from forks run in fork context with restricted `GITHUB_TOKEN`. | Workflow runs |
| Public repo log surface | Workflow logs and `.trx` test-results artifact are publicly visible (`kennethlong/Utinni` is a public repo). | Test names, pass/fail status, assertion messages |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-01-01 | Tampering | NuGet transitive dep drift between local and CI restore | mitigate | `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` (`UtinniCoreDotNet.Tests.csproj:15`) + committed `packages.lock.json` (13 pins) + CI msbuild `/restore` step (`ci.yml:78`). | closed |
| T-01-02 | Tampering | NuGet package compromise (slopsquatting / malicious upload) | accept | All three direct packages are first-party (Microsoft + xunit org); no third-party authors. Verified by direct inspection of `UtinniCoreDotNet.Tests.csproj:18-20`. | closed |
| T-01-03 | InfoDisc | MIT header copyright attribution | accept | `HotkeyTests.cs:1-23` preserves `Copyright (c) 2020 Philip Klatt` verbatim per CONVENTIONS.md File Headers + sovereign-fork strategy. | closed |
| T-01-SC | Tampering | npm / pip / cargo installs (slopcheck blocking-human pattern) | accept | N/A — no npm/pip/cargo introduced in this plan. Only NuGet (covered by T-01-02). slopcheck 0.6.1 does not cover NuGet ecosystem. | closed |
| T-02-01 | Tampering | Third-party GitHub Action compromise (backdoored release) | mitigate | `ci.yml` uses only first-party actions (`actions/*` and `microsoft/*`) all major-version pinned to `@v2`/`@v4`. Zero third-party-author actions. SHA-pinning deferred to Phase 6+ hardening. | closed |
| T-02-02 | Tampering | NuGet package compromise on CI side (tracked via T-01-02) | mitigate | CI restores against `packages.lock.json` via `msbuild /restore` + `RestorePackagesWithLockFile=true` (`ci.yml:78`). Same caveat as T-01-01: no explicit `RestoreLockedMode=true`, so a lockfile edit in a PR is not rejected — relies on PR review for that vector. | closed |
| T-02-03 | EoP | `GITHUB_TOKEN` granted write scope to malicious workflow code | mitigate | `ci.yml:12-13` explicitly sets workflow-level `permissions: contents: read`. No `secrets:` key, no `gh`/`git push` invocations, no `${{ github.event.* }}` interpolation in `run:` blocks. All `run:` literals are hardcoded constants or `$env:RUNNER_TEMP`. | closed |
| T-02-04 | Tampering | Cache poisoning across forks | accept | `actions/cache@v4` default scoping is per-repo, per-branch. NuGet cache key is content-addressed (`hashFiles('**/packages.lock.json', '**/*.csproj')`). DXSDK cache uses fixed key `dxsdk-jun2010-v1` (immutable artifact). No cross-branch bypass step. | closed |
| T-02-05 | InfoDisc | `.trx` artifact leak to public log | accept | `HotkeyTests.cs` verified line-by-line: only literals are key-chord test strings (`"F1"`, `"Control + S"`, etc.). No env-var reads, no API keys, no PII, no file I/O. `.trx` content is test names + status + Skip strings only. | closed |
| T-02-06 | DoS | Long-running workflow exhausts GH Actions minutes | accept | `ci.yml:19` declares `timeout-minutes: 25`. Public repos have unlimited Actions minutes per `github.com/pricing`. | closed |
| T-02-SC | Tampering | npm / pip / cargo installs in workflow | accept | N/A confirmed — `ci.yml` contains no `npm`, `pip`, `cargo`, `npx`, or `pipx`. DXSDK install is a direct Microsoft CDN download (NOT Chocolatey, as `01-02-SUMMARY.md` originally misdescribed). | closed |
| T-02-07 | Tampering | **DXSDK_Jun10.exe download integrity (no SHA-256 verification)** — surfaced retroactively by code-review WR-01; not in plan-time register because the DXSDK install step was a Plan 01-02 scope expansion fix for the missing `d3dx9.h` build dependency | accept | See Accepted Risks Log entry **AR-01** below for trust assumption, blast radius, tripwire, and review trigger. | closed |

*Status: open · closed*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-01 | T-02-07 | See detailed rationale below. | Kenneth Long (user-authorized via `/gsd-secure-phase 1` AskUserQuestion option "Accept — document trust assumption") | 2026-05-16 |

### AR-01: DXSDK_Jun10.exe download without SHA-256 verification

**Threat ref:** T-02-07

**Rationale:**

`.github/workflows/ci.yml:45-69` downloads `DXSDK_Jun10.exe` (~600 MB) over HTTPS from `download.microsoft.com/download/a/e/7/ae743f1f-632b-4809-87a9-aa1bb3458e31/DXSDK_Jun10.exe` and executes it silently as Administrator (`Start-Process $exe -ArgumentList '/U' -Wait -NoNewWindow`). The post-install `Test-Path 'C:\Program Files (x86)\Microsoft DirectX SDK (June 2010)\Include\d3dx9.h'` check (line 67) verifies install success but **not** artifact integrity — a tampered installer that still drops `d3dx9.h` passes the check. The result is cached under key `dxsdk-jun2010-v1`, so any compromise persists across every subsequent CI run that hits the cache (potentially indefinitely until the cache is evicted or the key is bumped).

**Trust assumption:**
- `download.microsoft.com` TLS endpoint integrity (Microsoft-issued cert, public CA chain)
- Microsoft CDN content integrity (Microsoft's own asset hosting)
- GitHub Actions runner network path integrity (Azure backbone from `eastus` region per runner image to Microsoft CDN)

This trust assumption is consistent with how every other first-party action in the workflow is trusted (e.g. `actions/checkout@v4` is trusted because GitHub serves it, not because we SHA-pin the action repo).

**Blast radius if trust assumption fails:**
- Malicious installer runs unattended as **Administrator** on a `windows-2022` runner.
- Cache amplification: poisoned install persists under cache key `dxsdk-jun2010-v1` across every subsequent CI run hitting the cache. Effective TTL is 7 days idle per GH Actions cache policy, but could be re-poisoned on each fresh download.
- Runner subsequently builds `Utinni.sln` and could be made to inject malicious headers/code into the build output. However, build output is not published as a release artifact (only `.trx` test results on failure) — blast radius is mostly the runner itself, not downstream consumers.
- The repo is a hobbyist modding tool with no production deployment surface, no end-user binaries distributed via CI, and no secrets in workflow scope (no `secrets:` block per T-02-03 mitigation).

**Tripwire condition (re-evaluate immediately if any of these occur):**
- Any non-200 HTTP response from the DXSDK download URL (`Invoke-WebRequest` would throw)
- Any change in installer behavior (e.g., `d3dx9.h` no longer at expected default path → installer step would throw the explicit error on line 67-69)
- Any GitHub Security Advisory or Microsoft CVE referencing `DXSDK_Jun10.exe` or `download.microsoft.com/download/a/e/7/`
- Cache hit ratio anomalies in workflow telemetry (would indicate cache bypass attempts)

**Review trigger:**
- Re-evaluate this accepted risk when bumping the cache key from `dxsdk-jun2010-v1` to `v2` (e.g., if the install step is modified for any reason)
- Re-evaluate during the Phase 6 cleanup pass (assessment.md STAB-* items + dep bumps)
- Re-evaluate if Utinni begins distributing binaries built via CI (which would expand blast radius downstream)

**Mitigation upgrade path (deferred to Phase 6 or earlier if a Microsoft CDN compromise materializes):**
1. Independently verify canonical `DXSDK_Jun10.exe` SHA-256 against multiple authoritative sources (Microsoft download manifest, archive.org snapshots, ≥2 third-party hash databases).
2. Add `Get-FileHash` compare in `ci.yml` install step before `Start-Process`.
3. Bump cache key to `dxsdk-jun2010-v2` to invalidate any potentially-poisoned existing cache.

*Accepted risks do not resurface in future audit runs unless the review trigger fires.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-05-16 | 12 | 12 | 0 | `gsd-security-auditor` agent (orchestrated by Claude Opus 4.7 via `/gsd-secure-phase 1`) |

### 2026-05-16 — Initial Phase 01 audit

- Mode: State B (built register from artifacts; no prior SECURITY.md)
- `register_authored_at_plan_time: true` (both `01-01-PLAN.md` and `01-02-PLAN.md` contained parseable `<threat_model>` blocks)
- 11 plan-time threats verified against implementation:
  - 4 `mitigate`-disposition (T-01-01, T-02-01, T-02-02, T-02-03) — evidence-verified, all CLOSED
  - 7 `accept`-disposition (T-01-02, T-01-03, T-01-SC, T-02-04, T-02-05, T-02-06, T-02-SC) — rationale-verified against code, all CLOSED
- 1 retroactive threat (T-02-07, DXSDK installer integrity) surfaced by code-review WR-01:
  - STRIDE category: Tampering
  - Severity: High (not Critical — requires Microsoft CDN compromise or `download.microsoft.com` DNS hijack)
  - User-authorized disposition: `accept` with documented trust assumption (AR-01)
- 1 unregistered_flag: `01-02-SUMMARY.md:214-216` originally misdescribed the DXSDK install as "Chocolatey" — corrected in the same audit pass to accurately reflect direct Microsoft CDN download (see commit alongside this SECURITY.md).
- Outcome: `threats_open: 0`, all 12 threats have documented dispositions. Phase 01 PHASE-SECURE.

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log (AR-01 for T-02-07)
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-05-16 by Kenneth Long via interactive disposition choice in `/gsd-secure-phase 1`
