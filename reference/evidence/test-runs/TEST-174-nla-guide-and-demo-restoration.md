---
id: TEST-174
type: test-run
title: NLA demonstration questions and clean baseline restoration
status: executed
recordedAtUtc: 2026-10-27
testedCommit: 08381712811ed2676107ac780046a3fe9ed6231f
sourceBranch: feature/demo-scenario-coverage
evidenceLevel: locally-executed
---

# NLA guide and demonstration restoration

The requested scope was the verified audit's interpreter/guide mismatch,
safe expressible query operations, and repeatable restoration after a demo.
Two Luna executors were used at most. The original checkout was used without
creating worktrees. No manuscript evaluation records were changed.

## Source and execution identity

The starting revision was 84680a54de75cf50424bcd72030b91aab90ebd7b.
The baseline script's successful verification ran on that base with the new
script in the working tree; the same source was committed as
a7ef662c3e44e815b5767d439b6ec5ba4809c654. The focused parser regression ran on
a7ef662 with the parser/guide working patch. That same patch was committed as
08381712811ed2676107ac780046a3fe9ed6231f. The API used the DLL compiled by
that regression command. Later report-only changes did not alter that DLL.

## Parser and guide acceptance

The parser retains clarification for ambiguous Boolean outcome unions, and
supports category unions and explicitly separate missed/completed-late counts.
Explicit sorting requires a supported output field and direction. Schedule
and form status filters and the guide's Inspector-period measures are handled
without changing report subjects or bypassing plan validation or sanitization.
The guide explicitly states scope, PM-cycle dates, and acknowledgement through
the Inspector's session on behalf of the concerned Department Head.

The existing targeted regression passed 1/1:

```text
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --filter FullyQualifiedName~PmAnalyticsV3RegressionTests.Ambiguous_dates_filters_and_rank_measures_are_clarified_without_broadening_the_plan
```

The ten guide questions were submitted to the actual local interpretation API
using explicit DeterministicParser mode. All ten returned Valid and matched
the expected subject, mode, categories, measures, grouping, PM-cycle period and
status filters. All ten preview/execute requests succeeded with page size 100.
An additional five deterministic grammar checks passed: category OR union,
explicitly separate outcome counts, ambiguous OR clarification, department
ascending sorting, and ordinary "in order to" wording without invented sorting.

The same ten synthetic guide questions were submitted once each using explicit
DeepSeek mode. All ten returned Valid with interpreter DeepSeek and matched
the same expected plan scope. This was a real-provider run through the backend
client, not a fake-provider test. Exactly ten logical interpretation requests
were made for this run. Provider prompt text, full provider payloads, API keys
and authentication tokens were not persisted. The saved artifacts contain
question hashes, normalized plans and result summaries.

These ten cases are demonstration acceptance examples. They do not establish
held-out accuracy, independent GSD acceptance or a new manuscript evaluation
score. DeepSeek plan agreement is separate from numerical execution checks;
the ten report executions in this record used deterministic-mode plans.

Initial harness setup errors were preserved: an untrusted Origin caused login
403 before interpretation; initial execute requests used unsupported page size
200 and returned 400; a Markdown table separator counted as an eleventh question
caused a DeepSeek preflight to stop before any provider call. Origin omission,
page size 100 and correct row selection resolved these harness errors.

## Clean baseline restoration

The new reference/demo/demo-baseline.ps1 command captures a clean checksummed
SQL Server backup, pins its backup-set identifier, and preserves the fixture
manifest locally. Verify restores into a unique disposable database, changes
an asset location, detects the fingerprint change, restores again, and confirms
all eleven fixture-table fingerprints match the clean baseline. That actual
restore verification passed. SQL Server's own backup validation is used because
the interactive account cannot read the protected SQL backup directory directly.

Restore requires an explicit local UniPMDb target or a named disposable check
database. It backs up an existing target before replacement. Full restoration
includes accounts/session state; it is for the disposable local demonstration
database. The ordinary seed reset still refuses changed records. README
documents stopping/restarting the API and signing in again after restoration.

The original UniPMDb was not restored or mutated during this acceptance run.
An isolated copy was prepared for the subsequent role and browser walkthrough.
No schema migration or user-account provisioning was needed.

## Local artifacts

Ignored paths under artifacts/nla-demo-verification:

- deterministic-20261027T125823Z.json, interpretation results plus the initial execute errors.
- deterministic-counts-20261027T125918Z.json, corrected execution results.
- deterministic-grammar-20261027T130100Z.json, five grammar cases.
- deepseek-20261027T130250Z.json, real-provider normalized-plan agreement.

Ignored paths under artifacts/demo-rebuild include baseline-capture.log,
baseline-verify.log, clean-baseline.json and the disposable API launch records.
Backups remain in SQL Server's private backup directory. Raw artifacts and
private configuration are not committed.

Annual on-time compliance remains unavailable while selected cycles are active
or future. The guide uses closed periods for that calculation and progress for
ongoing work. Browser/printing and the role walkthrough are recorded separately.
