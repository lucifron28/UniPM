---
id: IMP-050
type: implementation
title: Inspection photo lifecycle, focus, and camera-session hardening
status: reviewed
recordedAtUtc: 2026-10-11T02:36:00Z
sourceBranch: feat/inspection-photo-evidence
evidenceLevel: source-inspected
---

# Inspection photo lifecycle, focus, and camera-session hardening

## Objective

Close the photo-upload/form-submission race, keep the web preview keyboard-operable, and release the Flutter camera while the app backgrounds.

## Source identity

- Base: `eb45d5d9edf51f55a1c9264f390849cd6d2d9fe7` (`feat/pre-evaluation-ux-motion`, PR #88).
- Final source inspected: `d9299fca0803cf82412c41776a7166ed1764a59d`.
- Backend change: `acd0a5e90622acb58ab1f29547f8b4890eca0d3a`.
- Flutter lifecycle change: `5bbfbce3488a8cc783e252add2bd3829778a5d87`.
- Web focus changes: `ef3dc7ff048ad665ab189a292f23923557f8d763` and `d9299fca0803cf82412c41776a7166ed1764a59d`.

## Implementation

An upload could previously complete after its parent form was submitted. Upload and delete now lock the parent form with `UPDLOCK, HOLDLOCK` in a read-committed SQL transaction, then recheck owner and Draft status. Submission takes the same parent-row lock before reading its inspection rows. Upload stages the image before locking and removes that staged file if persistence fails. This establishes a consistent lock order across photo mutation and submission.

The web preview uses a modal dialog. It focuses the close button, handles Escape, returns focus to the thumbnail, and wraps Tab and Shift+Tab. E2E coverage checks desktop and mobile.

The Flutter capture card disposes its camera controller whenever the app leaves the resumed state and initializes a new controller when it resumes. A late initialization cannot keep the camera open after backgrounding.

## Files and database

- `server/Features/Inspections/InspectionPhotoEvidenceEndpoints.cs`
- `server/Features/PreventiveMaintenanceForms/PreventiveMaintenanceFormEndpoints.cs`
- `tests/UniPM.Api.Tests/Inspections/SqlServerPhotoSubmissionRaceTests.cs`
- `mobile/lib/features/preventive_maintenance/inspection_photo_evidence_card.dart`
- `web/src/features/inspections/inspection-detail.tsx`
- `web/e2e/inspections.spec.ts`
- `web/src/test/polyfill.ts`

No schema or migration changes were made in this follow-up. The existing photo migration passed Up -> Down -> Up on native SQL Server 2019 at compatibility level 150.

## Verification and limits

See [TEST-075](../test-runs/TEST-075-inspection-photo-lifecycle-and-regression-verification.md) for exact source SHAs and execution results. Physical-device camera behavior, iOS, staging acceptance, deployment, production storage permissions, persistence, backup, and retention remain unverified. The web checks are not a complete WCAG or screen-reader audit. A photo remains supporting evidence and does not prove presence or authenticity.

Related: [IMP-049](IMP-049-inspection-photo-evidence.md); draft stacked [PR #89](https://github.com/lucifron28/UniPM/pull/89), based on PR #88.
