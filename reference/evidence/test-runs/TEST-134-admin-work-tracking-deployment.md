---
id: TEST-134
type: test-run
title: Admin and work tracking Azure staging redeployment
status: executed
recordedAtUtc: 2026-10-21T12:43:00Z
testedCommit: bd3ed8e450ef4255316eb138104eb24891c32cb9
sourceBranch: deploy/azure-staging
evidenceLevel: locally-executed
---

# Admin and work tracking Azure staging redeployment

The staging package workflow completed successfully for `bd3ed8e`. The package
SHA-256 is `955c8b642e493059349c0854a12dcf60ff38fd55c967b67381264e363c170083`.
The container image was pushed to GHCR with digest
`sha256:cc2b006ed0e9edc00697bed1baf00515a239a627ad1f869b65dda2282666ee9f`.

Azure Container App `unipm-staging` now runs revision
`unipm-staging--bd3ed8e-20261021`. Azure reports `Healthy`, `Provisioned`, one
replica, and 100% traffic to the new revision. A read-only preflight confirmed
Production mode, the existing SQL secret reference, 0.25 CPU, and replica range
0–1 before the image-only update.

Read-only HTTPS checks passed: `/` returned 200; the PNG logo, all 13 HTML-
referenced JavaScript/CSS assets returned 200; the page title is
`UniPM | Preventive Maintenance`; unauthenticated `/api/v1/admin/overview`
returned 401. Container logs show ASP.NET Core started in Production on port
8080. The app image and revision were the only Azure settings changed.

No migration, seed, reset, database write, secret/configuration update, Blob
change, or AI-provider request ran. This verifies the deployed package and
service startup, not authenticated browser behavior or GSD acceptance.
