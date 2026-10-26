---
id: TEST-152
type: test-run
title: Capstone remediation baseline
status: executed
recordedAtUtc: 2026-10-25T05:36:18Z
testedCommit: bdc07caeeb37c31ca7b524b7ef45cde5824a783c
sourceBranch: feature/capstone-remediation
evidenceLevel: locally-executed
---

# Capstone remediation baseline

## Source identity

The backend baseline used the `main` integration commit above. Its committed
tree matches guided-interpretation tip
`6437fa7b1e9585a6d4ab7aa377fe0d99dcae1503`. No branch merge occurred.
An isolated worktree preserves the original checkout's uncommitted changes.

The Node 22 lint reproduction identifies
`0591182c4f079e4756e2f2e7fc3b2f7ce5041423`, which adds only the Phase 0
audit document to the integration baseline. No application source had changed.

## Executed commands and results

| Command | Result |
|---|---|
| `dotnet test UniPM.slnx --logger "trx;LogFileName=baseline.trx" --results-directory artifacts/remediation/backend` | Exit 1; 645 total, 593 passed, 2 failed, 50 skipped; 4 minutes 12 seconds |
| `npm run lint`, from `web`, Node 22.23.4 | Exit 1; one `react-hooks/refs` error at `pm-analytics-v3.tsx:3259:35` |
| `flutter analyze`, from `mobile` | Exit 0; no issues found, 225.7 seconds |

The full backend command compiled its projects. No separate baseline build
was needed. The client dependency setup used lockfiles; it is not an
application-source correction. The system Node 24 runtime does not meet the
project's Node 22 engine requirement, so subsequent checks use isolated Node 22.

## Failures

1. The V3 evaluator harness expects 18 pending review decisions, but the
   approved frozen corpus contains zero. Preserve approved labels and use a
   temporary pending-review fixture to test rejection independently.
2. An Ollama invalid-output theory case fails before provider-output parsing.
   `NaturalLanguageAnalyticsQuestionGuard.FindMonths` throws
   `RegexMatchTimeoutException` while assessing the short question containing
   `Nov 2026`. Repeated dynamic abbreviation patterns are the identified
   correction target. This run does not establish whether host load
   contributed to the timeout.
3. The web render IIFE passes a callback that reaches a ref. Move derived
   filter values into ordinary rendering and retain user-event callbacks.

## Artifacts and limits

Raw backend output, TRX and audit notes are ignored under
`artifacts/remediation/backend/`. The Node 22 lint output is
`artifacts/remediation/clients/phase0-web-lint.log`. Flutter's result was
captured in the executor's command output; no separate retained analyzer log
is claimed.

Fifty backend tests skipped, including SQL-dependent cases. No isolated SQL
connection was configured. No physical Android device was attached. This
record does not establish native SQL behavior, physical-device acceptance,
live-provider interpretation, deployment, or complete frontend regression.
No paid provider request or institutional database operation was performed.
