# Manuscript insertion: controlled NLA numerical verification

Insert in Section 7.3.5. Put source records and detailed comparisons in Appendix B.
Replace the earlier illustrative numerical table rather than presenting its
unverified values as executions of these scenarios. Assign the final table number.

## Method

On October 23, 2026, ten controlled report scenarios were executed using the
existing backend report-normalization and query-execution services. The data
comprised seven synthetic fixtures containing 21 persisted asset-cycle input
rows in total; one fixture had no rows. Each scenario used a specified report
plan and fixed reporting date. Disposable Entity Framework Core InMemory
stores isolated the checks from the deployed database.

Expected values were separately recomputed from the source rows using Python
arithmetic and date comparisons. The computation did not read the fixtures'
existing expected-value blocks. Counts, group membership, record membership,
numerators, denominators and measurability flags were compared exactly.
Percentage values were compared after rounding to two decimal places;
numerators and denominators were also checked exactly.

This is a separate reference implementation prepared by the same development
agent, not an independent external human review. Backend response capture
and reference calculations are retained separately for researcher inspection.

## Controlled numerical and result-selection comparison

| ID | Controlled scope and check | Expected calculation or selection | Captured backend result | Result |
|---|---|---|---|---|
| NUM-01 | Fire extinguishers; Operations; February 2024 | 3 scheduled; 3 completed; 1 late; on-time compliance = 2/3 x 100 = 66.67% | Scheduled 3; completed 3; late 1; compliance 66.67%, numerator 2 and denominator 3 | Pass |
| NUM-02 | Same Operations records; progress, missed work and non-operational findings | Progress = 3/3 x 100 = 100%; missed 0; non-operational findings 0 | Progress 100%; missed 0; non-operational findings 0 | Pass |
| NUM-03 | February 2024; non-operational matching filter | 4 eligible rows; 2 matching completed rows; completed count 2, denominator remains 4 | Eligible 4; matching 2; completed count 2, numerator 2 and denominator 4 | Pass |
| NUM-04 | February 2024 compliance by department; threshold >=60% | Operations 2/3 = 66.67% qualifies; Research 1/2 = 50% does not; retain 1 of 2 groups | Operations returned at 66.67%, numerator 2 and denominator 3; groups before 2, after 1 | Pass |
| NUM-05 | Fire extinguishers and fire alarms; 2024; category grouping | 1 persisted schedule per category; extinguisher cycles February/May/August/November; alarm cycles June/December | Scheduled count 1 per category; category cycle lists matched | Pass |
| NUM-06 | Fire extinguishers; 2025 Q1; North building; non-operational records | 1 matching source row: QTR-FEB | Eligible 1; matching 1; source record QTR-FEB | Pass |
| NUM-07 | Fire extinguishers; 2025 Q4; North building; non-operational records | 1 matching source row: QTR-NOV | Eligible 1; matching 1; source record QTR-NOV | Pass |
| NUM-08 | 5 matching records; page 2; page size 2 | Total pages = ceiling(5/2) = 3; page 2 contains PAGE-C and PAGE-D | Total pages 3; page 2 returned PAGE-C and PAGE-D | Pass |
| NUM-09 | May-June 2025; extinguisher and alarm compliance; reporting date June 15, 2025 | June alarm cycle remains open; compliance must be unmeasurable, despite numerator 1 and denominator 1 | Value null; measurable false; numerator 1; denominator 1; open-cycle reason returned | Pass |
| NUM-10 | 2024 progress with no eligible schedules | Denominator 0; progress unmeasurable rather than 0% or 100% | Value null; measurable false; numerator 0; denominator 0 | Pass |

Table notes: NUM-01 through NUM-08 and NUM-10 use the fixture reporting instant
June 1, 2025 at 00:00 UTC. NUM-09 uses June 15, 2025 at 00:00 Manila time.
The Q4 source-selection fixture deliberately contains a stored November
completion beyond its fixed June reporting instant. NUM-07 tests selection of
stored records by PM quarter; it does not validate the acceptability of future
inspection entry or demonstrate a historical November execution.

NUM-05 counts persisted schedules, not every possible calendar cycle. NUM-03
counts completed findings satisfying the condition filter, while preserving
the eligible schedule denominator. The percentage display 66.67% is rounded;
unrounded captured values are retained in the observations file.

## Summary for Chapter 7

All ten controlled scenarios matched the reference calculations and selected
source records across 75 declared field comparisons. An initial comparison
script treated the equivalent numeric representations 100 and 100.00 as
different; after correcting that representation comparison, the same captured
backend outputs matched all 75 checks. No backend calculation was changed.

The results support the tested backend calculations and result-selection
behavior under the specified synthetic conditions. They do not establish
native SQL Server execution, deployed-data correctness, independent external
acceptance, live DeepSeek accuracy, or completion of the manuscript's 32
black-box scenarios. This numerical verification is separate from the
18-question deterministic interpretation evaluation.

## Appendix B evidence

- [Controlled source records](numerical-source-records.csv): all 21 fixture input rows.
- [Expected versus actual comparisons](numerical-comparison-details.csv): all 75 fields, formulas and source asset codes.
- [Selected backend observations](numerical-backend-observations.json): plans, reporting instants, populations, measure values, source codes and pagination. Decimal values are preserved as strings where needed for precision; this does not change the API contract.
- [TEST-141 execution record](../../evidence/test-runs/TEST-141-controlled-nla-numerical-verification.md).
- [Existing fixture definitions](../pm-analytics-v3/synthetic-calculation-fixtures.json).
