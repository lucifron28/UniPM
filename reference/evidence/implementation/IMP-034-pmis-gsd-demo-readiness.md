---
id: IMP-034
type: implementation
title: PMIS GSD demonstration readiness
status: reviewed
recordedAtUtc: 2026-09-14T03:39:43Z
sourceBranch: feature/pmis-gsd-demo-readiness
evidenceLevel: locally-executed
---

# PMIS GSD demonstration readiness

## Objective

Prepare the PMIS-only validation baseline for a GSD walkthrough without adding
an AI feature, deployment claim, WMS integration, or new workflow behavior.

## Source identity

- Starting validation commit: `d6e5f66a1546abbeebc73c66a2a7cdee8896fd5e`
- Tested implementation commit: `c9deb6d6b90b51f59c527b4d23f6202c8030cb22`
- Corrected runbook commit: `88dab27d596f357ce3b7873646a65b8e08f8087d`
- Source branch: `feature/pmis-gsd-demo-readiness`

## Implemented changes

- Refreshed the live OpenAPI snapshot and Orval models for the current
  preventive-maintenance fields.
- Added asset code, location, and skilled-worker display name to form-review
  rows while retaining the underlying technical identifiers.
- Displayed Water Drinking Station accomplishment and work-item fields in form
  review and official inspection history.
- Replaced the empty dashboard with links to the implemented PMIS modules and
  an accurate summary of the confirmed form lifecycle.
- Added a GSD demonstration runbook based on the repository's real maintenance
  commands and the verified native SQL Server 2019 setup.

## File-number boundary

Submission still assigns one provisional UniPM file number to one form. The
number is independent from WMS and RMRF numbers. A form can contain several
inspection rows, and downstream corrective requests remain outside UniPM.

## Scope limits

- No AI, RAG, embedding, or generation path was enabled or extended.
- No database schema, migration, authentication contract, or mobile behavior
  changed.
- No real institutional records or signatures were added.
- This work does not claim production deployment or GSD adoption.
- A new physical-device rehearsal and backup recording remain demonstration
  preparation tasks. `TEST-040` records the earlier accepted physical-device
  workflow at its own commit.

## Verification

See [TEST-041](../test-runs/TEST-041-pmis-gsd-demo-readiness.md).
