---
id: ADR-009
type: decision
title: Coordinate memory-only browser sessions with bounded refresh replay
status: reviewed
recordedAtUtc: 2026-07-18T17:57:08Z
sourceCommit: 8b2f1436fa6c15962f3db04dc66ba4dd8aa266b1
evidenceLevel: source-inspected
---

# Coordinate Memory-Only Browser Sessions With Bounded Refresh Replay

## Context

The backend issues short-lived JWT access tokens and rotates opaque refresh
tokens through an HttpOnly cookie. The browser must restore sessions after a
reload and recover concurrent expired-token requests without making the refresh
token JavaScript-readable or triggering backend replay protection.

## Decision

Keep access tokens only in the minimal Zustand authentication lifecycle and
keep refresh tokens entirely server-managed in the HttpOnly cookie. Restore the
session through the generated refresh operation during startup. Coordinate
concurrent refresh needs through one flight per session generation and permit
each ordinary request to replay at most once.

Keep current-user data in TanStack Query because it is server state. Seed that
cache from validated login and refresh responses, use `/auth/me` only when the
cache is absent or later revalidation is needed, and clear the entire Query
cache on logout or terminal authentication failure. Keep URL and redirect state
in TanStack Router and form interaction state in TanStack Form.

Use a monotonic session generation so stale asynchronous results cannot restore
or clear a newer session. Do not let a new generation reuse an older refresh
flight. Serialize login and logout cookie mutations behind settlement of an
older refresh, ensuring the transition request is the final cookie writer while
preserving immediate local logout clearing. Do not decode JWTs in the browser:
identity and roles come from validated backend responses. Do not add token
persistence, cookie reading, a general authentication framework, or a competing
state store.

## Consequences

A reload requires a successful backend refresh-cookie exchange before the
browser becomes authenticated. Concurrent 401 responses within one generation
rotate the cookie only once, while failed refresh and replayed 401 responses
terminate local state without loops. Login or logout may wait for an already
dispatched refresh response before sending its own cookie mutation; this avoids
a stale response becoming the final browser cookie writer. Logout still removes
prior-user server data from memory immediately, but an unconfirmed network
logout may leave the server refresh family usable on a later full reload.

The shell may display roles as returned identity information, but role-aware
navigation and final institutional RBAC remain deferred. Registration,
password recovery, MFA, SSO, mobile authentication, and production deployment
are separate decisions.
