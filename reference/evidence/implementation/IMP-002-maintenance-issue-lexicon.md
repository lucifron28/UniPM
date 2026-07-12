---
id: IMP-002
type: implementation
title: Versioned maintenance issue lexicon
status: reviewed
recordedAtUtc: 2026-07-12T01:10:00Z
sourceBranch: main
evidenceLevel: source-inspected
---

# Maintenance Issue Lexicon

## Objective

Normalize documented English, Tagalog, and Taglish maintenance vocabulary into
deterministic category-bounded issue keys before retrieval.

## Source Identity

- Source branch: `main` at merge commit `00e5401`.
- Relevant commits: `537e53a`, `737f924`, `d52b9e6`, and `b51d57d`.
- Implementation date from Git history: 2026-07-11.
- Source paths: `server/Features/Retrieval/Resources/`,
  `MaintenanceIssueLexiconLoader.cs`, `MaintenanceIssueNormalizer.cs`, and
  `tests/UniPM.Api.Tests/Retrieval/MaintenanceIssueLexiconTests.cs`.

## Implementation Summary

The repository contains a versioned JSON lexicon with strict schema and loader
validation, approved issue-to-category mappings, aliases, deterministic longest
alias scoring, key ordering, and a narrow documented negation guard.

## Architecture And Contracts

Normalization requires an asset category, returns canonical issue keys with
scores and matched aliases, and does not diagnose or decide maintenance action.
The resource is runtime operational vocabulary; evaluation labels remain in the
test-only manifest.

## Important Files

- `server/Features/Retrieval/Resources/maintenance-issue-lexicon-v1.json`
- `server/Features/Retrieval/Resources/maintenance-issue-lexicon-v1.schema.json`
- `server/Features/Retrieval/MaintenanceIssueLexiconLoader.cs`
- `server/Features/Retrieval/MaintenanceIssueNormalizer.cs`
- `tests/UniPM.Api.Tests/Retrieval/MaintenanceIssueLexiconTests.cs`

## Database Changes

No migration or persistence model is introduced by the lexicon itself.

## Tests Present

The source tree contains schema, strict-loader, multilingual alias, category
boundary, scoring, multi-issue, and negation tests. No independent labeled
lexicon precision/recall/F1 evaluator is present.

## Verification Status

Source-inspected only for historical implementation. Behavioral tests present
in the repository are not claimed as historical executions. The current
lexicon evidence is implementation inspection and behavioral test coverage,
not an accuracy baseline.

## Known Limitations

Vocabulary is limited to the documented fixture and form language. No general
language negation parser or independent labeled lexicon evaluation exists.

## Related Evidence

- [IMP-003](IMP-003-lexical-full-text-retrieval.md)
- [IMP-005](IMP-005-retrieval-benchmark.md)
