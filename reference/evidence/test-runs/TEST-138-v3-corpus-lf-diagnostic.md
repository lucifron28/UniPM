---
id: TEST-138
type: test-run
title: V3 corpus line-ending correction and deterministic diagnostic
status: reviewed
reviewedAt: 2026-10-23
reviewedBy: Researcher / repository owner, approval in this chat
reviewScope: Evidence record and stated limitations; no additional execution asserted
recordedAtUtc: 2026-10-23
testedCommit: 79852bf075ca082781365bdeb5981e8a2d0422a2
sourceBranch: feature/nla-guided-interpretation
evidenceLevel: locally-executed
---

# V3 corpus line-ending correction and deterministic diagnostic

## Correction and source identity

The user authorized resolving the TEST-137 checksum blocker and retrying.
The only corpus change was replacing CRLF separators with LF. Before writing,
the normalized bytes were checked against the existing manifest checksum:
`b7362e13106b8872353b934ceedd33c70e356334c67343a78e55f316453348e6`.
The questions, expected labels, manifest and review decisions were unchanged.
An LF rule for this specific candidate file was added to `.gitattributes`,
matching existing rules for the older evaluation corpora. Hash validation was
not weakened or bypassed.

The run used the already compiled evaluator from TEST-137 and the existing
source commit above, with the line-ending/Git-attributes patch and preexisting
uncommitted Web/mobile changes. This is not a frozen release. Exact UTC time
and corrected file hashes are retained in ignored
`artifacts/manuscript-verification-2026-10-23/lf-diagnostic-baseline.json`.

## Executed command

```powershell
dotnet tools/UniPM.PmAnalytics.V3Evaluation/bin/Debug/net10.0/UniPM.PmAnalytics.V3Evaluation.dll diagnose
```

Standard output and errors were retained in ignored
`artifacts/manuscript-verification-2026-10-23/nla-development-diagnostics-lf.json`.

Exit code: 0. The diagnostic processed 18 candidates and identified two cases
with differences from their provisional expectations. It reported scoring
blocked because labels await human review. No scoring command was run.

| Case | Expected and observed status | Expected plan disposition | Observed plan disposition | Differences |
|---|---|---|---|---|
| V3D-008 | NeedsClarification | ClarificationDraft | None | planDisposition, plan.categories, plan.period |
| V3D-009 | NeedsClarification | ClarificationDraft | None | planDisposition, plan.categories, plan.period |

For both cases the requested clarification fields also matched the provisional
expectation, Filters and Measures. Neither was accepted as an executable plan.
The missing category/period assertion matches result from the absent draft;
they do not establish that an incorrectly filtered report was executed.
The other candidates had no differences among their declared assertions.
This is not an approved accuracy result or proof of complete plan correctness.

## Human review and manuscript use

The user volunteered to review the outcomes. No review has been completed or
recorded as approval by this operation. Treat any subsequent user review as
researcher review unless the actual reviewer and process justify a different
description. Do not imply adviser, GSD or external independent approval.

The manuscript collection's question register now contains the actual
diagnostic status, clarification fields, disposition and assertion counts.
`nla-review-packet.md` provides a readable case-by-case comparison. These are
deterministic rule-parser diagnostics, not DeepSeek or end-to-end API results.
The evaluator does not execute database reports or catalog resolution.

No provider call, database connection, deployment, migration or reseed was
performed. The prior 17-test numerical/regression check is recorded separately
in TEST-137 and was not repeated. Manual BB cases, independent numerical
review, native SQL verification, SUS and ISO response verification remain
pending.
