---
id: IMP-057
type: implementation
title: NLA V3 numeric and bounded semantic flexibility follow-up
status: reviewed
recordedAtUtc: 2026-11-07T03:21:27Z
sourceCommit: 6064fa17e78a88de82dff20748521ef414d101a2
sourceBranch: feature/nla-flexibility-evidence-hardening
evidenceLevel: source-inspected
---

# NLA V3 numeric and bounded semantic flexibility follow-up

## Baseline and boundary

The follow-up starts at the untouched PR #116 frozen source
ef0feecee2b7f2092cd97086b8da2296c6f30e64. Implementation began at
74cedcbcd7f3a3a5bd3d9aca20c4fe15ea932daf and the verified correction is
6064fa17e78a88de82dff20748521ef414d101a2 on
feature/nla-flexibility-evidence-hardening.

It changes only the V3 language lexicon, question interpreter, and backend
acceptance/guard tests. It adds no SQL, formula, report subject, category,
database schema, API DTO, web application source, provider call, or migration.
PR #116 remains unchanged; no merge, deployment, or freeze is claimed.

## Findings and corrections

The actual deterministic interpretation endpoint and fake DeepSeek proposal
guard reproduced a confirmed numeric issue: comma/space-grouped counts could
be truncated to 1, plain 1000 could be mistaken for a year, malformed
separators and negatives could return Valid, overflow could silently lose its
filter, and top 1,000 could become top 1. An additional audit found that
repeated commas, underscores, and slashes could also leave a leading 1 as a
Valid threshold with the remainder ignored.

The lexicon now captures a complete numeric token and separately validates
grouping, sign, decimal syntax, range, and integer requirements. Malformed
comma, underscore, slash, and backslash separators are captured as part of
the token so strict validation rejects them instead of losing the threshold.
Threshold operands are excluded from year extraction only when a
comparison/percentage threshold was actually recognized. Ranking uses the
same complete value and the existing 1–20 limit. Invalid values produce a
safe clarification or the existing unsupported ranking response rather than
a partial valid plan.

The final punctuation audit found that the malformed-token boundary could also
discard a valid threshold immediately before a sentence-final period. The
narrow regex correction preserves that ordinary punctuation, with a complete
typed-plan endpoint regression.

The model-result guard now preserves a bounded set of explicitly supported
outcome paraphrases: finished before the PM deadline, wrapped up within the
scheduled cycle, done on time, selected Filipino on-time wording, never
completed by the deadline, still overdue and unfinished, finished after the
due date, selected Filipino/Taglish late wording, and contextual inspection
findings stated as not working. Conflicting paraphrases and a compound outcome
with a separate measure clarify. needs repair and may sira remain
clarifications. No unknown model category, period, name, status, subject, or
cross-subject join is accepted by these rules.

When a measure is unresolved, the guard strips measure-dependent columns and
uses a safe table draft while retaining only compatible, already resolved
grouping scope. This prevents an invalid draft from leaking a requested measure
column after it has been cleared.

## Change-to-regression map

| Change | Regression coverage |
|---|---|
| Full integer/group parsing; malformed, mixed-separator, negative, fractional, overflow handling | NUM-01–10 and NUM-12–14 in TEST-197; deterministic endpoint and canned guard tests |
| Full rank-limit token consumption and existing top cap | NUM-11 in TEST-197; deterministic endpoint |
| Sentence-final punctuation preserves a valid threshold | NUM-15 in TEST-197; deterministic endpoint |
| Bounded model outcome mappings and conflict policy | LANG-01–09 and SAFE-01–03 in TEST-197; fake local model handler |
| Existing language/canonical plan behavior remains stable | unchanged developer matrix; 108/108 on baseline and candidate |

Evidence details and limitations are in
[TEST-197](../test-runs/TEST-197-nla-v3-flexibility-follow-up-local.md) and
the [follow-up evaluation record](../../evaluation/pm-analytics-v3-flexibility-follow-up/README.md).
