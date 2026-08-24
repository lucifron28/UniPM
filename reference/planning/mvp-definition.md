# UniPM Evaluated MVP Definition

## Working Title

**UniPM: A Preventive Maintenance System with RAG-Assisted Inspection History
Analysis for University General Services**

This is the preferred working title. Adviser and panel approval remains
pending; this document does not treat the title as formally approved.

## Purpose And Boundary

The evaluated MVP is a local development prototype for the University General
Services Department. It covers preventive-maintenance workflow, acknowledged
inspection-history analysis, source-bounded retrieval, optional assistive
interpretation, and technical observability. It is not a complete maintenance
or corrective-work management system.

The selected evaluated asset categories are fire extinguishers, fire alarm
systems, emergency lights, and water drinking stations. One preventive-
maintenance form represents one existing one-page form and may contain
multiple inspection rows. The form lifecycle is `Draft -> Submitted ->
Acknowledged`.

Only acknowledged form rows, together with eligible legacy inspection records
without a form, are official inspection-history evidence. Draft and Submitted
rows remain excluded from official history, retrieval, deterministic analysis,
and generated explanations. Acknowledgement completes linked schedules through
the backend workflow.

## Included Backend Capabilities

- Authentication and provisional authorization.
- Asset management.
- Preventive-maintenance schedules.
- Multi-row preventive-maintenance forms.
- Draft form lifecycle and inspection-row operations.
- Whole-form submission.
- Provisional form file numbers.
- Whole-form acknowledgement.
- Linked schedule completion after acknowledgement.
- Acknowledged-only official inspection history.
- Corrective-action handoff preparation as a read model.
- The existing source-bounded maintenance-history review endpoint.
- Deterministic inspection-history analysis.
- RAG-assisted inspection-history interpretation constrained by deterministic
  results and displayed sources.
- Technical OpenTelemetry, Prometheus, and Grafana observability.

The analysis feature calculates counts, denominators, percentages, recurrence
intervals, groupings, and timelines with SQL and deterministic application
code. RAG retrieves supporting acknowledged records. Optional generation only
explains the computed result model and displayed sources. It does not diagnose
equipment, infer causes as facts, approve actions, mutate records, or replace
the authoritative calculations.

## Included Web Capabilities

- Authentication.
- Asset management.
- Schedule management.
- Inspection and history review.
- Preventive-maintenance form review.
- Corrective-action handoff review.
- Inspection-history analysis.
- Maintenance-history review.

The web application presents deterministic facts before any optional generated
interpretation and keeps supporting source records inspectable.

## Mobile Responsibility

Mobile is part of the overall UniPM system and remains a Flutter client using
the backend API. Its implementation is owned by a separate partner workstream.
This workstream does not implement mobile features and does not make partner
mobile completion a blocker for the backend and web MVP phases. Mobile must
remain compatible with the existing backend contracts and confirmed lifecycle
boundaries.

## Privacy And RAG Boundaries

Acknowledgement signatory names, positions, signatures, signature data, and
signature checksums never enter retrieval documents, embeddings, prompts,
deterministic analysis, generated explanations, corrective-handoff data, or
observability dimensions. AI remains assistive and source-bounded, not a
chatbot or autonomous decision-maker.

## Explicit MVP Exclusions

- Deployment completion or IIS rehearsal.
- Mobile implementation owned by the partner workstream, including later field
  operations in this workstream.
- Offline synchronization.
- RMRF processing.
- Direct WMS integration.
- Corrective-work tracking.
- OEM retrieval.
- Institutional-document ingestion.
- A separate vector database.
- Native SQL vector features.
- Another embedding experiment.
- Final enterprise RBAC redesign.
- Autonomous diagnosis or corrective decisions.
- Predictive maintenance.
- Chatbot behavior.
- Production monitoring or production-readiness claims.

## Workstream Completion Criteria

This workstream is complete when the repository demonstrates the following
controlled loop:

```text
acknowledged official history
        -> deterministic analysis
        -> supporting evidence retrieval
        -> optional generated interpretation
        -> web presentation
        -> technical observability
        -> focused verification
        -> documentation and manuscript alignment
```

The result remains a controlled development evaluation using fictional data.
It does not claim institutional deployment, production readiness, real
institutional accuracy, or proven operational improvement.
