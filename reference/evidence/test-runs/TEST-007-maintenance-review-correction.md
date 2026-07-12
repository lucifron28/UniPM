---
id: TEST-007
type: test-run
title: Maintenance review fresh migration verification
status: superseded
recordedAtUtc: 2026-07-12T16:59:24Z
testedCommit: 847495d2fc9fb9c2fc5fed73b178199ed7c9046f
sourceBranch: feat/retrieval-review
evidenceLevel: locally-executed
supersedes: TEST-006
supersededBy: TEST-008
---

# Maintenance Review Fresh Migration Verification

## Commands And Results

```powershell
dotnet restore .\UniPM.slnx
dotnet build .\UniPM.slnx --configuration Release --no-restore
dotnet test .\UniPM.slnx --configuration Release --no-build
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\evidence\Invoke-MaintenanceReviewVerification.ps1 -FreshDatabase -RemoveVolumes
```

- Restore: passed.
- Release build: passed with zero warnings and zero errors.
- Full Release test suite: passed with 216 passed, 0 failed, and 17 skipped.
- A focused rerun confirmed the one transient observability scrape failure before
  the final full-suite pass.
- Formatting verification was attempted; it reports existing whitespace findings
  in unrelated files and made no changes.

## Fresh SQL Server And Endpoint Verification

- SQL Server 2025 Developer Edition with Full-Text Search started on a fresh
  Compose volume.
- The explicit `--migrate-database` stage applied migrations from an empty
  database before seeding.
- Development-only synthetic seed reported 20 assets, 34 schedules, and 30
  inspections.
- The maintenance search-document rebuild completed successfully.
- Readiness polling used `/health/ready`.
- The review endpoint returned HTTP 200 with a selected source record.
- The response reported `isDegraded: true`, `passesExecuted: 2`,
  `lexicalStatus: success`, and `semanticStatus: unavailable` while embeddings
  were disabled.
- Summary generation remained disabled; source records were retained for human
  verification.
- The verification summary recorded `worktreeClean: true` and exit code `0`.

## Artifacts And Hashes

The ignored artifact capture is retained under:

```text
artifacts/evidence/20260712-165844Z-847495d2fc9f-maintenance-review
```

```text
b80b25d80200951df7702d5268f3056a8de751054ea66289f588144e3df3d61c  maintenance-review-response.json
720d9261ff2686d88dc1908d84227283ad70b6f5ec66b99c10be8518da50c066  verification-summary.json
```

The response contains fictional maintenance source data only. It does not
contain a summary-provider payload, API key, prompt, token map, or embedding
vector. No separate `environment.json` was emitted by this harness; the
retained `verification-summary.json` is the authoritative environment and
worktree record for this run.

## Limitations

This is local SQL Server orchestration evidence on fictional data. It does not
establish real summary quality, generated-summary faithfulness, authenticated
production access, IIS deployment, or production GSD performance. Semantic
model-quality and fused-retrieval-quality claims remain pending a configured
real provider and executed quality benchmark.
