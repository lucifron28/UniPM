---
id: TEST-134
type: test-run
title: Guided NLA interpretation and reviewed exact-question reuse
status: executed
recordedAtUtc: 2026-10-22T13:23:55Z
testedCommit: e18db077a1ff5f002ff8179a58dffd11152e47ba
sourceBranch: feature/nla-guided-interpretation
evidenceLevel: real-provider-executed
---

# Guided NLA interpretation verification

DeepSeek receives sanitized questions before lexical subject/category gates.
The deterministic parser remains an explicit alternative. Existing plan
validation, catalog resolution, preview signing, authorization, and report
execution remain in place. Clarification uses the safe partial plan and local
choices, followed by backend preview validation and explicit confirmation.

Two developer-reviewed exact questions are stored in code for the 2026 PM
cycles: late fire-extinguisher inspections, and missed fire-extinguisher
inspections grouped by department. Only case, surrounding whitespace, and
repeated whitespace are normalized. Schema/catalog version changes invalidate
the mappings. This is a fixed reviewed collection, not automatic learning,
GSD-approved terminology, or cached report results.

## Executed checks

- `dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --no-restore --filter FullyQualifiedName~PmAnalyticsV3EndpointTests --logger "console;verbosity=minimal"`
  passed 8, failed 0, skipped 0 in one run. These endpoint tests use synthetic
  data and disposable EF InMemory stores, including authorization and the
  interpretation/preview/execution path. Log: ignored
  `artifacts/nla-guided-smoke/backend-endpoints.log`.
- Under the bundled Node 22 runtime, the web executor ran
  `npm exec -- prettier --write src/features/reports/pm-analytics-v3.tsx` and
  `npm run typecheck` from `web/`. The initial typecheck failed on button
  variants, an unused parameter, and nullable filter values. After one focused
  correction, the same typecheck passed. The checked web source is committed
  as `7ebe652`, unchanged in the tested commit above. No web test suite ran.
- `dotnet restore artifacts/nla-guided-smoke/smoke.csproj --ignore-failed-sources`
  passed. `dotnet run --project artifacts/nla-guided-smoke/smoke.csproj --no-restore -- --live`
  passed against the actual interpreter using its existing DeepSeek client,
  configured model `deepseek-flash`, and an injected counting HTTP handler.
  Exactly two provider HTTP requests were sent. Credentials and raw provider
  payloads were not logged or committed. Log: ignored
  `artifacts/nla-guided-smoke/live-smoke.log`.

The first real question, asking how many extinguisher checks were finished
past their deadline in 2026, produced `completedLateCount` for fire
extinguishers and year 2026. The second, asking which departments were falling
behind on extinguisher checks, returned a safe partial plan preserving the
category and department grouping, with `Period` and `Measures` clarification.
Selecting year 2026 and completed-late count required no further provider call.
Both resulting plans passed catalog/plan validation, signed preview creation
and confirmation, and query-service execution against an empty disposable
InMemory store. The expected eligible population was zero. Both reviewed
exact questions also passed that path without a provider request.

## Limits

This is a two-question live-provider smoke check, not an interpretation accuracy
benchmark or GSD acceptance result. No held-out corpus scoring, browser
interaction test, native SQL Server check, full suite, migration, seed change,
Azure operation, or deployment was performed. DeepSeek remains enabled only
in configured local Development. Clarification without a safe draft, or for
fields outside the compact guided controls, requires editing the question.
The pre-existing mobile visual patch and untracked `tmp/` were preserved and
excluded from these commits and verification.
