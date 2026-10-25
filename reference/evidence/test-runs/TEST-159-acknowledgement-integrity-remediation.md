---
id: TEST-159
type: test-run
title: Acknowledgement integrity and capture-operator presentation
status: executed
recordedAtUtc: 2026-10-25
testedCommit: 41b988e77f0a4e6acb327cbcc0bfa7b4cb5fb9ab
additionalTestedCommits:
  - a63428211d76919978fb42683bff92dbdec597b7
  - 6b28909cdd29ec067c6a423f07c339301f71469e
sourceBranch: feature/capstone-remediation
evidenceLevel: locally-executed
---

# Acknowledgement integrity

The existing acknowledgement represents digitally captured receipt by the
entered signatory, not cryptographic signing or independently verified identity.
One receipt covers the Submitted multi-asset form. The authenticated capture
operator remains separate from the entered Department Head name and position.

At `41b988e77f0a4e6acb327cbcc0bfa7b4cb5fb9ab`:

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --filter "FullyQualifiedName~PreventiveMaintenanceFormDomainTests|FullyQualifiedName~PreventiveMaintenanceFormDraftEndpointsTests|FullyQualifiedName~SqlServerInspectionSubmissionIntegrityTests.Concurrent_acknowledgements_write_one_receipt_and_preserve_completion_timestamps" --logger "trx;LogFileName=phase5-acknowledgement-tests.trx" --results-directory artifacts/remediation/backend
```

Exit 0, 46 passed, zero failures or skips. DTO tests cover trimmed and
case-insensitive PNG MIME, invalid MIME/magic bytes and the effective base64
limit. 196,608 decoded bytes encode to 262,144 characters and are accepted;
196,609 encode to 262,148 and fail the earlier encoded-length cap. This does
not establish full image decoding or signatory identity verification.

The SQL race holds two requests immediately before saving the same Submitted
form. It observes one 200, one 409 and exactly one stored acknowledgement.
Operator, signatory, image/type/checksum and timestamp are checked separately.
Submission and inspection/schedule completion clocks remain unchanged. Both
process-only SQL test variables used local Windows-authenticated master; the
fixture created and disposed a fresh GUID synthetic database. No application
database, migration or acknowledgement runtime change was needed.

The initial `e9a2f83` test build failed because a helper was private to another
test class. `41b988e` supplies a local request and corrects the assertion helper.
The existing Submitted/Acknowledged row-update/delete rejection tests remain.

Client commits `42148b0` and `a634282` show the returned capture-operator ID on
the immediate receipt. A display name is used only when the returned ID matches
the authenticated account. No Department Head account requirement or new read
contract was added. `6b28909` formats acknowledgement time explicitly in Manila.

At `a63428211d76919978fb42683bff92dbdec597b7`, mobile acknowledgement tests
passed 11/11, Flutter analysis found no issues, web review tests passed 21/21
and web type checking passed. At final web source `6b28909cdd29ec067c6a423f07c339301f71469e`,
`npm run test:run -- src/features/preventive-maintenance-forms/preventive-maintenance-form-review.test.tsx --maxWorkers=2`
passed 21/21 on Node 22.23.4. Mobile command was
`flutter test test/preventive_maintenance_form_acknowledgement_test.dart --concurrency=2`.

Raw backend commands/TRX are in ignored `artifacts/remediation/backend/phase5*`;
client logs are `artifacts/remediation/clients/phase5*`. Targeted web lint was
reported successful but its log retains only EXIT=0, so complete command
provenance is unavailable. No deployment, physical-device signing or external
acceptance is established.
