# Current Priorities - Active Work Items

Read `AGENTS.md` first. These tasks assume its architecture, privacy, and
scope rules.

## Current Status

- Backend baseline: done. The solution restores, builds, and its backend tests
  pass.
- Initial migration: done for `Asset`, `PreventiveMaintenanceSchedule`, and
  `InspectionRecord`.
- Asset reads: done. Create, list, detail, and QR lookup are available.
- Schedule reads: done. Create, list, and detail are available.
- Inspection history: done.
- Inspection list/detail: pending.
- Synthetic fixture and development seeder: in progress.
- Maintenance issue lexicon: next after seed work.
- FTS retrieval, semantic retrieval, benchmark, fusion, and maintenance review:
  pending.

## Risk-First Order

1. Confirm the backend baseline.
2. Add the synthetic fixture and development-only seeder.
3. Complete inspection list/detail endpoints.
4. Implement the maintenance issue lexicon.
5. Add a maintenance search-document projection.
6. Implement lexical and semantic retrieval separately.
7. Benchmark and fuse retrieval results.
8. Add source-bounded summarization.
9. Add authentication scaffolding.

## Current Constraints

- The maintenance-history feature is bounded support, not a chatbot or
  autonomous diagnostic system.
- Retrieval evaluation labels must remain test-only and must never be persisted
  or used as searchable, embedding, prompt, or API-response content.
- Semantic retrieval is a required target channel but is operationally
  degradable. When embeddings are unavailable, maintenance review may use SQL,
  lexicon normalization, and FTS fallback while explicitly reporting lexical
  fallback.
- Core preventive-maintenance workflows must never depend on embeddings or an
  LLM.
- The four physical forms are only available as blank Page 1 references.
  Page 2, completed samples, acknowledgement, and RMRF rules remain
  provisional pending GSD clarification.

## Next Branches

- `chore/synthetic-maintenance-seed`
- `feat/api-inspection-detail-endpoints`
- `feat/retrieval-maintenance-issue-lexicon`
