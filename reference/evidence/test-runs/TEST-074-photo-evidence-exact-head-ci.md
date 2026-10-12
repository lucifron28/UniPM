---
id: TEST-074
type: test-run
title: Exact-head CI for optional photo evidence and GPS removal
status: executed
recordedAtUtc: 2026-10-10T18:30:10Z
testedCommit: 3d0f48f2c7fc7e96b0f8f27830c663d445058f14
sourceBranch: feat/inspection-photo-evidence
evidenceLevel: ci-executed
---

# Exact-head CI for optional photo evidence and GPS removal

## Objective

Record GitHub CI for the latest pushed draft PR head after the photo-preview
fixture correction and its evidence updates.

## Execution Identity

- Tested source commit: `3d0f48f2c7fc7e96b0f8f27830c663d445058f14`.
- Branch: `feat/inspection-photo-evidence`.
- Draft PR: [#89](https://github.com/lucifron28/UniPM/pull/89), stacked on
  PR #88 at `eb45d5d9edf51f55a1c9264f390849cd6d2d9fe7`.
- PR state at verification: open, draft, unmerged, and undeployed.
- The tested head contains evidence/test-fixture updates after the implementation
  and code-test commits. Local implementation checks are recorded in TEST-071
  and TEST-072.

## GitHub Actions Results

- Backend CI push run #319: reported as passed on the tested commit in PR #89.
- Web CI pull-request run #202 (`38075591378`): passed on the tested commit.

The Web workflow run was queried directly by the exact commit SHA. The PR
description reports Backend CI run #319 against the same head SHA; the
commit-workflow query returned PR-triggered runs only.

## Limitations

This CI run does not verify native SQL Server migration execution, physical
camera behavior, an iOS build, staging acceptance, deployment, or production
photo-storage permissions, backup, and retention. See TEST-071.
