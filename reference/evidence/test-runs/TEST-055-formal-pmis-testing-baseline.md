---
id: TEST-055
type: test-run
title: Formal PMIS testing baseline
status: executed
recordedAtUtc: 2026-10-03T07:33:00Z
testedCommit: 7e4476c719dd3032170f0f35dcc7bed86beb0f6a
sourceBranch: main
evidenceLevel: ci-executed
---

# Formal PMIS testing baseline

## Scope and identity

The tag `formal-testing-baseline-2026-10-03` peels to
`7e4476c719dd3032170f0f35dcc7bed86beb0f6a`. This record freezes that software
baseline for the NLA branch. The tag does not represent GSD, professor, or
adviser acceptance.

## Results

- Backend CI [run 37105925851](https://github.com/lucifron28/UniPM/actions/runs/37105925851)
  passed at the tagged commit. It ran
  `dotnet build ./UniPM.slnx --configuration Release --no-restore` and
  `dotnet test ./UniPM.slnx --configuration Release --no-build --verbosity normal --logger trx --blame-hang-timeout 5m`.
- Web CI [run 37105925857](https://github.com/lucifron28/UniPM/actions/runs/37105925857)
  passed at the tagged commit. It ran formatting,
  lint, type, API contract and generated-client checks, unit tests, coverage,
  build, and browser tests from `.github/workflows/web-ci.yml`.
- The prior local retirement integration execution is recorded separately in
  [TEST-054](TEST-054-maintenance-history-rag-retirement-integration.md) at
  `7cbb09280e3ac168ef46846cf4df0ac4253169e2`. That record includes its exact
  commands and local SQL Server, HTTP, EF, and backend results. This baseline
  record does not relabel that run as a fresh execution at the tag.

## Limits

This record makes no new claim about physical-device acceptance, IIS
deployment, adviser or GSD acceptance, or real-provider quality. The tag and CI
results identify a software test baseline only. The separate NLA scope is
documented in [PLAN-NLA-001](../../planning/schema-constrained-nla.md).
