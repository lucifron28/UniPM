---
id: TEST-131
type: test-run
title: Query-first reporting and UniPM branding deployment
status: executed
recordedAtUtc: 2026-10-21T04:24:00Z
testedCommit: bbc96e0592afe5491d99e7b693213dc505509d47
sourceBranch: deploy/azure-staging
evidenceLevel: locally-executed
---

# Query-first reporting and UniPM branding deployment

The owner approved deploying both the filter-based PM dashboard and the
question-first NLA interface, with the manual V3 builder hidden. Previously
uncommitted reporting work was reviewed and committed separately from the
unrelated asset, form-registry, assignment, startup, and planning changes.
Those unrelated changes remain local. Branding commit `c58e794` supplies the
UniPM logo favicon, page title, and maroon table-header backgrounds with white
text. The dashboard remains available independently of NLA.

## Focused checks

- `npm run test:run -- src/features/reports/pm-analytics-v3.test.tsx`: 14/14
  passed for the source committed as `634754d1e89141e0fbe0dab1c3ae80153536431d`.
  This covers hidden manual controls, interpretation review, inventory,
  preview/confirmation/results, errors, and PM-cycle source drill-down.
- Earlier attempts passed 10/14 and 13/14. Corrections updated stale error
  wording, the combined building-filter label, and the automatic drill-down
  preview expectation. The owner explicitly authorized the final retry.
- A later helper-only rename from `useScheduledCycleWording` to
  `withScheduledCycleWording` passed ESLint for the affected TSX file. It does
  not change the text-rewriting operation. The UI test was not repeated after
  that rename or formatting-only changes.
- `scripts/package-azure-staging.ps1` passed Node 22.23.3 frontend typechecking,
  production bundling, .NET 10 backend publish, and package checks at the exact
  tested staging commit above. The initial package at `9391465` also passed
  but was superseded before any Azure image update.
- `npm run api:generate` passed the existing API contract check and generated
  the interpreter enum model, its barrel export, and request-model reference.
  These generated-client follow-ups were committed after packaging.

## Deployment

The existing context preparation script verified the final ZIP provenance,
checksum, required files, archive paths, and settings exclusions. Docker build
and push succeeded. The Azure update verified the previous image, Production
mode, database secret reference, CPU allocation, and replica limits, then
changed only the image and revision suffix.

- Package SHA-256: `d28de55b96f54e1a97f938df3218e1a6092510e79eafd1a697ed674239fc1cc6`.
- Image digest: `sha256:9a44edf85fb0e2d3c83767f3c3e69c781ecd3fcfb2475788ed7eb0b7f08dd814`.
- Healthy, ready revision: `unipm-staging--ui-bbc96e0-20261021`.
- Traffic: 100% to the latest revision.

Twenty-four read-only HTTPS assertions passed. They check health/readiness,
dashboard and analytics SPA responses, index and referenced asset checksums,
the UniPM title and PNG favicon, maroon/white table-header CSS, anonymous
catalog denial, and the exact lazy-loaded dashboard and analytics chunks.
These checks establish deployed files and service health, not authenticated
browser rendering or live natural-language interpretation accuracy.

No migration, seed, database reset, business write, account change, SQL firewall
change, Blob permission change, new resource provisioning, or AI-provider call
ran. Production mode and the existing database connection reference remain
unchanged. The prior image metadata is retained for rollback.

## CI and limitations

The exact deployed commit's Azure package workflow
[39543351564](https://github.com/lucifron28/UniPM/actions/runs/39543351564)
passed. Its Web CI
[39543351578](https://github.com/lucifron28/UniPM/actions/runs/39543351578)
passed formatting, lint, types, and contract checks, then stopped on generated
client drift. Commit `d0d4109` supplies the generator's three missing changes;
that follow-up is not part of the packaged source commit and does not alter
the reporting API contract. No green full-suite claim is made. Previously
recorded unrelated CI failures in TEST-130 remain outside this release.

Full backend, Web, mobile, Playwright, native SQL, provider, and authenticated
browser suites were not run locally. No provider configuration was enabled.
Raw logs, package records, rollback metadata, and reviewed operator helpers
remain under ignored `artifacts/query-first-redeploy/`.
