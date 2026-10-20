# UniPM Web

The UniPM web application uses React, TypeScript, Vite, TanStack Router and
Query, Axios, Zustand, TanStack Form, Tailwind CSS, shadcn-compatible Radix
primitives, Vitest/MSW, and Playwright. Use Node 22 (`.nvmrc`) and `npm ci`.

```powershell
npm run dev
npm run typecheck
npm run test:run
npm run build
```

`VITE_API_BASE_URL` defaults to `http://localhost:5000`; the backend must allow
the local web origin `http://localhost:5173`. The committed OpenAPI snapshot is
the input for `npm run api:generate`; `api:pull` needs a running backend while
`api:contract:check`, `api:generate`, and `api:check` work offline. Generated
code under `src/api/generated` is not handwritten.

## Browser Authentication

The browser client implements email/password login, refresh-cookie session
restoration, protected routes, current-user loading, and logout through the
generated `/auth/login`, `/auth/refresh`, `/auth/me`, and `/auth/logout`
contracts. Axios is the sole transport and applies at most one replay after a 401. Concurrent 401 responses share one refresh request.

The access token exists only in Zustand memory. It is never stored in
`localStorage`, `sessionStorage`, cookies, URLs, or logs. The backend owns the
HttpOnly refresh cookie. TanStack Query owns the current authenticated user;
the auth store contains only the access token and session status. Logout clears
local auth and query state even when server-side revocation cannot be confirmed,
and the UI warns the user in that case.

For local authentication, run the API at `http://localhost:5000`, the web app at
`http://localhost:5173`, and keep the backend CORS origin configured for that
exact web origin. Create the five fictional Development users with:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:UNIPM_DEV_USER_PASSWORD = "<local-secret>"
dotnet run --project ..\server -- --seed-development-users
```

Do not commit or print the local password. The Development users reuse the
fixture's deterministic actor IDs.

## Visual Reference

The login and shell styling were source-inspected against the approved UniPM
Figma nodes `4010:4`, `2310:28`, `2338:152`, and `2010:3`. The implementation
uses the approved maroon, neutral, and status tokens plus a local exported logo.
This is implementation alignment, not a pixel-perfect or final institutional
design certification.

## Asset Registry

Authenticated users can browse `/app/assets`, view a route-backed asset detail,
and copy the returned asset code or QR identifier. The registry sends only the
supported category, status, building, and department filters to the API. Text
search and ten-row pagination run client-side over that returned filtered list
and are reflected in the URL. Summary cards derive only from the unfiltered
asset response and reference-data categories.

Only the provisional `GSD` role sees and may use `/app/assets/new`; the backend
policy remains authoritative. The form accepts only the current create contract
fields. Category labels come from reference data, responses are runtime-checked
with Zod, and QR values are shown/copied without generating an image or label.
No real institutional asset data, final RBAC, asset editing, inspection, audit,
condition, work-order, or device-specification workflow is implemented.

## Inspection Review

Authenticated users can browse `/app/inspections`, apply the existing asset,
schedule, recorded-result, and inspected-date filters, and open immutable
source-record details. Asset details include a compact recent inspection-history
panel. This module does not submit inspections or expose acknowledgement
actions, RMRF, or approval workflows. Form acknowledgement is handled in the
preventive-maintenance form review; reporting is a separate NLA V3 workflow.

## Preventive-Maintenance Form Review

Authenticated GSD and Inspector users can browse `/app/preventive-maintenance-forms`
and inspect form details with their row-level condition, remarks, recommended
corrective action, asset, schedule, and skilled-worker context. Water drinking
station rows also show the recorded completion date and visible filter/UV work
items. Submitted forms expose the existing whole-form acknowledgement capture
to an authorized reviewer. The interface distinguishes `Draft`, `Submitted`,
and `Acknowledged` without calling an asset condition `Completed`.

For GSD only, an Acknowledged form may display the existing corrective-action
handoff read model. It preserves nullable `AssetDeviceNumber` as unresolved and
does not substitute `AssetCode`. The interface never displays signature data,
checksums, or acknowledgement-response payloads. It provides no draft creation,
inspection-row editing, submission, export, RMRF, WMS, or handoff-tracking
action.

## NLA V3 Reporting

The integrated, schema-constrained PM Analytics V3 workflow lets users enter a
question, review the interpretation, edit the typed plan, inspect a preview,
confirm it, and review results with supported source-record drill-down. It is
available on `main` and `deploy/azure-staging`. Calculations and SQL execution
remain backend-controlled; the browser does not generate SQL.

The `pmExecution` subject reports eligible asset-cycle outcomes. The
`assetInventory` subject reports one current registered asset per row,
independent of PM schedules and inspection history, with category, building,
department, location, and registration-status filters. Registration status and
inspection condition are separate fields. The Phase 5 evaluation corpus remains
unreviewed and no V3 scoring result is claimed; native SQL Server 2019 execution
remains unverified.

The initial integration checks are recorded in
[`TEST-122`](../reference/evidence/test-runs/TEST-122-offline-nla-integration-candidate-verification.md).
Current Web CI passed on the checked `main` head `5a92f9b`
([run](https://github.com/lucifron28/UniPM/actions/runs/39408165969)) and
staging head `1266887`
([run](https://github.com/lucifron28/UniPM/actions/runs/39408183632)).

## Preventive Maintenance Schedules

Authenticated users can browse `/app/schedules`, apply the supported asset,
status, date, quarter, and year filters through URL state, and open direct
schedule detail routes. Ten-row pagination is client-side over the filtered API
response. Summary cards count only statuses actually returned by the unfiltered
API; the client does not infer overdue status.

Only provisional `GSD` and `Supervisor` roles see and may use
`/app/schedules/new`; the backend policy remains authoritative. Creation sends
only asset, schedule date, period type, optional quarter, and year. Quarter is
required and visible only for quarterly schedules. Recurrence generation,
editing, deletion, status commands, acknowledgement, RMRF, and corrective
handoff remain outside this module. GSD users can use the separate **Planning &
assignment** view to assign an eligible batch to a Supervisor; Supervisors can
assign an eligible batch to an Inspector. These actions use existing backend
authorization and are operational workflows, not NLA V3 reporting subjects.

Registration, password recovery, MFA, SSO, and final institutional RBAC remain
deferred. The authenticated shell intentionally avoids invented maintenance
records, metrics, or workflows.
