---
id: TEST-067
type: test-run
title: Pre-evaluation exact-head GitHub CI
status: executed
recordedAtUtc: 2026-10-10T05:06:46Z
testedCommit: f1fd7c798e61fdc4f54221fc1c7dfa32e62ceb6d
sourceBranch: feat/pre-evaluation-ux-motion
evidenceLevel: ci-executed
---

# Pre-evaluation exact-head GitHub CI

## Execution identity

All listed workflows checked out the exact PR head
f1fd7c798e61fdc4f54221fc1c7dfa32e62ceb6d on
feat/pre-evaluation-ux-motion. PR #88 remained a draft targeting
feat/pre-evaluation-remediation.

## Results

| Workflow | Trigger | Result | Duration |
| --- | --- | --- | --- |
| Backend CI, run 38026177092 | Push | Passed | 1m 9s |
| Web CI, run 38026177097 | Push | Passed | 3m 8s |
| Web CI, run 38026179566 | Pull request | Passed | 2m 53s |
| Update UniPM Notion tasks, run 38026205342 | Pull request target | Passed | 6s |

Backend CI completed restore, Release build, tests, and repository observability
configuration checks. Web CI completed formatting, lint, typecheck, API
contract checks, generated-client consistency, unit tests, coverage, production
build, and Playwright. The push and pull-request Web workflows both passed.

GitHub reported the existing CS8602 warning at
PreventiveMaintenanceFormEndpoints.cs:540 and an Ubuntu runner-image notice.
Neither failed the workflow.

## Run records

- https://github.com/lucifron28/UniPM/actions/runs/38026177092
- https://github.com/lucifron28/UniPM/actions/runs/38026177097
- https://github.com/lucifron28/UniPM/actions/runs/38026179566
- https://github.com/lucifron28/UniPM/actions/runs/38026205342

## Limitations

This record covers GitHub CI at the exact commit above. The local native SQL
Server 2019 execution is recorded separately in TEST-065. GSD acceptance and
deployment remain pending.
