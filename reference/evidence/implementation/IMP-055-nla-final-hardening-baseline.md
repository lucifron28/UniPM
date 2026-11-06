---
id: IMP-055
type: implementation
title: NLA pre-evaluation hardening baseline
status: draft
recordedAtUtc: 2026-11-06T10:42:32Z
testedCommit: 53afe5708ca640b6fe7618589cbf3be1a1de6ae3
sourceBranch: main
evidenceLevel: source-inspected
---

# NLA pre-evaluation hardening baseline

## Baseline identity

- Source branch: `main`
- Source commit: `53afe5708ca640b6fe7618589cbf3be1a1de6ae3`
- Implementation branch: `feature/nla-final-hardening`
- Pre-change test command: `dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj --filter FullyQualifiedName~PmAnalyticsV3 --logger 'console;verbosity=minimal'`
- Local result: 61 passed, 3 skipped, 0 failed. `UNIPM_SQLSERVER_TEST_CONNECTION` was absent, so the three SQL Server 2019 facts skipped.
- Raw output: ignored `artifacts/nla-hardening-baseline/pm-analytics-v3-tests.log`

The test command ran on the source commit above in a clean feature worktree. No provider requests, report execution, production database access, or GSD-authored query access occurred.

## Existing behavior to preserve

V3 already has a closed typed plan, two report subjects (`pmExecution` and `assetInventory`), four supported CPMP asset categories, strict server validation, GSD authorization, a bounded preview and explicit confirmation flow. The model proposes a plan; backend code validates it and calculates results. Existing limits include four measures, two groupings, four aggregate filters, a 12-calendar-month period, and Top/Bottom 20.

The report definitions already separate scheduled persisted cycles from current registered assets. `CompletedAt` defines PM completion, missed work is incomplete after the PM deadline, completed-late work is completed after that deadline, and Inspector assignment timing remains separate. The current UI already reviews normalized scope and requires preview before confirmation.

## Source-confirmed coverage defects

The following defects are confirmed in current code and known development evidence.

1. `ResolveCategories` contains English aliases only. The model post-check uses that result to compare or replace proposed categories, so it can discard a correct category inferred from a well-grounded Filipino alias. TEST-179 documents shortened-category failures including `alarms` and `drinking-station checks`.
2. Period parsing recognizes English month names and `this year`/`last year`. It does not recognize Filipino month names or relative-year phrases. The English month token `May` is also matched without checking date context.
3. Measure, grouping, inventory, record-list, and ranking cues are primarily English. `IsRecordsQuestion` only recognizes `record(s)`, `individual assets`, and `source rows`; everyday list requests such as `which assets` and Filipino `alin`/`pakilista` are not covered.
4. Threshold parsing recognizes percentage values only. `ResolveThresholds` associates them only when exactly one selected measure has a percent unit. Integer count thresholds are not represented, and the model post-check cannot verify them against the question.
5. A multi-measure Top request without an explicit ranking measure is clarified by the deterministic parser, but the DeepSeek post-check does not compare the proposed Top selection with the question's ranking basis.
6. The parser can preserve quoted named building, department, and location filters. Unquoted values are generally clarified. The DeepSeek post-check does not enforce an explicitly named field, which permits a building value to be proposed under the general-location field.
7. The DeepSeek post-check enforces category, period, threshold, and some outcome constraints. It does not consistently verify record-vs-aggregate mode, explicitly requested groupings, or explicit ranking scope against the question.

These are interpretation and validation gaps. No change to formulas or the report contract is needed to address the supported cases.

## Ambiguous terms and decisions

The following terms need conservative handling. Public wording and researcher hypotheses do not establish institutional approval.

| Candidate wording | Risk | Baseline decision |
|---|---|---|
| `pamatay-sunog`, `alarma sa sunog` | Equipment versus a fire event or alarm event | Add only with nearby PM, asset, system, or inspection context where needed. |
| `ilaw pang-emergency`, `inuman ng tubig` | Candidate Filipino equipment names lack local GSD confirmation | Keep as researcher hypotheses and require a specific equipment or PM context. |
| `alarm`, `light`, `sira`, `water dispenser`, `FE` | Generic, colloquial, or unapproved aliases | Do not map alone to a supported category or saved finding. |
| `nakatala` | Could describe an inventory entry or a recorded inspection | Clarify unless registration context is explicit. |
| `may` | English month or Tagalog existential verb | Resolve as May only with explicit calendar context. |
| Unqualified building-like place names | Building versus general location | Preserve the value and ask which catalog field when the question does not name it. |

Public research leads for the new lexicon are the Bureau of Fire Protection's use of `pamatay sunog` for a fire extinguisher, the BJMP mapping `Fire Alarm` to `Alarma sa sunog`, DILG Region III's use of `inspeksyon` and `pagsusuri`, and the Komisyon sa Wikang Filipino's Filipino month names. These are public-language sources, not GSD terminology approval.

- [BFP, Fire Safety for Teenagers](https://bfp.gov.ph/wp-content/uploads/2024/05/Volume-2_-Fire-Safety-for-Teenagers.pdf)
- [BJMP, JSIOC communications manual and annexes](https://car.bjmp.gov.ph/wp-content/uploads/2025/05/BJMP-JSIOC-MC-156-WITH-ANNEXES.pdf)
- [DILG Region III, project inspection and review usage](https://www.region3.dilg.gov.ph/aurora/index.php/news-events/1193-2025-08-27-lfp-sglgif-sanluis)
- [Komisyon sa Wikang Filipino, Manwal sa Masinop na Pagsulat](https://kwf.gov.ph/wp-content/uploads/MMP_Full.pdf)

## Evidence boundary

TEST-179 and TEST-189 remain historical development evidence. TEST-189 reports 22/30 parser and 23/30 DeepSeek-selected full semantic case agreement on exposed developer questions. This is not independent, held-out, multilingual, numeric-calculation, or production evidence.

The proposed v2 multilingual corpus is provisional and was not used for tuning. Its `dev.jsonl`, `heldout.jsonl`, review CSV, and query rows were not opened. The upcoming GSD Head-authored 20-query set and answer sheet remain sealed and were not searched for or accessed.

## Future features kept separate

Schedule-coverage comparisons, repeated non-operational findings, cross-year compliance change, non-operational percentages, and repair/referral turnaround each need a separate business definition and data review. They are not implemented as part of this pass. Existing metrics and formulas remain the only executable measures.

## Pre-change test coverage

The baseline filtered run found the existing V3 SQL Server 2019 tests, but skipped all three because the native test connection was unavailable. The remaining V3 tests passed. A final test-run record will add post-change commands, tested commit, exact counts, environment limitations, and CI results.
