---
id: TEST-128
type: test-run
title: GSD validation Azure replacement and deployment
status: executed
recordedAtUtc: 2026-10-21T02:49:00Z
testedCommit: f78d7216f869fb27507f57d2089e3ff5823990fa
sourceBranch: deploy/azure-staging
evidenceLevel: locally-executed
---

# GSD validation Azure replacement and deployment

The owner authorized synchronizing the deployment branch, replacing the active
seed dataset, and deploying Azure staging. The deployment branch merged local
`main` at `ec473e4157ae1ca35de80c6c9f7a84098d94f13d` without conflicts.
Uncommitted Web, NLA, startup-script, and planning changes in the main checkout
were excluded and preserved. This record does not claim those edits were
deployed.

## Package and revision

- Deployed application source: `f78d7216f869fb27507f57d2089e3ff5823990fa`.
- ZIP SHA-256: `0625ab35ac3d3502ebf1256c1c2fc9f5b506d8efa93ebfcf51bd5f9aaaf2db15`.
- Published container digest: `sha256:602ddd2dc1743190d0e62bc26d23582e34a031f4de078bd9523d728f467306af`.
- Healthy revision: `unipm-staging--gsd-f78d721-20261021`.
- Single-revision ingress sends 100% of traffic to the new revision.

Executed the existing packaging and context scripts from the clean deployment
checkout using portable Node 22.23.3 and .NET 10. Packaging ran its required
frontend typecheck/build and backend publish; both completed successfully.
Docker Desktop initially had no available engine and was started. The image
build and push to the existing registry package succeeded.

```powershell
pwsh -NoLogo -NoProfile -File scripts/package-azure-staging.ps1
pwsh -NoLogo -NoProfile -File scripts/prepare-container-apps-context.ps1
docker build --pull --label org.opencontainers.image.revision=f78d7216f869fb27507f57d2089e3ff5823990fa -t ghcr.io/lucifron28/unipm-staging:f78d7216f869fb27507f57d2089e3ff5823990fa artifacts/container-apps-context/f78d7216f869fb27507f57d2089e3ff5823990fa
docker push ghcr.io/lucifron28/unipm-staging:f78d7216f869fb27507f57d2089e3ff5823990fa
```

Production, replica limits, ingress, origin, JWT settings, managed identity,
Blob configuration, and existing provider switches were preserved. The existing
schedule-worker setting remains false. This deployment does not claim that
future automatic schedule generation is enabled. No AI provider was called.

## Database replacement

Created `unipm-gsd-validation-20261021` on the existing SQL server, with the
free offer enabled and `AutoPause` when the free allowance is exhausted.
The old `unipm-staging-db` was paused at discovery and was retained unchanged
for rollback. No old tables, rows, migration history, accounts, or Blob objects
were deleted. Old seed records are excluded from the active application through
the database-reference switch, not through a destructive reset.

Read-only preflight confirmed the exact replacement identity, Azure SQL engine
edition 5, Full-Text Search, and zero application tables. Set only this new
database's compatibility level from its default 170 to the required 150.
Ran the published assembly's commands in controlled order:

```powershell
dotnet UniPM.Api.dll --migrate-database
dotnet UniPM.Api.dll --seed-gsd-validation-users
dotnet UniPM.Api.dll --seed-gsd-validation
```

All three commands exited successfully. Migrations ran only on the new empty
database. The seed commands enforced their current-migration, opt-in,
exact-target, empty-state, and encrypted-connection guards. A short-lived
Development operator process performed initialization; the public application
remained Production. The owner entered credentials locally. The existing
explicit shared-password exception was enabled for the four temporary accounts.
No credential was included in command arguments or committed files.

Independent read-only postflight confirmed these counts before and after
cutover:

| Record | Count |
| --- | ---: |
| Applied migrations | 23 |
| Assets | 200 |
| Schedules | 500 |
| Inspections | 248 |
| Forms | 20 |
| Acknowledgements | 10 |
| WMS referrals | 15 |
| WMS referral audits | 15 |
| Accounts | 4 |

Created a contained runtime identity with only `db_datareader` and
`db_datawriter`. Verified that it could connect, was not `db_owner`, and had no
database ALTER permission. The new connection uses an Azure secret reference;
the previous runtime secret and image remain available for rollback.

An ARM response interrupted the cutover helper after the new secret was staged.
Inspection confirmed the old image and SQL reference were still active. The
reviewed resume checked the staged secret against the intended connection and
completed only the template update. It did not repeat migration or seeding.

## Live verification and limits

The final HTTPS smoke passed all 36 assertions. It checked both health
endpoints, SPA route responses, exact index/JavaScript package checksums,
unknown API 404 behavior, all four account logins, matching roles, secure
HttpOnly cookies, current-user reads, schedule role restrictions, logout,
GSD asset reads with new codes, and the V3 catalog. The first smoke attempt used
an incorrect catalog path; its failed report was preserved, the helper was
corrected to the existing analytics route, and the corrected smoke passed.
No inspection, assignment, submission, acknowledgement, referral, or photo
write workflow was exercised. Authentication checks created ordinary session
records; successful check sessions were logged out.

Automatic GitHub package run
[39534291208](https://github.com/lucifron28/UniPM/actions/runs/39534291208)
passed at the deployed SHA. Automatic
[Web CI 39534291261](https://github.com/lucifron28/UniPM/actions/runs/39534291261)
failed seven unit tests. The logs contain missing older form links and
asset-level schedule controls, including `Review batch`, `View batch`,
`Reassign`, and `FE-001`. These failures remain unresolved; the live smoke does
not replace those regressions or establish complete browser acceptance.
No additional full suite was started locally.

Removed this task's temporary exact-IP SQL firewall rule after postflight.
Deleted the three Windows-DPAPI credential files after use. Existing firewall
rules and Blob permissions were not changed. Public readiness still returned
200 after cleanup. Sanitized reports, logs, the rollback template, and reviewed
operator helpers remain in ignored `artifacts/azure-gsd-deploy-20261021/`.

Rollback requires an authorized operator to restore the saved old image and SQL
reference together. Do not run EF Down or delete the retained database. Its
paused status means rollback database availability must be checked separately.
Users should sign out and sign in again after the dataset switch. GSD workflow,
browser UX, physical-device acceptance, and any uncommitted local changes remain
outside this deployment's verification claim.
