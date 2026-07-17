# Maintenance Review API v0.1

## Safety Boundary

`POST /api/v1/maintenance-review` is an assistive, source-bounded review path.
It is disabled by default through `MaintenanceReview:Enabled=false`. When
enabled in any environment, JWT bearer authentication and the
`CanReviewMaintenanceHistory` policy are required. The policy currently allows
`GSD`, `Supervisor`, and `DepartmentHead`; `Admin` alone is not sufficient.
No review-history table, prompt storage, summary storage, or token-map storage
is part of this contract.

## Request

```json
{
  "assetId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "findingText": "mahina ang pressure, for refill",
  "generateSummary": true
}
```

`assetId` is required. `findingText` is compatibility-Unicode and whitespace
normalized and is limited by `MaintenanceReview:MaxFindingCharacters` (2000 by
default). `generateSummary` defaults to `true`. Unknown JSON properties are
rejected.

## Response

The response returns the target asset, normalized `findingIssueKeys`,
`evidenceStatus`, `recurringSameAssetPatternSupported`, retrieval metadata,
`summaryStatus`, an optional `summary`, bounded `limitations`, and the original
selected source inspection records. Source records include stable labels such as
`SRC-1`, issue keys, deterministic matched reasons, context tier, original
remarks/actions, and fused component trace fields.

The response never includes provider endpoints, API keys, prompts, query
embeddings, provider payloads, or sanitizer token maps.

## Evidence Status

- `same_asset_history_found`: at least one selected source belongs to the target
  asset.
- `similar_asset_fallback`: no same-asset source was selected and a different
  asset matched an issue key plus exact building, department, or location.
- `same_category_fallback`: no same-asset or contextual source was selected and
  a different asset matched an issue key in the canonical category.
- `insufficient_evidence`: no candidate survived the conservative rules.

When evidence is insufficient, no summary provider call is made and the summary
status is `not_generated_insufficient_evidence`.

## Source Selection

At most five sources are returned by default, after at most two fused passes.
Candidates are ordered by these tiers, then issue overlap count, exact context
matches, FusionScore, inspection date, and inspection ID:

1. `same_asset_issue_match`
2. `same_asset_history`
3. `contextual_issue_match`
4. `same_category_issue_match`

Different-asset category-only records without issue-key overlap are rejected.
When the finding has no normalized issue keys, only same-asset history may be
selected. Recurrence is supported only when two selected same-asset records
share one current-finding issue key.

## Summary Status

- `generated`: provider output passed source-citation validation.
- `not_requested`: `generateSummary=false`.
- `disabled`: summary configuration is disabled.
- `provider_unavailable`: provider configuration or execution was unavailable.
- `failed`: prompt or generated content failed the bounded contract.
- `not_generated_insufficient_evidence`: no eligible evidence existed.

Summary failure never removes successfully selected source records. Semantic
degradation is reported separately in `retrievalStatus` and limitations; it
does not change the evidence status.

## Privacy And Provider Boundary

Before any external summary call, the current finding and provider-bound source
text pass through request-scoped prompt sanitization, token masking, and
pseudonymization for emails, supported Philippine mobile number formats, and
labeled employee/student/staff/personnel IDs. Tokens are in-memory only and
discarded after the request. This is pattern-based masking, not anonymization:
it does not identify arbitrary free-text personal names and does not guarantee
that every identifier is masked.

The response returns original authorized source records for human verification.
Those records are not a public or anonymized response, and authorization does
not anonymize their contents. This v0.1 contract does not guarantee runtime
prevention of unsafe provider configuration. Remote-provider use with real or
unscreened institutional text requires a separately approved privacy process or
a stronger sanitizer.

`maintenance-review-v1` is the stable prompt template. The prompt treats source
records as quoted data, requires source labels, forbids invented dates, causes,
RMRF numbers, corrective actions, personnel decisions, and outside evidence,
and includes the assistive-only disclaimer. The provider adapter is
OpenAI-compatible and provider-neutral, uses a relative path bound to the
configured host, rejects remote endpoints unless explicitly allowed, and never
logs raw request/response bodies.

Configuration keys are under `MaintenanceReview:*` and `Summary:*`; the API key
belongs only in an environment variable (`Summary__ApiKey`). The default local
path keeps embeddings and summaries disabled while still returning source
records through lexical fallback.

## Limitations

This is a synthetic, provisional MVP. It does not prove real provider quality,
infer equipment equivalence, diagnose failures, finalize
GSD acknowledgement/RMRF workflows, make corrective decisions, retrieve
external reference documents, or provide chatbot/multi-turn behavior.

Prompt sanitization is limited to the documented pattern coverage. Free-text
personal names can remain in provider-bound input, so synthetic names and
fictional fixtures must not be treated as evidence of real-name protection.
