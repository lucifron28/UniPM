# UniPM System Capabilities Reference

This reference describes the implemented backend and client boundaries at the
current project baseline. It is an inventory of available behavior, not a
promise that every institutional workflow is finalized.

## Platform and Clients

| Area | Current implementation |
| --- | --- |
| API | ASP.NET Core Web API |
| Database | Native Windows SQL Server 2019, compatibility level `150`, Full-Text Search |
| Web | React, TypeScript, Vite, generated OpenAPI client |
| Mobile | Flutter Android-first client with memory-only authentication and Draft form workflow, maintained by a separate partner-owned workstream |
| Proposed deployment target | ASP.NET Core hosted through IIS with native Windows SQL Server |
| Docker | Optional development tooling for the retained SQL Server 2025 experiment |
| Maintenance-history RAG | Runtime, tooling, and derived storage retired; historical evidence retained |
| ReferenceDocument foundation | Fictional metadata/sections, Full-Text Search, shared provider-neutral embeddings retained |

The capstone was evaluated as a local prototype. IIS deployment, public HTTPS
exposure, production workload testing, final secret management, and final
institutional RBAC are not claimed.

## Backend API Surface

All routes use the `/api/v1` prefix.

### Authentication

- `POST /auth/login`
- `POST /auth/refresh`
- `POST /auth/logout`
- `GET /auth/me`

Browser clients use the refresh-cookie contract. The mobile client uses the
login and current-user contracts with a memory-only access token and requires a
fresh login after restart or terminal session failure. The mobile client does
not persist refresh-cookie material or implement refresh/replay behavior.

### Assets, Schedules, and Inspections

- Assets: `POST /assets`, `GET /assets`, `GET /assets/{id}`, and
  `GET /assets/by-qr/{qrCodeValue}`.
- Schedules: `POST /schedules`, `GET /schedules`, and `GET /schedules/{id}`.
- Inspections: `POST /inspections`, `GET /inspections`,
  `GET /inspections/{id}`, and `GET /inspections/history/{assetId}`.

Inspection reads are source-record reads. The one-inspection-per-schedule
constraint remains in force.

### Preventive-Maintenance Forms

A form represents one existing one-page form and contains multiple inspection
rows. The implemented routes are:

- `POST /preventive-maintenance-forms`
- `GET /preventive-maintenance-forms`
- `GET /preventive-maintenance-forms/{id}`
- `POST /preventive-maintenance-forms/{id}/inspections`
- `PUT /preventive-maintenance-forms/{id}/inspections/{inspectionId}`
- `DELETE /preventive-maintenance-forms/{id}/inspections/{inspectionId}`
- `POST /preventive-maintenance-forms/{id}/submit`
- `POST /preventive-maintenance-forms/{id}/acknowledge`
- `GET /preventive-maintenance-forms/{id}/corrective-handoff`

The lifecycle is `Draft -> Submitted -> Acknowledged`:

| Form state | Row mutation | Official history | Schedule completion |
| --- | --- | --- | --- |
| Draft | Allowed for authorized draft owners/GSD | Excluded | Set independently by field-work completion |
| Submitted | Immutable | Excluded | Set independently by field-work completion |
| Acknowledged | Immutable | Eligible | Already determined by `InspectionRecord.CompletedAt` |

Submission assigns a provisional file number. Acknowledgement records the
department-head signatory as form data captured through the skilled worker's
authenticated session. It records receipt/noting, does not alter execution or
compliance timestamps, and does not complete linked schedules. It makes
completed rows eligible for acknowledged-only official history. Maintenance-history
RAG and its search projection are retired. Historical evidence remains. The GSD-only corrective
handoff is a read model for manual follow-up; UniPM does not create or track RMRFs or
integrate directly with the Work Management System. Its `AssetDeviceNumber`
field remains nullable until an institutional device-number source is confirmed;
the API does not substitute `AssetCode` for that unresolved value.

### Reference data and retired maintenance review

- `/maintenance-review` no longer exists, even when a legacy enable flag is set.
- Reference-data routes continue to provide controlled client values.
- Health/readiness and opt-in HTTP/runtime metrics remain.

Maintenance-history RAG has been retired. Its review endpoint, summary
provider, maintenance retrieval/fusion, projection, rebuild commands,
benchmark, and experiment runners are removed.

Historical migrations, API descriptions, ADRs, experiments, and verification
records remain as evidence of prior work. The separate fictional
ReferenceDocument foundation, Full-Text Search, section embeddings, and shared
provider-neutral embedding components remain.

Schema-constrained natural-language analytics remains a planned post-validation
direction, pending professor/adviser confirmation. It is not implemented or
approved for this branch; any implementation requires a separate approved task
and branch after GSD validation.

## Authorization Boundary

The current provisional roles are `Admin`, `GSD`, `Inspector`, `Supervisor`,
and `DepartmentHead`. `Admin` is a technical system-administration role and is
not an operational super-role.

The mobile shell currently admits only `GSD` and `Inspector` users. This is a
client navigation boundary; backend authorization remains authoritative. The
web application exposes the implemented read and role-gated modules described
in [`web/README.md`](../../../web/README.md).

## Mobile Capability

The mobile client is maintained in a separate partner-owned workstream. The
backend and web analysis workstream does not own the mobile implementation, but
the current client contract includes the accepted core PM workflow.

The Flutter client currently provides:

- login, current-user loading, logout, and bounded terminal-session handling;
- Inspector/GSD role gating;
- a home shell;
- QR-based asset lookup and acknowledged-only asset history;
- Draft preventive-maintenance form creation, listing, detail loading, and
  inspection-row add/update/delete operations;
- whole-form submission, submitted-form review, and whole-form acknowledgement
  with signatory capture.

Access tokens remain in memory only. The app does not persist cookies or tokens,
does not restore a session after restart, and does not implement offline sync.
Offline synchronization is deferred and its persistence and synchronization
architecture remain undecided pending a separate approved decision.

## Explicitly Excluded or Planned

- The previous RAG-assisted inspection-history analysis direction is historical
  and inactive, and is not published by the current runtime.
- Schema-constrained natural-language analytics is a planned post-validation
  direction pending professor/adviser confirmation. It is not implemented or
  approved; any implementation requires a separate approved task and branch
  after GSD validation.
- Approved institutional CPMP, checklist, form, and SOP ingestion and
  retrieval remain pending authorization and ingestion decisions.
- OEM retrieval is excluded from the evaluated MVP.
- Final RBAC, audit-log persistence, official building/department/location
  lists, and schedule-adjustment authority remain deferred.
- Offline synchronization, persistent session restoration, and later mobile
  field workflows remain outside the current mobile scope.
- No client calls SQL Server, an embedding provider, or an LLM directly.
