---
id: EVIDENCE-INDEX
type: index
title: UniPM engineering evidence index
status: reviewed
evidenceLevel: source-inspected
---

# Engineering Evidence Index

This index lists committed engineering evidence. Claims are bounded by the
record's evidence level and tested/source commit.

## Records

| ID | Type | Title | Status | Evidence level | Tested/source commit | Record | Note |
|---|---|---|---|---|---|---|---|
| IMP-001 | implementation | Synthetic maintenance dataset and Development seeder | reviewed | source-inspected | `00e5401` | [record](implementation/IMP-001-synthetic-maintenance-dataset.md) | Fictional deterministic fixture and scoped seed/reset behavior. |
| IMP-002 | implementation | Versioned maintenance issue lexicon | reviewed | source-inspected | `00e5401` | [record](implementation/IMP-002-maintenance-issue-lexicon.md) | Deterministic category-bounded normalization; no accuracy baseline claimed. |
| IMP-003 | implementation | SQL Server lexical Full-Text retrieval | reviewed | source-inspected | `00e5401` | [record](implementation/IMP-003-lexical-full-text-retrieval.md) | Internal SQL Server FTS channel over the rebuildable projection. |
| IMP-004 | implementation | Provider-neutral semantic retrieval | reviewed | source-inspected | `00e5401` | [record](implementation/IMP-004-semantic-retrieval.md) | Provider abstraction and persisted document embeddings. |
| IMP-005 | implementation | Reproducible retrieval benchmark | reviewed | source-inspected | `00e5401` | [record](implementation/IMP-005-retrieval-benchmark.md) | Test-only synthetic lexical/semantic evaluation tool. |
| ADR-001 | decision | Use SQL Server Full-Text Search for lexical retrieval | reviewed | source-inspected | `00e5401` | [record](decisions/ADR-001-sql-server-full-text-retrieval.md) | Stack-constrained architecture decision. |
| ADR-002 | decision | Keep semantic embeddings behind a provider-neutral service | reviewed | source-inspected | `00e5401` | [record](decisions/ADR-002-provider-neutral-embeddings.md) | Provider, privacy, cost, and degradation boundaries. |
| TEST-001 | test-run | Current backend verification baseline | executed | locally-executed | `899ea5e` | [record](test-runs/TEST-001-current-backend-baseline.md) | Clean-worktree restore/build/full tests, SQL scope, and lexical benchmark executed. |
| EXP-001 | experiment | Lexical SQL Server Full-Text synthetic baseline | reviewed | locally-executed | `899ea5e` | [record](experiments/EXP-001-lexical-fts-baseline.md) | Immutable 24-query lexical baseline with sanitized reports. |
| IMP-006 | implementation | Optional OpenTelemetry metrics and local system-health monitoring | reviewed | source-inspected | `c4fc0df` | [record](implementation/IMP-006-observability-metrics.md) | Opt-in technical metrics and optional local Prometheus/Grafana infrastructure. |
| ADR-003 | decision | Use optional OpenTelemetry, Prometheus, and Grafana for technical health metrics | reviewed | source-inspected | `c4fc0df` | [record](decisions/ADR-003-prometheus-grafana-observability.md) | Low-cardinality local monitoring boundary; tracing, logs, and alerting deferred. |
| TEST-002 | test-run | Observability metrics and local monitoring baseline | executed | locally-executed | `6691f048c9d0` | [record](test-runs/TEST-002-observability-baseline.md) | Backend and local Compose observability proof with sanitized artifacts and hashes. |
| IMP-007 | implementation | Internal reciprocal-rank fusion retrieval | reviewed | source-inspected | `58668f9` | [record](implementation/IMP-007-reciprocal-rank-fusion.md) | Bounded deterministic RRF orchestration with explicit semantic degradation. |
| ADR-004 | decision | Use Reciprocal Rank Fusion for internal retrieval combination | reviewed | source-inspected | `58668f9` | [record](decisions/ADR-004-reciprocal-rank-fusion.md) | Avoids incompatible raw-score combination and keeps review-layer policy separate. |
| TEST-003 | test-run | Retrieval fusion implementation baseline | superseded | locally-executed | `58668f9` | [record](test-runs/TEST-003-retrieval-fusion-baseline.md) | Historical fusion baseline superseded by TEST-004 after the semantic failure and benchmark metadata correction. |
| TEST-004 | test-run | Retrieval fusion correction verification | executed | locally-executed | `4dffb64` | [record](test-runs/TEST-004-retrieval-fusion-correction.md) | Corrected Release restore/build/tests and evidence capture; SQL/provider/fused quality runs not executed. |
| IMP-008 | implementation | Bounded maintenance review and source-returning summary path | reviewed | source-inspected | `079240d` | [record](implementation/IMP-008-bounded-maintenance-review.md) | Development-only two-pass review orchestration with deterministic source tiers, sanitization, optional summary, and no persistence. |
| ADR-005 | decision | Use conservative source selection and summary degradation | reviewed | source-inspected | `079240d` | [record](decisions/ADR-005-source-selection-and-summary-degradation.md) | Separates evidence status from AI status and preserves source records when summary generation is unavailable. |
| TEST-005 | test-run | Maintenance review baseline | superseded | locally-executed | `079240d` | [record](test-runs/TEST-005-maintenance-review-baseline.md) | Historical baseline superseded by TEST-006 after the citation, cancellation, prompt-safety, and verification-harness corrections. |
| TEST-006 | test-run | Maintenance review correction verification | superseded | locally-executed | `7cf0687` | [record](test-runs/TEST-006-maintenance-review-correction.md) | Historical verification superseded by TEST-007 after sanitized retrieval, bounded prompts, explicit migration, and fresh-volume corrections. |
| TEST-007 | test-run | Maintenance review fresh migration verification | superseded | locally-executed | `847495d` | [record](test-runs/TEST-007-maintenance-review-correction.md) | Historical fresh-volume verification superseded by TEST-008 after lexical-safe, issue-first review-query correction. |
| TEST-008 | test-run | Maintenance review lexical query correction verification | executed | locally-executed | `bb8d307` | [record](test-runs/TEST-008-maintenance-review-lexical-query-correction.md) | Fresh SQL Server volume, explicit migration, seed, rebuild, and degraded source-only lexical review after the query correction; real summary and semantic quality remain unclaimed. |
| IMP-009 | implementation | Identity, JWT, and coarse authorization scaffolding | reviewed | source-inspected | `42d7324` | [record](implementation/IMP-009-authentication-and-role-scaffolding.md) | Guid Identity persistence, JWT login/current user, Development accounts, and provisional write policies. |
| ADR-006 | decision | Use IdentityCore, JWT, and coarse operational policies | reviewed | source-inspected | `42d7324` | [record](decisions/ADR-006-identity-jwt-and-coarse-authorization.md) | Keeps technical Admin separate from provisional maintenance authority and defers final RBAC. |
| TEST-009 | test-run | Authentication and role-policy baseline | superseded | locally-executed | `205c1ac` | [record](test-runs/TEST-009-authentication-baseline.md) | Historical authentication baseline superseded by TEST-010 after Inspector identity-binding correction. |
| TEST-010 | test-run | Authentication inspection identity-binding verification | executed | locally-executed | `37981a4` | [record](test-runs/TEST-010-authentication-identity-binding.md) | Fresh SQL Server migration, Development identities, real JWT policy checks, protected writes, and corrected Inspector identity binding. |
| EXP-002 | experiment | DeepSeek V4 source-bounded summary baseline | executed | real-provider-executed | `929d99f` | [record](experiments/EXP-002-deepseek-v4-summary-baseline.md) | Fictional 12-case DeepSeek run with developer-approved source-faithfulness ratings; not production-ready. |
| IMP-010 | implementation | Database-enforced inspection submission integrity | reviewed | source-inspected | `6518a2d` | [record](implementation/IMP-010-inspection-submission-integrity.md) | Unique schedule inspection index, duplicate-data migration preflight, and narrow conflict handling. |
| TEST-011 | test-run | Inspection submission integrity baseline | executed | locally-executed | `6518a2d` | [record](test-runs/TEST-011-inspection-submission-integrity.md) | Release restore/build/tests passed; SQL Server integrity tests were skipped because no test connection was configured. |
| TEST-012 | test-run | Inspection submission integrity final verification | executed | locally-executed | `6e8d4b8` | [record](test-runs/TEST-012-inspection-submission-integrity-final.md) | Final non-SQL Release build/tests passed with explicit unknown-schedule coverage. |
| TEST-013 | test-run | Inspection submission integrity SQL Server verification | executed | locally-executed | `6e8d4b8` | [record](test-runs/TEST-013-inspection-submission-integrity-sql-server.md) | Focused SQL Server migration, unique-index, and concurrent endpoint tests passed; a separate existing benchmark assertion blocks the SQL-enabled full suite. |
| TEST-014 | test-run | SQL Server retrieval benchmark warning assertion verification | executed | locally-executed | `795bc3f` | [record](test-runs/TEST-014-retrieval-benchmark-warning-assertion.md) | Test-only assertion correction; direct and complete SQL-enabled suites passed without changing benchmark behavior. |
| IMP-011 | implementation | Rotating browser refresh sessions | reviewed | source-inspected | `fa75e16` | [record](implementation/IMP-011-rotating-refresh-sessions.md) | Short-lived JWTs, opaque rotating refresh families, and exact-origin browser boundary. |
| ADR-007 | decision | Use rotating opaque refresh sessions for the browser MVP | reviewed | source-inspected | `fa75e16` | [record](decisions/ADR-007-browser-refresh-session-boundary.md) | Hash-only persistence, same-site cookies, family rotation, and no separate auth server. |
| TEST-015 | test-run | Refresh-session ordinary verification | executed | locally-executed | `fa75e16` | [record](test-runs/TEST-015-refresh-session-verification.md) | Release and focused SQL verification passed, including forced rotation-failure cleanup. |
| IMP-012 | implementation | React web foundation | reviewed | source-inspected | `6d1fd52` | [record](implementation/IMP-012-react-web-foundation.md) | Offline auth-contract gate plus generated-client drift protection. |
| ADR-008 | decision | Use a generated API client and in-memory browser token boundary | reviewed | source-inspected | `6d1fd52` | [record](decisions/ADR-008-browser-foundation-api-boundary.md) | Offline auth contract validation with no browser-readable refresh token. |
| TEST-016 | test-run | React web foundation verification | executed | locally-executed | `6d1fd52` | [record](test-runs/TEST-016-web-foundation-verification.md) | Offline auth-contract gate, 31 unit tests, and 5 Playwright route-smoke tests passed. |
| IMP-013 | implementation | React browser authentication integration | reviewed | source-inspected | `4ffbdf8` | [record](implementation/IMP-013-web-browser-authentication.md) | Memory-only access token, refresh-cookie restoration, bounded replay, Query-owned current user, and protected routes. |
| ADR-009 | decision | Coordinate memory-only browser sessions with bounded refresh replay | reviewed | source-inspected | `4ffbdf8` | [record](decisions/ADR-009-browser-session-restoration-and-refresh-retry.md) | Single-flight refresh, one replay, generation races, and complete Query clearing. |
| TEST-017 | test-run | React browser authentication verification | executed | locally-executed | `4ffbdf8` | [record](test-runs/TEST-017-web-authentication-verification.md) | 58 Vitest and 10 Chromium tests passed with an unchanged 280-pass backend Release baseline. |

## Pending Evidence

- Real semantic model-quality baseline: pending a configured real provider and
  a retained real-provider benchmark run.
- Lexicon normalization accuracy baseline: pending an independent labeled
  dataset and executable precision/recall/F1 evaluator.
- Fused retrieval quality baseline: pending a configured real provider and an
  executed fused benchmark; TEST-004 contains the latest orchestration evidence
  only.
- Independent generated-summary faithfulness evaluation: pending a labeled
  evaluation set and review protocol.
- Production authentication deployment verification: pending configured IIS
  hosting, institutional secret management, and final RBAC decisions.
- IIS deployment verification: pending a configured Windows Server deployment.
