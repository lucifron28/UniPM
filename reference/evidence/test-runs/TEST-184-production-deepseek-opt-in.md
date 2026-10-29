---
id: TEST-184
type: test-run
title: Explicit production DeepSeek activation and live interpretation check
status: executed
recordedAtUtc: 2026-10-29T06:10:53Z
testedCommit: 692b3418d3b482bbb6ff4d95535a7299fea69d2c
sourceBranch: feature/enable-staging-deepseek
evidenceLevel: real-provider-executed
---

# Production DeepSeek opt-in

The owner explicitly requested DeepSeek on the deployed Azure app. The V3
interpreter now permits a non-Development host only when both
`NaturalLanguageAnalytics:V3DeepSeekEnabled` and
`NaturalLanguageAnalytics:V3DeepSeekProductionEnabled` are true. Both remain
false by default. Explicit deterministic selection still bypasses the provider.
The sanitizer, prompt, provider limits, validation and catalog resolution were
not changed. The older analytics enable flag remains unchanged.

The existing backend key was stored in the Container App secret store and
referenced through the backend-only `DEEPSEEK_API_KEY` environment variable.
No key was committed, passed as a command-line argument or included in logs.
The deployed environment remains Production. No database migration, seed reset,
account change or business-record write was required for this activation.

## Verification

The local targeted production-default test could not build because the running
local API locked its Debug executable. No assertion ran. The owner authorized
continuing through CI without stopping that API. No local test rerun was made.

The exact source commit passed
[Backend CI](https://github.com/lucifron28/UniPM/actions/runs/40826999294),
[Web CI](https://github.com/lucifron28/UniPM/actions/runs/40826999340), and
[Azure packaging](https://github.com/lucifron28/UniPM/actions/runs/40827007601).
Existing tests were not modified. The default production-rejection test remains
applicable when the additional production opt-in is absent.

The verified CI package replaced the application layer over the previously
deployed official .NET 10 runtime layers. Deployed image digest:
`sha256:c944c431dda5072e398e845cd4744c2081ca12afbb6d6f1336b8e60663e3ee26`.
The ready revision is `unipm-staging--ds-692b341-20261029`.

One authenticated live request to `/api/v1/analytics/pm/v3/interpret`, with no
explicit interpreter selection, asked:

> how many fire alarm are registered in ccms building

It returned HTTP 200, `interpreter: DeepSeek`, `status: Valid`, no clarification
fields, subject `assetInventory`, category `fire-alarm`, measure
`registeredAssetCount`, and a building equality filter resolved to
`CCMS BUILDING`. The plan has no historical period and uses Summary
presentation. The measured endpoint response time was 11,429 ms. This includes
network and backend processing and is not provider-only latency.

Authentication used a short-lived verification JWT for the existing active GSD
account, constructed in memory with the existing backend signing configuration.
No account password, security stamp or persisted session changed. No token was
written to the retained record. The first connection preflight encountered a
changed operator IP before any interpretation request; the temporary exact-IP
SQL firewall rule was corrected and then removed after verification.

## Limits and retained records

This is a single synthetic deployment smoke check, not a new accuracy study,
held-out evaluation, browser acceptance test or repeated corpus evaluation.
No report was executed. Provider-only latency, retries, token usage, cost and
raw model proposals were not instrumented. Existing evaluation results remain
unchanged. The unrelated blank clarification UI was not changed by this task.

Ignored records under `artifacts/staging-deepseek/` include the local build
failure, package provenance, image assembly helpers and the sanitized final
backend response in `live-result.json`. Secret values and verification tokens
were not included in these records. Unrelated local route-tree and untracked
files were preserved.
