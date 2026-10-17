---
id: TEST-088
type: test-run
title: Azure staging browser verification follow-up
status: executed
recordedAtUtc: 2026-10-17T15:40:45Z
testedCommit: bccc2ff8cc1db0d55b61f65d3f7e2e91f37844a4
sourceBranch: deploy/azure-staging
evidenceLevel: locally-executed
---

# Azure staging browser verification follow-up

## Result

The approved full Playwright rerun passed **56 tests with 1 skipped**, with
no test or production code changes. It ran from the clean normal web checkout
at `bccc2ff8cc1db0d55b61f65d3f7e2e91f37844a4`, using Node 22.23.3,
Chromium, two workers, and an owned Vite server on port 5194. The skipped
case is the opt-in live schedule/WMS workflow; mocked tests do not establish
institutional acceptance or live field-workflow verification.

The initial 54/1/2 result remains preserved in
[TEST-087](TEST-087-azure-staging-package-verification.md). This follow-up does
not erase or relabel those navigation failures.

## Diagnosis and execution

A readiness probe against the isolated artifact source reproduced the
30-second navigation timeout before the approved suite rerun started.
Request capture showed the HTML document completing in 34 ms and `main.tsx`
in 14 ms, while `/@vite/client`, `/@react-refresh`, optimized React modules,
application modules, and CSS were still pending at the deadline. The document
was interactive, but had not reached the browser load event. This narrows
the failure to development-resource loading; it does not establish the
underlying Vite/plugin/filesystem cause.

The normal web checkout then reached the dashboard heading in 8,296 ms and
the asset heading in 523 ms. The approved single full-suite rerun started
only after those real page-readiness conditions succeeded. Existing test
assertions, timeouts, workflow mocks, and production authentication were
unchanged. No arbitrary sleep was added to a test. The readiness clients used
fictional GSD session data and intercepted API calls, without contacting a
live application database or provider.

The execution invoked the existing Playwright CLI with `test --workers=2
--reporter=line`, an ignored output directory, the repository's unchanged
configuration, and `UNIPM_E2E_PORT=5194`. `CI` was unset to let that
configuration reuse the owned, readiness-checked server. Node and npm used
the portable Node 22 installation. The wrapper stopped only its owned Vite
process afterward. Earlier results and the existing servers were preserved.

Raw artifacts:

- `artifacts/azure-staging/qa/web-playwright-final-rerun.log`
- `artifacts/azure-staging/qa/web-playwright-final-server.log`
- `artifacts/azure-staging/qa/playwright-final-results/`
- `artifacts/azure-staging/qa/vite-load-diagnosis.json`
- `artifacts/azure-staging/qa/web-playwright-approved-rerun.log`

## Package and remaining boundaries

The local ZIP, its exact source commit, checksum, successful native SQL/HTTPS
smoke, and earlier backend/web checks are recorded in TEST-087. The tested
ZIP is from `c51e673691dca541530886bfe0139150848db80b`; subsequent commits
change the smoke harness and documentation only. Do not label that artifact
as a build of the later evidence commit. CI can produce a separate ZIP tied
to its own exact head and checksum.

Local verification is complete within those recorded boundaries. Exact-head
GitHub CI is pending at the time this record is written and must be checked
after pushing. No Azure resources were modified or deployed. Storage account
name, private container permissions, managed identity, Azure SQL preflight
and Full-Text Search guards, actual App Service runtime/HTTPS behavior, and
physical-device acceptance remain unverified. The owner reported creation
of `inspection-photos`; this is not an independent Azure access check.

Existing Pkcs dependency advisories require review before public deployment.
No dependency remediation, real-provider language evaluation, or held-out
evaluation is claimed. Status remains UNMERGED and NOT DEPLOYED.

## Changed-file inventory

Files changed from starting source `ac62139af203ad06b6b1a712426d8558d73982d8`,
including this follow-up and its index/guide updates:

```text
.github/workflows/azure-staging-package.yml
database/azure-staging-preflight.sql
reference/evidence/INDEX.md
reference/evidence/test-runs/TEST-087-azure-staging-package-verification.md
reference/evidence/test-runs/TEST-088-azure-staging-browser-followup.md
reference/guides/azure-sql-migration-audit.md
reference/guides/azure-staging-deployment.md
reference/templates/azure-staging-appsettings.env.example
scripts/initialize-azure-staging-demo.ps1
scripts/package-azure-staging.ps1
scripts/smoke-azure-package.mjs
server/Features/Auth/DevelopmentUserSeeder.cs
server/Features/Inspections/AzureBlobInspectionPhotoEvidenceStorage.cs
server/Features/Inspections/InspectionPhotoEvidenceStorage.cs
server/Hosting/PackagedFrontend.cs
server/Program.cs
server/server.csproj
tests/UniPM.Api.Tests/Auth/AuthEndpointsTests.cs
tests/UniPM.Api.Tests/Hosting/PackagedFrontendHostingTests.cs
tests/UniPM.Api.Tests/Infrastructure/FoundationEndpointsTests.cs
tests/UniPM.Api.Tests/Inspections/AzureBlobInspectionPhotoEvidenceStorageTests.cs
tests/UniPM.Api.Tests/Inspections/InspectionPhotoEvidenceEndpointsTests.cs
tests/UniPM.Api.Tests/Seeding/DevelopmentUserSeederTests.cs
web/openapi/unipm-v1.json
web/scripts/build-azure-staging.mjs
web/scripts/check-openapi-contract.mjs
web/src/api/generated/endpoints.ts
web/src/api/http-client.test.ts
web/src/api/http-client.ts
web/src/config/env.test.ts
web/src/config/env.ts
```
