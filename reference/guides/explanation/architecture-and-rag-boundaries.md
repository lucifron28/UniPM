# UniPM Architecture and RAG Boundaries

UniPM is a preventive-maintenance system with a bounded maintenance-history
review feature. The system of record is the ASP.NET Core API and SQL Server;
retrieval and language-model behavior support human review rather than replace
maintenance judgment.

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
evidence. Acknowledged rows become eligible for official history and retrieval.
Legacy inspections without a form remain eligible for continuity.

Whole-form acknowledgement completes linked schedules and publishes the rows
through the rebuildable `MaintenanceSearchDocument` projection. Signatory names,
positions, signatures, signature data, and checksums are deliberately excluded
from search documents, embeddings, prompts, and corrective-handoff responses.

Corrective-action handoff preparation stops at a source-traceable read model for
GSD manual follow-up. UniPM does not create, process, approve, monitor, or track
RMRFs and does not integrate directly with the external Work Management System.

## Retrieval Pipeline

The implemented maintenance-review path follows this bounded shape:

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

The semantic channel is a required target channel of the architecture, but its
provider is operationally optional. When embeddings are unavailable, the review
path reports degradation and uses lexical retrieval without labeling the result
as hybrid. Core preventive-maintenance workflows do not depend on embeddings or
an LLM.

## Current Review Contract Versus Planned Analysis

`POST /api/v1/maintenance-review` is implemented as an authenticated,
source-bounded review/summarization contract. It accepts a finding and target
asset, retrieves related acknowledged evidence, and may return an optional cited
summary. It does not calculate the broader inspection-history analysis model.

The planned RAG-assisted inspection-history analysis capability is a separate
future service. SQL and deterministic application code must calculate its
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

The planned capability is documented in
[`reference/planning/rag-assisted-inspection-history-analysis.md`](../../planning/rag-assisted-inspection-history-analysis.md)
and must not be described as an existing endpoint.

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
