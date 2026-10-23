---
id: TEST-148
type: test-run
title: Saved reports backend verification after fixture correction
status: executed
recordedAtUtc: 2026-10-23T13:37:08Z
testedCommit: 622129fdc7c6c65c3d3fa7a35fccbf9a28bd287c
sourceBranch: feature/nla-saved-reports
evidenceLevel: locally-executed
---

# Saved reports backend verification

After the user authorized continuation from TEST-147, one targeted invocation
passed one fact, with zero failures and zero skips. Exit code was 0.
The host used EF InMemory and disabled DeepSeek. Native SQL Server was not
contacted and the new migration was not applied.

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --no-restore --filter FullyQualifiedName~Saved_reports_are_private_revalidated_and_execute_current_data_without_AI --logger 'console;verbosity=minimal' --nologo
```

## Cause of the earlier rejection

The TEST-147 fixture supplied an empty output-column list. The existing V3
validator requires at least one selected output column. The corrected fact
explicitly submits the same empty-column case, checks HTTP 400 and its
`Columns` error, then successfully saves a plan containing the measure column.
No production validator rule was weakened.

The fixture also previously requested a January fire-extinguisher cycle.
That category has scheduled cycles in February, May, August and November.
The corrected relative-period checks use valid CPMP months and matching
measure columns. TEST-147 did not reach these later assertions.

## Executed checks

- Save and list an inventory definition; reject blank names and inventory
  relative periods.
- Return no other user's saved reports and reject cross-owner update, delete
  and preview with 404.
- Reject a missing update revision, then rename with the current revision.
- Open a preview and execute, insert a synthetic asset, then open and execute
  again. The returned asset count increases by one.
- Resolve this month across a UTC/Philippine calendar boundary and again after
  time advances from February to May. Check previous month, this year,
  previous year and fixed-year settings through updates and new previews.
- Reject a saved plan whose catalog version is changed to an unsupported value.
- Delete a definition and reject subsequent preview access.
- Reject Supervisor access to all five saved-report operations.

The fact covers new API routes through an isolated HTTP test host. It does
not establish SQL constraints, browser behavior, race handling, anonymous-role
coverage, GSD acceptance or manuscript evaluation quality.

The host exported its OpenAPI contract after all assertions passed.
Only saved-report routes and new schemas were added to the existing web
snapshot before Orval generation, preserving unrelated contract definitions.
No provider call was made. No additional backend test invocation was run.

Raw log: `artifacts/saved-reports-resume-2026-10-23/targeted-test.log`.
SHA-256: `45026999e99a92177498608e5abb98303c001d0f2d324b8e1eb738501619aadc`.

The frontend was still being completed during this run and was not exercised.
See TEST-149 for subsequent TypeScript checking.
