# Phase 16: Live-injected MCP bridge + Blender ecosystem boundary - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-13
**Phase:** 16-Live-injected MCP bridge + Blender ecosystem boundary
**Areas discussed:** Live-preview ambition, Live-tier gating/exposure, Blender contract doc, Open verbs + fixtures

---

## Area selection

User selected all four offered gray areas (multi-select): Live-preview ambition, Live-tier gating/exposure, Blender contract doc, Open verbs + fixtures.

---

## Live-preview ambition (MCP-03)

### Preview success bar

| Option | Description | Selected |
|--------|-------------|----------|
| Round-trip proven, render best-effort | Agent sends edit over pipe, client acks/attempts apply/reload; visible re-render best-effort, rides on deferred RESID-03 | ✓ |
| Full visible re-render required | Forces re-opening the loose-searchPath gate inside this phase (deferred RESID-03 path) | |
| Scene/spawn preview only | Bridge drives scene-change/spawn-and-look but no edited-asset push | |

**User's choice:** Round-trip proven, render best-effort.
**Notes:** Keeps the highest-risk new mechanism the focus; visible re-render not a success gate.

### Bridge command surface

| Option | Description | Selected |
|--------|-------------|----------|
| Minimal: ping + reload-asset | Handshake/health ping + apply-edited-asset with ack envelope | ✓ |
| + scene/spawn control | Adds drive-scene-change / spawn-and-look (TJT-callback path) | |
| + readback (status/screenshot) | Adds client-status query and/or screenshot-back | |

**User's choice:** Minimal: ping + reload-asset.
**Notes:** Un-retrofittable-safe; mirrors Phase 14's minimal write surface.

---

## Live-tier gating/exposure (MCP-03)

### Bridge architecture

| Option | Description | Selected |
|--------|-------------|----------|
| New tool tier on Utinni.Mcp | live_* tools on the existing net10 server; pipe client is another dispatch target | ✓ |
| Distinct bridge process | Separate dedicated bridge server, isolated lifecycle | |
| Let research decide | Capture both, defer to researcher | |

**User's choice:** New tool tier on Utinni.Mcp.
**Notes:** Reuses Phase 14 host; out-of-proc boundary holds (pipe client is client-side).

### Gating / opt-in

| Option | Description | Selected |
|--------|-------------|----------|
| Server-launch flag, tools hidden when off | Explicit --enable-live opt-in; live_* unregistered by default | ✓ |
| Tools always visible, fail at call-time | Advertised always; error when tier disabled | |
| Auto-detect injected client | Enabled when pipe present; no explicit flag | |

**User's choice:** Server-launch flag, tools hidden when off.
**Notes:** Fail-closed by absence; consistent with resolvedRoot startup-pinning.

---

## Blender contract doc (ECO-01)

### Doc home / authority

| Option | Description | Selected |
|--------|-------------|----------|
| Authoritative in Utinni, mirrored note in Blender | Single source of truth in Utinni docs/; pointer note in blender repo | ✓ |
| Authoritative in Blender repo | Spec next to rsp_builder.py reference impl | |
| Duplicated in both repos | Full copy both sides; drift risk | |

**User's choice:** Authoritative in Utinni, mirrored note in Blender.
**Notes:** Matches "Utinni owns format" (project_swg_toolchain_crosswalk).

### Doc scope (multi-select)

| Option | Description | Selected |
|--------|-------------|----------|
| .rsp search-path contract | data_*.rsp format + bucket rules + load priority + client_search_paths.cfg | ✓ |
| Format-version matrix (.iff/.tre) | Versions Utinni reads vs Blender writes | ✓ |
| Directory/bundle layout | export_bundle.py output shape consumed by open/preview verbs | ✓ |
| Ownership/anti-coupling rules | No-runtime-coupling statement; honors DEC-A3 | ✓ |

**User's choice:** All four.
**Notes:** Full contract surface in scope for v1.

---

## Open verbs + fixtures (ECO-01)

### Open/preview verb scope

| Option | Description | Selected |
|--------|-------------|----------|
| Bundle-level: open .tre + validate .rsp/.iff | Reuse ParseTre/InspectIff/DecodeIff; mesh opaque | |
| + mesh metadata peek | Thin .msh/.mgn header read | |
| Let research decide reader scope | Lock "reuse readers, no 3D" principle; researcher sets exact verbs/formats | ✓ |

**User's choice:** Let research decide reader scope.
**Notes:** Principle locked (reuse existing readers, no 3D / DEC-A3); exact reachability research-directed.

### Cross-validation fixtures (SC4)

| Option | Description | Selected |
|--------|-------------|----------|
| Blender repo is fixture source; Utinni reads pinned copy | tests/golden/ is origin; Utinni vendors pinned copy and asserts open + .rsp conformance | ✓ |
| Shared fixtures in Utinni repo | Centralize canonical set in authoritative repo | |
| Let research/plan decide | Lock assertion intent, defer location to CON-O-09 | |

**User's choice:** Blender repo is fixture source; Utinni reads a pinned copy.
**Notes:** Cross-validation = Blender writes, Utinni reads same bytes; storage mechanics defer to CON-O-09.

---

## Mid-discussion clarification

User asked whether the Blender reference was `D:\swg-blender-plugin`. Verified: that path does NOT exist; only `D:\Code\swg-blender-plugin` is on disk. All ECO-01 refs anchored there.

## Claude's Discretion

- Exact reader/verb scope for ECO-01 open/preview (D-07, delegated to research).
- Named-pipe wire format / framing, pipe-server thread placement in the injected host (heap-free hot-path constraint), live_* tool input schemas.
- Named-pipe trust/auth model (local-only, ACL) — research-directed security gray area.
- CI/test approach for the un-injectable bridge (loopback pipe protocol test; live confirmation is Tier-4 manual).

## Deferred Ideas

- Visible live render-on-reload (RESID-03) — gated on the disabled loose searchPath; remains deferred.
- Scene/spawn control + readback/screenshot-back over the live bridge — out of v1 verb surface; later increment.
- Mesh-metadata peek (.msh/.mgn header read) — deferred to keep DEC-A3's no-3D line clean.
