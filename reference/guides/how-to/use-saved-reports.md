# Use saved reports

Authenticated GSD users can save private report settings on the analytics
page. A saved report belongs to its creator. Saving stores its validated
definition and date preferences; each confirmed run reads current data.

## Save and reopen

1. Prepare a report, create its preview and check the scope.
2. Enter a name under **Save the reviewed report settings**.
3. For PM execution, choose **Keep the reviewed dates** or a relative period.
4. Select **Save as new report**.
5. Expand **My reports** and select **Open preview** beside a saved report.
6. Review the resolved scope and confirm execution.

Reopening a saved report does not call DeepSeek or the deterministic question
interpreter. Backend validation still applies before preview and execution.
The saved definition contains no cached result rows or original question text.

Relative periods resolve when a new preview opens, using Philippine time.
The resolved dates remain fixed for that preview until it expires. Open a new
preview to resolve them again. Existing CPMP rules apply: a monthly report
must contain a scheduled cycle for the selected categories. A saved report
whose categories, values or catalog are no longer supported must be corrected
before it can run.

## Change or remove settings

- **Edit settings** opens the stored plan in the editor. Make changes, create
  a new preview and select **Replace settings for** the intended report.
- **Save as new report** creates a separate definition.
- **Rename** updates the name and revalidates the stored settings.
- **Delete** requests confirmation before removing the saved definition.
- **Reload my reports** retrieves the current definitions after another tab
  has changed them.

Updates and deletion include the revision displayed by the browser. If another
tab changes that revision, the API returns a conflict and leaves the newer
definition intact. Reload and review it before trying again. The API allows
up to 100 reports per user; concurrent creation is serialized per owner on
SQL Server. Names contain 1 to 120 characters without control characters.

## API and database integration

The feature adds migration `20261023131748_AddSavedAnalyticsReports` and the
`SavedAnalyticsReports` table after the existing 23 migrations. Both staging
preflights require the exact combined 24-migration schema and still reject
unexpected or partially initialized state. They do not migrate automatically.

Deployment and real-data migration are separate operational actions. The
engineering checks use disposable synthetic SQL Server databases. No existing
application database is updated by those checks.

The DELETE endpoint requires a UUID `revision` query parameter. A missing or
malformed parameter returns 400; a stale revision returns 409; an inaccessible
report returns 404. Other saved-report routes inherit authenticated GSD
authorization and filter by the authenticated owner.

Scheduled execution, sharing, automatic refresh and interpretation learning
remain outside this feature. The saved-report workflow does not change the
interpreter routing or numerical report definitions.
