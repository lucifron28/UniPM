# Manuscript evidence collection, October 23, 2026

The [post-correction DeepSeek rerun](manuscript-deepseek-correction-results.md)
records TEST-143's focused local regression and TEST-144's new live comparison.
Exact agreement improved from 13/18 to 16/18, with one retained catalog
normalization difference and one inconsistent model envelope rejected safely.
The latter remains a failure; no case was retried or relabeled. This is
development verification using questions already inspected during correction.

The [next correction plan](../../planning/nla-v3-clarification-recovery-plan.md)
defines a narrow recovery for the contradictory ranking envelope and a
prospective, separately reported catalog-equivalence comparison. It is
planning only and preserves the original strict results.

This collection prepares the manuscript's existing BB-01 through BB-32 cases
for traceable execution. It does not claim those 32 cases have passed.

The [manuscript-ready NLA tables and paragraph](manuscript-nla-interpretation-tables.md)
are prepared for Section 7.3.4 and Appendix A, with scope notes and evidence links.

The [supplementary live DeepSeek comparison](manuscript-deepseek-comparison.md)
now records the same frozen 18 questions, with 17 provider responses and one
pre-provider safety rejection. TEST-142 records final status matches of 16/18,
exact declared-expectation matches of 13/18 and plan assertion matches of
67/69. Typed model candidates matched exactly in 9/17 cases before backend
controls. Five final differences remain, including an ambiguous ranking
accepted without the expected clarification. The case CSV and sanitized typed
observations retain every result. No report was executed or model case retried.

The [recorded conclusion and correction plan](../../planning/nla-v3-deepseek-correction-plan.md)
trace the five differences to the tested source and distinguish backend
defects, interpretation-policy choices and catalog-normalization scoring.
This follow-up is source inspection and planning only; it makes no correction
or additional verification claim.

The [controlled numerical verification table](manuscript-nla-numerical-verification.md)
is prepared for Section 7.3.5 and Appendix B. TEST-141 records ten captured
backend InMemory scenarios and 75/75 field matches against separate Python
calculations. Source rows, expected/actual comparisons and selected backend
observations are included. This is not native SQL or deployed-data verification.

Update after researcher review: the user approved the 18 declared NLA outcomes.
The revised, frozen deterministic development evaluation matched 18/18 cases
and 69/69 assertions; see TEST-139. TEST-141 separately records controlled
numerical execution; neither result completes the manual BB cases or native
SQL verification. The researcher also reviewed and approved the earlier evidence
package on October 23, 2026. Approval covers the recorded results, proposed BB
expected outcomes, tables and stated limitations; it does not establish
unrecorded test execution or external adviser/GSD approval.

- `black-box-execution-register.csv` contains 32 pending cases based on the
  supplied manuscript. Expected results have researcher approval; execution
  and actual-result evidence remain outstanding. BB-08 explicitly separates saving a completed
  inspection row from whole-form submission; schedule completion occurs when
  the row is saved, not when the form is acknowledged.
- `nla-question-review-register.csv` copies the 18 existing provisional V3
  questions, approved expected assertions and researcher review decisions. The initial diagnostic was blocked by a corpus
  line-ending discrepancy. After correction, the register contains deterministic
  diagnostic outputs; these are not live DeepSeek or complete API results.
- `nla-review-packet.md` preserves all 18 pre-review diagnostic comparisons.
  Researcher review is complete; the revised frozen results are in TEST-139.
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

The supplied final SUS/ISO results PDF is the user-designated final results
source. Original per-item SUS responses and historical black-box execution
records remain unavailable in this repository. Do not infer individual responses or
recreate missing historical execution records from the manuscript's summaries.

## Next execution steps

1. Identify the exact working version and dataset for the approved BB cases.
2. Review the retained diagnostic and disagreements. The line-ending discrepancy
   is resolved; the expected labels have not been changed to fit the output.
3. Retain the completed 18-question researcher review, TEST-139 score and
   TEST-141 controlled numerical evidence. Review the new numerical tables
   and retain the InMemory/native-SQL distinction when reporting results.
4. Execute the API/browser cases against an identified disposable dataset, and
   record physical-device offline, QR and acknowledgement cases separately.
5. Align manuscript survey reporting with the final supplied results PDF and
   retain its stated source limitations.

No new provider calls, live database writes, deployments, migrations or reseeds
are authorized by this document itself.
