---
id: TEST-198
type: test-run
title: NLA V3 flexibility follow-up exact-code-head CI
status: executed
recordedAtUtc: 2026-11-07T03:21:27Z
testedCommit: 6064fa17e78a88de82dff20748521ef414d101a2
sourceBranch: feature/nla-flexibility-evidence-hardening
evidenceLevel: ci-executed
---

# TEST-198: NLA V3 flexibility follow-up exact-code-head CI

## Tested source identity

Both workflows ran on the exact parser and regression-test commit
6064fa17e78a88de82dff20748521ef414d101a2. Evidence-only Markdown updates
followed these runs; no application or test source changed afterward.

## Backend CI

[Backend CI run 42332930860](https://github.com/lucifron28/UniPM/actions/runs/42332930860)
completed successfully. Restore, Release build, backend tests, observability
Compose validation, Prometheus configuration validation, and Grafana dashboard
JSON validation passed. Backend tests reported 788 passed, 0 failed, and 55
skipped. Skipped tests are not counted as passes.

## Web CI

[Web CI run 42332930821](https://github.com/lucifron28/UniPM/actions/runs/42332930821)
completed successfully. Formatting, lint, type checking, API contract checks
and tests, API checks, unit tests, coverage tests, production build, Playwright
setup, and E2E all passed. Both unit and coverage runs reported 32 files and
278 tests passed. E2E reported 64 passed and 2 skipped.

## Raw logs and limits

Ignored raw workflow logs are retained locally at:

- artifacts/nla-flexibility-evidence/backend-ci-6064fa1.log
- artifacts/nla-flexibility-evidence/web-ci-6064fa1.log

These CI runs verify code quality and workflow behavior; they do not establish
GSD acceptance, numerical report correctness, held-out accuracy, or a freeze.
No live provider call, SQL Server execution, merge, or deployment occurred.
