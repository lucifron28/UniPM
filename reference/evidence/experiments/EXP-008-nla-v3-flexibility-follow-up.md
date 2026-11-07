---
id: EXP-008
type: experiment
title: NLA V3 flexibility follow-up paired regression experiment
status: executed
recordedAtUtc: 2026-11-07T02:55:43Z
sourceCommit: d59b4a3c3d893d244b0d1ca7f40a5551ceb3397e
sourceBranch: feature/nla-flexibility-evidence-hardening
evidenceLevel: deterministic-provider-executed
---

# EXP-008: NLA V3 flexibility follow-up paired regression experiment

## Objective and controlled inputs

Measure a developer-authored 28-case regression expansion against the exact
frozen source ef0feecee2b7f2092cd97086b8da2296c6f30e64 and candidate
d59b4a3c3d893d244b0d1ca7f40a5551ceb3397e (implementation source
74cedcbcd7f3a3a5bd3d9aca20c4fe15ea932daf). The same test inputs and complete
typed-plan rubric were used for both source versions. The baseline worktree was
detached at the frozen source and contained only the candidate's added test
files; its application source remained byte-for-byte frozen.

The cases are intentionally labeled a regression expansion, not a prospective
or held-out matrix: their families were chosen after the related failures had
been reproduced. The pre-existing developer matrix and its expectations were
not changed; it passed 108/108 on both versions. No GSD data or institutional
data was accessed. The endpoint tests use an isolated in-memory database and
never preview or execute a report. Fake model tests use predeclared canned
responses and one local handler call each.

## Paired results

| Outcome | Frozen baseline | Candidate |
|---|---:|---:|
| Cases with exact expected status, fields and typed plan | 4/28 | 28/28 |
| Cases with any semantic mismatch | 24/28 | 0/28 |
| False-valid numeric/measure outcomes observed | 16 | 0 |
| Negative-control cases passed | 4/4 | 4/4 |

The 24 corrected mismatches include complete number consumption, rejection of
three mixed-separator tokens, ranking limit parsing, model-guard numeric
preservation, bounded outcome mapping, and compound-measure clarification.
Baseline cases already correct were fractional integer 1.5 clarification,
needs repair clarification, may sira clarification, and contextual Filipino
hindi gumagana. These controls remain in the denominator.

The case list and observed semantic difference for each are in the
[paired result table](../../evaluation/pm-analytics-v3-flexibility-follow-up/README.md#case-by-case-defect-comparison).

## Provider, latency, and cost

No production DeepSeek request occurred. Fourteen semantic-guard invocations
used a local fake HTTP handler with canned typed proposals; each asserted one
handler call. Real-provider requests: 0. Provider usage, token counts, model
latency and cost: not applicable/unavailable. The prior paid-call budget was
not used or extended. Final baseline and candidate expansion durations were
8 and 11 seconds; these are harness totals, not provider or per-query latency.
An initial attempt at the three additional malformed-token controls failed
3/9 numeric cases. The token-capture correction passed the same target.

## Interpretation and limitations

The test suite verifies interpretation output and complete plan fields, not
calculated report rows. The paired gain is confined to these authored cases.
It does not establish GSD terminology approval, real-user accuracy, model
quality, field acceptance, result correctness, production latency, or
independent evaluation. No score denominator was changed after the run, and
all 28 cases remain in the comparison.

See [TEST-197](../test-runs/TEST-197-nla-v3-flexibility-follow-up-local.md)
for commands, commit identity, counters and CI linkage.
