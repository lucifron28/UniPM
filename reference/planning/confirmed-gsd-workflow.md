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
3. One digital form represents one institutional PM form and contains multiple
   asset inspection rows.
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

## Digital-Form Direction

The four supplied GSD forms are the authoritative visible field structures for
the current implementation pass. GSD has confirmed that the visible
`Page 1 of 2` notation is document-control context and does not indicate a
missing continuation page.

The final digital form model should preserve the approved information and
workflow rather than reproduce the paper layout mechanically. Predictable and
analytically important values may be represented with structured controls,
while free text remains available for genuine exceptions. Final requiredness,
allowed values, category-specific procedures, measurements, and historical
revision handling remain subject to GSD validation.

## History And Privacy Boundary

- Draft and Submitted rows are excluded from official inspection history.
- Acknowledged rows are eligible official maintenance-history records. Legacy
  rows without a form may remain eligible for continuity where applicable.
- Signatory names, positions, signatures, signature data, and checksums are not
  part of operational analytics inputs or corrective-handoff responses.
- The previously implemented maintenance-history retrieval/RAG infrastructure
  is historical and inactive in the PMIS-only validation baseline.

## Corrective-Action Boundary

UniPM prepares an acknowledged corrective-action handoff containing the
relevant finding and recommended action. GSD manually encodes the handoff in
the existing Work Management System. UniPM does not create, approve, process,
monitor, or track RMRFs or corrective-maintenance work, and it does not
integrate directly with the Work Management System.

Acknowledgement means receipt/noting of the PM form. It is not corrective-
maintenance approval, budget approval, or RMRF approval.

## Current PMIS Validation Scope

The selected asset categories are:

- Fire Extinguishers
- Fire Alarm Systems
- Emergency Lights
- Water Drinking Stations

The current PMIS validation baseline covers the deterministic preventive-
maintenance workflow: asset registry, QR lookup, schedules, category-specific
multi-row PM forms, submission, Department Head acknowledgement, schedule
completion, acknowledged-only official history, and corrective-handoff
preparation.

The core lifecycle has passed physical-device acceptance against the live
development backend. Production signing, IIS deployment, attachments, alerts,
offline synchronization, persistent mobile sessions, and any replacement
innovation are separate follow-on concerns and are not blockers for the GSD
workflow-validation session.

## Remaining Clarifications

- Confirmation that the supplied forms are the current approved revisions.
- Required versus optional fields and allowed values for each category.
- Category-specific procedures, checks, measurements, and result semantics.
- Which values should be auto-filled from asset and schedule records.
- Which recurring findings, work-performed values, and recommended actions
  should become structured choices versus free text.
- Whether official printable/exported output must reproduce the paper layout or
  only preserve approved information and signatory requirements.
- Official building, department, and location lists.
- Schedule-adjustment authority and final audit-log persistence rules.
- Completed paper samples and routine PM reports where GSD can share them.
- GSD's actual information-access and reporting questions, including whether a
  competent conventional reporting/filter interface leaves any meaningful
  residual difficulty.

## Innovation Direction

No replacement innovation is confirmed by this workflow document. The previous
RAG-assisted inspection-history direction is historical and inactive.

Schema-constrained natural-language analytics is currently a leading
post-validation hypothesis, not an approved requirement. It should be adopted
only if GSD evidence shows repeated, varied analytical questions that remain
cumbersome even with a competent conventional reporting/filter interface, and
if the adviser accepts the revised emerging-technology contribution.
