---
id: TEST-136
type: test-run
title: NLA progressive clarification focused web verification
status: executed
recordedAtUtc: 2026-10-22T15:01:13Z
testedCommit: "bdc67c220305e44e78a28155eb8a3d99127a055c plus uncommitted web working patch; see source hashes"
sourceBranch: feature/nla-guided-interpretation
evidenceLevel: locally-executed
---

# NLA progressive clarification focused web verification

This record covers the initial focused Vitest run, its correction rerun, and
one user-authorized follow-up retry. The base `HEAD` for all runs was
`bdc67c220305e44e78a28155eb8a3d99127a055c`; the tested UI and test files also
contained uncommitted working changes. Their SHA-256 identities at the passing
retry were:

- `web/src/features/reports/pm-analytics-v3.tsx`:
  `71EF827E96DB0C37776DDA32453AB808482E803F0E043BE3FEB88A1C0F6B0556`
- `web/src/features/reports/pm-analytics-v3.test.tsx`:
  `D7A6BCB560E773E83BC1580263A4D07627BB12926507FA8935EA6CFD1619DDB0`

The passing working patch was subsequently committed as
`814ce91b15cf1b2dd29cf775d12d8c130f4fd832`.

## Executed checks

Both commands ran from `web/` with the bundled Node 22 runtime directory
prepended to `PATH`:

```powershell
$env:PATH = 'C:\Users\Ron\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin;' + $env:PATH
New-Item -ItemType Directory -Force -Path ..\artifacts\nla-progressive-clarification | Out-Null
npm exec -- vitest run src/features/reports/pm-analytics-v3.test.tsx 2>&1 | Tee-Object ..\artifacts\nla-progressive-clarification\vitest-focused.log
$testExitCode = $LASTEXITCODE
exit $testExitCode
```

The first run exited 1: 16 passed, 2 failed of 18. One failure was an old
expectation that the unsafe partial-plan response would show raw interpreter
detail; the inventory case attempted to expand report options before the
clarification heading was ready. After those test-only corrections, the same
file was rerun:

```powershell
$env:PATH = 'C:\Users\Ron\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin;' + $env:PATH
npm exec -- vitest run src/features/reports/pm-analytics-v3.test.tsx 2>&1 | Tee-Object ..\artifacts\nla-progressive-clarification\vitest-focused-rerun.log
$testExitCode = $LASTEXITCODE
exit $testExitCode
```

That correction rerun exited 1: 17 passed, 1 failed of 18, in 5.10 seconds.
The sequential measure-then-period clarification test passed, including
Back/Change value retention and a single interpretation call. The inventory
case timed out waiting for `Registered assets` after attempting to open
`More report options`.

Source tracing confirmed the test race: interpreting an `assetInventory` plan
changes `selectedSubject`, clears the current catalog, and sets
`catalogLoading=true`. The catalog-loading effect clears that flag only after
the subject catalog resolves, while `More report options` is disabled during
catalog loading. The test waited for the clarification heading, which appears
before catalog readiness. It now waits for the `assetInventory` catalog request
and an enabled options button, then asserts `aria-expanded=true` before checking
the `Registered assets` choice.

The user-authorized retry ran the same test file and exited 0: 18 passed, 0
failed, in 5.76 seconds.

```powershell
$env:PATH = 'C:\Users\Ron\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin;' + $env:PATH
npm exec -- vitest run src/features/reports/pm-analytics-v3.test.tsx 2>&1 | Tee-Object ..\artifacts\nla-progressive-clarification\vitest-authorized-retry.log
$testExitCode = $LASTEXITCODE
exit $testExitCode
```

- First-run output:
  [`artifacts/nla-progressive-clarification/vitest-focused.log`](../../../artifacts/nla-progressive-clarification/vitest-focused.log)
- Correction-rerun output:
  [`artifacts/nla-progressive-clarification/vitest-focused-rerun.log`](../../../artifacts/nla-progressive-clarification/vitest-focused-rerun.log)
- Passing retry output:
  [`artifacts/nla-progressive-clarification/vitest-authorized-retry.log`](../../../artifacts/nla-progressive-clarification/vitest-authorized-retry.log)

The tests use mocked API responses; no live provider or database calls were
made. No typecheck, build, lint, broader test suite, or browser test was run.
