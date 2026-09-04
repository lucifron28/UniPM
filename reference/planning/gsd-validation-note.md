# GSD Validation Session Note

This branch (`validation/pmis-only-gsd`) exists to demonstrate UniPM as a
plain preventive-maintenance information system so the University General
Services Department can validate the actual workflow before any replacement
innovation is selected. Maintenance-history RAG was previously implemented and
evaluated as controlled development work; it is inactive here and no
replacement innovation is proposed or approved yet.

## Demo Flow Shown To GSD

Login -> Asset registry -> QR lookup where available -> Schedule ->
Create Draft PM form -> Add multiple rows -> Edit row -> Submit whole form ->
Acknowledge whole form -> Linked schedules become Completed ->
Acknowledged rows become official history -> Corrective handoff available
where applicable.

The mobile path uses the skilled worker's authenticated session for QR asset
lookup, official history review, PM form work, and whole-form acknowledgement;
the Department Head does not need a UniPM account.

Known demo limitations are listed at the end of this note.

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
21. Which Android device, approved HTTPS API host, application identity,
    signing owner, camera behavior, and network-transition checks are required
    for GSD acceptance of the mobile release?

### Overall

22. What is the biggest remaining pain point if this plain PMIS workflow were
    digitized as shown?

These questions deliberately do not pitch AI summarization, schema-driven
protocols, analytics, or any other innovation; selection happens after GSD
requirements are collected.

## Known Demo Limitations

- Exact institutional form fields/revisions remain subject to GSD validation;
  current forms demonstrate the workflow, they are not the final schema.
- The partner-owned mobile client covers authenticated QR asset lookup,
  acknowledged-only official history, Draft creation and row editing,
  whole-form submission, submitted-form review, and mobile whole-form
  acknowledgement with signatory capture. Physical-device, live-backend,
  production-signing, and distributable-release verification remain
  unexecuted.
- Inspection attachments, operational alerts, offline synchronization, and
  persistent session restoration remain conditional capabilities awaiting GSD
  or project-owner decisions; the current mobile session is memory-only.
- Maintenance-history RAG, semantic search, embeddings, and AI summaries are
  intentionally absent from this baseline; they are preserved inactive in the
  repository for later retirement decisions.
- No WMS/RMRF integration exists by confirmed boundary; handoff ends at manual
  encoding preparation.
