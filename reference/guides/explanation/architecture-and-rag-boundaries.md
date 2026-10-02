# UniPM Architecture and Historical RAG Boundaries

UniPM's current PMIS validation baseline is a preventive-maintenance system.
The system of record is the ASP.NET Core API and SQL Server. Maintenance-history RAG has been retired. Its review endpoint, summary
provider, maintenance retrieval/fusion, projection, rebuild commands,
benchmark, and experiment runners are removed.

Historical migrations, API descriptions, ADRs, experiments, and verification
records remain as evidence of prior work. The separate fictional
ReferenceDocument foundation, Full-Text Search, section embeddings, and shared
provider-neutral embedding components remain.

Schema-constrained natural-language analytics remains a planned post-validation
direction, pending professor/adviser confirmation. It is not implemented or
approved for this branch; any implementation requires a separate approved task
and branch after GSD validation.

The historical pipeline sections below describe prior controlled work, not
available code or an active roadmap.

## Proposed deployment architecture

The following diagram describes the proposed target architecture. It is not a
record of a completed IIS deployment.

```text
React web application / Flutter mobile application
                    |
                  HTTPS
                    |
             ASP.NET Core API
               hosted on IIS
                    |
                 EF Core
                    |
Native Windows SQL Server 2019 + Full-Text Search
```

Web and mobile clients call the API only. They do not access the database,
embedding provider, or summary provider directly. Native Windows SQL Server 2019
with Full-Text Search and compatibility level `150` is the minimum supported
database platform. Docker remains optional development tooling, not a required
production component.

## Preventive-Maintenance Records and Eligibility

One digital form represents one existing one-page form and contains multiple
inspection rows. The lifecycle is:

```text
Draft -> Submitted -> Acknowledged
```

Draft and Submitted rows are workflow data, not official maintenance-history
evidence. Acknowledged rows become eligible for acknowledged-only official
history. Legacy inspections without a form remain eligible for continuity.

Field-work completion is recorded in `InspectionRecord.CompletedAt` and completes
linked schedules before form submission. Whole-form acknowledgement records
receipt/noting, does not alter execution or compliance timestamps, does not
complete schedules, and makes completed rows eligible for acknowledged-only
official history. Signatory names, positions, signatures, signature data, and
checksums are deliberately excluded from preserved search documents, embeddings,
prompts, and corrective-handoff responses.

Corrective-action handoff preparation stops at a source-traceable read model for
GSD manual follow-up. UniPM does not create, process, approve, monitor, or track
RMRFs and does not integrate directly with the external Work Management System.

## Historical Retrieval Pipeline

The preserved maintenance-review implementation followed this bounded shape:

```text
finding
  -> lexical and semantic retrieval
  -> bounded candidate selection
  -> inspectable RRF fusion
  -> source selection and context tiers
  -> request-scoped sanitization
  -> optional source-bounded summary
  -> source display and human verification
```

The lexical channel uses SQL Server Full-Text Search over the persisted
`MaintenanceSearchDocument.SearchText` projection. The semantic channel uses
versioned serialized document embeddings. SQL Server filters a bounded candidate
set; the ASP.NET Core backend calculates cosine similarity in memory. RRF
combines eligible lexical and semantic ranks using the implemented deterministic
configuration. Query vectors are transient and are never persisted.

The preserved semantic channel had an optional provider. When embeddings were
unavailable, that review path reported degradation and used lexical retrieval.
Core preventive-maintenance workflows do not depend on embeddings or an LLM.

## Historical Review Contract and Unimplemented Analysis Proposal

The historical `POST /api/v1/maintenance-review` contract was an authenticated,
source-bounded review/summarization path. It accepted a finding and target asset,
retrieved related acknowledged evidence, and could return an optional cited
summary. The current runtime does not publish this endpoint or its generated
client operation.

RAG-assisted inspection-history analysis was proposed as a possible
post-validation direction but was never implemented. It is not an active
direction or pending approval. The preserved design described authoritative
counts, denominators, percentages, recurrence intervals, timelines, and
groupings calculated by SQL and deterministic application code. Proposed
analyses included recurring findings, condition frequencies, time comparisons,
cross-asset patterns, location and category distributions, and single-asset
timelines.

The proposal expected retrieval to return acknowledged records supporting
computed facts, with optional generation explaining only the computed result
and displayed sources. Authorized personnel would verify and interpret any
result. A language model would not calculate authoritative statistics,
diagnose equipment, infer causes, approve actions, or mutate records.

The former proposal expected any analysis output to include:

- query scope and date range;
- computed facts;
- RAG-assisted interpretation;
- supporting acknowledged source records and locators;
- limitations and no-diagnosis wording.

The historical proposal is documented in
[`reference/planning/rag-assisted-inspection-history-analysis.md`](../../planning/rag-assisted-inspection-history-analysis.md)
and is not an existing endpoint or active product capability.

## Privacy and Evidence Boundaries

The retired MVP sanitizer used pattern-based token masking and pseudonymization
for emails, supported Philippine mobile numbers, and labeled IDs. It did not
generally identify arbitrary free-text personal names. Synthetic names in
fixtures did not prove protection for real institutional records.

Any future separately approved external-provider use requires fictional or
reviewed, pre-sanitized data and must not log raw prompts, token maps, full
provider payloads, or vectors. In the former review flow, source records were
the evidence and generated text was a review shortcut. UniPM is not a chatbot
or autonomous diagnostic tool.

## Operational Limits

- IIS deployment readiness and production workload testing remain unverified.
- SQL Server 2019 compatibility evidence is development verification, not a
  production deployment claim.
- Institutional CPMP/checklist/form/SOP authorization and ingestion are pending.
- OEM retrieval is excluded from the evaluated MVP.
- Final RBAC, audit persistence, official location lists, and schedule policy
  remain deferred.
- Mobile offline synchronization is deferred; its persistence and synchronization
  architecture remains undecided.
