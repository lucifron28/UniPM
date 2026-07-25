---
id: IMP-017
type: implementation
title: Reference-document evidence foundation
status: reviewed
recordedAtUtc: 2026-07-25T05:30:00Z
sourceBranch: feat/reference-document-foundation
evidenceLevel: source-inspected
sourceCommit: 14ec87d4e585e137db1c25b672941d134a131de6
---

# Reference-Document Evidence Foundation

## Objective

Create a separate SQL Server 2019-compatible persistence foundation for
fictional institutional and OEM-style source material.

## Implementation Summary

`ReferenceDocument`, applicability, ordered sections, and one-to-one section
embedding records preserve source type, revision, lifecycle, locator, checksum,
applicability, and synthetic provenance. A Development-only seed/reset command
loads only clearly fictional data. A separate Full-Text catalog indexes section
heading and text.

## Source Identity

- Relevant commit: `14ec87d4e585e137db1c25b672941d134a131de6`
- Migration: `20260725041647_AddReferenceDocumentFoundation`
- Important paths: `server/Features/ReferenceDocuments/`,
  `server/Data/Seeding/SyntheticReferenceDocumentSeeder.cs`, and the migration
  above.

## Tests Present

- Registration, provenance, lifecycle, ordering, hash, embedding, idempotent
  fixture, reset-scope, and command-parser tests.
- A dedicated native SQL Server 2019 migration test verifies the separate
  reference Full-Text catalog.

## Boundaries

No actual institutional or OEM content, uploads, PDF/OCR extraction, public
API, retrieval channel, provider call, combined evidence, RRF change, or
generated synthesis is included. Existing maintenance-history retrieval is
unchanged.

## Verification Status

Unit and SQL Server 2019 migration/Full-Text verification are recorded only by
their executed test-run evidence. This record is source-inspected and does not
claim production or source-authority validation.

## Related Evidence

- ADR-014
- TEST-022 (platform compatibility)
