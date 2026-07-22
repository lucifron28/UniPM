---
id: ADR-012
type: decision
title: Keep web inspections source-record review only
status: reviewed
recordedAtUtc: 2026-07-22T15:02:35Z
evidenceLevel: source-inspected
---

# Keep Web Inspections Source-Record Review Only

## Context

The current API already supplies bounded inspection list, detail, and
asset-history reads, and separately protects field inspection submission.
Mobile field submission and several institutional workflows remain deferred
pending GSD clarification.

## Decision

The web module reads only the existing inspection endpoints. Router search
state owns the supported filters and client-only page, TanStack Query owns
server state, strict Zod schemas validate public responses, and the generated
client remains the API transport boundary. Summary cards count only returned
records; the client does not infer conditions or mutate inspection status.

Inspection detail displays the returned source record, links to its asset and
schedule context, and tolerates missing contextual reads without hiding the
source IDs. Asset detail shows the latest five history records. There is no
web inspection form, POST call, approval control, acknowledgement, RMRF,
corrective-handoff, reporting, audit, or maintenance-review UI.

## Consequences

The browser can support administration and source verification while preserving
the existing backend as the authoritative record of field submission. The
feature cannot claim full checklist execution, institutional approval, or final
workflow policy. A future web write surface requires explicit approval and a
separate contract review.

## Security And Privacy

The feature reuses the authenticated shell, in-memory access-token boundary,
and generated Axios client. It adds no browser token storage, credentials,
provider calls, prompts, embeddings, real institutional data, or new sensitive
fields. Returned inspection remarks are displayed as React text, not HTML.

## Visual Boundary

Figma nodes `4010:4`, `2010:3`, `2354:1409`, and `2354:3777` informed only the
approved maroon/neutral hierarchy, summary cards, table/card responsiveness,
and source-record emphasis. Prototype totals, people, checklist, approval, and
unsupported workflow controls were not copied.

## Related Evidence

- [IMP-016](../implementation/IMP-016-react-web-inspection-review.md)
- [TEST-020](../test-runs/TEST-020-web-inspection-review-verification.md)
