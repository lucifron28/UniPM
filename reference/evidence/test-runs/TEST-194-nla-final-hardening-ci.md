---
id: TEST-194
type: test-run
title: NLA V3 final-hardening exact-head CI
status: executed
recordedAtUtc: 2026-11-06T12:45:46Z
testedCommit: 4338481e4012d1a2a30b8dea88109171e3d906e0
sourceBranch: feature/nla-final-hardening
evidenceLevel: ci-executed
---

# NLA V3 final-hardening exact-head CI

## Objective and identity

Record the GitHub Actions results for the NLA V3 final-hardening branch after
the schedule-picker viewport assertion was made tolerant of the resize-driven
position update.

- Tested source commit: `4338481e4012d1a2a30b8dea88109171e3d906e0`.
- Feature branch: `feature/nla-final-hardening`.
- Pull request: [#116](https://github.com/lucifron28/UniPM/pull/116), review only.
- Recorded at: 2026-11-06 12:45:46 UTC.

## GitHub Actions results

| Workflow | Event | Run | Result |
|---|---|---|---|
| Backend CI | `pull_request` | [42219350040](https://github.com/lucifron28/UniPM/actions/runs/42219350040) | Passed. |
| Web CI | `pull_request` | [42219350006](https://github.com/lucifron28/UniPM/actions/runs/42219350006) | Passed, including the full browser suite. |
| Web CI | `push` | [42219341180](https://github.com/lucifron28/UniPM/actions/runs/42219341180) | Passed, including the full browser suite. |
| Sync GitHub work to Notion | `pull_request_target` | [42219345590](https://github.com/lucifron28/UniPM/actions/runs/42219345590) | Passed. |

Each run reported the tested head SHA above. No native SQL Server 2019, GSD
acceptance, production-provider, or production-deployment evidence is implied.

## Browser assertion follow-up

The first web run on preceding commit `460f6bd4ced18eff09e8bf9c8c2815fe51ed3fee`
failed the schedule-creation browser check after resizing from desktop to a
375-pixel viewport. Its immediate geometry sample still reported the desktop
popup edge at 1,035 pixels. The isolated test passed, and the measurement was
consistent with the resize listener's React state update arriving after the
test sampled the popup. The assertion now polls until the picker fits the
viewport and workspace, then checks the same bounds. The exact-head web runs
above passed with that change.

No production UI code, report contracts, API behavior, dependencies, or report
execution paths changed in this follow-up. No paid provider calls were made.
