---
id: IMP-046
type: implementation
title: Pre-evaluation navigation focus and deferral guidance
status: reviewed
recordedAtUtc: 2026-10-09T20:35:25Z
sourceCommit: a8541f7d0bbb602137b759c06d07f16b1cb4bae3
sourceBranch: feat/pre-evaluation-ux-motion
evidenceLevel: source-inspected
---

# Pre-evaluation navigation focus and deferral guidance

## Changes

The active primary-navigation links use Tailwind's shadow-sm utility. The
previous keyboard-focus style used box-shadow inside Tailwind's base layer, so
the later utility layer replaced it. The focused link remained keyboard
reachable but had no distinct visible focus ring. The navigation now uses a
two-pixel maroon outline with a two-pixel offset. A Chromium test checks its
computed style on the active desktop link.

The GSD deferred-enrollment panel now explains that a next eligible cycle in a
future calendar year is a planning marker. UniPM generates its schedule when
that year begins. This clarifies the current-year-only generation rule without
changing schedule generation or CPMP frequency.

## Scope

Only primary-navigation focus styling, GSD deferral wording, and focused web
assertions changed in this follow-up. Roles, API contracts, schedule behavior,
assignment, inspection, acknowledgement, and reporting logic did not change.

## Verification

See TEST-062 for exact source identity, commands, results, and limitations. The
record also links local synthetic before/after screenshots of the navigation
focus state.
