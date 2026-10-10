---
id: TEST-070
type: test-run
title: CPMP Water Drinking Station frequency clarification
status: executed
recordedAtUtc: 2026-10-10T08:50:43Z
testedCommit: 84347dfa21267e2528aa0647261e20c56a58cd6c
sourceBranch: feat/pre-evaluation-ux-motion
evidenceLevel: locally-executed
---

# CPMP Water Drinking Station frequency clarification

## Objective

Resolve whether the existing Water Drinking Station cycle months match the
operative CPMP schedule table, and verify the category reference contract with
the focused backend test. No schedule code, migration, or test logic changed.

## Authoritative source review

The project copy of
`Comprehensive_Preventive_Maintenance_Program_OCR_final.pdf` was reviewed
directly. Its SHA-256 is
`4BAF983C8FCBD532F39112C68FF1616CD79B35DC9791C3BC9B7B9200427F3F84`.

- Section 2.4.2, PDF page 3, lists Water Drinking Stations in February, May,
  August, and November.
- Section 2.7, PDF page 19, has a revision-history entry describing Water
  Drinking Stations as scheduled in June and December.

These statements conflict within the manual. The project owner's 2026-10-10
decision is to follow the operative schedule table in Section 2.4.2. The
revision-history inconsistency remains documented; no correction to the
manual is claimed.

## Source and test result

At source commit `84347dfa21267e2528aa0647261e20c56a58cd6c`,
`server/Features/ReferenceData/CpmpScheduleFrequency.cs` maps Water Drinking
Stations to months `[2, 5, 8, 11]`. The existing
`ReferenceDataEndpointsTests.Asset_categories_returns_the_selected_study_scope`
test asserts that frequency and the other three category frequencies.

Command:

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName=UniPM.Api.Tests.ReferenceDataEndpointsTests.Asset_categories_returns_the_selected_study_scope" --logger "console;verbosity=normal"
```

Result: passed, 1 test, 0 failures. The build emitted warning CS8602 at
`server/Features/PreventiveMaintenanceForms/PreventiveMaintenanceFormEndpoints.cs:540`.

## Remaining limits

The frequency choice is resolved for this implementation and is not a
remaining release blocker. GSD approval of the schedule-generation effective
date, GSD/staging acceptance, physical-device testing, and deployment
verification remain pending. The CPMP manual still contains the documented
Section 2.4.2 versus Section 2.7 inconsistency.
