---
id: IMP-043
type: implementation
title: Matching source records for analytics count queries
status: implemented
evidenceLevel: source-inspected
---

Source commit: `a7837ed1c6d04a172304ff011f0df6ea80c3823d`.

Before this UI correction, an authenticated local API diagnostic on source head
`be2caff` executed the deterministic query for November 2025 water-drinking-station
late inspections grouped by department. It returned CompletedLate, with counts
of one for Demo CCMS, one for Demo Library, and zero for Food Services. Its source
population contained two Late and three OnTime records. No AI provider was called.

The backend deliberately returns the eligible source population. The web now
shows only contributing returned records by default for CompletedLate and
NonOperational queries. A checkbox preserves access to all calculation source
records. Percent metrics, API contracts, calculations, and the existing
100-record source cap remain unchanged. Matching lists are bounded by that cap
and cannot establish exhaustive results if sources are truncated.

Changed-file Prettier formatting and git diff --check passed. Automated tests,
typecheck, build, browser acceptance, and remote CI were not run under the
user's demo-time no-tests instruction. The user must refresh and rerun the query
to verify the updated UI. No push or merge occurred.
