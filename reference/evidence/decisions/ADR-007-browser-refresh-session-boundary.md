---
id: ADR-007
type: decision
title: Use rotating opaque refresh sessions for the browser MVP
status: reviewed
recordedAtUtc: 2026-07-17T15:45:19Z
evidenceLevel: source-inspected
---

# Use Rotating Opaque Refresh Sessions For The Browser MVP

## Context

Access-token-only authentication does not provide a bounded browser reload or
session-renewal contract for the planned Vite client.

## Decision

Use JWT access tokens in client memory and opaque 256-bit refresh tokens in an
HttpOnly same-site cookie. Persist only SHA-256 hashes in SQL Server, use one
family per login, rotate on every refresh, revoke a family on replay, bind the
family to a hashed Identity security stamp, and use exact-origin credentialed
CORS with Origin validation for cookie-backed authentication POSTs.

## Alternatives

Local-storage refresh tokens, browser-readable cookies, JWT refresh tokens,
wildcard credentialed CORS, and a separate auth server were rejected for this
MVP. They either increase browser exposure, add unnecessary trust complexity,
or exceed the current deployment scope.

## Consequences

The future frontend must keep access tokens in memory and make refresh requests
single-flight. Logout revokes future refresh capability but cannot immediately
invalidate a previously issued JWT. Mobile secure-storage behavior remains a
separate contract.

## Security And Privacy

No raw refresh tokens, access tokens, security stamps, IP addresses, full user
agents, device fingerprints, request payloads, or provider credentials are
persisted by this feature.
