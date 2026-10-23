---
id: TEST-145
type: test-run
title: Ranking clarification recovery and catalog equivalence focused regression
status: executed
recordedAtUtc: 2026-10-23T12:55:10Z
testedCommit: 8a5966eff5e48ec826ed1501c67e155f31899ea2
sourceBranch: feature/nla-clarification-recovery
evidenceLevel: deterministic-provider-executed
---

# Ranking recovery and catalog equivalence focused regression

The researcher authorized executing the recorded clarification recovery plan.
The new feature branch starts from updated main at `bdc07ca`, with the prior
recorded validation branch merged as `db0c739`. Conflicts arose from main's
squashed integration of the earlier guided interpretation work. Resolution
preserved the existing recorded fixes and evidence; the resolved tree matched
the prior validation branch before the new changes. Uncommitted Web/mobile
edits in the original checkout were excluded.

## Implementation

Commit `bb1c8f5` adds a narrow helper before the DeepSeek envelope gate. It
recovers only a parsed Valid candidate with exactly Top clarification and a
valid Aggregate PM draft with no selected ranking. A deterministic parse must
independently establish Top-only ambiguity with matching period, category,
measures, grouping, columns and filters. The helper returns NeedsClarification
with code RecoveredRankingClarification. It never promotes output to Valid.
The existing envelope, question and draft validators remain active.

Commit `8a5966e` adds an auxiliary, versioned catalog-equivalence comparator
beside the unchanged strict comparator. It uses the fixed one-asset catalog
constants and normalizes only known building/department values. Unknown values,
contract identifiers, operators and other fields remain exact. The protocol
was committed before its results were measured.

## Executed command

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --no-restore --filter FullyQualifiedName~PmAnalyticsV3ModelGuardTests --logger "console;verbosity=minimal" --nologo
```

Exit 0: passed 3, failed 0, skipped 0. Compilation completed with no warning
or error in the retained output. This was one targeted invocation, without
a correction or rerun. The command compiled the evaluator used in TEST-146;
no separate or full-suite build/test invocation followed.

The three facts cover:

- Existing ranking and canonical clarification behavior with 12 controlled
  model responses.
- Recovery of the observed contradiction and reordered requested measures;
  rejection of changed period/category/grouping/filters, dropped measures,
  invalid schema, out-of-bounds ranking, missing ranking intent, explicit
  ranking contradictions, unknown/duplicate clarification identifiers, missing
  plans, malformed JSON and unknown properties. Consistent clarification,
  explicit ranking and an at-most threshold remain supported. The unsafe SQL
  question is rejected without a transport call.
- Recognition of known catalog-value capitalization/whitespace equivalence,
  with different buildings, changed operators, contract-ID case changes,
  extra array values and unknown catalog-value case changes still failing.

Tests use the actual model interpreter and existing client parsing with a
controlled HTTP handler and disposable EF InMemory contexts. No external
provider, SQL Server, browser, report execution or deployment was involved.

## Identity and limits

| Assembly | SHA-256 |
|---|---|
| Test assembly | `df911431491dfc357f74463e217fbee04af66e8c763bac4c580bc6ef6db13f02` |
| Backend assembly | `8ed24ffd56f768ea21d31dc8dd058b88ce17a1bd2c6088ee6b30312434d3dd89` |
| Evaluator assembly | `c8bed3cb16dd6c0e00ba901bd8d4e063b04af3b5777b618b6ab53f8f2e002639` |

The log is ignored at
`artifacts/nla-recovery-checks-2026-10-23/focused-test.log`, SHA-256
`d406edb18d44edccc9552197795ef59996a9f936a0a0c1430a41d1ef256af886`.
Safe metadata is retained in `test-identity.json` in the same ignored folder.
No tests were broadened beyond this class. Passing these controlled cases
supports the specified recovery boundary, not general live-model reliability.
The web continues to use its existing edit-question fallback for Top; no
inline ranking selector or browser acceptance claim is made.
