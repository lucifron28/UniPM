---
id: TEST-048
type: test-run
title: Authentication browser evaluation synchronization
status: executed
recordedAtUtc: 2026-10-02T02:47:34Z
testedCommit: ea87c2b66a1194d5396c0a1b18395d4398658216
sourceBranch: refactor/web-mobile-ux-audit
evidenceLevel: locally-executed
---

# Authentication browser evaluation synchronization

The original test at `9cd825be175505de9024005df694d247d75a34b9`
passed ten traced repetitions, then failed once in 30 untraced repetitions.
The failure matched CI: `page.evaluate` failed after the cookie assertion
confirmed `session-b`.

Navigation probes showed dashboard/login/dashboard transitions. Waiting for
an idle dashboard router did not eliminate the error. Chromium protocol
capture then exposed `Promise was collected`, which Playwright's installed
Chromium adapter rewrites to the generic execution-context/navigation error.
This diagnostic finding does not establish a production authentication bug.
Temporary probes and unsuccessful synchronization drafts were removed.
Diagnostic runs used temporary patches and are not exact-commit verification.
Raw diagnostic logs remain ignored under `artifacts/auth-ci/`.

The final test starts logout/login and the last refresh synchronously, retains
their promises on `window`, and uses `page.waitForFunction` to wait for the
operations. This avoids returning the vulnerable import promise directly from
`page.evaluate`. All cookie assertions, intercepted request checks and the
three-refresh assertion remain. Production authentication, completed UX code,
timeouts and sleeps were not changed.

## Executed verification

All final checks below ran at exact commit
`ea87c2b66a1194d5396c0a1b18395d4398658216` and exited 0.
Windows used Node 22.23.3 and Chromium via Playwright. Logs are ignored under
`artifacts/auth-ci/`.

| Check | Result | Log |
|---|---|---|
| Affected test repeated after formatting and commit | 20 passed | `final-repeat.log` |
| Full Playwright suite | 43 passed, one skipped | `full-playwright.log` |
| Web Prettier check | Passed | `web-format.log` |
| ESLint with zero warnings allowed | Passed | `web-lint.log` |
| TypeScript build-mode check | Passed | `web-typecheck.log` |
| Full web unit tests | 171 passed in 23 files | `web-unit.log` |
| Production Vite build | Passed | `web-build.log` |
| Diff whitespace check against `9cd825b` | Passed | Terminal output, no errors |

The same final test logic also passed 50 repetitions before formatting and
commit, on a working patch; this is supplementary flakiness investigation,
not an exact-commit run. Its log is `polled.log`.

## Commands

```text
node22 web/node_modules/@playwright/test/cli.js test --config artifacts/ux/playwright.config.mts authentication.spec.ts --grep "a stale refresh cannot overwrite" --repeat-each 20 --workers 2 --trace off --output artifacts/auth-ci/final-repeat-results
node22 web/node_modules/@playwright/test/cli.js test --config artifacts/ux/playwright.config.mts --output artifacts/auth-ci/full-results
node22 node_modules/prettier/bin/prettier.cjs --check .
node22 node_modules/eslint/bin/eslint.js . --max-warnings 0
node22 node_modules/typescript/bin/tsc -b
node22 node_modules/vitest/vitest.mjs run
node22 node_modules/vite/bin/vite.js build
git diff --check 9cd825b..HEAD
```

Browser checks used the existing ignored runner with an isolated Vite server
on port 5180 and two Chromium workers. The generated QR screenshot was moved
into ignored artifacts. Unit runs emitted jsdom scrollTo notices. The browser
suite logged a router preload TypeError in a passing navigation test; this
unrelated console issue was not changed.

## Not verified

The live seeded schedule-assignment test was skipped because
`UNIPM_DEV_USER_PASSWORD` was not supplied. Live backend operation, other
browsers, physical devices and backend/mobile suites were not run. Browser
checks used mocked API fixtures. GitHub Web CI is verified separately on the
exact pushed head after this evidence commit. No merge is included.
