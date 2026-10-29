---
id: TEST-191
type: test-run
title: Non-operational finding attention and Supervisor referrals
status: executed
recordedAtUtc: 2026-10-29T13:32:36Z
testedCommit: d970d527ed35b91ebc9fe3e84c7c1a21811bb6d0
sourceBranch: feature/nonoperational-attention
evidenceLevel: locally-executed
---

# Finding attention and Supervisor referrals

The implementation adds all-period finding counts and a paginated, oldest-first
list to the dashboard. The stages distinguish findings awaiting acknowledgement,
acknowledged findings without a WMS referral, and referred findings. These are
historical non-operational findings, not assertions of current asset condition.
Draft and Submitted findings remain outside official inspection history.

Form review reuses the existing private photo viewer and shows non-operational
rows first. GSD and Supervisors can use the existing WMS-number editor on eligible
acknowledged forms. Server read scopes constrain Supervisor access. Existing
completion, acknowledgement, revision and audit checks remain in force. Recording
a reference does not represent repair completion or manage external WMS work.

## Verification

Checks ran on Windows with installed .NET, Node and Chromium dependencies,
against the working-tree implementation before the commits identified above.
The final application commit contains that source with its reviewed label text.

- The focused `InspectionWmsReferralEndpointsTests` run passed all eight cases.
  Its new case verifies assigned-Supervisor access, denial for another
  Supervisor's finding, stage counts, referral transition, handoff access and
  audit persistence. Existing eligibility and revision tests also passed.
- Web TypeScript checking, lint, API contract checks, client generation and
  the production build passed.
- An ignored Playwright probe used synthetic API fixtures for both GSD and
  Supervisor sessions. It checked dashboard attention navigation, Submitted-form
  thumbnail opening and closing, and no horizontal overflow at 1440 and 1536
  pixels. For both roles, it also entered a PM number on an Acknowledged form,
  verified the submitted revision and displayed the saved reference.
- Screenshots were rendered and inspected. The probe uses synthetic photo bytes.
  It does not establish physical-device photo capture or live Blob access.

The initial photo probe failed because it clicked the dashboard thumbnail while
the lazy-loaded destination route was still pending. Navigation removed that
old popup. Waiting for the destination's Inspection rows heading resolves the
race. A second probe issue used the superseded Supervisor banner text; its
selector was corrected to the retained read-only-form banner. No photo-popup
production behavior or assertion about photo availability was weakened.

Raw local outputs and screenshots are ignored under `artifacts/`, including
`nonoperational-backend-test.log`, `nonoperational-typecheck.log`,
`nonoperational-lint.log`, `attention-build.log`, `attention-role-verified.log`
and `attention-complete-final.log`. The reproducible local browser probe is
`artifacts/attention-qa.mjs`, with fixture modules derived from committed browser
fixtures. No live GSD record or provider call was used for this feature check.

## CI selector follow-up

Head `6988066670d093963ed1195697ea49c0abe60a5a` passed backend CI.
Web CI passed its unit, coverage, contract and build checks, then reported
62 browser scenarios passed, two skipped and one failed. The inspection-detail
check selected `Recommendation` by substring, matching both the enclosing
section heading and the field heading. It now selects the exact heading role
and name. The same single affected scenario passed locally without changing
its visibility or record-content assertions. The source correction was tested
as an uncommitted change based on that head. Output is retained in
`artifacts/attention-selector-verification.log`; the original failing CI output
is retained in `artifacts/attention-ci-failure.log`.

## Limits

The browser API responses are mocked. Native SQL Server translation, deployed
role behavior, physical phones and real photo storage were not exercised for
this feature. Historical scores and evidence records are unchanged. This record
does not claim deployment or institutional acceptance.

