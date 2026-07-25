---
id: ADR-014
type: decision
title: Separate reference-document evidence from maintenance history
status: reviewed
recordedAtUtc: 2026-07-25T05:30:00Z
evidenceLevel: source-inspected
sourceCommit: 6f04c6557497ffb686ba6c807f69d57bd2c39726
---

# Separate Reference-Document Evidence From Maintenance History

## Status

Accepted.

## Context

Future UniPM review needs authorized institutional and applicable OEM source
material without conflating those sources with recorded maintenance history.

## Decision

Store institutional and OEM reference documents in a separate relational
aggregate with lifecycle, revision, applicability, ordered sections, locators,
checksums, synthetic provenance, section-scoped embeddings, and a separate SQL
Server Full-Text catalog. Maintenance-history storage and retrieval remain
unchanged. SQL Server 2019 compatibility level 150, serialized vectors, bounded
application-memory cosine similarity, and RRF remain the platform boundary.

## Consequences

This branch creates no upload, extraction, public endpoint, retrieval channel,
combined evidence result, or generated synthesis. Superseded and archived
references are recorded for traceability and are intended to be excluded by
future retrieval defaults.

## Evidence References

- TEST-022 establishes the SQL Server 2019 and Full-Text compatibility basis.
- IMP-017 records the source-inspected implementation foundation at
  `6f04c6557497ffb686ba6c807f69d57bd2c39726`.
