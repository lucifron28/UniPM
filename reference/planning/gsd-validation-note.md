# GSD Validation Session Note

This branch (`validation/pmis-only-gsd`) exists to demonstrate UniPM as a
plain preventive-maintenance information system so the University General
Services Department can validate the actual workflow before any replacement
innovation is selected. Maintenance-history RAG was previously implemented and
evaluated as controlled development work; it is inactive here and no
replacement innovation is proposed or approved yet.

## Demo Flow Shown To GSD

Login -> QR -> schedule -> category form -> multi-row Draft -> submit ->
Department Head acknowledgement -> schedule completion -> official history.

The web and mobile clients use the backend as the source of truth for asset
identity, category, schedules, acknowledgement, completion, and official
history. Corrective-handoff preparation remains available where applicable but
is outside the core PM acceptance gate.

The mobile path uses the skilled worker's authenticated session for QR asset
lookup, official history review, PM form work, and whole-form acknowledgement;
the Department Head does not need a UniPM account.

## Core PM Physical-Device Acceptance

The core preventive-maintenance milestone is complete. The successful manual
acceptance lifecycle was exercised on the connected Samsung Galaxy A16 against
the reachable development backend:

`Login -> QR -> schedule -> category form -> multi-row Draft -> submit ->
Department Head acknowledgement -> schedule completion -> official history`

The detailed execution record is [`TEST-040`](../evidence/test-runs/TEST-040-mobile-core-pm-physical-acceptance.md).
This closes the core PM workflow gate; it does not claim production signing,
store distribution, or IIS deployment.

Known deferred capabilities are listed at the end of this note.

## Questions For GSD

### Workflow

1. Does the current Draft -> Submitted -> Acknowledged workflow match actual
   practice?
2. Does one digital PM form correctly represent one institutional form with
   multiple asset rows?
3. Who may revise PM forms or checklists today?
4. How often do forms change?

### Form Fidelity

5. Which exact form fields are missing or different for each category?
6. Are there additional pages or revisions we have not yet seen?
7. Which fields are always filled versus optional in practice?

### Category Evidence To Collect

For each category below, collect the current approved source before finalizing
the mobile form model:

- form title, revision, effective date, and approving authority;
- complete blank form, including Page 2, plus an approved completed sample;
- exact field labels, types, required/optional status, and allowed values;
- inspection procedures, checks, test measurements, and result semantics;
- remarks and recommendation requirements;
- confirmation that historical records retain their original form revision.

Categories:

- Fire Extinguishers
- Fire Alarm Systems
- Emergency Lights
- Water Drinking Stations

The category-specific form gate remains open until each category has an
authoritative answer for every item above. Synthetic fixtures, obsolete
manuscript material, blank Page 1 references, and generic industry practice
cannot close this gate.

### Reports And History

8. What reports are prepared after PM work?
9. Who prepares those reports?
10. How often are they prepared?
11. What questions do managers commonly ask of PM history?
12. Which parts of report consolidation are currently manual?
13. What still requires duplicate encoding today?

### Corrective Handoff

14. Does the corrective-handoff representation match what GSD transfers into
    the Work Management System?
15. What is missing from that handoff sheet?

### Mobile Capability Decisions

17. Does the field workflow require inspection evidence attachments? For each
    category, which evidence is mandatory or optional, what file types and
    limits apply, whether it belongs to a row or whole form, and what are the
    deletion, read-only, retention, and access rules?
18. Are preventive-maintenance alerts operationally required? If so, what are
    the trigger, recipient role, schedule status, notice period, overdue and
    dismissal behavior, local-versus-server delivery rule, and assignment
    rule?
19. Is offline PM work needed in the field? If so, what connectivity evidence
    justifies it, which data and Draft actions may work offline, and what
    synchronization, conflict, idempotency, authentication, and local-data
    protection design should be approved?
20. Is secure mobile session restoration required after an app restart? If so,
    what secure storage, refresh, revocation, corrupted-data, and unavailable-
    network behavior should be accepted?
21. Which additional release, signing, HTTPS-host, camera, and
    network-transition checks should be required before a production mobile
    release?

### Overall

22. What is the biggest remaining pain point if this plain PMIS workflow were
    digitized as shown?

These questions deliberately do not pitch AI summarization, schema-driven
protocols, analytics, or any other innovation; selection happens after GSD
requirements are collected.

## Known Demo Limitations

- The four supplied GSD forms are the authoritative visible field structure for
  the current mobile implementation pass: Fire Extinguishers Rev. 2 (November
  2023), Fire Alarm Rev. 1 (May 2022), Emergency Lights Rev. 1 (May 2022), and
  Water Drinking Stations Rev. 1 (November 2023). Broader institutional
  decisions about requiredness, allowed values, procedures, measurements, and
  historical revision preservation remain subject to GSD validation.
- The partner-owned mobile client covers authenticated QR asset lookup,
  acknowledged-only official history, Draft creation and row editing,
  whole-form submission, submitted-form review, and mobile whole-form
  acknowledgement with signatory capture. The core lifecycle was accepted on a
  physical Android device against the live development backend. Production
  signing and distributable-release verification remain outside this milestone.
- Inspection attachments, operational alerts, offline synchronization, and
  persistent session restoration are deferred and are not blockers for the core
  PM milestone; the current mobile session remains memory-only.
- Maintenance-history RAG, semantic search, embeddings, and AI summaries are
  intentionally absent from this baseline and remain deferred/out of scope.
- No WMS/RMRF integration exists by confirmed boundary; handoff ends at manual
  encoding preparation.

### Deferred and out-of-scope capabilities

The following capabilities are intentionally excluded from this completed
milestone and must not be treated as blockers:

- Inspection attachments.
- Operational alerts.
- Offline synchronization.
- Persistent session restoration.
- AI/RAG, semantic search, and other replacement-innovation capabilities.
- WMS/RMRF processing and other downstream administrative automation.

No feature implementation branch is created for these deferred capabilities by
this acceptance record.
