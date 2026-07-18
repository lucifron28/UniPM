# UniPM Web

The UniPM web application uses React, TypeScript, Vite, TanStack Router and
Query, Axios, Zustand, TanStack Form, Tailwind CSS, shadcn-compatible Radix
primitives, Vitest/MSW, and Playwright. Use Node 22 (`.nvmrc`) and `npm ci`.

```powershell
npm run dev
npm run typecheck
npm run test:run
npm run build
```

`VITE_API_BASE_URL` defaults to `http://localhost:5000`; the backend must allow
the local web origin `http://localhost:5173`. The committed OpenAPI snapshot is
the input for `npm run api:generate`; `api:pull` needs a running backend while
`api:contract:check`, `api:generate`, and `api:check` work offline. Generated
code under `src/api/generated` is not handwritten.

## Browser Authentication

The browser client implements email/password login, refresh-cookie session
restoration, protected routes, current-user loading, and logout through the
generated `/auth/login`, `/auth/refresh`, `/auth/me`, and `/auth/logout`
contracts. Axios is the sole transport and applies at most one replay after a 401. Concurrent 401 responses share one refresh request.

The access token exists only in Zustand memory. It is never stored in
`localStorage`, `sessionStorage`, cookies, URLs, or logs. The backend owns the
HttpOnly refresh cookie. TanStack Query owns the current authenticated user;
the auth store contains only the access token and session status. Logout clears
local auth and query state even when server-side revocation cannot be confirmed,
and the UI warns the user in that case.

For local authentication, run the API at `http://localhost:5000`, the web app at
`http://localhost:5173`, and keep the backend CORS origin configured for that
exact web origin. Create the five fictional Development users with:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:UNIPM_DEV_USER_PASSWORD = "<local-secret>"
dotnet run --project ..\server -- --seed-development-users
```

Do not commit or print the local password. The Development users reuse the
fixture's deterministic actor IDs.

## Visual Reference

The login and shell styling were source-inspected against the approved UniPM
Figma nodes `4010:4`, `2310:28`, `2338:152`, and `2010:3`. The implementation
uses the approved maroon, neutral, and status tokens plus a local exported logo.
This is implementation alignment, not a pixel-perfect or final institutional
design certification.

Registration, password recovery, MFA, SSO, final institutional RBAC, and
operational web modules remain deferred. The authenticated shell intentionally
contains only real identity data, logout, and a dashboard placeholder; it does
not invent maintenance records, metrics, or workflows.
