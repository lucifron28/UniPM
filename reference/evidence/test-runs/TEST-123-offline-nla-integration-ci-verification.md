---
id: TEST-123
type: test-run
title: Offline and NLA V3 integration branch CI verification
status: executed
recordedAtUtc: 2026-10-20T03:41:14Z
testedCommit: a93e71b244f51d6d0ca7a8a2122e0eebf2ecaa86
sourceBranch: feature/offline-nla-v3-integration
evidenceLevel: ci-executed
---

# Offline and NLA V3 integration branch CI verification

## Integration source commit

All three repository workflows completed successfully on the exact pushed source commit `a93e71b244f51d6d0ca7a8a2122e0eebf2ecaa86` on `feature/offline-nla-v3-integration`.

| Workflow | Run | Result |
|---|---|---|
| Backend CI | [#370](https://github.com/lucifron28/UniPM/actions/runs/39364885266) | Passed in 1m 21s. |
| Flutter CI | [#50](https://github.com/lucifron28/UniPM/actions/runs/39364885217) | Passed in 6m 55s, including formatting, analysis, focused acknowledgement/offline tests, full Flutter suite, and Android debug APK build. |
| Web CI | [#283](https://github.com/lucifron28/UniPM/actions/runs/39364885166) | Passed in 2m 39s. |

The corresponding local .NET, Web, Playwright, and V3 evaluator results are in
[TEST-122](TEST-122-offline-nla-integration-candidate-verification.md).

## Source branch CI

| Source | Exact/source commit | CI evidence |
|---|---|---|
| Offline inspection | `60788e123864dd82fd055677b773f82541bc8ef3` | Flutter CI [#49](https://github.com/lucifron28/UniPM/actions/runs/39362166990) passed. |
| Mobile acknowledgement/home UX | `0aa12b978d22e5ef67762d8c2e369877eaa4db47` | Flutter CI [#34](https://github.com/lucifron28/UniPM/actions/runs/39359136181) passed. |
| NLA V3 asset inventory | `c6e47620bff512ad29192622309845ee4348f921` | Backend CI [#368](https://github.com/lucifron28/UniPM/actions/runs/39355163448) passed. Web CI [#280](https://github.com/lucifron28/UniPM/actions/runs/39297020004) passed on `be07b6835e6a80e0a12e0507cad8cd7c267374e6`; no Web files changed between that commit and `c6e4762`. |

## Remaining limitations

- Native SQL Server 2019 execution remains unverified. A disposable image pull was blocked by HTTP 403; no database or container was created.
- All 18 V3 question labels still require independent human review. V3D-008 and V3D-009 remain diagnostic differences; no score or GSD acceptance is claimed.
- Physical-device and GSD field acceptance remain open.
- No provider calls, held-out evaluation, Azure/staging operation, migration, deployment, or merge occurred.

This record establishes CI for source commit `a93e71b`. Documentation/evidence follow-ups do not alter application source; their resulting CI is tracked by the workflow links on the final pushed branch.
