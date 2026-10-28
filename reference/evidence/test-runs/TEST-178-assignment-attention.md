---
id: TEST-178
type: test-run
title: Assignment attention eligibility and dashboard verification
status: executed
recordedAtUtc: 2026-10-28
testedCommit: dd5c01737a996c7814852432ece2285c901120fc
sourceBranch: feature/assignment-attention-plan
evidenceLevel: ci-executed
---

# Assignment attention verification

The assignment-batches read endpoint now derives actionable missing-assignment
counts and urgency from the existing batch identity, assignment locks,
unfinished-work state and authenticated Supervisor ownership. The dashboard
shows scoped pending and urgent counts; Planning & assignment consumes the same
server metadata for eligibility, badges and ordering. Manila calendar dates
determine the 14-day and 7-day warning thresholds and the month-ended state.
The separate Inspector assignment-period calculation is unchanged.

Implementation commits are `8f847a8` and `873645b`. Fixture corrections are
`ecc265e` and `dd5c017`. The final two commits only update mock response metadata
and ownership consistency; existing filter, count, stale-response, navigation
and layout assertions remain in place.

## Executed CI

Both runs below identify the exact tested commit in the front matter.

| Workflow | Run | Result |
| --- | --- | --- |
| Backend CI | [40655616264](https://github.com/lucifron28/UniPM/actions/runs/40655616264) | Passed; 633 tests passed, 54 skipped, 687 total |
| Web CI | [40655603354](https://github.com/lucifron28/UniPM/actions/runs/40655603354) | Passed; formatting, lint, types, API contract and generated-client checks, 278 unit tests, coverage, build, 63 browser tests passed and two skipped |

Backend CI runs the repository's solution build and test workflow on Linux.
Web CI runs the checked-in npm verification commands and Playwright Chromium
suite on Linux. Browser fixtures use mocked API responses. Existing desktop
assignment-attention cases cover 1440- and 1536-pixel widths, role-specific
messages, scoped navigation and badges, zero/unavailable states, keyboard
navigation and hidden banner controls in print media.

Backend tests cover calendar boundaries including leap February and Manila
midnight, role-specific actionable counts and changes after assignment. These
are automated development checks, not institutional acceptance evidence.

## Failed run and correction

Web CI [40650542089](https://github.com/lucifron28/UniPM/actions/runs/40650542089)
on `873645b` reported 277 unit tests passed and one failed. The existing
application-shell assignment-scope fixture omitted attention fields required
by the updated queue. A targeted local execution of that one test reproduced
the failure at its pending-batch count assertion. No application rule was
changed to satisfy the fixture. The corrected unit fixture and schedule browser
fixture retain their original assertions and provide the new server metadata.
The final passing CI supersedes that failure as the current branch result.

## Boundaries and retained output

No new local full suite was run for this record. The earlier implementation
report described focused local tests and desktop checks; this record uses the
independently inspected CI outcomes above for its pass counts. Native SQL
Server and physical-device checks were not run for this change. No migrations
or provider calls are part of assignment attention.

Raw logs remain ignored under `artifacts/`:

- `assignment-attention-review-ci.log`: initial failing Web CI output.
- `assignment-attention-review-targeted.log`: local reproduction of that failure.
- `assignment-attention-web-ci-final.log`: final passing Web CI output.
- `assignment-attention-backend-ci-final.log`: final passing Backend CI output.

The planning documents were uncommitted during the tested application runs.
Their later documentation commit does not change the tested runtime source.
