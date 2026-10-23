# Use saved reports

Saved reports are available to authenticated GSD users in the analytics page.
Each saved report belongs to the user who created it.

## Save and reuse a report

1. Prepare a report and create its preview. Check its scope.
2. In **Save the reviewed report settings**, enter a name.
3. For PM execution, choose fixed dates or a relative period. Fixed dates keep
   the reviewed period. Relative choices are this month, previous month, this
   year and previous year.
4. Select **Save as new report**.
5. Expand **My reports** and select **Open preview** for a saved report.
6. Review the actual scope, then confirm execution using the existing report
   controls. Execution reads current data, including changes since the last run.

Reopening does not call DeepSeek or the deterministic question interpreter.
The saved settings go directly through backend plan validation. Results are
not stored with the saved report.

Relative dates resolve when a new preview opens, using Philippine time,
UTC+08:00. The resulting dates stay pinned to that preview until it expires.
An old preview does not silently change its dates at midnight. Open a new
preview to resolve the period again. Existing CPMP rules still apply. A monthly
period must contain a scheduled cycle for every selected category.

## Edit, rename or delete

- **Edit settings** loads the stored plan into the editor. Make changes,
  create and review a new preview, then use **Replace settings for** the
  selected report. **Save as new report** creates a separate copy instead.
- **Rename** changes the name through the existing saved-settings update.
  Updates still validate the plan and current relative period.
- **Delete** asks for confirmation and removes the saved definition.
- **Reload my reports** fetches the user's current saved definitions. Use it
  after another browser changes a report. An outdated revision cannot replace
  a newer saved definition.

The API rejects new saves when the user already has 100 reports. Names must
contain 1 to 120 characters without control characters.

## Database setup

The feature adds the `SavedAnalyticsReports` table through migration
`20261023131748_AddSavedAnalyticsReports`. After configuring the intended
development database using the existing backend setup, apply migrations with:

```powershell
dotnet ef database update --project server/server.csproj
```

Feature implementation did not apply this migration. SQL Server execution and
a combined browser-to-database run remain unverified.
[TEST-148](../../evidence/test-runs/TEST-148-saved-reports-backend-verification.md)
records the backend InMemory check;
[TEST-149](../../evidence/test-runs/TEST-149-saved-reports-web-typecheck.md)
records TypeScript checking; and
[TEST-150](../../evidence/test-runs/TEST-150-saved-reports-chromium-workflow.md)
records the passing Chromium workflow with controlled API responses.

## Storage and execution rules

The table stores owner ID, name, normalized typed plan JSON, period mode,
revision and update time. It stores no question text, provider payload or
result rows. Every saved-report endpoint requires the existing GSD role.
Owner filtering applies to listing, updating, deleting and opening a preview.
An inaccessible report returns 404.

Opening a preview checks current schema/catalog compatibility and selected
values. Confirmation uses the existing user-bound, expiring preview token.
Execution revalidates the plan and queries current records. Saved reports do
not grant additional access or add parser mappings.

Scheduled execution, sharing, automatic refresh and automatic interpretation
learning are outside this feature.
