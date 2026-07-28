---
id: IMP-021
type: implementation
title: Preventive-maintenance form acknowledgement
status: reviewed
recordedAtUtc: 2026-07-28T12:26:30Z
sourceBranch: feat/preventive-maintenance-form-acknowledgement
evidenceLevel: source-inspected
---

# Preventive-Maintenance Form Acknowledgement

## Objective

Add the bounded backend transition from a `Submitted` preventive-maintenance
form to `Acknowledged`, publish its inspection rows, and complete their linked
schedules.

## Source Identity

- Relevant commits:
  - `e8ca645e7a72c1c668516724948c19ba03d6d3af`
  - `f1aaf27604a8dbe25a7e70dc8c2174d139e39a1b`
- Source paths:
  - `server/Features/PreventiveMaintenanceForms/PreventiveMaintenanceFormEndpoints.cs`
  - `tests/UniPM.Api.Tests/Forms/PreventiveMaintenanceFormDraftEndpointsTests.cs`

## Implementation Summary

- Adds `POST /api/v1/preventive-maintenance-forms/{id}/acknowledge`.
- Requires an authenticated GSD or Inspector caller and a `Submitted` form
  without an existing acknowledgement.
- Treats the department-head signatory name and position as form data. The
  authenticated GSD or Inspector account is recorded as the capturing user.
- Allows GSD to acknowledge any eligible form. An Inspector may acknowledge
  only a form they created when every row is assigned to that Inspector.
- Accepts a bounded base64 PNG signature, verifies the PNG signature header,
  and calculates an uppercase SHA-256 checksum server-side.
- In one relational transaction, creates the acknowledgement, marks the form
  `Acknowledged`, completes all linked schedules, and rebuilds the linked
  maintenance-search documents.
- Returns acknowledgement metadata without returning the signature payload.

## Publication Boundary

The existing official inspection-history and maintenance-search eligibility
rules expose legacy rows without a form and rows linked to an `Acknowledged`
form. Draft and Submitted rows remain hidden. Acknowledgement publishes only
the selected form's rows; it does not invoke embedding or summary generation.

## Database Changes

No migration was added. The endpoint uses the acknowledgement relationship,
signature limits, status catalog, and uniqueness constraints introduced by
IMP-018.

## Tests Present

Two focused endpoint tests cover:

- successful acknowledgement, schedule completion, official-history
  visibility, search-document projection, and absence of generated embeddings;
- invalid signature input, repeated acknowledgement, wrong form status, and
  unauthorized Inspector rejection.

## Verification Status

TEST-027 records the focused test execution for commit
`f1aaf27604a8dbe25a7e70dc8c2174d139e39a1b`.

## Known Limitations

This phase does not implement corrective-action handoff, RMRF processing, OEM
retrieval, frontend acknowledgement, embedding generation, or generated
summaries. Native SQL Server acknowledgement execution and the complete test
suite were not run in this focused change.

## Related Evidence

- [IMP-018](IMP-018-preventive-maintenance-form-domain.md) - form domain
  foundation.
- [IMP-019](IMP-019-preventive-maintenance-form-drafts.md) - draft workflows.
- [IMP-020](IMP-020-preventive-maintenance-form-submission.md) - submission
  workflow.
- [TEST-027](../test-runs/TEST-027-preventive-maintenance-form-acknowledgement.md)
  - focused acknowledgement verification.
