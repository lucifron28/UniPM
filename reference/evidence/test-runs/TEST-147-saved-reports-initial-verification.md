---
id: TEST-147
type: test-run
title: Saved reports initial verification
status: failed
evidenceLevel: locally-executed
---

# Saved reports initial verification

Date: October 23, 2026.

Tested rerun commit: `9ce628b489a0c8d2467cda48fa1fbcd80b6a12a3`.
The frontend files were uncommitted and were not exercised by this backend test.

## Command and result

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --no-restore --filter FullyQualifiedName~Saved_reports_are_private_revalidated_and_execute_current_data_without_AI --logger 'console;verbosity=minimal' --nologo
```

The first invocation at commit `29db348` failed compilation because response
metadata was attached to a route group using a route-handler extension.
One focused correction moved that metadata onto individual handlers.

The permitted rerun compiled, then failed its first creation assertion.
`POST /api/v1/analytics/pm/v3/saved-reports` returned HTTP 400 when the
test expected HTTP 201. The test did not capture the response body, so the
cause is unresolved. One fact failed; none passed or were skipped.
Later assertions for ownership, edit/delete, relative periods, current data,
and authorization were not reached. They are not verified results.

Implementation stopped under the repository's correction/rerun limit.
No further correction or test invocation was attempted.

## Scope and remaining work

- EF generated the new migration successfully before verification. It was
  not applied to any database. Native SQL Server execution remains unverified.
- The test host uses EF InMemory and disables DeepSeek. No provider call or
  cost was incurred.
- The API contract export was not reached. The frontend has imports for
  clients that have not yet been generated and is incomplete.
- No frontend check, browser run, complete suite, deployment or push occurred.
- The next work is to capture the rejected request's validation details,
  correct the demonstrated cause, complete client generation, and verify the
  feature within a newly authorized verification allowance.

Raw local output remains in ignored
`artifacts/saved-reports-2026-10-23/targeted-test.log` and
`artifacts/saved-reports-2026-10-23/targeted-test-rerun.log`.
This record establishes a failed implementation check, not working-feature
evidence or manuscript evaluation evidence.
