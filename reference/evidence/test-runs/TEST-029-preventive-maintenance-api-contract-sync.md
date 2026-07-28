---
id: TEST-029
type: test-run
title: Preventive-maintenance API contract synchronization verification
status: executed
recordedAtUtc: 2026-07-28T16:09:24Z
testedCommit: 2cd1bf7181b7c68ad6ddf7066a2351e03021b97f
sourceBranch: chore/preventive-maintenance-api-contract-sync
evidenceLevel: locally-executed
---

# Preventive-Maintenance API Contract Synchronization Verification

## Objective

Pull the live OpenAPI v1 document from the current backend, regenerate the
Orval client, and verify that the committed generated client has no drift.

## Execution Identity

- Tested source commit: `2cd1bf7181b7c68ad6ddf7066a2351e03021b97f`
- Source branch: `chore/preventive-maintenance-api-contract-sync`
- Starting main commit: `bc0fe8243ec38a7ab56ba013324838db7dbb4287`
- Execution date: 2026-07-28 UTC

## Commands

The backend was started from the current repository in Development mode. The
existing web refresh command then pulled the live OpenAPI v1 document and
regenerated the client:

```powershell
npm run api:refresh
```

The focused committed-state contract and generated-drift check was then run:

```powershell
npm run api:check
```

## Results

- `npm run api:refresh`: passed after one transient Windows generated-file
  write failure was retried; the live snapshot pull, contract sanity check,
  Orval generation, and formatting completed.
- The first pre-commit `api:check` invocation correctly reported the newly
  generated files as uncommitted drift. After those generated files were
  committed, the focused `npm run api:check` completed successfully with no
  drift.
- The live contract inspection confirmed all nine preventive-maintenance form
  operations have stable operation IDs.
- The corrective-handoff row model retains nullable `assetDeviceNumber` and
  `assetCode`; signature fields are limited to acknowledgement-related
  contracts and absent from corrective-handoff responses.

## Verification Scope

No full web test suite, Playwright suite, backend suite, SQL Server suite, or
frontend behavior verification was run for this contract-only phase.

## Generated Artifacts

No raw logs, credentials, connection strings, tokens, API keys, provider
payloads, prompts, vectors, or real institutional records were committed.
