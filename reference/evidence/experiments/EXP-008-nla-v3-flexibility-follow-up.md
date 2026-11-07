---
id: EXP-008
type: experiment
title: NLA V3 flexibility follow-up paired regression experiment
status: executed
recordedAtUtc: 2026-11-07T03:21:27Z
sourceCommit: 6064fa17e78a88de82dff20748521ef414d101a2
sourceBranch: feature/nla-flexibility-evidence-hardening
evidenceLevel: deterministic-provider-executed
---

# EXP-008: NLA V3 flexibility follow-up paired regression experiment

## Objective and controlled inputs

Measure a developer-authored 29-case regression expansion against the exact
frozen source ef0feecee2b7f2092cd97086b8da2296c6f30e64 and candidate
6064fa17e78a88de82dff20748521ef414d101a2 (initial implementation source
74cedcbcd7f3a3a5bd3d9aca20c4fe15ea932daf plus a narrow sentence-period
correction). The same test inputs and complete
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
| Cases with exact expected status, fields and typed plan | 5/29 | 29/29 |
| Cases with any semantic mismatch | 24/29 | 0/29 |
| False-valid numeric/measure outcomes observed | 16 | 0 |
| Negative-control cases passed | 4/4 | 4/4 |

The 24 corrected mismatches include complete number consumption, rejection of
three mixed-separator tokens, ranking limit parsing, model-guard numeric
preservation, bounded outcome mapping, and compound-measure clarification.
Baseline cases already correct were fractional integer 1.5 clarification,
needs repair clarification, may sira clarification, and contextual Filipino
hindi gumagana. The sentence-final-period threshold control passed on both
baseline and corrected candidate. These cases remain in the denominator.

The case list and observed semantic difference for each are in the
[paired result table](../../evaluation/pm-analytics-v3-flexibility-follow-up/README.md#case-by-case-defect-comparison).

## Provider, latency, and cost

No production DeepSeek request occurred. Fourteen semantic-guard invocations
used a local fake HTTP handler with canned typed proposals; each asserted one
handler call. Real-provider requests: 0. Provider usage, token counts, model
latency and cost: not applicable/unavailable. The prior paid-call budget was
not used or extended. Final baseline and candidate expansion durations were
9 and 10 seconds; these are harness totals, not provider or per-query latency.
An initial attempt at the three additional malformed-token controls failed
3/9 numeric cases. The token-capture correction passed the same target. A later
sentence-period endpoint regression first failed on the candidate and passed
after its narrow regex correction. The paired 29-case run passes every
candidate expectation.

## Interpretation and limitations

The test suite verifies interpretation output and complete plan fields, not
calculated report rows. The paired gain is confined to these authored cases.
It does not establish GSD terminology approval, real-user accuracy, model
quality, field acceptance, result correctness, production latency, or
independent evaluation. One sentence-punctuation case was added after a final
regex audit; the final 29-case denominator is reported consistently in the
paired run, with no cases removed after results were observed.

See [TEST-197](../test-runs/TEST-197-nla-v3-flexibility-follow-up-local.md)
for commands, commit identity, counters and CI linkage.
