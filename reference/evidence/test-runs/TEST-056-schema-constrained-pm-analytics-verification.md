---
id: TEST-056
type: test-run
title: Schema-constrained PM analytics web verification
status: draft
recordedAtUtc: 2026-10-03T11:55:10Z
testedCommit: 699424aa710c173253eb88908b4aab5c7d626506
sourceBranch: feature/schema-constrained-nla
evidenceLevel: locally-executed
---

# Schema-constrained PM analytics web verification

## Objective

Record local OpenAPI/client generation and focused web verification for the schema-constrained PM analytics change.

## Execution identity and commands

- Web checks ran at 699424aa710c173253eb88908b4aab5c7d626506. The OpenAPI refresh used source commit ed188962f1c8b0d6757373247e6934a8cbc6a5c6; generated contracts were committed as 01150b57c47b1f0a05da03130c98f6c4b4b67519.
- Refresh helper: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\artifacts\evidence\Run-OwnedOpenApiRefresh.ps1`. It ran `npm run api:pull` and `npm run api:generate`; both exited 0, and the contract check passed within each command. Refresh ran 2026-10-03T11:28:09.1453918Z to 2026-10-03T11:28:29.2182211Z.
- The helper launched the Development API on loopback with embeddings disabled, remote-provider access disabled, and provider credentials cleared. It issued only GET /openapi/v1.json. No operational, database, migration, or seeding request was issued.
- `npm run typecheck` exited 0 at the web-check commit in 15.211759 seconds. Start and end UTC were not captured.
- Focused unit command: `npm run test:run -- src/features/reports/pm-analytics.test.tsx src/features/reports/pm-period-dashboard.test.tsx`. The wrapper lost its session handle after 30.2 seconds at startup and retained no exit code or count. Status: NOT VERIFIED. No rerun.
- Browser command: `npm run e2e -- e2e/pm-period-dashboard.spec.ts --grep "shows a GSD-scoped PM analytics result with source navigation"`. At the same commit it stalled during startup for about 180 seconds after a WebServer warning. Ctrl+C stopped the owned wrapper with exit 1; a later poll returned Unknown process id 72828. Status: NOT VERIFIED, not a test assertion result. No retry.
- A separate `Invoke-WebRequest -Uri "http://localhost:5173" -UseBasicParsing -TimeoutSec 5` probe was blocked by sandbox socket permissions. This does not establish the cause of the Playwright startup stall.

## Results and counts

- OpenAPI baseline: 31 paths and 36 operations. The refreshed snapshot preserves those paths, operations, and existing schemas unchanged; it adds POST /api/v1/analytics/pm/query, operation ID QueryPmAnalytics, and five PM analytics schemas.
- The new operation documents 200, 400, 401, and 403 responses. The generated client function is `queryPmAnalytics(pmAnalyticsQuestionRequest, signal?)`, returning `Promise<PmAnalyticsResponse>`.
- Typecheck: passed. Focused Vitest: NOT VERIFIED. Local browser: NOT VERIFIED. Backend CI and the CI browser run are pending.
- A preliminary 35-pass backend run at dirty source 71485aa is not attributed to tested commit 699424aa710c173253eb88908b4aab5c7d626506. Final backend verification at the tested commit awaits CI.

## Environment and cleanup

- The OpenAPI helper summary recorded `OwnedProcessCleanupVerified: false`. An independent check at 2026-10-03T11:39:09.7883864Z found its PID absent, port 5219 closed, and zero environment restoration failures.
- The Playwright child cleanup was not confirmed by its wrapper. A root follow-up at 2026-10-03T11:54:50.5332135Z found the recorded webServer child PID 35588 absent and no listener on port 5173. This does not confirm the state of every child process.
- No external AI provider was called. No NLA native SQL Server test, physical-device test, IIS deployment, GSD/adviser acceptance, or real-provider quality test was run.

## Generated artifacts and limitations

- Sanitized web run metadata: `artifacts/verification/TEST-056-web-local.json`.
- OpenAPI refresh metadata: `artifacts/evidence/openapi-refresh-20261003T112808Z-ed188962/summary.json`, `environment-restoration.json`, and `handoff-metadata.json`. Raw logs remain ignored.
- This is a draft software verification record. CI results remain pending and will be captured with the pull request.
