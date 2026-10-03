---
id: PLAN-NLA-001
type: planning
status: branch-authorized
sourceBranch: feature/schema-constrained-nla
---

# Schema-constrained natural-language PM analytics

## Scope and authority

This document specifies the first analytics question flow for the separately
authorized `feature/schema-constrained-nla` branch. It does not record GSD,
professor, or adviser acceptance. The flow reports existing preventive
maintenance data. It does not make maintenance decisions or publish official
history.

The first interface is a compact GSD-only panel inside the existing PM period
dashboard. It adds no route or navigation item. The dashboard remains the
source of schedule and inspection measures. The analytics request adapts a
question into the same bounded category, cycle, and department scope.

## Supported question form

Accept only a finite form, with a maximum question length of 512 characters:

`Show <metric> for <category> in <cycle> [department "<name>"] [grouped by department]`

The metric must resolve to one of `Progress`, `OnTimeCompliance`,
`CompletedLate`, or `NonOperational`. Accept the documented aliases
`progress`, `on-time compliance`, `late inspections`, and `non-operational
assets`. Map the four existing PM categories from their canonical IDs or
display names: fire extinguishers, fire alarm systems, emergency lights, and
water drinking stations. Parse an explicit cycle as `yyyy-MM` or an English
month name followed by a four-digit year, then normalize it to `yyyy-MM`. The
category and cycle must also form a CPMP-valid category-month pair. Valid date
syntax alone is not sufficient.

The optional department is one quoted exact name. `grouped by department` is
the only grouping option. Without it, use `groupBy: None`. The parser rejects
missing or duplicate fields, conflicting values, multiple categories or
cycles, unsupported grouping, extra conditions, and questions outside this
form. It returns a clarification or unsupported response. It never broadens
the request silently.

## Canonical plan and measures

The parser produces a typed plan with these fields:

```json
{
  "metric": "Progress",
  "assetCategory": "fire-extinguisher",
  "pmCycle": "2026-11",
  "department": null,
  "groupBy": "None"
}
```

The allowed metric values are `Progress`, `OnTimeCompliance`,
`CompletedLate`, and `NonOperational`. The only grouping values are `None` and
`Department`. The asset category and cycle are required. Department is optional.

The backend independently validates the canonical plan before execution. It
checks the metric, category, group, optional department, and CPMP-valid
category-month pair against the supported values. The API never treats the
parser or browser as a trusted authorization or scope boundary.

Measures use the current PM period dashboard rules and exclude cancelled
schedules. Progress reports completed inspections over the scheduled count.
On-time compliance reports inspections completed by the cycle deadline over
the scheduled count. Compliance is measurable only when the cycle is closed
and its denominator is nonzero. Completed-late counts inspections completed
after the deadline. Non-operational counts completed inspections recorded as
non-operational. Counts stay counts; the response does not present them as
percentages. Draft and Submitted forms do not remove completed field work from
these operational measures. Count metrics are measurable only when at least one
eligible schedule is in scope. When schedules exist but no matching event
occurs, the value is numeric zero; when no schedules are in scope, the value is
null and `IsMeasurable` is false. These results are not official maintenance
history.

Department, when supplied, narrows the authoritative totals. Grouping by
department returns departmental groups for the same selected category and
cycle. Display-only filters do not alter the measures.

## Response and sources

For a valid plan, return the canonical `plan`, `deadline`, `periodState`,
`result`, `groups`, `sources`, `totalSourceCount`, `sourcesTruncated`, and
`scopeNote`. A result or group contains `department` when grouped,
`numerator`, `denominator`, `value`, `unit`, and `isMeasurable`. A source
contains `scheduleId`, `assetId`, optional `inspectionId`, `assetCode`,
optional `department`, `pmCycle`, `deadline`, optional
`inspectionCompletedAt`, `timeliness`, `condition`, and optional `formStatus`.

Return at most 100 sources with the total count and truncation flag. Web
source links use the existing asset and PM review routes. The UI shows the
parsed plan, measure definition, cycle state, result, and source rows. Format
deadlines and completion times in Asia/Manila with the offset visible. Do not
make raw identifiers the primary source label.

## Access, privacy, and provider boundary

Only GSD users may use the panel or query endpoint. Enforce this in the API;
the web role check is a visibility aid, not the security boundary. Other roles
must not gain access through a direct API request.

The API reads the existing PM schedule, inspection, asset, and form data used
by the PM period dashboard. It does not query ReferenceDocument content, FTS,
section embeddings, or the shared embedding provider. It does not send a
question or operational data to a model or external provider. Do not log or
persist the raw question. Do not return remarks, recommendations, signatures,
or signatory data.

The initial interpreter is deterministic. No SQL is generated from question
text. A future interpreter replacement requires separate approval and must
produce the same typed plan, pass the same allowlist, and preserve these source,
scope, access, and privacy rules. Open-ended chatbot answers and autonomous
maintenance decisions remain out of scope.
