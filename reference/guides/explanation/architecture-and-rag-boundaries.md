# UniPM Architecture and Historical RAG Boundaries

UniPM's current PMIS validation baseline is a preventive-maintenance system.
The system of record is the ASP.NET Core API and SQL Server. The preserved
maintenance-history retrieval/RAG implementation is historical and inactive,
not published by the current runtime. Its documentation describes prior
controlled development work, not an active product capability.

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

## Historical Review Contract Versus Planned Analysis

The historical `POST /api/v1/maintenance-review` contract was an authenticated,
source-bounded review/summarization path. It accepted a finding and target asset,
retrieved related acknowledged evidence, and could return an optional cited
summary. The current runtime does not publish this endpoint or its generated
client operation.

The planned RAG-assisted inspection-history analysis capability is a separate
post-validation direction pending professor/adviser confirmation. If it is
approved later, SQL and deterministic application code must calculate its
authoritative counts, denominators, percentages, recurrence intervals,
timelines, and groupings. Planned analyses include recurring findings,
condition frequencies, time comparisons, cross-asset patterns, location and
category distributions, and single-asset timelines.

RAG will retrieve the exact acknowledged records supporting computed facts.
Optional generation may explain only the computed result model and displayed
sources. Authorized personnel must verify and interpret the result. A language
model must not calculate authoritative statistics, diagnose equipment, infer
causes, approve actions, or mutate records.

Every future analysis output is expected to include:

- query scope and date range;
- computed facts;
- RAG-assisted interpretation;
- supporting acknowledged source records and locators;
- limitations and no-diagnosis wording.

The historical/planned capability is documented in
[`reference/planning/rag-assisted-inspection-history-analysis.md`](../../planning/rag-assisted-inspection-history-analysis.md)
and must not be described as an existing endpoint or active product capability.

## Privacy and Evidence Boundaries

The MVP sanitizer uses pattern-based token masking and pseudonymization for
emails, supported Philippine mobile numbers, and labeled IDs. It does not
generally identify arbitrary free-text personal names. Synthetic names in
fixtures do not prove protection for real institutional records.

External provider use requires fictional or separately reviewed, pre-sanitized
data and must not log raw prompts, token maps, full provider payloads, or
vectors. Returned source records remain the evidence; generated text is a
review shortcut. The system is not a chatbot or autonomous diagnostic tool.

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
