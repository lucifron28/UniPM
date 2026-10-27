---
id: TEST-176
type: test-run
title: Full CI findings and alignment of existing UI fixtures
status: executed
recordedAtUtc: 2026-10-27
testedCommit: c553d2e4cc6d615702d5ec4faa8a71abfee98da9
sourceBranch: feature/demo-scenario-coverage
evidenceLevel: ci-executed
---

# CI findings and existing UI fixtures

The audited branch's original Web CI run stopped at lint. The report timestamp
fix removed that warning. The first remediation publication then stopped at
Prettier for two changed web files; those files were formatted without changing
their behavior.

At c553d2e, Backend CI run 40510842530 passed with 621 tests passed and 54
skipped, 675 total. Web CI run 40510842387 passed formatting, lint, type checking,
API contract checks and generated-client checks. Its first execution of the
full web unit suite reported 273 passed and five failed, across 32 test files.
Coverage, build and browser stages were skipped after those failures.

The five failures expected UI elements removed or changed by previously
approved work. Four asset-registry tests expected overall-inventory cards,
their loading skeleton, or summary-card errors. The compact registry now shows
its filtered record count and table. An application-shell test expected a batch
record link; the implemented navigation opens a side panel through a button.

The corrective fixture work is limited to existing assertions in
asset-registry.test.tsx and app-shell-access.test.tsx, and the corresponding
obsolete assertion blocks in assets.spec.ts and schedules.spec.ts. The fixtures
must preserve asset filtering, counts, record/loading/error behavior and batch
scope checks while using current controls and panel behavior. No production
behavior is changed to satisfy obsolete expectations, and no new tests are
introduced by this correction.

This record reports the c553d2e executions and the reason for the fixture
correction. It does not claim that a later publication's CI has passed before
that run completes. GitHub Actions records the exact SHA and final outcome of
the corrective publication separately. The live multi-role test remains a
separate check subject to the explicit retry limit in TEST-175.

Raw failed-unit output is kept in ignored
artifacts/audit-remediation-web/ci-unit-failure.log. The remote primary records
are https://github.com/lucifron28/UniPM/actions/runs/40510842530 and
https://github.com/lucifron28/UniPM/actions/runs/40510842387.
