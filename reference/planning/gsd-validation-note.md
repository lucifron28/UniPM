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

## Settled Form Clarification

GSD has confirmed that the supplied institutional forms do not have a missing
Page 2 continuation form. The visible `Page 1 of 2` notation is treated as
document-control context, not evidence that content is missing. Do not ask GSD
to provide a missing Page 2.

The remaining form-validation work is to confirm that the supplied forms are
the current approved revisions and to validate the required information,
category-specific checks, field requiredness, and digital representation.

Known deferred capabilities are listed at the end of this note.

## Questions For GSD

### Workflow

1. Does the current `Draft -> Submitted -> Acknowledged` workflow match actual
   practice?
2. Does one digital PM form correctly represent one institutional form with
   multiple asset rows?
3. Who may revise PM forms or checklists today?
4. How often do forms change?

### Digital Form Fidelity

The proposed direction is semi-structured and digital-first: predictable,
recurring, and analytically important values should use structured controls,
while free text remains available for genuinely unusual observations. The
mobile interface does not need to copy the paper layout directly as long as it
preserves required information and workflow meaning.

5. Are the supplied forms the current approved revisions for the four selected
   categories?
6. Which fields are required, optional, or rarely used in actual PM work?
7. Which values should be auto-filled after QR lookup from the asset and
   schedule records, such as asset ID, category, building, department,
   location, and due schedule?
8. Which inspection results, findings, work-performed values, and recommended
   actions are recurring enough to be structured choices?
9. In which situations is free text genuinely required for an inspection row?
10. Should a whole PM form retain a separate form-level
    `Actions / Recommendations` entry when one recommendation applies to
    several inspected assets?
11. If UniPM later produces an official printable/exported form, must it match
    the existing paper layout exactly, or only preserve the approved
    information and signatory requirements?

### Category Evidence To Confirm

For each category below, confirm the current approved source before finalizing
the digital form model:

- form title, revision, effective date, and approving authority;
- complete current blank form plus an approved completed sample where available;
- exact field labels, types, required/optional status, and allowed values;
- inspection procedures, checks, test measurements, and result semantics;
- remarks, work-performed, and recommendation requirements;
- which information may be auto-filled from the asset or schedule;
- whether historical records must retain their original form revision.

Categories:

- Fire Extinguishers
- Fire Alarm Systems
- Emergency Lights
- Water Drinking Stations

The four supplied forms are the authoritative visible structures for the
current implementation pass. Final requiredness, allowed values, procedure
semantics, and historical revision handling remain subject to GSD validation.

### Reports And Information Needs

Use these conversational questions while showing the PMIS. Start with GSD's
actual work; do not introduce the proposed natural-language feature until the
last question.

12. Kapag nagre-review po kayo ng inspection results, ano pong information ang
    usually una ninyong tinitingnan?
13. May recent example po ba kayo na kailangan ninyong kumuha ng information
    mula sa maraming inspection forms? Ano po yung gusto ninyong malaman?
    - Follow-up if needed: `Paano niyo po nakuha yung sagot?`
14. Kapag nakuha niyo na po yung information, saan niyo po usually ginagamit?
15. Usually pare-pareho lang po ba yung reports or information na kailangan
    ninyo, or may iba-ibang tanong depende sa situation?
    - Follow-up if needed: `May example po ba kayo?`
16. Kung titingnan po ninyo itong filters at reports sa system, meron pa po
    bang information na mahihirapan kayong hanapin?

Only after the questions above, the interviewer may briefly explain the
proposal:

> May kino-consider din po kaming option na pwede kayong mag-type ng tanong,
> tapos ita-translate ng system into supported report filters. Ipapakita rin
> niya kung ano yung pagkaintindi niya, at magtatanong siya kung may hindi
> malinaw.

17. Para po dun sa example na binigay ninyo, mas makakatulong po ba kung
    ita-type niyo lang yung tanong, or mas prefer niyo pa rin pumili ng filters
    or gumamit ng saved report? Bakit po?

Suggested closing request:

> Pwede po ba kayong magbigay ng dalawa o tatlong actual questions na gusto
> ninyong masagot ng system? Gagamitin po namin yun para malaman kung worth
> adding talaga yung feature.

Record the actual task, how often it happens, what makes it difficult, and what
GSD does with the answer. Positive reactions alone do not establish a
requirement.

### Existing Reports

18. Ano-ano pong reports ang regular ninyong ginagawa gamit ang PM records?
19. Aling part po ng paggawa ng reports ang pinaka-manual o pinaka-matagal?
20. If available, can GSD show one recent PM summary, status, or accomplishment
    report and explain how its counts, tables, and narrative sections were
    prepared?

### Corrective Handoff

21. Does the corrective-handoff representation match what GSD transfers into
    the Work Management System?
22. What is missing from that handoff sheet?

### Mobile Capability Decisions

23. Does the field workflow require inspection evidence attachments? For each
    category, which evidence is mandatory or optional, what file types and
    limits apply, whether it belongs to a row or whole form, and what are the
    deletion, read-only, retention, and access rules?
24. Are preventive-maintenance alerts operationally required? If so, what are
    the trigger, recipient role, schedule status, notice period, overdue and
    dismissal behavior, local-versus-server delivery rule, and assignment
    rule?
25. Is offline PM work needed in the field? If so, what connectivity evidence
    justifies it, which data and Draft actions may work offline, and what
    synchronization, conflict, idempotency, authentication, and local-data
    protection design should be approved?
26. Is secure mobile session restoration required after an app restart? If so,
    what secure storage, refresh, revocation, corrupted-data, and unavailable-
    network behavior should be accepted?
27. Which additional release, signing, HTTPS-host, camera, and
    network-transition checks should be required before a production mobile
    release?

### Audit, Scheduling, And Remaining Pain Points

These questions validate possible residual problems without proposing a
specific technology.

28. When GSD, QMR, or auditors review a PM record, what evidence do they check
    to verify the inspection and acknowledgement?
29. Has the date, performer, result, or acknowledgement of a PM inspection ever
    needed to be verified or questioned?
30. Is Department Head acknowledgement captured on the skilled worker's device
    acceptable as the official digital acknowledgement process?
31. Can GSD show one PM schedule that was difficult to arrange or had to be
    changed and explain what caused the change?
32. What is the biggest remaining pain point if this plain PMIS workflow were
    digitized as shown?

These questions deliberately avoid presenting natural-language analytics,
report generation, cryptographic provenance, scheduling optimization, or any
other emerging technology as a decided requirement. Innovation selection is
based on GSD evidence and adviser approval.

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

### Deferred And Out-Of-Scope Capabilities

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
