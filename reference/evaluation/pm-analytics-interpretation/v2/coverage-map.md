---
id: NLA-INTERPRETATION-V2-COVERAGE
status: provisional
---

# v2 phase 5 coverage map

Case IDs use `<familyId>-<proposedSplit>-<language>`, with language values `en`, `fil`, and `taglish`. Each listed family therefore identifies exactly three sibling cases. Every row below is provisional and awaits human review.

English is the formally supported natural-language analytics input for the current UniPM scope. Filipino and Taglish are exploratory robustness slices in this corpus; the table describes evaluation data, not additional product language commitments.

| Coverage dimension | Exact family IDs / case IDs | Notes |
|---|---|---|
| English, Filipino, and Taglish corpus slices | F01-F07, C01-C10, U01-U07, A01-A06 | Every family contains one case in each language. There are 30 cases per language; Filipino and Taglish are exploratory, not production-support claims. |
| Natural free phrasing | F01-F07, C01-C10, U01-U07, A01-A06 | All questions are free phrasing except the four English strict-template controls listed below. |
| Strict-template controls | `nla2-F02-dev-en`, `nla2-F03-dev-en`, `nla2-F04-dev-en`, `nla2-F06-heldout-en` | The tag appears only on the English Valid case in each family. Their Filipino and Taglish siblings remain free phrasing in the same family split. Three controls are in development and one in held-out. |
| Count versus percentage wording | F01, F05, F02 | F01 explicitly asks for a Progress count; F05 explicitly asks for a Progress percentage; F02 uses OnTimeCompliance percentage. |
| Supported metrics | F01-F05 in development; F06-F07 in held-out | Development covers Progress, OnTimeCompliance, CompletedLate, and NonOperational. |
| All four supported PM asset categories | F01-F05 in development; F06-F07 in held-out | Development covers fire extinguisher, fire alarm, emergency light, and water drinking station. |
| Explicit month and year | F01-F07 | Every Valid case names both. Dates in non-valid cases test omissions, conflicts, and rejection paths. |
| Department filtering | F04 in development; F06 in held-out | Uses fictional department labels only. |
| Grouping by department | F03 and F05 in development | Both use the existing `Department` grouping. |
| Abbreviations | C09 | `FE` is deliberately unapproved and expects `NeedsClarification` with `AssetCategory`; it does not teach or authorize an alias. |
| Missing metric | C01 | Clarification field `Metric`. |
| Missing category | C02 | Clarification field `AssetCategory`. |
| Missing month | C03 | Has an explicit year; clarification field `Month`. |
| Missing year | C04 | Has an explicit month; clarification field `Year`. |
| Conflicting months | C06 | Offers May or November in one year; clarification field `Month`. |
| Conflicting years | C07 | Offers 2025 or 2026 for one month; clarification field `Year`. |
| Conflicting categories | C08 | Offers two supported categories; clarification field `AssetCategory`. |
| Clearly unsupported category | U01 | Names elevators, outside the four-category schema; expected status `Unsupported`. |
| Unsupported metric | U02 | Requests cost savings; expected status `Unsupported`. |
| Invalid CPMP months | U03, U04 | Fire alarm in November and fire extinguisher in June are outside their scheduled-month sets; expected status `Unsupported`. |
| Unsupported grouping | U05 | Requests grouping by building; expected status `Unsupported`. |
| Comparisons | U06 | Requests comparison across multiple periods; expected status `Unsupported`. |
| Relative-date request | C05 | Requests the previous PM cycle without a month/year; clarification fields `Year` and `Month`. |
| Requests to invent values | A01 | Adversarial class, expected status `Unsupported`. |
| Requests for SQL | A02 | Adversarial class, expected status `Unsupported`. |
| Authorization bypass requests | A03 | Adversarial class, expected status `Unsupported`. |
| Prompt injection | A04 | Adversarial class, expected status `Unsupported`. |
| Requests for secrets and private data | A05 | Requests provider keys and employee phone numbers; expected status `Unsupported`. |
| Requests to mutate PM records | A06 | Held-out family asks to change schedules and asset status; expected status `Unsupported`. |

## Coverage by proposed split

| Split | Families | Cases | Valid | NeedsClarification | Ordinary Unsupported | Adversarial Unsupported |
|---|---:|---:|---:|---:|---:|---:|
| Development | 20 | 60 | 15 | 15 | 15 | 15 |
| Held-out, not evaluated | 10 | 30 | 6 | 15 | 6 | 3 |

Expected status counts within each language:

| Proposed split | Language | Valid | NeedsClarification | Ordinary Unsupported | Adversarial Unsupported | Expected Unsupported total | Cases |
|---|---|---:|---:|---:|---:|---:|---:|
| Development | Each of English, Filipino, and Taglish | 5 | 5 | 5 | 5 | 10 | 20 |
| Held-out, not evaluated | Each of English, Filipino, and Taglish | 2 | 5 | 2 | 1 | 3 | 10 |

The four supported metrics, all four supported categories, a department filter, and department grouping are present in development. This map establishes only intended synthetic coverage. It does not establish label correctness or language validation.
