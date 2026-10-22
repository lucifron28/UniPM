---
id: TEST-139
type: test-run
title: Researcher-reviewed deterministic NLA development evaluation
status: reviewed
reviewedAt: 2026-10-23
reviewedBy: Researcher / repository owner, approval in this chat
reviewScope: Evidence record and stated limitations; no additional execution asserted
recordedAtUtc: 2026-10-23
testedCommit: 79852bf075ca082781365bdeb5981e8a2d0422a2
sourceBranch: feature/nla-guided-interpretation
evidenceLevel: locally-executed
---

# Researcher-reviewed deterministic NLA development evaluation

## Review and changes before scoring

The repository owner explicitly approved the reviewed outcomes in this chat
and reported trying the application. This is researcher approval, not adviser,
GSD or independent external approval. The manual accuracy statement is a user
attestation; no per-case manual execution captures were supplied here.

As discussed before approval, V3D-008 and V3D-009 retain NeedsClarification and
Filters/Measures clarification fields, but now expect no partial plan rather
than a ClarificationDraft. Their plan assertions are null. This accepts safe
clarification for unresolved OR/negation rather than requiring a draft. The
change followed diagnostic inspection and is disclosed as a development-set
revision. Other candidate expectations were unchanged. No executable outcome
was substituted for an ambiguous question.

The review ledger records researcher approval dated October 23, 2026. The
manifest freezes the revised corpus to SHA-256
`dd313076a40ea5dd8eddfef610ff8db37ca6c5ba1934a286c6023a1586ca0e25`.
The evaluator source is pinned to the commit above. Existing uncommitted
Web/mobile changes remain outside this rule-parser-only evaluation; no full
application release freeze is claimed. Exact UTC time and hashes of corpus,
manifest, ledger and evaluator DLL are retained in ignored
`artifacts/manuscript-verification-2026-10-23/nla-reviewed-score-identity.json`.

## Execution

```powershell
dotnet tools/UniPM.PmAnalytics.V3Evaluation/bin/Debug/net10.0/UniPM.PmAnalytics.V3Evaluation.dll score
```

Output was retained in ignored
`artifacts/manuscript-verification-2026-10-23/nla-researcher-reviewed-score.json`.
Exit code: 0. The existing evaluator was not modified to produce this result.

| Measure | Observed result |
|---|---|
| Cases | 18 |
| Status matches | 18/18 |
| Clarification-field set matches | 18/18 |
| Plan-disposition matches | 18/18 |
| Declared plan assertion matches | 69/69 |
| Cases matching all declared expectations | 18/18, 100% |
| Unsafe acceptance flags | 0 |
| Pending label approvals | 0 |

## Interpretation and limits

This measures the deterministic V3 safety guard and rule parser on 18
researcher-reviewed development cases. It does not run DeepSeek, database
execution, catalog resolution, final interactive clarification, or the full
browser workflow. Omitted plan assertions are not proven correct. In
particular, historical review notes about incomplete assertions remain
relevant when interpreting this bounded result.

The corpus was already used for development and inspected before two labels
were revised. Do not describe this as held-out accuracy, independent
institutional acceptance, all-question NLA accuracy, or a numerical-report
correctness score. No provider calls or database operations occurred.

The 17 regression passes in TEST-137 and these 18 interpretation cases are
different populations. Neither automatically marks BB-01 through BB-32 as
executed. Numerical verification and original SUS/ISO response verification
remain separate tasks.

## Suggested manuscript wording

The deterministic interpretation component was evaluated on 18
researcher-reviewed development questions. All 18 matched their declared
expected outcomes, including status, clarification fields, plan disposition
and 69 specified plan assertions. Two provisional draft expectations were
revised following development diagnostics before the corpus was frozen.
These results describe agreement on the reviewed development set and do not
establish performance on unseen questions, live DeepSeek interpretation or
numerical-report correctness.
