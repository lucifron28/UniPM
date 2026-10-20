---
id: TEST-101
type: test-run
title: Server-side bounded assignee search verification
status: executed
recordedAtUtc: 2026-10-18T10:27:09Z
testedCommit: d474cb4725ffce031a7c903cf2201d7afe2427ab
testedWorkingTreeSourceManifestSha256: da023446a4a87f428144c875ad6789209400a9157d1966092fa7eedd28e7f984
sourceBranch: feature/staging-workflow-followups
evidenceLevel: locally-executed
---

# Server-side bounded assignee search verification

## Objective

Close the gap recorded in TEST-100: the web picker showed only a small set of
accounts, while its API still returned every active Supervisor or Inspector.
Keep assignment queue names available without loading those role-wide lists.

## Execution identity

The source was based on branch `feature/staging-workflow-followups` at HEAD
`d474cb4725ffce031a7c903cf2201d7afe2427ab`, with uncommitted changes. The
working-tree manifest SHA-256 covers the 14 source, contract, generated-client,
and focused-test files involved in bounded search and queue display names. It
hashes each relative path and file bytes, separated by zero bytes. The digest is
`da023446a4a87f428144c875ad6789209400a9157d1966092fa7eedd28e7f984`.

The shell used Node `v24.15.0` and npm `11.12.1`. The web package requires
Node `>=22 <23`; the successful production build therefore ran outside the
supported runtime range.

## Changes under verification

The Supervisor and Inspector option endpoints now accept a display-name search
and return at most ten eligible active accounts in stable order. Search input
longer than 100 characters is rejected. An already selected active account
with the required role is appended when it is not among the current matches, so
the picker retains its selection while searching. Assignment queue batches
include the current Supervisor and Inspector display names; queue cards no
longer request full role-option collections to render those names.

## Commands and results

| Command or check | Result |
| --- | --- |
| `npm run api:generate` (from `web/`) | Passed OpenAPI contract sanity and generated the client parameters and queue response models. |
| Focused `ScheduleBatchAssignmentEndpointsTests.Assignment_options_and_mutations_are_limited_to_the_relevant_role` test | First run failed because the test expected one queue batch while its fixture included both August and November. The request was narrowed to `pmCycle=2026-08`; the single permitted rerun passed 1/1. The test covers server-side result caps, later-page matching, selected-account retention, overlong-search rejection, role boundaries, and queue assignee names. |
| `npx prettier --write` on the modified web files | Completed successfully. |
| `npm run build` (from `web/`) | Passed TypeScript project checks and Vite production build; 2,224 modules transformed. Node 24 is outside the supported package range. |
| `git diff --check` | Passed. Git reported line-ending conversion notices for the OpenAPI snapshot and generated route tree. |

## Not verified

- A build using the supported Node 22 runtime.
- The full backend or web suites, Playwright, browser screenshots, deployed
  acceptance, or GitHub CI.
- Performance against a large real account directory.
- Azure connectivity or any Azure database operation. No Azure access, database
  write, commit, push, or deployment occurred.

The earlier TEST-100 record is retained as historical evidence of the UI-only
cap. This record describes the source state tested in the uncommitted working
tree and does not claim that an uncommitted change has been pushed or deployed.
