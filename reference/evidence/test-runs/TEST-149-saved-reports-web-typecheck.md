---
id: TEST-149
type: test-run
title: Saved reports web TypeScript check
status: executed
recordedAtUtc: 2026-10-23T13:37:08Z
testedCommit: 92815926eb1f8e9b3914a64f34e07ef277efbb9d
sourceBranch: feature/nla-saved-reports
evidenceLevel: locally-executed
---

# Saved reports web TypeScript check

The initial check at `2f5c298c86398b6b092a2e38cd83d136a4cf46bc` failed
with TS2345 in the existing clarification Back button. An indexed field could
be undefined. One focused correction checks for that field before reopening
it. The subsequent TypeScript check at the tested commit passed with exit 0
and no diagnostic output.

Initial command, from `web/`:

```powershell
node ./node_modules/typescript/bin/tsc -b --pretty false
```

Rerun command, from the repository root:

```powershell
node ./web/node_modules/typescript/bin/tsc -b web --pretty false
```

The check includes generated saved-report API types, client functions, the
My reports component and its report-page integration. It does not execute
React interactions or verify the browser-to-database flow.

Orval 8.22.0 generated the new client from saved-report definitions exported
by the TEST-148 host. Prettier encountered Windows file-open errors on some
unchanged generated models. Their original definitions were restored; no
unrelated generated model changes remain. The generated saved-report types
and endpoint additions are retained.

Raw logs under `artifacts/saved-reports-resume-2026-10-23/` include
`web-typecheck.log`, `web-typecheck-rerun.log`, client-generation logs and
formatting logs. No browser, frontend test suite, production build, SQL Server
execution or deployment was run. Backend behavior is recorded separately in
TEST-148.
