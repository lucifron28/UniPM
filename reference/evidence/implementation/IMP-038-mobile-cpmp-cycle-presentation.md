---
id: IMP-038
type: implementation
title: Mobile CPMP cycle and due-date presentation
status: reviewed
recordedAtUtc: 2026-09-30T19:08:03Z
sourceCommit: e87a2290e283ab265523375523c862b335442032
sourceBranch: fix/cpmp-month-end-deadlines-compliance
evidenceLevel: source-inspected
---

# Mobile CPMP cycle and due-date presentation

Mobile field screens display the canonical PM cycle as month and year, with a
separate civil month-end due date. The QR schedule selector displays cycle and
status. Neither display treats the due date as a visit appointment.

`mobile/lib/features/preventive_maintenance/pm_cycle_presentation.dart` parses
canonical `YYYY-MM` cycles and computes calendar month-end using UTC calendar
fields. These fields represent the Asia/Manila civil date, not the backend
instant. Display never calls `toLocal()` for the due date. The existing model's
legacy cycle inference still adds fixed +08:00 to the UTC schedule timestamp.
Actual inspection and Draft creation timestamps retain their existing behavior.

Presentation changes cover scanned entry, Draft metadata and rows, batch cards,
and the inspection completion sheet. Existing legacy batch text remains visible
when no canonical cycle exists, with an unrecorded due date. Batch identity,
assignment filtering, inspection saves, Draft reuse, location capture,
whole-batch submission and acknowledgement logic are unchanged.

Progress remains inspected assets divided by scheduled assets. No mobile UI
labels it compliance or adds GSD Compliance rate to Inspector screens. The
mobile README and planning document now distinguish these measures.

Fixtures use FE February/May/August/November, EL June, and WDS August as
applicable. FA and EL allow June/December; FE and WDS allow
February/May/August/November. No new mobile frequency policy duplicates the
backend category reference contract. Historical mismatched timestamps are
explicitly tested as legacy presentation inputs.

Atomic mobile commits are `3c4567b`, `14a66cc`, `418e484`, and `e87a229`.
Execution and limitations are recorded separately in [TEST-045](../test-runs/TEST-045-mobile-cpmp-presentation.md).
