---
id: TEST-133
type: test-run
title: Demo Admin management focused verification
status: executed
recordedAtUtc: 2026-10-21T12:10:00Z
testedCommit: b245cce7185b83974bb8d1c6a4905b7940e79df5
sourceBranch: feature/admin-demo-management
evidenceLevel: locally-executed
---

# Demo Admin management focused verification

The final focused backend run used the source committed as the tested SHA.
`dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~UniPM.Api.Tests.AdminManagement --logger "trx;LogFileName=admin-focused-final.trx" --results-directory artifacts/admin-management-verification`
passed 9 tests with no failures or skips. The run compiled the changed backend
and exercised accounts, password reset, GSD transfer, dataset load/reset,
operational actions, and typed Admin OpenAPI contracts.

The six focused Admin UI tests passed during implementation against the base
`e09a7d1d98422df4fa466a82b9381a967c54f5d0` plus the working frontend patch.
Command: `npm exec vitest run src/features/admin/admin-management.test.tsx`.
Later picker and dialog corrections were checked by TypeScript and targeted
ESLint; this record does not claim the earlier UI run tested those corrections.

Final API generation and contract sanity checks, TypeScript, and targeted
Admin ESLint passed. Raw backend results are in ignored
`artifacts/admin-management-verification/admin-focused-final.trx`.

Tests used disposable EF InMemory databases and synthetic HTTP responses.
They do not establish SQL Server foreign-key enforcement, transaction rollback,
or native app-lock concurrency. CI and physical-device acceptance are separate.
No migration, deployment, live database reset, Azure write, or paid AI request
was executed. The current seed builder and historical migrations are unchanged.
