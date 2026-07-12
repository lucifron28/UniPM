---
id: ADR-005
type: decision
title: Use conservative source selection and summary degradation
status: reviewed
recordedAtUtc: 2026-07-12T13:25:00Z
testedCommit: 079240dce614de247b65a43e730a0bc2fda8eeac
sourceBranch: feat/retrieval-review
evidenceLevel: source-inspected
---

# Decision

Use deterministic context tiers after RRF, and keep evidence status separate
from summary status. Do not add arbitrary weighted score terms to FusionScore.

## Rationale

Context tiers make the reason a source was selected inspectable: same-asset issue
matches outrank same-asset history, which outranks contextual and then
same-category issue matches. Category fallback requires issue-key overlap so a
generic record is not presented as related evidence merely because it shares a
category. Recurrence requires two same-asset records sharing a current issue key
so one record, category similarity, and semantic similarity cannot become a
recurrence claim.

Evidence status describes what source history was found. Summary status
describes whether an optional provider produced a bounded, cited summary. This
separation prevents AI availability from changing the factual evidence result.
When summary generation fails or is unavailable, the API returns the selected
source records instead of failing successful retrieval.

The endpoint is Development-only before authentication because a source-returning
review operation must not be exposed as a production capability without an
identity and authorization boundary. Prompts and request-scoped token maps are
not persisted because they contain transformed copies of maintenance text and
temporary privacy mappings that are not part of the source-of-truth domain.

## Alternatives Rejected

- Arbitrary linear boosts to lexical or semantic scores: rejected because RRF
  rank contributions and their traceability must remain unchanged.
- Category-only fallback without issue overlap: rejected because it overstates
  relevance and weakens cold-start evidence boundaries.
- Treating summary availability as evidence availability: rejected because
  source records remain useful for human verification without AI.
- Production exposure before authentication: rejected because the temporary
  Development gate is the current safety boundary.
- Persisting prompts, summaries, or token maps: rejected because the review loop
  has no approved persistence or audit contract.
