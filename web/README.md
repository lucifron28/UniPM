# UniPM Web

The UniPM web foundation uses React, TypeScript, Vite, TanStack Router and
Query, Axios, Tailwind CSS, shadcn-compatible Radix primitives, Vitest/MSW,
and Playwright. Use Node 22 (`.nvmrc`) and `npm ci`.

```powershell
npm run dev
npm run typecheck
npm run test:run
npm run build
```

`VITE_API_BASE_URL` defaults to `http://localhost:5000`; the backend must allow
the local web origin `http://localhost:5173`. The committed OpenAPI snapshot is
the input for `npm run api:generate`; `api:pull` needs a running backend while
`api:generate` and `api:check` work offline. Generated code under
`src/api/generated` is not handwritten.

Axios is the sole transport, TanStack Query owns server state, Router owns URL
state, and Zustand holds only small in-memory client state. The access token is
not persisted: reload clears it. Real login, refresh restoration, and business
modules are deferred. No Figma alignment is claimed.
