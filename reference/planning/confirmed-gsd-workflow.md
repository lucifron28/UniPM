# Confirmed GSD Workflow Direction

## Status

- **Status:** Authoritative requirements baseline for current implementation
- **Confirmed source:** [GSD Head Interview - Confirmed Project Direction](https://app.notion.com/p/3ae92377e48b81cc8948d7b199ed7d2f)
- **Synchronized:** 31 July 2026

This document summarizes confirmed workflow decisions. It does not replace the
CPMP manual for procedure details or finalize unrelated institutional policies.

## Preventive-Maintenance Workflow

1. GSD creates and manages a preventive-maintenance schedule.
2. A skilled worker conducts the inspection.
3. One digital form represents one existing one-page institutional form and
   contains multiple asset inspection rows.
4. The skilled worker submits the whole form. UniPM assigns one provisional
   file number while each asset row keeps its own inspection ID.
5. The concerned department head acknowledges the whole form through the
   skilled worker's authenticated mobile session. The department head does not
   require a UniPM account; signatory name, position, and signature are form
   data.
6. Only acknowledgement changes the form to `Acknowledged` and completes the
   linked preventive-maintenance schedules.

The form lifecycle is `Draft -> Submitted -> Acknowledged`. Asset condition is
`Operational` or `Non-operational`; `Completed` is a schedule state, not an
asset condition.

## History, Retrieval, And Privacy Boundary

- Draft and Submitted rows are excluded from official inspection history and
  retrieval evidence.
- Acknowledged rows are eligible maintenance-history evidence. Legacy rows
  without a form remain eligible for continuity.
- The current finding may be retrieval query context but is not official
  evidence until its form is acknowledged.
- Signatory names, positions, signatures, signature data, and checksums never
  enter retrieval text, embeddings, prompts, or corrective-handoff responses.
- Generated summaries are assistive; returned sources remain the evidence.

## Corrective-Action Boundary

UniPM prepares an acknowledged corrective-action handoff containing the
relevant finding and recommended action. GSD manually encodes the handoff in
the existing Work Management System. UniPM does not create, approve, process,
monitor, or track RMRFs or corrective-maintenance work, and it does not
integrate directly with the Work Management System.

## Evaluated MVP Scope

The evaluated asset categories are fire extinguishers, fire alarm systems,
emergency lights, and water drinking stations. Approved institutional CPMP,
checklist, form, and SOP sources may become a separate evidence group only
after authorization and ingestion are approved. OEM retrieval is excluded from
the evaluated MVP.

## Remaining Clarifications

- Page 2 fields and official completed paper samples.
- Official building, department, and location lists.
- Schedule-adjustment authority and final audit-log persistence rules.
- Authorization and ingestion of institutional CPMP, checklist, form, and SOP
  sources.
- Stronger privacy treatment for arbitrary free-text personal names.

## Manuscript Direction

The preferred working title is **UniPM: A Preventive Maintenance System with
RAG-Assisted Inspection History Analysis for University General Services**. It
remains subject to adviser and panel approval.
