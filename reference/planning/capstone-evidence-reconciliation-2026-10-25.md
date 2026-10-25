# Evidence reconciliation and manuscript revision proposal

Prepared for remediation Phase 9. This is a revision proposal, not an edit to
the submission manuscript or appendices. The requirements below come from the
supplied final submission manuscript, FR-01 through FR-23. Source
links identify implementation and tests; they do not assert that every named
test executed in this run. Executed evidence is identified separately.

## Functional requirement traceability

Paths below are repository-relative. Test paths begin at `tests/UniPM.Api.Tests/`
unless a `web/` or `mobile/` prefix is shown.

| Requirement | Implementation and screen | Supporting tests / executed evidence | Boundary |
|---|---|---|---|
| FR-01 | `server/Features/Auth/`; `OperationalInspectionReadScope.cs`; web route guards; mobile auth scope | `Auth/AuthEndpointsTests.cs`, `Auth/AuthorizationPolicyTests.cs`, `Inspections/OperationalInspectionReadScopeEndpointsTests.cs`; TEST-155, TEST-158 | Admin alone has technical-management access, not ordinary operational access. Shared form members can read their whole batch. |
| FR-02 | `server/Features/Assets/AssetsEndpoints.cs`; web asset registry; explicit Admin asset-update tool | `Assets/AssetReadEndpointsTests.cs`, `Registries/RegistryReadEndpointsTests.cs`, `AdminManagement/AdminOperationalEndpointsTests.cs`; TEST-155 | Ordinary asset registration is GSD. General asset editing is an explicit technical Admin function. Do not imply a GSD transfer/edit screen exists. |
| FR-03 | Asset QR/code endpoints; `mobile/lib/features/assets/`; web QR label | `mobile/test/asset_qr_lookup_test.dart`, `mobile/test/qr_scanner_test.dart`, asset endpoint tests | QR resolves an asset identifier. Scanning does not establish physical inspection. |
| FR-04 | `server/Features/Schedules/PreventiveMaintenanceScheduleGenerationService.cs`; schedule create/coverage screens | `Schedules/PreventiveMaintenanceScheduleGenerationTests.cs`; TEST-155; historical TEST-070 | CPMP frequency decision is preserved. Do not claim the conflicting manual text itself was corrected. |
| FR-05 | Schedule batch assignment/reassignment endpoints; web assignment queue | `Schedules/ScheduleBatchAssignmentEndpointsTests.cs`, web schedule workflow tests | GSD assigns supervisors; supervisors assign workers within the existing workflow. |
| FR-06 | Schedules endpoints, coverage review, enrollment-deferral review and dashboard | schedule query/generation tests; `Reports/PmPeriodDashboardTests.cs`; TEST-155/156 | Final institutional schedule-adjustment authority remains a policy decision. |
| FR-07 | `PreventiveMaintenanceFormEndpoints.cs`; mobile category forms; web form detail | `Forms/PreventiveMaintenanceFormDraftEndpointsTests.cs`, `mobile/test/preventive_maintenance_form_draft_test.dart`; TEST-156/159 | One form is a department/category/PM-cycle batch with multiple asset rows. |
| FR-08 | Draft inspection-row create/update/delete; mobile row editor | same Draft endpoint/widget suites; TEST-156 | Entered date and findings are operator-supplied. Current asset row labels are not historical snapshots. |
| FR-09 | Backend first-save completion and schedule transition | Draft endpoint and dashboard tests; TEST-156 | Server receipt is authoritative. Delayed synchronization can count as late. |
| FR-10 | `InspectionPhotoEvidenceEndpoints.cs`, storage implementations; mobile photo card | `Inspections/InspectionPhotoEvidenceEndpointsTests.cs`, storage/JPEG/race suites; `mobile/test/inspection_photo_evidence_test.dart`; TEST-155 | Reads are scoped. Writes require the Inspector row owner and Draft state. A standalone GSD session cannot upload/delete a photo. |
| FR-11 | Mobile offline repository and account/origin cache | `mobile/test/offline_inspection_repository_test.dart`; TEST-156/158 | Only cached supported records are available offline. Fresh network lookup remains network-dependent. |
| FR-12 | Encrypted offline Draft persistence and resume | offline repository and Draft widget tests; TEST-156/158 | A failed persistence write is reported. Unsaved memory cannot survive physical process/device loss. |
| FR-13 | Stable queued IDs, retry, optimistic conflict review and scope locking | offline repository tests; TEST-156/158 | Submission, photo upload and acknowledgement require connectivity. The current mobile client has no active location-capture workflow. |
| FR-14 | Backend batch-completeness submission and file numbering; mobile submit | Draft endpoint tests and mobile Draft tests; TEST-156/159 | Saving a completed row can complete its schedule while the form remains Draft. |
| FR-15 | Whole-form acknowledgement; stored operator and signatory fields; immediate receipt screens | `Forms/PreventiveMaintenanceFormDomainTests.cs`, concurrent SQL integrity test, mobile acknowledgement suite; TEST-159 | Drawn signature is captured receipt, not independently verified identity or cryptographic signing. |
| FR-16 | `OfficialInspectionHistoryPolicy.cs`, scoped asset history | `Inspections/InspectionQueryEndpointsTests.cs`; TEST-155/160 | Acknowledged form rows and eligible legacy rows are official history. Draft/Submitted rows are excluded. |
| FR-17 | PM-period dashboard and V3 execution/report service; web monitoring | dashboard/V3 endpoint/regression suites; TEST-156/157/160 | Execution progress and official history have different eligibility rules. Grouping uses current asset metadata. |
| FR-18 | GSD corrective-handoff and external WMS-reference endpoints | `Inspections/InspectionWmsReferralEndpointsTests.cs` | Handoff/reference tracking stops before external WMS processing. No automatic RMRF creation or WMS integration. |
| FR-19 | `PmAnalyticsV3QuestionInterpreter.cs`; web NLA | V3 acceptance/regression/model-client suites; TEST-157 | Two fixed interpretation shortcuts were removed. The existing manuscript FR-19 needs a version-specific update. |
| FR-20 | `PmAnalyticsEndpoints.cs`, typed resolver/validator and preview store | V3 endpoint/acceptance suites; TEST-157 | Backend validates the plan and preview confirmation contract. A model does not supply executable SQL. |
| FR-21 | `PmAnalyticsV3QueryService.cs`, deterministic PM measures | V3 regression/SQL suites; TEST-141/156/157/160 | TEST-141 is ten InMemory scenarios and 75 comparisons, prepared within the development team. Native SQL evidence is separate. |
| FR-22 | V3 asset-inventory subject and query service | V3 endpoint/regression/SQL suites | Inventory is a separate population and does not require a PM cycle. |
| FR-23 | NLA typed clarification editor, preview and revalidation | `web/src/features/reports/pm-analytics-v3.test.tsx`; TEST-157 | A completed report may include explicit user-supplied clarification. Do not score that as an unassisted model answer. |

## Black-box and historical evidence

The researcher confirmed that BB-01 through BB-32 were executed and recorded
in the supplied final evaluation report. Retain that attestation and the study's
reported result. The repository's October 23 register records a narrower fact:
those historical executions were not captured in that collection. Its empty
version/date/tester/capture cells must not be populated from new automated tests.
The original register and approved evidence stay unchanged.

The accessible `UniPM_Complete_SUS_and_ISO25010_Evaluation_Actual_Results.pdf`
contains 15 pages of survey instruments/results and qualitative feedback. It
does not contain the 32 case-level black-box outcomes. This file therefore
supports the survey summaries, while the BB result remains researcher-attested
pending identification of its particular final-report/table attachment in the
source inventory. This does not assert that the cases were never executed.

| Evidence | Meaning | Does not establish |
|---|---|---|
| TEST-139 | Researcher-reviewed parser, 18/18 development outcomes and 69/69 declared assertions | Held-out accuracy, live DeepSeek, database execution |
| TEST-141 | Ten controlled InMemory calculation/source-selection scenarios, 75/75 comparisons | SQL translation, independent external review, respondent acceptance |
| Historical TEST-146 | 17 actual DeepSeek calls across 18 cases; final strict 17/18 and 68/69; auxiliary catalog equivalence 18/18 and 69/69; raw model candidates 11/17 | Perfect standalone model accuracy, report execution, current-branch live retest |
| Historical TEST-151 | Eight HTTP integration scenarios, 62 checks, three real calls, six report executions after simulated confirmation | Browser clicks, native SQL, historical per-question respondent routing |
| TEST-157 | New parser routing and fake-provider checks, parser 18/18 and 69/69; focused native SQL check | A new live-provider result or replacement for TEST-146 |
| TEST-040 | Earlier Android core lifecycle acceptance at its recorded September commit | Acceptance of the new authorization, paging or offline fixes |

Historical TEST-146 and TEST-151 were read from Git object `aa505a8` in the
separate evaluation history. They are not imported into this remediation branch.
Their tested source identities remain `8a5966eff5e48ec826ed1501c67e155f31899ea2`
and `b7d22c0a6e24fafce3d80c4dc8bedf4ba54e7c88`. TEST-151's fixed-template
INT-04 route describes its historical version; the shortcut is removed here.

## Accepted survey sources

The supplied SUS CSV is the accepted item-level source. It has ten complete
ten-item responses. Apply odd-item response minus one, even-item five minus
response, sum and multiply by 2.5. Recalculation gives web 94.17 for three
respondents, mobile 95.00 for seven, and pooled 94.75 for ten. The pooled result
does not replace the interface-specific means. No individual data were imputed.

| Accepted source | Count and result | SHA-256 |
|---|---|---|
| SUS response CSV | 100 ratings; scores total 947.50 | `290747a611118a0614b72b9f187870ea2f2be732793c45b80d0d90d1b14469de` |
| GSD Administrative Personnel ISO CSV | One respondent, 26 ratings; 123/26 = 4.73 | `38e87e901069940ffb7606237da3f24bb98ddaa77cee85c059997ef82af92965` |
| IT Experts ISO CSV | Three respondents, 78 ratings; 379/78 = 4.86 | `58bb8adb645cfd3ec6cd34a01e843d5e5f1ba5107f8f61ebf6d66a8e6f13e78f` |
| Supplied final results PDF, supervisor table | Two respondents, 30 ratings; 134/30 = 4.47 | Local source retained separately |
| Supplied final results PDF, inspector table | Seven respondents, 105 ratings; 471/105 = 4.49 | Local source retained separately |

The CSVs remain outside public Git. Only aggregate counts and file hashes are
recorded. The PDF's older statements that SUS item responses are unavailable
are superseded by the supplied CSV. There is no need to repeat those statements
in the manuscript. Role-specific ISO questionnaires differ; their means should
remain separate. These are descriptive perception results, not ISO certification
or proof that all security/device/performance properties passed.

## Proposed manuscript changes after selecting this branch

Use the study's third-person academic voice and existing IEEE reference numbers.
These implementation changes can cite a dated internal verification table or
appendix record. Do not invent a new external literature citation for an internal
test result. Preserve the AI declaration, acknowledgement, dedication and
contributors as previously directed.

| Location | Existing claim / concern | Proposed replacement or action |
|---|---|---|
| Section 2.2, scope, Table II DeepSeek description, FR-19, Fig.12 architecture explanation | Two fixed approved questions route locally | Remove that current-version route. State the environment/flag gate and explicit parser choice below. Retain fixed-template provenance only in historical TEST-151's result table. |
| FR-19 | Default fixed approved interpretations | "The system shall return Valid, NeedsClarification, or Unsupported for supported English-language maintenance reporting requests. Default routing shall select DeepSeek when enabled in the Development environment; otherwise, it shall select the deterministic parser. Users shall be able to select the deterministic parser explicitly. Missing provider credentials and provider failures shall produce an explicit error without silently changing the selected interpreter. Report plans shall undergo backend validation, clarification where required, preview, and user confirmation before execution. No interpreter shall execute model-generated SQL." |
| Runtime-selection explanation | A configured mode may be confused with parser-first complexity routing | "Default interpretation uses DeepSeek when its Development configuration is enabled. A missing key produces a provider-unavailable error. When the configuration is disabled or the application runs outside Development, the default uses the deterministic parser. Explicit parser selection avoids a provider call. This routing does not select DeepSeek according to question complexity." |
| Completion/date definitions and offline limitations | Field-work time could be inferred from server completion | "The first backend-accepted inspection save records the authoritative completion timestamp. The entered inspection date and device-recorded offline time do not override that timestamp. Work synchronized after the cycle deadline can therefore count as late." |
| Historical reporting scope and figure/table labels | Department/building at inspection time may be implied | "Report grouping and inspection-row asset details use current asset metadata. Transferring an asset can change the grouping of earlier records. The stored PM form header remains separate; historical asset-location snapshots were not implemented." |
| Acknowledgement procedure and signature definition | Digital signature may imply verified identity | "Acknowledgement records receipt of the whole submitted form through the authenticated worker's session. It stores the entered signatory details, drawn signature, capture operator and timestamp. The signature does not independently authenticate the signatory's identity." |
| FR-02 and FR-10 role descriptions | General editing/upload permissions may be overstated | Identify GSD registration, technical Admin general asset editing, and Inspector-owned Draft photo writes precisely. No new operational edit authority was approved by this remediation. |
| Testing/results native SQL limitation | All native SQL execution is described as unverified | Add a separate dated engineering-verification entry for the exact SQL cases actually run. Preserve the InMemory limitation of TEST-141/151. Do not relabel those old runs as SQL. |
| NLA results and conclusion | Historical 17/18 may be mixed with new fake-provider tests | Retain historical model/system scores and source commits. Add new parser/fake-provider engineering checks as a separate revision entry. No live DeepSeek was called by this remediation. |
| Respondent procedure | Detailed task completion/routing could be invented | Retain the reported demonstration followed by hands-on use. GSD Head used the existing deterministic/DeepSeek configuration; historical per-question route logs are not established. Describe a website walkthrough without inventing completion of specific tasks. |
| Interview/data gathering | Wrong date or surname | Retain February 19, 2026 and Engr. Ariel Nicolas. The request letter is dated February 9. Do not add the photo merely as proof of the date. |
| Separate appendices | Six attachment placeholders remain | E.1–E.3 and F.2–F.4 remain for researcher-supplied attachments. The signed letter is already F.1. Do not mark the package submission-ready until required attachments are filled or explicitly excluded by the professor. |

This proposal introduces no new saved-query feature into the manuscript. The
manuscript and appendices remain untouched by the remediation task.
