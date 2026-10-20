# V3 provisional question review notes

Protocol `NLA-V3-EVAL-PROTOCOL-001` remains draft. Every decision in
[`review-decisions.json`](review-decisions.json) is pending. These observations
identify questions for an independent domain/reporting reviewer; they do not
approve or replace any expected label.

| Case | Review status | Issue for reviewer |
|---|---|---|
| V3D-001 | Pending | Confirm “scheduled cycles” means eligible persisted asset-cycle rows and the 2024 PM-cycle basis. |
| V3D-002 | Pending | Confirm “completion progress” maps to `progressPercent`, rather than a workflow/form progress measure. |
| V3D-003 | Pending | Confirm department grouping and that both measures share the eligible schedule denominator. |
| V3D-004 | Pending | The question asks to compare two categories, but expected assertions omit `groupBy: [assetCategory]`. Without category grouping, the result combines both populations and fails the comparison intent. Proposed label correction: require category grouping and a category-level result for each selected category. |
| V3D-005 | Pending | Confirm condition is based on each PM-cycle inspection finding and the intended matching-record report. |
| V3D-006 | Pending | Expected assertions omit the `onTimeCompliancePercent` measure implied by “compliance”. Confirm its meaning and current-asset building scope. |
| V3D-007 | Pending | “February and November” spans separated cycles. Confirm whether clarification should ask for one period, a multi-period union, or two results. Current V3 period shape may not represent a disjoint union. |
| V3D-008 | Pending | The `or` condition could mean a union or separate reports. The candidate expects a clarification draft with category/period, while the parser returns no plan to avoid dropping the unresolved outcome. Decide whether a partial draft is useful and explicitly non-executable. |
| V3D-009 | Pending | “Not late” can mean on-time, not completed, or exclude late records. The candidate expects a clarification draft although the parser returns none. Confirm exact wording and safe outcome. |
| V3D-010 | Pending | “No fire alarm systems” is a negative category selection, not a condition filter. Confirm that no positive category plan is appropriate and that clarification should contain no draft. |
| V3D-011 | Pending | Ranking has no metric. Confirm the candidate can safely preserve department and requested measures in a clarification draft without selecting a ranking measure. |
| V3D-012 | Pending | “Most fire alarm systems” may ask for asset inventory, a subject not in V3 Phase 5. Decide whether the correct label is unsupported for `pmExecution` or clarification for a different report subject. |
| V3D-013 | Pending | Confirm “top” means descending completed count and how tied groups are ordered. |
| V3D-014 | Pending | Confirm compliance threshold inclusivity, percent units, and whether a threshold over a partly unmeasurable group blocks the result. |
| V3D-015 | Pending | Confirm the department catalog is unresolved in the fixed corpus environment and that asking for Department is the intended response. |
| V3D-016 | Pending | Question-based sorting is representable by the editable plan. Review whether the right response is clarification with the department sort preserved or unsupported; current parser rejects automatic sorting. |
| V3D-017 | Pending | Confirm elevator is outside the approved V3 category catalog and should be rejected rather than clarified. |
| V3D-018 | Pending | Confirm this adversarial SQL request is rejected with no plan and no provider call. |

## Coverage gaps to address in a reviewed revision

The 18 cases do not test a question that omits required category or period,
relative dates, two-dimension grouping, zero eligible schedules, missing closed
cycle coverage, requests based on inspection dates, completion versus
acknowledgement, explicit category comparisons in grouped output, inventory
counts, or unsafe write/assignment requests. The calculation fixture suite
separately covers empty/unmeasurable calculations, cycles, thresholds,
pagination, filters, and source membership. Add any new question only after
reviewers agree on its expected interpretation; do not use this note as a
substitute for approval.
