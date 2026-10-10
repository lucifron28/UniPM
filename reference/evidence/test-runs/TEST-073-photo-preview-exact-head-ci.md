---
id: TEST-073
type: test-run
title: Exact-head CI for photo-preview test correction
status: executed
recordedAtUtc: 2026-10-10T18:22:49Z
testedCommit: bfecf51469a0826eb14286cd4728550a5e02fbbc
sourceBranch: feat/inspection-photo-evidence
evidenceLevel: ci-executed
---

# Exact-head CI for photo-preview test correction

## Objective

Verify the photo-preview test correction and evidence update on the exact
pushed PR head.

## Execution identity

- Tested commit: `bfecf51469a0826eb14286cd4728550a5e02fbbc`.
- Repository: `lucifron28/UniPM`.
- Draft PR: [#89](https://github.com/lucifron28/UniPM/pull/89), based on
  `feat/pre-evaluation-ux-motion` (PR #88).
- PR state at verification: open, draft, unmerged.

## GitHub Actions results

- Backend CI push run #318 (`38075290868`): passed on the tested commit.
- Web CI push run #200 (`38075290840`): passed on the tested commit.
- Web CI pull-request run #201 (`38075294141`): passed on the tested commit.

Both web triggers and Backend CI checked the same head SHA. The preceding
failed Web CI attempts and the test-fixture correction are described in
[TEST-072](TEST-072-web-photo-preview-ci-fixture.md).

## Limitations

These CI results do not verify native SQL Server migration execution, physical
camera behavior, iOS build, staging acceptance, deployment, or production
file-storage permissions, backup, and retention. See
[TEST-071](TEST-071-inspection-photo-evidence-and-gps-removal.md).
