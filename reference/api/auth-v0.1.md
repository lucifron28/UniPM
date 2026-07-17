# Authentication API v0.1

## Boundary

UniPM uses ASP.NET Core IdentityCore with Guid user and role keys. Browser
sessions use a short-lived JWT access token returned in JSON and an opaque,
rotating refresh token held only in an HttpOnly cookie. The future web client
keeps the access token in memory and uses the refresh endpoint after reload or
access-token expiry; it must coordinate refresh calls as a single flight to
avoid legitimate concurrent refresh races.

`Admin` is a technical UniPM system-administration role, not an operational
super-role. Final institutional RBAC, registration, password recovery, MFA,
external identity providers, user-management UI, mobile secure storage, and
device-management workflows remain deferred.

## Login

`POST /api/v1/auth/login`

```json
{
  "email": "gsd@unipm.local",
  "password": "<development-password>"
}
```

Successful login returns:

```json
{
  "accessToken": "<jwt>",
  "expiresAtUtc": "2026-07-17T01:00:00Z",
  "user": {
    "id": "00000000-0000-0000-0000-000000000000",
    "email": "gsd@unipm.local",
    "displayName": "GSD Personnel",
    "roles": ["GSD"]
  }
}
```

It also sets the raw opaque refresh token only in the `unipm_refresh` cookie.
The response has `Cache-Control: no-store` and `Pragma: no-cache`; it never
returns the refresh token in JSON, ordinary headers, logs, or ProblemDetails.
Unknown users, wrong passwords, inactive accounts, and locked accounts receive
the same generic HTTP 401 authentication-failed contract.

Each successful login creates an independent refresh-token family. Logging out
one browser/device family does not revoke other login families for the user.

## Refresh And Rotation

`POST /api/v1/auth/refresh` accepts no token in its request body. It reads only
the refresh cookie and returns the same JSON shape as a successful login with a
new access token. It is callable without a valid bearer token.

The server stores only a SHA-256 hash of each opaque 256-bit refresh token. A
refresh checks the active user, lockout state, captured security-stamp hash, and
the token family's original absolute expiration. It then revokes the consumed
row, creates exactly one replacement in the same family, and replaces the
cookie. Refresh does not extend the family's lifetime.

Missing, malformed, unknown, expired, revoked, replayed, inactive-user,
locked-user, deleted-user, security-stamp-change, and invalid-session failures
all clear the cookie and return the same generic HTTP 401 detail: `The session
could not be refreshed.` A previously rotated token is treated as replay: every
still-active token in that family is revoked. There is no replay grace period.

## Logout

`POST /api/v1/auth/logout` reads the refresh cookie when present, revokes that
stored token's family when it can be identified, clears the cookie, and returns
`204 No Content`. It remains idempotent for missing, malformed, unknown,
expired, and already-revoked cookies.

Logout revokes future refresh capability for that family. The client must also
discard its in-memory access token. UniPM does not maintain a JWT denylist, so
an already issued access token remains cryptographically valid only until its
short expiry unless the user becomes inactive.

## Cookie, Origin, And CORS Boundary

The host-only `unipm_refresh` cookie is `HttpOnly`, `SameSite=Lax`,
`IsEssential`, and scoped to `/api/v1/auth`; it has no Domain attribute. It is
Secure outside Development and may be non-Secure only for local HTTP
Development. Its expiry follows the family's original absolute expiration.

The MVP permits one exact configured frontend origin and credentialed CORS. It
does not use a wildcard origin, `AllowAnyOrigin`, or `SameSite=None`. When an
`Origin` header is present on login, refresh, or logout, it must exactly match
the configured origin or the endpoint returns a generic HTTP 403 response.
Requests without an `Origin` header remain usable for same-origin and
non-browser callers.

This is a bounded browser boundary, not a legal certification or a claim of
complete CSRF protection for every future deployment topology. Cross-site
deployment support is not implemented.

## Current User

`GET /api/v1/auth/me` requires a bearer access token and returns the current
user's `id`, `email`, `displayName`, and current roles. Token validation rejects
an account that no longer exists or is inactive.

## Provisional Roles And Policies

| Policy | Allowed roles |
|---|---|
| `CanManageAssets` | `GSD` |
| `CanManageSchedules` | `GSD`, `Supervisor` |
| `CanSubmitInspections` | `GSD`, `Inspector` |
| `CanReviewMaintenanceHistory` | `GSD`, `Supervisor`, `DepartmentHead` |

## Configuration

- `UNIPM_JWT_ISSUER`
- `UNIPM_JWT_AUDIENCE`
- `UNIPM_JWT_SIGNING_KEY` (at least 32 UTF-8 bytes)
- `UNIPM_JWT_ACCESS_TOKEN_MINUTES` (default `15`, range `1`-`1440`)
- `UNIPM_AUTH_REFRESH_TOKEN_DAYS` (default `7`, range `1`-`30`)
- `UNIPM_WEB_ORIGIN` (one absolute HTTP/HTTPS origin, default
  `http://localhost:5173`)

Refresh-session cleanup and device/session management are deliberately deferred.
Expired and revoked rows are retained through their original family expiry for
replay detection. Configuration and cookies contain no API keys or passwords.

## Development Users

Set `UNIPM_DEV_USER_PASSWORD` and run:

```powershell
dotnet run --project server -- --seed-development-users
```

It creates or repairs five fictional local accounts: `Admin`, `GSD`,
`Inspector`, `Supervisor`, and `DepartmentHead`. It never prints or persists
the supplied plaintext password.
