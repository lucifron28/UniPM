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

### Overall

16. What is the biggest remaining pain point if this plain PMIS workflow were
    digitized as shown?

These questions deliberately do not pitch AI summarization, schema-driven
protocols, analytics, or any other innovation; selection happens after GSD
requirements are collected.

## Known Demo Limitations

- Exact institutional form fields/revisions remain subject to GSD validation;
  current forms demonstrate the workflow, they are not the final schema.
- The partner-owned mobile client covers authenticated Draft creation and row
  editing only; mobile submission, acknowledgement, signatures, QR scanning,
  and later field actions are not implemented yet.
- Maintenance-history RAG, semantic search, embeddings, and AI summaries are
  intentionally absent from this baseline; they are preserved inactive in the
  repository for later retirement decisions.
- No WMS/RMRF integration exists by confirmed boundary; handoff ends at manual
  encoding preparation.
