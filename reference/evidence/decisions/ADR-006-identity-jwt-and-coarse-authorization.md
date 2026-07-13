---
id: ADR-006
type: decision
title: Use IdentityCore, JWT, and coarse operational policies
status: reviewed
recordedAtUtc: 2026-07-13T00:45:00Z
testedCommit: 42d73247547fe1df840430cbadc9d5d663bb073d
sourceBranch: feat/auth-scaffolding
evidenceLevel: source-inspected
---

# Decision

Use ASP.NET Core IdentityCore with EF Core stores and Guid keys for account
persistence, JWT bearer access tokens for API authentication, and named
role-based policies for the current operational write boundary.

## Rationale

IdentityCore supplies password hashing, normalized identity lookup, lockout,
security fields, and normalized user-role persistence without inventing a
parallel account model. JWT bearer tokens fit the React and Flutter API-client
architecture while keeping credentials and authorization decisions in the
backend. Tokens are intentionally short-lived access tokens with strict issuer,
audience, signing, and lifetime validation.

`Admin` represents technical UniPM administration and is not an operational
maintenance super-role. Combining technical and operational access therefore
requires multiple assigned roles. This avoids silently granting preventive-
maintenance authority to a system administrator before the institutional RBAC
matrix is confirmed.

Existing reads remain anonymous temporarily so current web/mobile contract work
and public technical health checks are not widened into an unreviewed visibility
matrix. Writes and maintenance review receive the first coarse policy boundary
because they alter operational state or expose bounded maintenance-history
review behavior.

The current roles and policies are provisional. They establish enforceable
software boundaries without claiming final GSD authority, schedule-adjustment,
acknowledgement, RMRF, or audit rules.

## Deferred Alternatives

- Self-registration is deferred because UniPM accounts are institution-managed.
- Refresh tokens are deferred until client session and revocation requirements
  are agreed.
- Password-reset email is deferred until an institutional mail provider and
  recovery policy exist.
- External identity providers are deferred until ICTD confirms deployment and
  account-integration requirements.
- Final RBAC and user-management UI are deferred pending GSD/ICTD validation.
