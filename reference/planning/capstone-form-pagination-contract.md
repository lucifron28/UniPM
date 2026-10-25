# PM form pagination contract

This implements remediation Phase 7. It preserves the existing form filters
and role scope; it does not introduce another form lifecycle or change report
calculations.

## Form list

`GET /api/v1/preventive-maintenance-forms`

Existing `status`, `assetCategory`, `department`, `pmCycle` and `search`
parameters remain. Add:

- `page`, default 1, positive integer.
- `pageSize`, default 25, from 1 through 100.
- `supervisor`, optional account GUID or `unassigned`. This moves the existing
  web filter into the server query; it does not grant another user's access.

Return the existing registry envelope convention:

```json
{"items":[],"page":1,"pageSize":25,"totalCount":0}
```

Apply caller visibility first, then every requested filter, then count and
page. Sort by `CreatedAt DESC, Id ASC`. Load inspection/asset/schedule/photo
navigation data and user-display names only for the selected page. Reject
invalid or overflow-producing pagination values with validation errors. A
valid page beyond the result set returns empty items with the requested page
and the filtered total count.

Supervisor filtering preserves the previous web predicate: a form matches
when at least one row has the requested current schedule supervisor, or a
null current supervisor for `unassigned`. An empty form does not match that
sentinel. All filtering remains inside the caller's readable form scope.

## Filter options

`GET /api/v1/preventive-maintenance-forms/options`

Use the same operational role and readable-form scope. Return:

```json
{"categories":[],"departments":[],"pmCycles":[],"totalCount":0}
```

These are distinct scalar metadata values and the total number of readable
forms before list filters. Do not load complete form graphs to build options.
This replaces the web page's second unpaged form download. Existing supervisor
account suggestions continue using their existing authorized endpoint.

## Clients and verification

The web list requests ten rows per page, renders `items`, uses `totalCount`
for pagination/counts, and uses the options endpoint for dropdowns and the
no-data versus no-match distinction. It must not filter supervisors or slice
an already paged response locally.

The mobile repository's existing complete-list interface remains. Its HTTP
implementation reads the page envelope and explicitly follows subsequent
pages at size 100 for callers and offline caching that require the complete
authorized set. Validate page metadata, avoid duplicate IDs, and stop/error
on inconsistent responses rather than silently treating page one as complete.
This is offset pagination, not a historical snapshot guarantee during
concurrent changes. Preserve pending local writes when refreshing cached data.

Refresh the OpenAPI snapshot and generated client from an isolated loopback
API on port 5198, using `UNIPM_OPENAPI_URL` explicitly. Disable schedule
generation and AI, and supply no application database connection. Do not
pull from another running checkout on the default port. Regeneration belongs
to the client executor; backend code/tests belong to the backend executor.

Test empty results, page boundaries, stable ordering, invalid inputs,
supervisor filters and authorization before totals/paging. Include a native
SQL Server 2019 scope/pagination case using a fresh synthetic GUID database.
Update existing contract checks and web/mobile mocks to the envelope; retain
their behavioral assertions.
