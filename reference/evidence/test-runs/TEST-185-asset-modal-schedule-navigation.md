---
id: TEST-185
type: test-run
title: Asset creation modal and schedule navigation consistency
status: executed
recordedAtUtc: 2026-10-29T06:32:00Z
testedCommit: 8b932ae9305ee807f57a1a640846a98ce8053375
sourceBranch: feature/asset-modal-schedule-navigation
evidenceLevel: locally-executed
---

# Asset modal and schedule navigation

The asset-registry Add asset button now opens a native modal using the existing
creation form, validation, role checks and cache invalidation. A successful save
closes the modal and keeps the registry URL and filters. Escape, Cancel and the
close button dismiss it unless creation is pending. The legacy direct creation
URL remains available for existing links.

Schedules now has one page heading above its three tabs. Child views use
section headings, with History retaining a standalone heading for existing
component consumers. A shared search schema accepts assignments, tracking and
history in the index route, detail route and nested return contexts. Detail
links from tracking and planning now carry their originating view and complete
registry search. Planning's building filter is saved in the URL, while retaining
immediate local filtering feedback.

## Executed checks

The existing focused command was run without changing tests:

```text
npm run test:run -- src/features/schedules/schedule-workflow.test.tsx
```

The initial run passed 24/25. Its building-filter check showed that a component
whose caller does not immediately update search props lost immediate filtering
feedback. One focused correction retained local selection while deriving its
saved value from the route. The same file was rerun once and passed 25/25.
No full local suite, separate type check or lint run was performed.

Playwright browser inspection used the existing running Vite app and synthetic
API responses at a 1536 by 960 desktop viewport. The modal rendered within the
viewport. A mocked successful creation closed it without navigation away from
the filtered registry. History, Planning and Work tracking each displayed one
Schedules H1. Their navigation bounding boxes were identical: x 288,
y 201.640625, width 1193, height 40. Opening schedule details and following
Back to schedules returned to view=history and view=tracking respectively,
retaining year=2026. The completed schedule walkthrough reported no page errors.

Early browser-helper attempts required narrowing API interception so it did not
intercept frontend modules, and opening the history drawer before selecting its
detail link. These were inspection-helper corrections, not application fixes
or changes to test assertions. The asset save was mocked and made no database
write. The screenshot and observations are development UI evidence only.

Ignored artifacts include schedule-navigation-unit.log,
schedule-navigation-unit-rerun.log and navigation-desktop/ containing screenshots
and observations.json. No live backend workflow, physical device, deployment,
provider call or independent acceptance evaluation was executed. Unrelated
routeTree.gen.ts line-ending state, tmp/ and web/.tanstack/ were preserved.
