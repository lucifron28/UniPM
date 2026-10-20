---
id: TEST-096
type: test-run
title: Local Container Apps image build and isolated smoke
status: executed
recordedAtUtc: 2026-10-18T04:43:21Z
testedCommit: 88428443e2a907bb2bc797f8bfa561ef9ffb96e4
sourceBranch: deploy/azure-staging
evidenceLevel: locally-executed
---

# Local container image build and isolated smoke

All 108 manifest file hashes and lengths matched, no additional app files or
reparse points were found, and Dockerfile/ignore bytes matched the tested source.
Package SHA-256: `1381c1f92217662735e205150d0c5ca03102a29087a7ee5cfba0fa6b6ee6f823`.
Later HEAD 353fc375 differed from that source only in evidence documentation.
No populated credential-like application settings were found.

One `docker build --pull --tag unipm-staging:8842844 .` passed from the verified
context. Docker engine version was 29.6.2. Image ID:
`sha256:3cf354a9ef0a460dd55e78294ce7760fb6a4071325c660cf0e7fbbff7b7ef92a`.
Base: `mcr.microsoft.com/dotnet/aspnet:10.0@sha256:d5633778c754e89a386882eddf8dbee6942447a8a4b8267c19cf1e1158d0b0ac`.

One disposable loopback-only container ran in Production with 512 MiB and 0.25
CPU, a generated signing key supplied by process environment name only, and no
SQL connection or database credentials. Worker, AI, embeddings and metrics were
disabled. No secret value was retained. Results:

| Check | Result |
| --- | --- |
| /health/live | 200, startup 2.48 seconds |
| /health/ready | 503, expected because SQL was deliberately unconfigured |
| / | 200 HTML |
| /api/info | 200 JSON |
| Unknown /api route | 404, non-HTML |
| /app/assets/ | 200 HTML |
| /favicon.svg | 200 SVG |
| Anonymous /api/v1/schedules | 401 |
| Runtime user | Non-root UID 1654 |

The owned container was removed successfully, exit 0. Raw build and smoke logs
remain under ignored `artifacts/azure-deployment-preflight/container-smoke/`,
with prefixes `20261018T123951-0d4f69f7` and `20261018T124224-422a8290`.
This proves isolated hosting, not database readiness or deployment acceptance.
No Azure operation, image publication, SQL access, login, photo operation,
full suite, Playwright run or merge occurred. Real SQL/managed-identity Blob,
HTTPS cookie behavior and cloud compute allocation remain unverified.