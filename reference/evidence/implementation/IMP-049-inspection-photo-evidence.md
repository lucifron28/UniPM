---
id: IMP-049
type: implementation
title: Optional inspection photo evidence and GPS verification removal
status: reviewed
recordedAtUtc: 2026-10-10T17:49:10Z
sourceBranch: feat/inspection-photo-evidence
evidenceLevel: locally-executed
---

# Optional inspection photo evidence and GPS verification removal

## Objective

Remove active geolocation prompts from the Inspector workflow and add one optional, camera-captured JPEG per inspection row. Keep inspection completion and whole-form submission independent of photo success, while making an unsaved photo omission explicit.

## Source Identity

- Base commit: `eb45d5d9edf51f55a1c9264f390849cd6d2d9fe7` (`feat/pre-evaluation-ux-motion`, PR #88).
- Source commit: `b1be6341e94543174b540d269d063bbafecc6b72`.
- Relevant commits:
  - `ed1a4c4` `refactor(web): remove experimental GPS asset controls`
  - `2fbac8e` `feat(api): add private inspection photo evidence`
  - `e204803` `feat(mobile): add optional photo evidence and remove GPS checks`
  - `b1be634` `feat(web): add read-only inspection photo preview`

## Implementation Summary

The mobile PM workflow no longer captures coordinates, requests location permission, or displays location verification dialogs. The web asset form no longer exposes experimental latitude, longitude, or radius controls. Ordinary Building, Department, and Location fields, QR lookup, assignment, and inspection entry remain. Existing location columns, migrations, and historical data are preserved; no location data migration was added or removed.

An Inspector can capture one optional JPEG per inspection row inside the Flutter app. The card shows the asset code and selected condition, supports preview, retake, save, and remove while the form is Draft, and stacks vertically on narrow screens. Capture cancellation, permission denial, and upload failure do not undo the completed inspection row. A pending image does not disable submission. The submit confirmation tells the Inspector that unsaved captures will not be attached, and the UI clears the temporary capture after successful submission.

The backend exposes authenticated `PUT`, `GET`, and `DELETE /api/v1/inspections/{id}/photo` routes. It accepts JPEG only, caps uploads at 4 MiB, limits image dimensions and pixel count, removes JPEG metadata segments, and stores randomly named files outside the public web root. The photo row is keyed by its Inspection ID and records server upload time, byte length, and revision. The existing inspection supplies the asset and Inspector association. Only the owning Inspector can change a photo, and only while the parent form is Draft. Inspectors can read their own photo; GSD can read acknowledged or legacy-unlinked records. Responses disable caching.

GSD inspection details show a read-only thumbnail and larger preview, or `No photo evidence recorded.` The mobile interface uses the authenticated API and has no gallery or file-picker path.

Photo evidence remains optional supporting documentation. The implementation makes no claim that a photograph proves physical presence or authenticity. No image hashing, GPS metadata, AI image analysis, or authenticity checks were added.

## Architecture And Contracts

- One photo row per inspection, enforced by the inspection-keyed table.
- Photo upload, replacement, and deletion do not change inspection completion or acknowledgement state.
- Once the form leaves Draft, the API rejects photo mutation. Read access follows Inspector ownership and GSD acknowledgement rules.
- Photo storage uses a private filesystem directory under the API content root. Storage retention, backup, and deployed-server permissions still require deployment review.
- The newly added photo-evidence migration is additive. It does not alter prior location columns or historical location records.

## Important Files

- Mobile capture card and API adapter: `mobile/lib/features/preventive_maintenance/inspection_photo_evidence_card.dart`, `mobile/lib/features/preventive_maintenance/inspection_photo_evidence_repository.dart`, and `mobile/lib/api/api_client.dart`.
- Mobile GPS removal and form integration: `mobile/lib/features/preventive_maintenance/scanned_asset_pm_entry.dart` and `mobile/lib/features/preventive_maintenance/preventive_maintenance_page.dart`.
- Backend routes, JPEG validation, and private storage: `server/Features/Inspections/InspectionPhotoEvidenceEndpoints.cs` and `server/Features/Inspections/InspectionPhotoEvidenceStorage.cs`.
- Web read-only preview: `web/src/features/inspections/inspection-detail.tsx` and `web/src/features/inspections/inspection-queries.ts`.
- API contract: `web/openapi/unipm-v1.json` and generated client files under `web/src/api/generated/`.

## Database Changes

Added the `InspectionPhotoEvidence` table through migration `20261010141251_AddInspectionPhotoEvidence`. It has a one-to-one inspection key, storage key, byte length, upload timestamp, and revision. Existing GPS/location schema and records are untouched. The migration has not been applied to a native SQL Server instance in this verification environment.

## Tests Present

- Backend tests cover authenticated upload/read/delete, Inspector ownership, GSD read access, Draft-only mutation, invalid media, upload limits, JPEG metadata removal, dimension limits, and the API contract.
- Flutter tests cover camera cancellation/failure, capture/preview/retake/remove, upload failure and retry, authenticated binary requests, read-only display, and explicit submission without an unsaved photo.
- Web tests cover the empty state and authorized thumbnail-to-preview flow. E2E fixtures include the new photo-evidence response field.

## Verification Status

See [TEST-071](../test-runs/TEST-071-inspection-photo-evidence-and-gps-removal.md) for executed commands, counts, and unavailable verification.

## Known Limitations

- Native SQL Server migration/integration tests were skipped because SQL Server was unavailable and `UNIPM_SQLSERVER_TEST_CONNECTION` was unset.
- Physical Android camera permission and capture behavior were not tested on a device. An Android debug APK was built. An iOS build was not run.
- Unuploaded captures remain in memory and are not queued for offline upload.
- Production filesystem permissions, backup/retention policy, staging acceptance, and deployment were not verified.
- GitHub CI for the final evidence-updated PR head is recorded separately after that head completes.

## Related Evidence

- [TEST-071: Optional photo evidence and GPS-removal verification](../test-runs/TEST-071-inspection-photo-evidence-and-gps-removal.md)
- Draft stacked PR: [#89](https://github.com/lucifron28/UniPM/pull/89), based on PR #88. It remains unmerged and undeployed.
