# npm dependency advisory triage

## Evidence and classification

The 2026-11-13 npm audit of the source lockfile at application commit `53afe5708ca640b6fe7618589cbf3be1a1de6ae3` reported 43 vulnerable package entries: 13 high and 30 moderate. A package-by-package comparison with that commit's `web/package-lock.json` classified 3 entries as runtime dependencies (2 high, 1 moderate) and 40 as development dependencies (11 high, 29 moderate). The original report is preserved as [npm-audit-before-2026-11-13.json](npm-audit-before-2026-11-13.json).

After compatible lockfile updates, the full audit reported 10 moderate vulnerable package entries and no high or critical findings. All 10 remaining entries are marked `dev: true` in the current lockfile. The production-only `npm audit --omit=dev` report has zero findings. The full and production reports are preserved as [npm-audit-full-after-2026-11-13.json](npm-audit-full-after-2026-11-13.json) and [npm-audit-production-after-2026-11-13.json](npm-audit-production-after-2026-11-13.json).

## Applied remediations

| Dependency group | Scope | Before | After | Action |
|---|---|---:|---:|---|
| `zod` | Runtime/transitive | 4.4.3 | 4.6.7 | Updated beyond the vulnerable range. |
| `follow-redirects` | Runtime/transitive | 1.16.0 | 1.16.3 | Updated to a patched compatible release. |
| `seroval` | Runtime/transitive | 1.6.9 | 1.8.1 | Updated beyond the vulnerable range. |
| `orval` and affected generator packages | Development | 8.22.0 | 8.34.0 | Updated within the existing 8.x major; regenerated the API client and checked its contract. |
| `vitest`, `@vitest/coverage-v8` and compatible transitives | Development | 4.1.10 | 4.1.12 | Applied patch updates and reran the frontend suite. |
| Other affected development transitives | Development | Vulnerable lock entries | Compatible patched lock entries | Updated the lockfile without changing business functionality or NLA interpretation/reporting. Examples include compatible updates to `postcss`, `vite`, `undici`, `fast-uri`, `js-yaml`, and `source-map-js`; vulnerable Orval and Vitest entries were removed from the resulting audit graph. |

One transitive development-only major-version resolution is present: the updated Scalar tooling graph requires `nanoid` 5.1.16, while PostCSS now uses its own patched `nanoid` 3.3.20 entry. Neither is a direct application dependency; both lockfile paths are development-only. The application's dependency manifest was not given an unrelated major-version upgrade.

The lockfile remediation is commit `77e23526c9ef906c6157bfe70d0c45769e7d029b`. It installed successfully with Node.js 22.23.5/npm 10.9.9; API client check, 278 React tests across 32 files, and production build/typecheck passed.

## Remaining development-tool findings

| Package entries | Severity | Classification | Triage and disposition |
|---|---|---|---|
| `eslint`, `@eslint/js`, `@eslint/config-array`, `minimatch` | Moderate | Development only | The `minimatch` advisory affects versions below 11.0.1; current `@eslint/config-array` 0.23.5 requires `minimatch ^10.2.4`. npm proposes ESLint/`@eslint/js` 9.39.5, which would downgrade the checked-in ESLint 10 toolchain. Deferred pending a compatible lint-toolchain change. |
| `typescript-eslint`, `@typescript-eslint/eslint-plugin`, `@typescript-eslint/parser`, `@typescript-eslint/type-utils`, `@typescript-eslint/typescript-estree`, `@typescript-eslint/utils` | Moderate | Development only | npm proposes `typescript-eslint` 8.36.0, below the locked 8.72.2 and declared `^8.62.0` range. Deferred pending compatibility review rather than changing the declared toolchain range solely to clear audit output. |

The 10 remaining entries are npm affected-package entries, not 10 independently confirmed exploit paths. No unrelated direct dependency major-version upgrade was applied.

## Verification and reproduction

From `web/` with Node 22:

```powershell
npm ci
npm audit --json > npm-audit-full.json
npm audit --omit=dev --json > npm-audit-production.json
npm run api:check
npm run test:run
npm run build
```

The preserved reports describe the lockfile as audited on 2026-11-13. Registry advisories can change over time, so a later audit may differ. No credentials or private test data are included in the reports.
