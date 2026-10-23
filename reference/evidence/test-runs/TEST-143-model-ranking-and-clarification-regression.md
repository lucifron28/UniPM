---
id: TEST-143
type: test-run
title: Focused model ranking and catalog clarification regression
status: executed
recordedAtUtc: 2026-10-23T08:10:47Z
testedCommit: e379baebb6c2a513a6dc5c5a4d8f96956de1e1b6
sourceBranch: feature/nla-deepseek-validation-fix
evidenceLevel: deterministic-provider-executed
---

# Focused model ranking and catalog clarification regression

The researcher requested implementation of the recorded correction plan and
rerunning the evaluation. The tested working patch was subsequently committed
as the exact source revision above. The original checkout's uncommitted Web
and mobile edits were excluded.

## Change and targeted check

The model path reuses the existing ranking intent and ranking-resolution
helpers. It requests clarification for ambiguous or conflicting ranking,
clears the guessed top selection, preserves explicitly requested measures,
and requests Mode/GroupBy when necessary. Catalog errors now return canonical
clarification identifiers while retaining existing error keys. The prompt
also states the planned default mode and time-basis policies.

One xUnit fact exercised the actual `PmAnalyticsV3QuestionInterpreter`, existing
DeepSeek client parsing and constraints using a controlled HTTP handler and
disposable EF InMemory catalog. It never contacted DeepSeek or a live database.
Its 12 controlled responses covered:

- Two ambiguous multi-measure questions with different wording/direction.
- A candidate that dropped one requested measure.
- An explicit ranking criterion, an unambiguous single-measure ranking, and a
  candidate that contradicted an explicit ranking measure.
- A Records-mode candidate for a grouped ranking question.
- Five unknown catalog fields requiring Department, Building, Location,
  AssetCode and AssetId clarification identifiers.

The command was:

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --filter FullyQualifiedName~PmAnalyticsV3ModelGuardTests --logger "console;verbosity=minimal" --nologo
```

The initial command exited 0: passed 1, failed 0, skipped 0. Compilation reported
CS8601 for a nullable Measures assignment in the new constraint. One focused
correction used an empty list for that null case. The same targeted check was
rerun once with `--no-restore`, exiting 0: passed 1, failed 0, skipped 0, with
no warning in the retained output. No third xUnit execution occurred.

## Source and artifacts

The rerun's test assembly SHA-256 was
`ba37ad2aa231c1f519359d3ed033f1a745cdbbc1ee999ac1a61211d61c97265f`.
The corresponding backend assembly SHA-256 was
`16f22f5bb96b6a6b82d42348577e3fed4bf313992d2b07eb6bcff74067a9cba3`.
Raw logs and safe assembly metadata are ignored under
`artifacts/deepseek-v3-correction-2026-10-23/` as `focused-test.log`,
`focused-test-correction.log` and `focused-test-identity.json`.

After this regression check, a separately committed source review refined the
ranking expression to exclude 'at most' and clarified that generic completed
counts must preserve explicitly named late/on-time outcomes. These refinements
are in `1c81400d0e92a9da0bcf92bdc2456d52843ef8ad`, not the regression-tested
commit above. They received a clean compilation and the live development
comparison recorded in TEST-144, without a third targeted test run. In
particular, no executed 'at most' regression assertion is claimed.

The existing unknown-building acceptance expectation was changed to Building,
but that separate acceptance class was not executed. The new focused fact
checks all five canonical identifiers through the actual model path. No full
suite, web/browser check, SQL Server, deployment, or real-provider request ran
for this record. Controlled model responses prove these backend checks, not
live model quality or coverage of every phrasing.
