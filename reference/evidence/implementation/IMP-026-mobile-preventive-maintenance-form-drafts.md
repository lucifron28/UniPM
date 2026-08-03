---
id: IMP-026
type: implementation
title: Flutter preventive-maintenance Draft form workflow
status: reviewed
recordedAtUtc: 2026-07-31T16:43:53Z
sourceBranch: feat/mobile-pm-form-drafts
evidenceLevel: locally-executed
testedCommit: ab41d305b49377048e7216d14b3a85ca77ae9317
buildVerificationCommit: ab41d305b49377048e7216d14b3a85ca77ae9317
buildVerificationStatus: passed
---

# Flutter Preventive-Maintenance Draft Form Workflow

## Objective

Add the first mobile field workflow for creating, resuming, and editing
preventive-maintenance Draft forms without adding submission or acknowledgement
behavior.

## Source Identity

- Tested implementation commit:
  `ab41d305b49377048e7216d14b3a85ca77ae9317`
- Source branch: `feat/mobile-pm-form-drafts`
- Starting main commit after PR #48 and PR #49:
  `4489f2b58103e1ec151d9a3f6551b0104d65cb2b`

## Implementation Summary

- Adds strict mobile DTOs and an API repository for the existing preventive-
  maintenance form and inspection-row routes.
- Adds authenticated registry and editor screens for creating a form header,
  adding multiple rows, resuming a Draft, editing row fields, and deleting a
  row after confirmation.
- Uses the existing reference-data and schedule APIs; schedule choices are
  narrowed by the form category while the backend remains authoritative for
  schedule existence and category matching.
- Keeps `ScheduleId` out of the row update payload and sends the authenticated
  Inspector ID for row writes.
- Presents Draft forms only. GSD users see returned Drafts, while Inspectors
  see Drafts created by their authenticated user ID.
- Persists each create, add, update, and delete operation immediately through
  the backend API. No local Draft database or offline synchronization was
  added.
- Does not add submission, file-number generation, acknowledgement, signature
  capture, schedule completion, corrective handoff, QR scanning, notifications,
  exports, RMRF/WMS/OEM work, retrieval, embeddings, generation, or backend
  changes.

## Verification Scope

TEST-032 records the analyzer, focused mobile foundation and Draft workflow
tests, and debug Android build executed for this implementation. No live
backend, emulator connectivity, mobile offline behavior, release build, SQL
Server verification, or production deployment verification was run.

