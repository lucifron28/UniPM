---
id: IMP-009
type: implementation
title: Identity, JWT, and coarse authorization scaffolding
status: reviewed
recordedAtUtc: 2026-07-13T00:45:00Z
testedCommit: 42d73247547fe1df840430cbadc9d5d663bb073d
sourceBranch: feat/auth-scaffolding
evidenceLevel: source-inspected
---

# Identity, JWT, And Coarse Authorization Scaffolding

The backend persists `ApplicationUser` through ASP.NET Core IdentityCore with
Guid user and role keys. Standard Identity user, role, claim, login, token, and
user-role tables are added in one migration. `DisplayName` and `IsActive` are
the only UniPM-specific user fields.

`POST /api/v1/auth/login` uses Identity email normalization, lockout-aware
password verification, and one generic unauthorized response for unknown,
inactive, locked, and invalid-password accounts. `GET /api/v1/auth/me` returns
only the current user's ID, email, display name, and roles. JWT access tokens
use HMAC SHA-256 and include subject, token ID, email, display name, and one role
claim per assigned role. Validation checks issuer, audience, signature,
expiration, lifetime, and current user activity.

The explicit `--seed-development-users` command creates the five provisional
roles and fictional local accounts, repairs missing expected assignments, and
reactivates expected users. It requires Development and
`UNIPM_DEV_USER_PASSWORD`; the plaintext password is not printed or persisted.

Coarse operational policies protect asset creation, schedule creation,
inspection submission, and maintenance review. `Admin` is intentionally absent
from those policies because it is a technical role rather than an operational
super-role. Existing read/list/detail/history routes remain anonymous during
this scaffold.

Maintenance review is no longer restricted to Development. It remains disabled
in committed configuration and requires `CanReviewMaintenanceHistory` whenever
enabled. Retrieval, sanitization, source selection, summary, evidence-status,
and degradation behavior are unchanged.

This is source-inspected evidence. Final institutional RBAC, registration,
refresh tokens, password-reset delivery, external identity providers,
user-management UI, and production IIS deployment remain outside this branch.
