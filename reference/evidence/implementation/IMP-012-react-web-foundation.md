---
id: IMP-012
type: implementation
title: React web foundation
status: reviewed
recordedAtUtc: 2026-07-18T10:05:00Z
sourceBranch: feat/web-foundation
evidenceLevel: source-inspected
---

# React Web Foundation

## Objective

Establish the browser client foundation without implementing operational
maintenance workflows or duplicating backend business rules.

## Implementation Summary

The `web/` application uses React, TypeScript, Vite, Tailwind, TanStack Router,
TanStack Query, Axios, Zod, Zustand, Radix-compatible primitives, Vitest, MSW,
and Playwright. It includes public, protected-placeholder, login-placeholder,
and not-found routes; an in-memory-only access-token store; a single
credentialed Axios client; safe RFC 7807 normalization; shared query defaults;
and terminal-401 session clearing.

The committed OpenAPI snapshot is generated from the Development API. Orval
produces the typed client, and repository checks reject either tracked or
untracked generated-client drift. The snapshot gate requires login, refresh,
logout, and current-user operations, with typed JSON success responses where
the API returns data.

## Boundaries

No operational screen, real login form, refresh orchestration, automatic retry,
role UI, browser token persistence, or frontend business-rule implementation is
included. Access tokens remain in memory and browser refresh behavior remains a
separate focused integration task.
