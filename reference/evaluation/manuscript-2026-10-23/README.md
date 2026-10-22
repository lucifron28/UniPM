# Manuscript evidence collection, October 23, 2026

This collection prepares the manuscript's existing BB-01 through BB-32 cases
for traceable execution. It does not claim those 32 cases have passed.

The [manuscript-ready NLA tables and paragraph](manuscript-nla-interpretation-tables.md)
are prepared for Section 7.3.4 and Appendix A, with scope notes and evidence links.

Update after researcher review: the user approved the 18 declared NLA outcomes.
The revised, frozen deterministic development evaluation matched 18/18 cases
and 69/69 assertions; see TEST-139. This does not complete manual BB cases or
numerical verification. References to pending review below describe the
initial collection state unless noted otherwise.

- `black-box-execution-register.csv` contains 32 pending cases based on the
  supplied manuscript. Expected results are proposed and require human
  confirmation before execution. BB-08 explicitly separates saving a completed
  inspection row from whole-form submission; schedule completion occurs when
  the row is saved, not when the form is acknowledged.
- `nla-question-review-register.csv` copies the 18 existing provisional V3
  questions and expected assertions for human review. No label has been approved
  or changed by this collection. The initial diagnostic was blocked by a corpus
  line-ending discrepancy. After correction, the register contains deterministic
  diagnostic outputs; these are not live DeepSeek or complete API results.
- `nla-review-packet.md` presents all 18 comparisons for researcher review.
  Approval remains pending. The user volunteered to perform the review.
- [TEST-137](../../evidence/test-runs/TEST-137-manuscript-evidence-collection-start.md)
  records the executed automated checks and their limits.
- [TEST-138](../../evidence/test-runs/TEST-138-v3-corpus-lf-diagnostic.md)
  records the line-ending correction and successful diagnostic execution.

## Collection rules

Record the exact version, dataset, execution time, actual outcome, tester and
evidence path for each executed black-box case. Retain failures and retests as
separate events. Automated regression results are supporting engineering
evidence; they do not automatically mark a manual BB case as passed.

Use a controlled dataset and fixed reporting date for numerical comparisons.
Retain expected calculations and actual backend results separately. InMemory
test execution does not establish native SQL Server 2019 compatibility or
correct values in the deployed database.

The 18-question corpus is a development set, not a held-out evaluation.
Review expected outcomes before scoring. Clarification tests must check the
completed interpretation and final scope when claiming end-to-end correctness.

Completed SUS responses, ISO-informed questionnaires and historical black-box
records are awaiting user delivery. Do not infer individual responses or
recreate missing historical execution records from the manuscript's summaries.

## Next execution steps

1. Confirm the working version and proposed BB expected results.
2. Review the retained diagnostic and disagreements. The line-ending discrepancy
   is resolved; the expected labels have not been changed to fit the output.
3. Review the 18 expected outcomes and seven calculation fixtures with a human
   reviewer before reporting interpretation accuracy or accepted numerical values.
4. Execute the API/browser cases against an identified disposable dataset, and
   record physical-device offline, QR and acknowledgement cases separately.
5. Verify respondent calculations when the original responses arrive.

No new provider calls, live database writes, deployments, migrations or reseeds
are authorized by this document itself.
