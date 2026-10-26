# Branch integration audit

The initial branch audit ran on October 26, 2026, 03:29–03:40 UTC.
Source corrections and the final ancestry check followed. This branch audit
uses source and Git-history inspection; executed redesign checks are in TEST-164.

Origin main is `bdc07caeeb37c31ca7b524b7ef45cde5824a783c`.
The fixed simulation base is `00e5ddc0bf441fcc7e89ece6a931e47e3ee40768`.
The reviewed integration source is
`545b531b32f709df001257e9612fc5a0a677bc72` on `feature/nla-redesign-integration`.

## Recommendation

The recommended consolidation target is `feature/nla-redesign-integration`.
At tested revision `545b531`, `origin/main` and `feature/capstone-remediation`
are both ancestors. `git rev-list --left-right --count origin/main...545b531`
returned `0 81`. A fast-forward can preserve existing main history; it does
not establish remote CI, device acceptance or institutional deployment.
No merge was performed. Closing documentation commits do not change source.

Some older tips can also fast-forward the older main by ancestry, but they
omit later remediation and are not the recommended consolidation target.
Several others are already included by ancestry or exact-tree equivalence.
Remaining unique features require a selective port and verification rather
than choosing old versions of conflicted files.

## Branch tips

Refs sharing a SHA are grouped. "In main" means ancestry; "same tree" means content equivalence without ancestry. Merge-tree simulations were run against the fixed `00e5ddc` base, not the moving integration head.

| Tip and refs | Relationship to `main` | Fixed-base simulation | Assessment |
|---|---|---|---|
| `545b531` `feature/nla-redesign-integration` (local) | `main` ancestor; 81 commits ahead | Not simulated as a candidate; this is the active integration line | Recommended consolidation target; ancestry checked at tested code SHA |
| `00e5ddc` `feature/capstone-remediation` (local) | `main` ancestor; 75 commits ahead | Fixed base | Superseded by the active integration line |
| `bdc07ca` `main`, `origin/main` | Main | N/A | Baseline |
| `6437fa7` `feature/nla-guided-interpretation` (local + origin) | Not ancestor; tree exactly equals main (`50eb1224…`) | Historical merge attempt is not informative | No merge needed; squash/tree-equivalent |
| `0212565` `feature/single-local-env` (local) | Tip is ancestor of main | N/A | Already included |
| `1bc8a5e` `refactor/web-mobile-ux-audit` (local only at snapshot) | Tip is ancestor of main | N/A | Already included |
| `71cdfe5` `fix/cpmp-month-end-deadlines-compliance` (local only at snapshot) | Tip is ancestor of main | N/A | Already included |
| `88f95e5` `feature/maroon-page-headers` (local + origin) | Tip is ancestor of main | N/A | Already included |
| `ac77754` `feature/mobile-draft-form-contrast` (local only at snapshot) | Tip is ancestor of main | N/A | Already included |
| `6986d10` `origin/fix/mobile-acknowledgement-ux` | Tip is ancestor of main | N/A | Already included |
| `0c32d09` `feature/work-tracking-ui` (local) | Diverged | Conflicts in offline repository, schedule endpoints/tests, E2E, OpenAPI/generated models, and `schedule-ongoing-work.tsx` | Do not merge wholesale |
| `523312b` `feature/work-tracking-ui-review` (local + origin) | Diverged; separate from `0c32d09` | Conflicts in offline repository, NLA interpreter, acknowledgement E2E, OpenAPI/generated models, and form-review tests | Do not merge wholesale |
| `652c078` `feature/admin-demo-management` (local + origin) | Diverged | Conflicts in evidence index, photo/schedule endpoints and tests, authorization tests, OpenAPI/generated endpoints | Do not merge wholesale; see scope finding below |
| `5984cfc` `feature/deepseek-manuscript-evaluation` (local + origin) | Diverged | Conflicts in project/readme/evidence, interpreter/tests, and NLA web component | Superseded NLA lineage; do not merge wholesale |
| `6c23646` `feature/nla-deepseek-validation-fix` (local + origin) | Diverged | Same main conflict family as `5984cfc` | Superseded NLA lineage; do not merge wholesale |
| `3d7b2f0` `feature/nla-clarification-recovery` (local + origin) | `main` ancestor; 24 commits ahead | Conflicts in manuscript evaluation README and evidence index | Interpreter differs from fixed base and restores reviewed-question shortcuts; do not merge wholesale |
| `98c6ba8` `feature/nla-saved-reports` (local + origin) | `main` ancestor; 31 commits ahead | Conflicts in evaluation README/index, OpenAPI/generated endpoints, and NLA web component | Optional saved-reports feature plus older interpreter; only consider as a reviewed port |
| `a2906af` `feature/nla-saved-reports-browser-test` (local + origin) | `main` ancestor; 33 commits ahead | Same conflicts as saved-reports branch | Same optional feature and older interpreter; browser-test continuation, not an independent runtime candidate |
| `aa505a8` `feature/nla-integrated-route-verification` (local-only) | `main` ancestor; 36 commits ahead | Conflicts in evaluation README/index, OpenAPI/generated endpoints, and NLA web component | Latest old NLA line; optional saved-reports code plus historical evidence, not a wholesale merge |
| `a56930d` `experiment/nla-v3-english-evaluation` (local-only) | Diverged; 11 experiment commits | Conflicts in `.gitattributes` and evidence index | Keep as a separate experiment; do not merge its runtime/tooling into NLA V3 |
| `e367ef3` `deploy/azure-staging` (local + origin) | Diverged | Conflicts in evidence index and development seeder tests | Deployment-specific branch; do not merge wholesale to main |

Branch history confirms the earlier guided, work-tracking, and admin lines have already been represented in the integrated baseline, but branch tips are not interchangeable with the newer remediation state. In particular, the guided branch is equal by tree, not ancestry. The UX audit, CPMP deadline, and mobile contrast branches are local-only in the ref snapshot; their commits are already ancestors of main.

## Semantic findings for unique candidates

- **Work tracking (`0c32d09`, `523312b`):** the old review tip lacks the remediation's offline account-scope authorization/locking and storage-failure handling. Compared with the fixed base, its form repository also lacks the paged list request and pagination loop in `mobile/lib/features/preventive_maintenance/preventive_maintenance_repository.dart`. Selecting these older file versions would regress account isolation, storage recovery, and pagination; Git's conflicted merge does not by itself dictate that resolution. Any desired UI refinement needs a selective port against the final integration branch.
- **Admin (`652c078`):** this branch adds Admin to operational role grants and its tests expect Admin operational access. That conflicts with the supplied boundary that Admin is technical, not an operational super-role. The `/admin` operational-management group protected by `CanManageAdminManagement` already exists at the fixed base as well, so this branch is not a safe way to resolve the current policy question. Review account and demo administration endpoints separately; do not bring forward the branch's operational role grants. This is a scope finding for root review, not a recommendation to re-add or remove the existing endpoint group in this audit.
- **NLA V3 (`aa505a8` and its ancestors):** the interpreter is not the same as the fixed-base source. Blob IDs are `b0ce4f9a…` at `00e5ddc`, `605aadcf…` at `3d7b2f0`, `98c6ba8`, `a2906af`, and `aa505a8`; `6437fa7` and `5984cfc` use `92f39e5b…`, and `6c23646` uses `aca8ca9c…`. The older interpreter contains two `ReviewedInterpretations` exact-question entries and labels those responses `ApprovedInterpretation`, bypassing the normal default provider/parser route for those questions. It also adds `RecoverRankingClarification`, which parses the question and rewrites certain DeepSeek responses after generation. The older parser grammar would regress two later fixes at the fixed base: its ranking regex matches `most` inside "at most," causing that wording to enter ranking clarification (`least` is already guarded in "at least"); and it lacks the bounded `late … inspection(s)` measure matcher with explicit negation/coordination exclusions. Its prompt also lacks later ranking, completed-count, generic-year, and explicit inspection-date distinctions. Do not merge any of these interpreter blobs over the fixed-base version. `feature/nla-saved-reports` adds a distinct migration, owner-filtered saved-plan API, and UI that are absent at the fixed base. The code includes owner checks, revision conflict handling, and a 100-report cap. Its recorded backend verification (TEST-148) used EF InMemory and did not apply the migration to SQL Server; TEST-150 used controlled browser API responses. Treat saved reports as an optional feature requiring a scoped port and native SQL review, not a safe merge candidate.
- **Historical NLA evidence (`aa505a8`):** TEST-142 records an authorized real-provider 18-question run; TEST-151 records an integrated route run with three real DeepSeek calls. These records apply to their stated historical SHAs. TEST-151 includes an `ApprovedInterpretation` route and exact-question shortcuts that are present in its tested interpreter blob but removed from the fixed-base implementation. Preserve the records as historical evidence if needed; do not use them to describe current runtime or current evaluation quality.
- **English evaluation experiment (`a56930d`):** it adds a separate interpreter/evaluation path and a reviewed 100-case English corpus. Its README says 98 cases were approved, two corrections were made, and no benchmark case was scored or provider call authorized. This is corpus-review evidence, not model-performance evidence and not the current NLA V3 runtime. Keep it separate unless a new experiment is explicitly approved.
- **Staging (`e367ef3`):** deployment-specific seeding and tests conflict with the current seeder and evidence index. It is not a source-to-main candidate.

## Commands and limits

Read-only checks included `git for-each-ref`, `git merge-base --is-ancestor`, `git rev-list --left-right --count`, `git rev-parse '<sha>^{tree}'`, `git worktree list --porcelain`, focused `git diff`/`git show`, and `git merge-tree --write-tree 00e5ddc <tip>` for divergent candidates. A merge-tree exit of 1 means reported conflicts; a clean simulation would still not establish semantic safety. All simulation conclusions are pinned to `00e5ddc`, so they are not claimed as final-head merge simulations. The recommended integration line received the final ancestry checks above.

Twelve worktrees were listed. Dirty status was sampled as counts only; no worktree was changed and no dirty file content was inspected. No tests, provider calls, institutional data reads, or deployment actions were performed.
