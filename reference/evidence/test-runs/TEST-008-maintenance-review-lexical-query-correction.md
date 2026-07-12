---
id: TEST-008
type: test-run
title: Maintenance review lexical query correction verification
status: executed
recordedAtUtc: 2026-07-12T23:12:53Z
testedCommit: bb8d3078504b933a4a00852e38679aa9607c0a0a
sourceBranch: feat/retrieval-review
evidenceLevel: locally-executed
supersedes: TEST-007
---

# Maintenance Review Lexical Query Correction Verification

## Commands And Results

```powershell
dotnet restore .\UniPM.slnx
dotnet build .\UniPM.slnx --configuration Release --no-restore
dotnet test .\UniPM.slnx --configuration Release --no-build
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\evidence\Invoke-MaintenanceReviewVerification.ps1 -FreshDatabase -RemoveVolumes
```

- Restore: passed.
- Release build: passed with zero warnings and zero errors.
- Focused maintenance-review and retrieval-query tests: 57 passed, 0 failed.
- Full Release suite: 219 passed, 0 failed, and 17 skipped.
- The fresh-volume verification harness completed with exit code `0` and
  recorded `worktreeClean: true`.

## Corrected Retrieval Behavior

- Maintenance-review retrieval queries use the lexical parser's searchable-term
  rules and remain within its eight-term and 256-character limits.
- Canonical issue-key terms are ordered before remaining sanitized finding terms.
- Sanitizer placeholders are excluded from retrieval terms, while canonical
  issue keys continue to pass separately to semantic retrieval.
- Citation tests include the required disclaimer before asserting rejection of
  an unknown source label.

## Fresh SQL Server And Endpoint Verification

- SQL Server 2025 Developer Edition with Full-Text Search started on a new
  Compose volume.
- The explicit `--migrate-database` stage applied migrations from an empty
  database before seeding.
- Development-only synthetic seed reported 20 assets, 34 schedules, and 30
  inspections.
- The maintenance search-document rebuild completed successfully.
- Readiness polling used `/health/ready`.
- The review endpoint returned HTTP 200 with a selected fictional source
  record after lexical retrieval.
- The response reported `isDegraded: true`, `passesExecuted: 2`,
  `lexicalStatus: success`, and `semanticStatus: unavailable` while embeddings
  were disabled.
- Summary generation remained disabled; source records were retained for human
  verification.

## Artifacts And Hashes

The ignored artifact capture is retained under:

```text
artifacts/evidence/20260712-231212Z-bb8d3078504b-maintenance-review
```

```text
b80b25d80200951df7702d5268f3056a8de751054ea66289f588144e3df3d61c  maintenance-review-response.json
a335ed095644b7fb659f5a01dd9def166889561846bcd8d4fe0d5f1ac2a13c76  verification-summary.json
```

The retained response contains fictional maintenance source data only. It does
not contain a summary-provider payload, API key, prompt, token map, or
embedding vector. No separate `environment.json` was emitted; the retained
`verification-summary.json` is the authoritative environment and worktree
record for this run.

## Limitations

This is local SQL Server orchestration evidence on fictional data. It does not
establish real summary quality, generated-summary faithfulness, authenticated
production access, IIS deployment, or production GSD performance. Semantic
model-quality and fused-retrieval-quality claims remain pending a configured
real provider and executed quality benchmark.
