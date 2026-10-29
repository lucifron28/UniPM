# V3P-001 first paired run rules

Recorded before execution, October 28, 2026. The project owner explicitly
requested completion of the test. Labels remain assistant-authored development
labels; there is no independent researcher or stakeholder approval claim.

Use the unchanged 30-question corpus with the recorded manifest hash.
Supported cases use full gold plans already validated by the backend. Report
strict plan equality and semantic agreement separately. Strict equality ignores
serialization of unused period properties as null. Semantic agreement also
allows Table versus Summary for ungrouped aggregates and order differences in
categories, measures and output columns. Column membership must still match.
Grouped plans require Table. Every other plan property must match, including
all filters, periods, grouping, ranking and sorting. No post-run exceptions.

Controls S01 and S03 require NeedsClarification with at least one non-empty
API clarification field. S02 additionally requires Period. This scores staged
clarification appropriateness, not exact clarification-field-set accuracy.
Unsupported controls require Unsupported and a null plan. Non-Valid drafts
are not executed. Incorrect acceptance or unsafe behavior is reported separately.

Call the full backend interpretation endpoint for both explicit modes using
synthetic GSD credentials and the owned disposable database. Alternate which
mode runs first per case. Run all 30 cases once per condition, with no adapter
retries, at most 30 logical DeepSeek requests. Backend transient retries remain
unchanged at a maximum of two per logical request, giving at most 90 physical
attempts. Actual attempts and token usage are unavailable through this endpoint;
do not claim exact physical-call counts or compute a cost estimate. Shared
SafetyRules rejections are system safety outcomes, not model-only outcomes.

Use the live server clock and record the UTC start. All gold periods explicitly
refer to 2025, so no relative-date interpretation is requested. The earlier fixed
clock proposal is replaced by this recorded live-clock condition before running.
No parser/prompt tuning is allowed during this round. Preserve all errors and
all paired cases. Report the four-way outcome counts across the 24 supported
questions and controls separately. Latency is backend end-to-end timing, not
isolated model computation. Eight intentions with three paraphrases each do
not establish general superiority.
