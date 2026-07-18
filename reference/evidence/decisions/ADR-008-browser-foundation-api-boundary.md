---
id: ADR-008
type: decision
title: Use a generated API client and in-memory browser token boundary
status: reviewed
recordedAtUtc: 2026-07-18T10:05:00Z
evidenceLevel: source-inspected
---

# Use A Generated API Client And In-Memory Browser Token Boundary

## Context

The web client needs a stable typed boundary to the backend before operational
screens and browser authentication integration begin.

## Decision

Commit a sanitized OpenAPI snapshot, generate React Query client code through
Orval, and fail verification whenever generation creates a tracked change,
deletion, or untracked model. Keep one Axios transport with credentials enabled
for the backend refresh-cookie contract, while holding the JWT access token only
in Zustand memory. Accept only internal `/app` redirect targets.

## Consequences

Frontend DTO types follow the API contract instead of hand-maintained copies.
Reload/session restoration and refresh single-flight handling are deferred to
the authentication-integration branch. The foundation does not use local
storage or expose refresh tokens to JavaScript.
