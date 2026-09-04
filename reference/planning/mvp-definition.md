# UniPM Validation Baseline Definition (PMIS-Only GSD Baseline)

## Working Title

**UniPM: A Preventive Maintenance Information System for University General
Services (PMIS-Only Validation Baseline)**

This working title describes the current validation branch
(`validation/pmis-only-gsd`). It replaces the previous RAG-inclusive working
title for validation purposes; no replacement innovation title is proposed or
approved yet.

## Purpose And Boundary

The active validation baseline is a plain preventive-maintenance information
system demonstrated to the university General Services Department so the
existing workflow, forms, reports, and remaining operational needs can be
validated before any replacement innovation is selected.

This branch is not the final evaluated innovation architecture. No replacement
innovation — AI report consolidation, schema-driven/versioned PM protocols,
Document AI/OCR, natural-language analytics, process mining, predictive
maintenance, or WMS automation — is claimed, approved, or included here.

Maintenance-history RAG was previously implemented and evaluated as controlled
development work. It is preserved in the repository as historical/inactive
infrastructure and is excluded from this validation runtime: the
maintenance-review endpoint is mapped only when explicitly enabled, committed
configuration keeps it disabled, and the published OpenAPI contract and
generated web client contain no maintenance-review operation.

The selected asset categories are fire extinguishers, fire alarm systems,
emergency lights, and water drinking stations. One preventive-maintenance form
represents one existing one-page institutional form and may contain multiple
inspection rows. The confirmed lifecycle is `Draft -> Submitted ->
Acknowledged`.

Exact final institutional form fields, revisions, and category-specific
requirements remain subject to GSD validation before any final form-model
redesign. The current digital forms demonstrate the preventive-maintenance
workflow; they are a workflow prototype, not the final institutional schema.

## Included Validation Scope

- Authentication.
- Asset registry: create, list, detail.
- QR lookup where implemented.
- Preventive-maintenance schedules: create, list, detail, statuses.
- Multi-row preventive-maintenance forms: Draft creation, inspection-row
  add/edit/delete within a Draft.
- Whole-form submission with provisional file numbers.
- Whole-form Department Head acknowledgement with name, position, and
  signature captured as signatory data through the authenticated
  GSD/skilled-worker workflow; the Department Head needs no separate UniPM
  account, and acknowledgement is not corrective-budget approval.
- Linked schedule completion after acknowledgement only.
- Acknowledged-only official maintenance history (Draft and Submitted rows are
  never official history).
- Existing deterministic list, filter, history, and status-summary behavior
  where currently implemented; the web dashboard is a placeholder and final
  GSD dashboard/reporting requirements remain subject to validation.
- Corrective-action handoff preparation as a read model ending at manual GSD
  Work Management System encoding.
- Web PMIS workflow (React) and the partner-owned mobile workflow for QR
  lookup, acknowledged-only asset history, multi-row Draft forms, whole-form
  submission, submitted-form review, and authenticated mobile acknowledgement.
- Bounded technical observability (`/metrics` opt-in only).

No AI provider, embedding call, summary call, or retrieval pass participates
in any of the above.

## Data And History Boundary

Only acknowledged form rows, together with eligible legacy inspection records
without a form, are official inspection-history evidence. Draft and Submitted
rows remain excluded from official history. Acknowledgement completes linked
schedules through the backend workflow.

Acknowledgement signatory names, positions, signatures, signature data, and
signature checksums never enter corrective-handoff data or observability
dimensions, and were never part of the historical retrieval documents,
embeddings, prompts, or deterministic analysis designs.

## Mobile Responsibility

Mobile is part of the overall UniPM system and remains a Flutter client using
the backend API. Its implementation is owned by a separate partner workstream.
The current mobile client supports authenticated access, QR scanning and
backend asset lookup, acknowledged-only official asset history, Draft form
creation and inspection-row add/update/delete, whole-form submission,
submitted-form review, and whole-form acknowledgement with signatory capture.
Release-boundary hardening enforces HTTPS configuration and no release
cleartext traffic, but physical-device, live-backend, production-signing, and
distributable-release verification remain unexecuted.

Final category-specific forms, attachments, alerts, offline synchronization,
and persistent session restoration remain separately approved or GSD-validated
work; the current mobile authentication session remains memory-only.

## Explicit Exclusions From This Branch

- Maintenance-history RAG exposure (review endpoint, semantic search,
  embeddings, AI summaries, retrieval UI).
- Any replacement innovation: AI report consolidation, schema-driven/versioned
  PM protocols, Document AI/OCR, natural-language analytics, process mining,
  predictive maintenance.
- Direct WMS integration, RMRF creation/processing, RPA, corrective-budget
  approval inside UniPM.
- Deployment completion or IIS rehearsal.
- Offline synchronization.
- OEM retrieval and real institutional-document ingestion.
- A separate vector database or native SQL vector features.
- Final enterprise RBAC redesign.
- Autonomous diagnosis, approval, or corrective decisions.
- Chatbot behavior.
- Production monitoring or production-readiness claims.

## Validation Completion Criteria

This branch is ready when:

```text
login -> assets -> QR lookup -> schedules -> draft multi-row PM form
      -> row edits -> whole-form submit -> whole-form acknowledge
      -> linked schedules Completed -> acknowledged rows in official history
      -> corrective handoff available where applicable
```

runs end to end without any AI configuration, and GSD can answer the prepared
validation questions against it. Findings are recorded; innovation selection
is deferred to a separate decision and branch.

## Historical Record: Previous Evaluated MVP Definition

The previous evaluated-MVP definition on `main` covered the preventive-
maintenance workflow plus acknowledged-history analysis, source-bounded
retrieval, optional assistive interpretation, and technical observability,
under the working title "UniPM: A Preventive Maintenance System with
RAG-Assisted Inspection History Analysis for University General Services".
That definition is preserved verbatim in git history (see `main`) and its
planning companion remains at
[`rag-assisted-inspection-history-analysis.md`](rag-assisted-inspection-history-analysis.md).
The implemented maintenance-review endpoint and retrieval infrastructure from
that phase remain in this repository, inactive, pending the post-validation
retirement decision (`refactor/retire-maintenance-history-rag`). Nothing in
this document revives or extends that work.
