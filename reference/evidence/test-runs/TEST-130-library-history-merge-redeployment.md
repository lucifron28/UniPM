---
id: TEST-130
type: test-run
title: Library history merge and Azure image redeployment
status: executed
recordedAtUtc: 2026-10-21T03:30:05Z
testedCommit: bbaf407c1fea7f7206bcb6e85ff9771380621caa
sourceBranch: deploy/azure-staging
evidenceLevel: locally-executed
---

# Library history merge and Azure image redeployment

The owner explicitly authorized merging the Library Complex/history refinement
and deploying it. Main fast-forwarded to
`c241deee9005ba6deeb09871ad40e02f8377c5bd`; staging merged that history at
`bbaf407c1fea7f7206bcb6e85ff9771380621caa`. Both were pushed without rewriting
history. The only conflict was the evidence index. Both TEST-128 and TEST-129
entries were retained. The fully merged local task branch was removed, and the
primary checkout remains on main. Uncommitted Web, NLA, startup-script, and
planning edits were preserved and excluded from this release.

## Package and deployment

The clean staging checkout ran the existing package/context scripts with
Node 22.23.3 and .NET 10. Frontend typecheck/build and backend publish passed.
The initial log-directory redirect failed before invoking packaging; the
absolute artifact path was corrected and the package completed successfully.
Docker build and push to the existing public package succeeded.

- Package SHA-256: `329bc9b2d94344eb86df889d502cabb6a16a0d7b5e9a077bfc2454ace6837a55`.
- Image digest: `sha256:6703a541a84bfd5b787219189eee4512ae35bdf5769a926cf0d95b7b813b3f74`.
- Healthy revision: `unipm-staging--lib-bbaf407-20261021`.
- Ingress: 100% to the new ready revision.

The operator verified the previous image, Production setting, current SQL
secret reference, and 0.25 CPU / 0-to-1 replica configuration before updating
only the image and revision suffix. The SQL target and all other runtime
settings were preserved. No migration, seed, database reset, account change,
SQL firewall change, Blob change, provider call, or new resource provisioning
ran during this redeployment. The previously updated validation data remains
in the existing active database; TEST-129 records its independent SQL checks.
The prior image/reference metadata was retained for rollback.

## Checks and CI limitations

Nine read-only HTTPS assertions passed after Azure reported the new revision
Healthy and ready. They cover liveness/readiness, two SPA fallback responses,
the exact index and referenced JavaScript package checksums, a hashed script
reference, unknown API 404 behavior, and anonymous V3 catalog access returning
401. SPA HTTP responses do not establish rendered browser navigation or UX.
Login and business-write workflows were not repeated for this image-only
release. Existing API, scheduling, and mobile runtime code did not change
relative to the previous deployed application; the source delta is the fixture,
its focused tests, and documentation.

The automatically triggered staging package workflow
[39538384914](https://github.com/lucifron28/UniPM/actions/runs/39538384914)
passed on the deployed SHA. Staging
[Web CI 39538384944](https://github.com/lucifron28/UniPM/actions/runs/39538384944)
failed with the seven previously observed form-link and asset-level schedule
expectations. They remain unresolved.

At the merged main head, automatic
[Backend CI 39538376978](https://github.com/lucifron28/UniPM/actions/runs/39538376978)
reported 568 passed, 1 failed, and 50 skipped. The failing batch reassignment
assertion compares the same two GUIDs in reversed order. Automatic
[Flutter CI 39538377210](https://github.com/lucifron28/UniPM/actions/runs/39538377210)
stopped at the `prefer_initializing_formals` analyzer diagnostic in the existing
offline-store constructor. Main Web CI reported the same seven failures.
These are open checks, not a green-CI claim. No extra full suite was started
locally and no unrelated correction was included in the deployment.

Reviewed operator helpers, package/build/push logs, rollback metadata,
sanitized CI diagnostics, and `https-checks.json` remain under ignored
`artifacts/library-history-redeploy/`. Physical-device, GSD, browser workflow,
and full regression acceptance remain outside these results.
