---
id: NLA-V3-FINAL-HARDENING-DEV
status: reviewed
---

# NLA V3 final-hardening developer regression matrix

## Purpose and boundary

This is a synthetic, developer-authored regression matrix for changes to the
existing V3 intent parser, DeepSeek proposal guard, and clarification response.
It is not an independent oracle, a GSD-approved query set, a held-out set, or an
accuracy evaluation. Cases are encoded with complete expected plan assertions
in [`PmAnalyticsV3FinalHardeningTests.cs`](../../../tests/UniPM.Api.Tests/Reports/PmAnalyticsV3FinalHardeningTests.cs)
instead of a separate scored corpus file.

The matrix contains 93 deterministic parser/model-guard cases plus two API
acceptance cases for Manila-relative-year and Filipino named-place
clarification behavior. The focused command below passes 95/95. Expectations
cover categories, measures, period, filters, thresholds, grouping, ranking,
record-vs-aggregate mode, report subject, and clarification/unsupported
boundaries rather than assigning a single accuracy score.

## Families

- English and Filipino/Taglish aliases for the existing four categories and
  catalog measures.
- Unknown and ambiguous aliases, false friends, named department names, and
  date-context handling for `May`.
- Filipino month/year parsing and fixed Asia/Manila time boundaries.
- Record list versus aggregate count, inventory versus PM execution, and
  mixed-subject rejection.
- Completed-late versus missed work, incomplete active-cycle clarification,
  and separation from Inspector assignment deadlines.
- Integer and percentage thresholds, inclusive/exclusive boundaries,
  multiple measure association, and metamorphic operator comparisons.
- Explicit ranking basis, missing rank basis, requested rank limit and group.
- Quoted/unquoted building and department scope, ambiguous place scope, and
  retention of known partial-plan fields.
- Category union, unsupported outcome OR/exclusion, prediction, destructive
  requests, SQL requests, prompt injection, unknown categories, and unsupported
  asset kinds.
- Mocked DeepSeek responses with correct and incorrect category, subject,
  grouping, rank, thresholds, and named-field proposals.
- API and browser clarification for the Filipino question
  `Ilan ang nakaiskedyul na pamatay-sunog sa North Block noong 2024?`.

## Execution

The focused command is:

```powershell
dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj --filter 'FullyQualifiedName~PmAnalyticsV3FinalHardeningTests|FullyQualifiedName~Filipino_relative_year_uses_fixed_Manila_clock|FullyQualifiedName~Filipino_named_place_returns_a_safe_partial_plan_for_guided_field_selection' --logger 'console;verbosity=minimal'
```

The test clock is fixed. Model corrections use a local fake HTTP handler and
predeclared canned responses; the test suite does not make an external model
request or execute a report against real institutional data.

## Freeze and independence

The matrix was committed with implementation source on branch
`feature/nla-final-hardening`. Do not change its questions or expected plans in
response to the upcoming GSD Head-authored 20-question set. GSD reviewers must
author or approve expected outcomes independently before any separate scoring.
Any bug found later should be handled as a new source/test revision with a
clear version boundary; it must not be reported as a held-out accuracy result.

The GSD Head's upcoming queries and answer sheet were not opened, searched, or
used. The matrix adds no precision, recall, F1, semantic-accuracy, or
production-readiness claim. Native SQL calculation correctness is outside
this parser-focused matrix and remains separately unverified.
