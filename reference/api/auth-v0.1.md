# Authentication API v0.1

## Boundary

UniPM uses ASP.NET Core IdentityCore with Guid user and role keys and JWT bearer
access tokens. This scaffold does not provide registration, refresh tokens,
password-reset email, external identity providers, user-management UI, or the
final institutional RBAC matrix.

`Admin` is a technical UniPM system-administration role. It is not an
operational super-role. A technical administrator who also performs GSD work
must hold both `Admin` and the applicable operational role.

## Login

`POST /api/v1/auth/login`

```json
{
  "email": "gsd@unipm.local",
  "password": "<development-password>"
}
```

Success returns a bounded bearer token contract:

```json
{
  "accessToken": "<jwt>",
  "expiresAtUtc": "2026-07-13T01:00:00Z",
  "user": {
    "id": "00000000-0000-0000-0000-000000000000",
    "email": "gsd@unipm.local",
    "displayName": "GSD Personnel",
    "roles": ["GSD"]
  }
}
```

Unknown users, wrong passwords, inactive accounts, and locked accounts receive
the same generic HTTP 401 contract. Identity password hashes, security stamps,
lockout fields, and other security state are never returned.

## Current User

`GET /api/v1/auth/me` requires a bearer token and returns the current user's
`id`, `email`, `displayName`, and current roles. Token validation rejects an
account that no longer exists or is inactive.

## Provisional Roles And Policies

| Policy | Allowed roles |
|---|---|
| `CanManageAssets` | `GSD` |
| `CanManageSchedules` | `GSD`, `Supervisor` |
| `CanSubmitInspections` | `GSD`, `Inspector` |
| `CanReviewMaintenanceHistory` | `GSD`, `Supervisor`, `DepartmentHead` |

The protected operations are asset creation, schedule creation, inspection
submission, and maintenance review. Existing list/detail/history/reference,
health, and optionally enabled metrics endpoints remain anonymous during this
scaffold. The role matrix is provisional pending institutional confirmation.

## Configuration

Required HTTP configuration outside Development:

- `UNIPM_JWT_ISSUER`
- `UNIPM_JWT_AUDIENCE`
- `UNIPM_JWT_SIGNING_KEY` (at least 32 UTF-8 bytes)
- `UNIPM_JWT_ACCESS_TOKEN_MINUTES` (defaults to 60; allowed range 1-1440)

JWTs use HMAC SHA-256 and validate issuer, audience, signature, expiration, and
lifetime with a 30-second clock skew. Signing keys must remain in environment
configuration and must never be logged or returned.

## Development Users

Set `UNIPM_DEV_USER_PASSWORD` and run this explicit Development-only command:

```powershell
dotnet run --project server -- --seed-development-users
```

It idempotently creates or repairs these fictional local users:

- `admin@unipm.local` - `Admin`
- `gsd@unipm.local` - `GSD`
- `inspector@unipm.local` - `Inspector`
- `supervisor@unipm.local` - `Supervisor`
- `departmenthead@unipm.local` - `DepartmentHead`

The command creates missing roles/users, repairs missing expected role
assignments, and reactivates expected Development users. It never prints or
persists the supplied plaintext password.
