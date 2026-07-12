---
id: TEST-006
type: test-run
title: Maintenance review correction verification
status: executed
recordedAtUtc: 2026-07-12T16:13:12Z
testedCommit: 7cf06874349af81de885efbefc27978e112a726e
sourceBranch: feat/retrieval-review
evidenceLevel: locally-executed
supersedes: TEST-005
---

# Maintenance Review Correction Verification

## Commands And Results

```powershell
dotnet restore .\UniPM.slnx
dotnet build .\UniPM.slnx --configuration Release --no-restore
dotnet test .\UniPM.slnx --configuration Release --no-build
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\evidence\Invoke-MaintenanceReviewVerification.ps1
```

- Restore: passed.
- Release build: passed with zero warnings and zero errors.
- Full Release test suite: passed.
- Docker/SQL Server verification: passed against a clean worktree and the
  recorded commit.
- Readiness polling used `/health/ready`.
- The endpoint verification used the bounded post-rebuild retry window and
  required selected source records before passing.

## Test Counts

| Total | Executed | Passed | Failed | Skipped |
|---:|---:|---:|---:|---:|
| 228 | 211 | 211 | 0 | 17 |

The focused maintenance-review suite passed 21 tests. Skipped tests are the
existing SQL Server integration tests, retrieval benchmarks, and optional real
embedding-provider smoke test in the ordinary .NET run; the endpoint script
did execute against SQL Server.

## SQL And Endpoint Verification

- SQL Server 2025 Developer Edition with Full-Text Search started successfully.
- Migrations applied or confirmed current successfully.
- Development-only synthetic seed reported 20 assets, 34 schedules, and 30
  inspections.
- The maintenance search-document rebuild completed successfully.
- The review endpoint returned HTTP 200 with selected source records.
- The response reported `isDegraded: true`, `passesExecuted: 2`,
  `lexicalStatus: success`, and `semanticStatus: unavailable` while embeddings
  were disabled.
- Summary generation remained disabled; source records were retained for human
  verification.

## Artifacts And Hashes

The clean script capture is retained under the ignored path
`artifacts/evidence/20260712-161251Z-7cf06874349a-maintenance-review`.

```text
b80b25d80200951df7702d5268f3056a8de751054ea66289f588144e3df3d61c  maintenance-review-response.json
4e3499a71bf028e5806db148e2b9411223998dcb77d5c3a3b24e237291776f0a  verification-summary.json
```

The response contains only fictional maintenance source data. It does not
contain a summary-provider payload, API key, prompt, token map, or embedding
vector.

## Limitations

This is local SQL Server orchestration evidence on fictional data. It does not
establish real summary quality, generated-summary faithfulness, authenticated
production access, IIS deployment, or production GSD performance. Semantic
model-quality and fused-retrieval-quality claims remain pending a configured
real provider and executed quality benchmark.
