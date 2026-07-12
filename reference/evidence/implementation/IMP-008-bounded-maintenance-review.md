---
id: IMP-008
type: implementation
title: Bounded maintenance review and source-returning summary path
status: reviewed
recordedAtUtc: 2026-07-12T13:25:00Z
testedCommit: 079240dce614de247b65a43e730a0bc2fda8eeac
sourceBranch: feat/retrieval-review
evidenceLevel: source-inspected
---

# Bounded Maintenance Review

The branch adds `POST /api/v1/maintenance-review` as a temporary
pre-authentication endpoint. It is disabled by default and fails startup when
enabled outside Development. The endpoint does not implement authentication,
role authorization, review persistence, prompt persistence, summary
persistence, token-map persistence, or autonomous maintenance decisions.

The service executes at most two bounded fused retrieval passes: one for the
target asset and one canonical-category fallback when the first pass does not
fill the configured source limit. It loads matching inspection and
`MaintenanceSearchDocument` rows through a bounded read query, verifies source
metadata, deduplicates candidates, and returns original source records.

Source selection uses four deterministic tiers:

1. same-asset issue match;
2. same-asset history;
3. contextual issue match for another asset in the same category;
4. same-category issue match.

Different-asset records without issue-key overlap are rejected. Recurrence is
supported only when two selected same-asset records share one current-finding
issue key. Evidence status is independent from summary status.

Provider-bound text passes through a request-scoped sanitizer session for email,
Philippine phone/mobile, and labeled employee/student/staff/personnel ID token
masking. `MaintenanceReviewPromptBuilder` is the only prompt constructor and
uses the versioned `maintenance-review-v1` template. Token maps and prompts stay
in memory and are never returned, logged, or persisted.

Summary generation is behind `ISummaryService` and the OpenAI-compatible adapter
uses bounded, provider-neutral HTTP contracts. Disabled, unavailable, malformed,
or uncited summary generation never removes the retrieved source records.
Semantic degradation remains explicit in retrieval metadata and limitations.

This is source-inspected implementation evidence. It does not claim a real
summary-provider run, summary quality, authenticated access, IIS deployment, or
production GSD performance.
