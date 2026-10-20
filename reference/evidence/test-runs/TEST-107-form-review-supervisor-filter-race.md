---
id: TEST-107
type: test-run
title: Form review supervisor search and assignment filter
status: executed
recordedAtUtc: 2026-10-18T11:25:45Z
testedCommit: d474cb4725ffce031a7c903cf2201d7afe2427ab
testedFocusedSourceManifestSha256: f0b4f527e0a1031e1f7b4f7286a8422c37c6c40e59990c448f872336ff19a005
sourceBranch: feature/staging-workflow-followups
evidenceLevel: locally-executed
---

# Form review supervisor search and assignment filter

## Execution identity

The focused test ran against the uncommitted working tree on branch
`feature/staging-workflow-followups`, based on HEAD
`d474cb4725ffce031a7c903cf2201d7afe2427ab`. It used Node `v24.15.0` and npm
`11.12.1`. The web package declares Node `>=22 <23`, so this records the
observed local test result but does not establish a supported-runtime test run.

The focused source manifest hashes these files in the listed order. For each
file, its UTF-8 relative path, a zero byte, the file bytes, and a zero byte are
fed to SHA-256:

- `web/src/features/preventive-maintenance-forms/form-registry.tsx`
- `web/src/features/preventive-maintenance-forms/preventive-maintenance-form-review.test.tsx`
- `web/src/features/schedules/schedule-queries.ts`
- `web/src/api/generated/endpoints.ts`
- `web/src/api/generated/models/scheduleAssigneeOption.ts`
- `web/src/components/ui/searchable-option-input.tsx`
- `web/vite.config.ts`

The resulting SHA-256 is
`f0b4f527e0a1031e1f7b4f7286a8422c37c6c40e59990c448f872336ff19a005`.

## Command and result

From `web/`, the command was:

```text
npm run test:run -- src/features/preventive-maintenance-forms/preventive-maintenance-form-review.test.tsx -t "filters forms by current supervisor account and shows assignments separately from inspection work"
```

**Result:** exit code 0; 1 passed; 19 unrelated tests skipped; Vitest duration
17.29 seconds.

The test covered partial-name suggestions, disambiguation of duplicate
supervisor names, filtering by the selected account, separate display of the
current supervisor and inspector assignments, clearing filters, and filtering
Unassigned rows.

## Finding and correction

The test exposed a UI synchronization race. When supervisor suggestions
resolved after typing began, an effect for the selected supervisor filter
reset the input to an empty string whenever no supervisor was selected. The
effect now leaves in-progress text alone when the filter is unset. It still
populates the field for an active account filter or the Unassigned option;
the explicit Clear action clears it.

No production API, authorization, assignment, or form-lifecycle behavior
changed in this correction.

The normalized local result note is stored at
`artifacts/web-followup-visual-review-2026-10-18/form-supervisor-filter/vitest-supervisor-filter-final-summary.txt`.
It is a normalized summary, not verbatim runner stdout.

## Not verified

- The full Form review test file, full web unit suite, full Playwright suite,
  and exact-head GitHub CI.
- A fresh Form review browser screenshot or deployed-app acceptance.
- Live API, database, Azure, or production behavior.
- A rerun under the package-supported Node 22 runtime.
