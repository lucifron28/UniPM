---
id: IMP-011
type: implementation
title: Rotating browser refresh sessions
status: reviewed
recordedAtUtc: 2026-07-17T15:45:19Z
sourceBranch: feat/auth-refresh-sessions
evidenceLevel: source-inspected
---

# Rotating Browser Refresh Sessions

## Objective

Replace the access-token-only scaffold with a bounded browser session contract
before the web client is introduced.

## Implementation Summary

UniPM now issues short-lived JWT access tokens in the existing login JSON
response and creates an independent refresh-token family for each login. The
raw 256-bit opaque refresh token is transient and is written only to the
host-only `unipm_refresh` HttpOnly cookie. SQL Server persists one row per
issued token with SHA-256 token and security-stamp hashes, a family ID,
absolute expiration, replacement relationship, revocation metadata, and a
rowversion concurrency token.

Refresh rotates the consumed token in the same family without extending its
original seven-day default lifetime. Reuse of a rotated token revokes active
tokens in that family. Logout revokes a recognizable current family and clears
the cookie, while an already-issued JWT is not denylisted.

## Browser Boundary

The refresh cookie is `HttpOnly`, `SameSite=Lax`, `IsEssential`, host-only, and
scoped to `/api/v1/auth`. It is Secure outside Development. The API permits one
configured exact HTTP/HTTPS web origin with credentialed CORS and validates a
present Origin header for login, refresh, and logout. This is an MVP browser
boundary, not a claim of complete CSRF protection or production readiness.

## Database Changes

Migration `20260717153842_AddRefreshSessions` adds only `RefreshSessions`, its
foreign keys, token-hash uniqueness, family/revocation indexes, and rowversion.
Expired/revoked rows are retained through the family expiration for replay
detection; automated cleanup and device-management data are deferred.

## Important Files

- `server/Features/Auth/RefreshSessionService.cs`
- `server/Features/Auth/RefreshTokenGenerator.cs`
- `server/Features/Auth/RefreshCookieService.cs`
- `server/Features/Auth/TrustedWebOriginValidator.cs`
- `server/Models/RefreshSession.cs`
- `server/Migrations/20260717153842_AddRefreshSessions.cs`

## Limitations

No JWT denylist, logout-all, session-list endpoint, device metadata, background
cleanup, mobile refresh-token contract, frontend retry logic, final RBAC, or
IIS deployment verification is included.
