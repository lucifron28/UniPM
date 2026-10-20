---
id: TEST-095
type: test-run
title: Container Apps staging context preparation
status: executed
recordedAtUtc: 2026-10-18T04:24:40Z
testedCommit: 88428443e2a907bb2bc797f8bfa561ef9ffb96e4
sourceBranch: deploy/azure-staging
evidenceLevel: locally-executed
---

# Container Apps staging context preparation

The reviewed preparation commit is `88428443e2a907bb2bc797f8bfa561ef9ffb96e4`.
The existing packaging script ran once with Node 22.23.3 and .NET 10, followed
by `pwsh -NoLogo -NoProfile -File scripts/prepare-container-apps-context.ps1`.
Both exited 0. Packaging includes frontend typecheck, production Vite build
and backend Release publish. Existing Pkcs advisory, nullable-reference and
SqlQueryRaw analyzer warnings remain; no application code changed in this task.

The context script verified clean Git HEAD, exact package provenance and
checksum, required API/frontend files and archive paths. It extracted 108 files
from the verified ZIP into a new ignored context and generated per-file hashes.
The ZIP SHA-256 is
`1381c1f92217662735e205150d0c5ca03102a29087a7ee5cfba0fa6b6ee6f823`.
Raw logs are `artifacts/azure-deployment-preflight/container-apps-package.log`
and `container-apps-context.log`. The prepared context is under
`artifacts/container-apps-context/88428443e2a907bb2bc797f8bfa561ef9ffb96e4/`.
PowerShell syntax parsing and `git diff --check` passed.

Docker CLI 29.6.2 was installed, but its Linux engine pipe was unavailable.
No image build, image push or container smoke test ran. No full suite,
Playwright or live SQL/Blob test ran. The Dockerfile uses the .NET 10 runtime,
port 8080 and the non-root image user. Runtime behavior is not established by
context preparation alone.

Earlier authorized Azure provider registration completed for Microsoft.App.
Subsequent read-only discovery returned environment quota 1 in Japan East,
Japan West, Southeast Asia and East Asia. This is not compute-allocation or
deployment verification. The proposed Consumption configuration is documented
in the Container Apps guide: 0.25 vCPU, 0.5 GiB, zero minimum replicas and one
maximum replica. Ancillary costs and trial expiry still apply.

No hosting resource, registry, SQL identity, Blob role, firewall rule or app
setting was created or changed by this preparation. No live SQL data operation,
migration, seed, reset, deployment or merge ran. Image publication, cloud
resource creation and security changes require their separate approvals.
Evidence-only commits after the tested commit do not change the package's
recorded source identity. Rebuild for any newly approved deployment source.
