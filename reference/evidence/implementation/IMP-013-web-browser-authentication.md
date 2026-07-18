---
id: IMP-013
type: implementation
title: React browser authentication integration
status: reviewed
recordedAtUtc: 2026-07-18T17:57:08Z
sourceBranch: feat/web-auth-integration
sourceCommit: 8b2f1436fa6c15962f3db04dc66ba4dd8aa266b1
evidenceLevel: source-inspected
---

# React Browser Authentication Integration

## Objective

Integrate the existing login, refresh, logout, and current-user API contracts
into the React application without exposing refresh tokens to JavaScript or
expanding the provisional institutional authorization model.

## Implementation Summary

The browser now has an accessible email/password login form backed by TanStack
Form and Zod. Login and refresh responses receive runtime validation before the
access token enters the minimal Zustand lifecycle store. The access token stays
in memory; the server-managed `unipm_refresh` HttpOnly cookie is used only by
credentialed generated API calls.

An idempotent session coordinator restores a browser session on startup, seeds
the generated current-user TanStack Query cache, invalidates protected routes,
and coordinates one refresh flight per session generation. The Axios boundary
attaches the current Bearer token only to non-auth API requests and permits one
replay after an ordinary 401. It preserves request configuration and does not
replay an aborted request. Login, refresh, and logout are excluded from
automatic refresh.

A monotonic session generation prevents late bootstrap or refresh results from
overwriting a newer login or restoring a session after logout. Cookie-mutating
login and logout requests are serialized behind any older refresh response, so
the transition request remains the final cookie writer; a newer generation
never reuses the older refresh promise. Logout still clears the token and
complete Query cache before waiting for network work, remains locally final
when server revocation cannot be confirmed, and presents only a bounded warning
in that case. Current-user identity remains Query-owned and is not copied into
Zustand.

## User Interface And Tests

The login and minimal authenticated shell use semantic project tokens and the
locally exported approved UniPM logo. Their palette, spacing, card, button,
sidebar, and top-bar treatment were source-inspected against Figma nodes
`4010:4`, `2310:28`, `2338:152`, and `2010:3`. This is source-inspected visual
alignment only, not pixel-perfect fidelity, accessibility certification, final
branding approval, or final responsive certification.

Vitest/MSW coverage exercises session restoration, malformed responses,
current-user ownership, safe login errors, generation-bound refresh
single-flight behavior, bounded replay, cancellation, Query clearing, and
generation-crossing cookie-writer ordering. Playwright uses fictional
intercepted responses and actual browser cookie handling to prove that a stale
Session A refresh settles before logout and Session B login write their cookies,
and that a later Session B 401 creates a new refresh flight. It also covers
login, protected restoration, logout, redirect safety, keyboard submission,
and the no-browser-storage token boundary without ASP.NET, SQL Server, Docker,
secrets, or real accounts.

## Boundaries

No backend runtime code, database migration, generated API output, operational
module, role-aware navigation, registration, password recovery, MFA, SSO,
session-management UI, mobile authentication, or production deployment is
included. Final RBAC remains provisional. The implementation does not claim
complete CSRF protection, complete accessibility conformance, multi-browser
certification, or production authentication readiness.
