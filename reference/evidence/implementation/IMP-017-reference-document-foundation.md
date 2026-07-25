---
id: IMP-017
type: implementation
title: Reference-document evidence foundation
status: reviewed
recordedAtUtc: 2026-07-25T05:30:00Z
sourceBranch: feat/reference-document-foundation
evidenceLevel: source-inspected
sourceCommit: a5709fb3118489529518ea398e1c7c6df48bfedf
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

- Relevant commit: `a5709fb3118489529518ea398e1c7c6df48bfedf`
- Migration: `20260725041647_AddReferenceDocumentFoundation`
- Important paths: `server/Features/ReferenceDocuments/`,
  `server/Data/Seeding/SyntheticReferenceDocumentSeeder.cs`, and the migration
  above.

## Tests Present

- Registration, immutable provenance, valid supersession, normalized
  applicability, ordering, hash, embedding, idempotent fixture, fixture-scoped
  reset, and command-parser tests.
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
