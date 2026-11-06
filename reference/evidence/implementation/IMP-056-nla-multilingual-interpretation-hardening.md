---
id: IMP-056
type: implementation
title: NLA V3 multilingual interpretation hardening
status: reviewed
recordedAtUtc: 2026-11-06T12:26:06Z
sourceCommit: 6449cbf2bd5d385a30a1b92c1e2c56112edf85bb
sourceBranch: feature/nla-final-hardening
evidenceLevel: source-inspected
---

# NLA V3 multilingual interpretation hardening

## Objective

Close confirmed question-to-plan gaps in the existing bounded V3 report
contract while keeping the parser, DeepSeek proposal guard, clarification
workflow, measures, access rules, and report execution aligned. The work adds
bounded English, Filipino, and Taglish recognition. It does not establish GSD
terminology approval or language accuracy.

## Source identity

- Baseline: `main` at `53afe5708ca640b6fe7618589cbf3be1a1de6ae3`; see
  [IMP-055](IMP-055-nla-final-hardening-baseline.md).
- Implementation: `b7e13a3b5afa313285b3116c4ce71a46888bca6d`.
- Final test-only follow-up: `6449cbf2bd5d385a30a1b92c1e2c56112edf85bb`.
- Branch: `feature/nla-final-hardening`.
- No database migration, new category, measure, report subject, formula, or API
  contract was added.

## Implementation summary

`PmAnalyticsV3LanguageLexicon` now owns the bounded phrases used by the
deterministic parser and by the post-DeepSeek question guard. It extracts
categories, measures, report subject and mode, dates, groupings, record-list
intent, ranking, threshold operands, and ambiguity cues without rewriting the
user's question or named filter values. Unicode letter/number boundaries and
bounded regular-expression timeouts remain in place.

The rule parser and model-result guard now share the same category and measure
evidence. The guard checks the proposed subject, grouping, categories, measures,
period, threshold operators and values, output mode, rank and rank basis, and
named filter field. It repairs a model proposal only where the question gives a
unique, supported interpretation; unknown categories, outcome disjunctions,
missing ranking bases, and ambiguous place fields remain clarifications or
unsupported results.

The parser recognizes Filipino calendar months and relative years against the
Asia/Manila clock, Filipino equipment and maintenance phrases, word-number
thresholds, and Filipino list requests. `May` is recognized as a month only
with date context. A bare `alarm`, `light`, `FE`, or colloquial condition such
as `sira` does not become a supported category or saved finding. Known English
water-dispensers and water-fountains wording remains an explicit unsupported
category clarification.

Safe partial plans retain resolved scope when a field needs user choice. For
example, the API recognizes `pamatay-sunog`, scheduled count, and 2024 while
asking whether `North Block` means a building or a general location. The
existing report wizard presents the corresponding field controls. A browser
test exercises that path using a mocked interpretation response; no front-end
runtime code changed.

Record-list wording now stays in record mode, while count questions stay
aggregate. Completed-late work remains distinct from missed work, and
Inspector-assignment deadlines remain distinct from PM-cycle deadlines.
Integer and percentage thresholds preserve their measure, operator, and value.
Supported category unions remain bounded; a disjunction between different
outcomes is not flattened into an AND or silently reduced to one side.

## Candidate-language evidence ledger

`GSD-approved` appears in no row because GSD terminology approval was not
available for this implementation. Public-language examples ground candidate
wording only; they do not prove that a phrase is used or accepted by UniPM's
GSD users. Test IDs below refer to the developer-only suite in
[`PmAnalyticsV3FinalHardeningTests.cs`](../../../tests/UniPM.Api.Tests/Reports/PmAnalyticsV3FinalHardeningTests.cs).

| Candidate phrase or class | Canonical target | Context required | Evidence kind | Ambiguity risk | Test IDs | Decision |
|---|---|---|---|---|---|---|
| `fire extinguisher`, `extinguisher` | `fire-extinguisher` | Asset or PM question; abbreviated `extinguisher` must not be a person or place name | existing-repo-test | Low with reporting context | `CAT-001..003`, `NEG-016` | Map at a bounded token boundary. |
| `pamatay-sunog`, `pamatay sunog` | `fire-extinguisher` | PM, inspection, schedule, inventory, or other report context | public-source; researcher-hypothesis; existing-repo-test | Phrase names fire-extinguishing equipment publicly, but local GSD usage is unconfirmed | `CAT-004..005`; endpoint clarification test | Map only in reporting context; do not label GSD-approved. |
| `fire alarm system`, `fire alarms`, `alarm systems`, `alarma sa sunog` | `fire-alarm` | PM or equipment reporting context | public-source; researcher-hypothesis; existing-repo-test | `Fire Alarm` / `alarma sa sunog` also occurs in emergency communications; generic alarms can be security alarms | `CAT-006..010`, `NEG-012` | Contextual phrases may map. Bare `alarm` or `alarm sensor` must clarify. |
| `emergency light`, `ilaw pang-emergency`, `ilaw para sa emergency` | `emergency-light` | Emergency-equipment or PM context | existing-repo-test; researcher-hypothesis | Filipino compound is plausible but lacks GSD confirmation; `light` may be a named department | `CAT-011..014`, `NEG-013`, `NEG-016` | Accept only the full bounded phrase; keep bare `light` unresolved. |
| `water drinking station`, `drinking station`, `istasyon ng inuming tubig`, `inuman ng tubig` | `water-drinking-station` | Asset, schedule, inspection, or PM context | existing-repo-test; researcher-hypothesis | Filipino names are not locally confirmed; dispensers and fountains may be a different asset class | `CAT-015..018`, `NEG-014` | Accept listed contextual phrases as hypotheses; ask when wording names a different or unknown class. |
| Filipino inspection and outcome wording: `inspeksyon`, `pagsusuri`, `natapos`, `nagawa`, `na-inspect`, `nainspeksyon`, `hindi gumagana`, `pagsunod sa iskedyul` | Existing V3 measures or inspection-record condition | Match the full outcome and PM context; incomplete work is missed only after the PM deadline | existing-repo-test; researcher-hypothesis | Everyday usage may not mean a persisted completed inspection, stored finding, or measured compliance | `MEASURE-010..012`, `TIME-001..003`, `MODE-002` | Use narrow forms. `sira` alone is not a saved non-operational finding. |
| Filipino month names and short forms (`Enero`, `Pebrero`, `Marso`, `Abril`, `Mayo`, `Hunyo`, `Hulyo`, `Agosto`, `Setyembre`, `Oktubre`, `Nobyembre`, `Disyembre`) | Calendar month / `pmCycle` month when explicitly requested | Clear date context; retain category-specific schedule-month checks | public-source; existing-repo-test | `May` is also an English modal and Tagalog existential word | `DATE-001..003`; `May_is_a_month_only_in_date_context...` | Parse in date context; never infer a month from bare `may`. |
| `ngayong taon`, `nitong taon`, `noong nakaraang taon`, `nakaraang taon` | Relative calendar year | Resolve from injected UTC time converted to Asia/Manila | existing-repo-test; researcher-hypothesis | Local user phrasing and intended calendar basis remain unreviewed | `YEAR-001..002`; fixed-clock endpoint test | Resolve only current and previous year. |
| Word-number thresholds (`lima`, `limang`, `dalawa`, `tatlong`, `sampu`) with `at least`, `mahigit`, `hindi bababa`, `mas mababa`, `%`, or `porsiyento` | Typed aggregate filter on one uniquely associated measure | Preserve inclusive/exclusive operator, number, unit, and nearest selected measure | existing-repo-test; researcher-hypothesis | A count can be confused with a ranking limit; a number without a unit/measure association is ambiguous | `THR-001..011`, multi-threshold test, threshold metamorphic test | Preserve only a uniquely resolvable typed threshold; otherwise clarify. |
| `which`, `alin`, `anu-ano`, `ilista`, `pakilista` | `Records` mode | Do not also infer a numeric count; a simultaneous count and list request needs a choice | existing-repo-test; researcher-hypothesis | `alin` can ask a category/group question rather than source rows | `MODE-001..003`; conflicting-mode test | Use neighboring asset/equipment/record wording and clarify conflicting output requests. |
| `top`, `most`, `least`, `pinakamarami`, leading `which five` / `aling limang` | Bounded Top/Bottom grouping and ordering | Require a supported group and unique measure when multiple measures are present | existing-repo-test; researcher-hypothesis | `at most` is a threshold, not a rank; ranking basis can be omitted | `RANK-001..003`, missing-basis test | Preserve the explicit basis; never guess among multiple measures. |
| Bare `FE`, `alarm`, `alarm sensor`, `light`, `water dispenser(s)`, `water fountain(s)`, `sira` | No category/measure mapping | Ask for a supported category, measure, or recorded inspection outcome | existing-repo-test | Short, generic, colloquial, or different equipment wording | `NEG-011..016`; `May_is_a_month_only_in_date_context...` | Keep unresolved. Do not add speculative category synonyms. |
| Unqualified named place such as `North Block` | `building`, `location`, or another explicit named dimension | Preserve the text; resolve only when the question names the field or catalog resolution is unique | existing-repo-test | Same proper name may exist in multiple catalog dimensions | `SCOPE-001..005`; Filipino guided-clarification API/browser tests | Keep the partial plan and ask which field; never rewrite the value. |

Public-language references:

- The [Bureau of Fire Protection's Fire Safety for Teenagers material](https://bfp.gov.ph/wp-content/uploads/2024/05/Volume-2_-Fire-Safety-for-Teenagers.pdf) uses `pamatay sunog` while referring to a fire extinguisher. This grounds the lexical candidate, not GSD acceptance.
- A [BJMP communications manual and annexes](https://car.bjmp.gov.ph/wp-content/uploads/2025/05/BJMP-JSIOC-MC-156-WITH-ANNEXES.pdf) pairs `Fire Alarm` with `Alarma sa sunog` in a communications context. This supports the phrase only; the code therefore requires reporting context and rejects generic alarm wording.
- [DILG Region III reporting](https://region3.dilg.gov.ph/aurora/index.php/news-events/1192-2025-08-20-lfp-safpb) uses `inspeksyon` and `pagsusuri` for a project review. It does not establish maintenance-record semantics.
- The Komisyon sa Wikang Filipino's [Manwal sa Masinop na Pagsulat, section 15.14](https://kwf.gov.ph/wp-content/uploads/MMP_Full.pdf) lists Filipino month names and abbreviations. Month parsing still requires date context, especially for `May`.

## Important files

- `server/Features/Reports/PmAnalyticsV3LanguageLexicon.cs`
- `server/Features/Reports/PmAnalyticsV3QuestionInterpreter.cs`
- `tests/UniPM.Api.Tests/Reports/PmAnalyticsV3FinalHardeningTests.cs`
- `tests/UniPM.Api.Tests/Reports/PmAnalyticsV3AcceptanceTests.cs`
- `tests/UniPM.Api.Tests/Reports/PmAnalyticsV3GuardCorrectionTests.cs`
- `web/e2e/pm-analytics-v3.spec.ts`
- `web/src/features/reports/pm-analytics-v3.tsx` was source-inspected and not changed.

## Verification and limitations

Local test counts, commands, environment, and skipped checks are in
[TEST-193](../test-runs/TEST-193-nla-final-hardening.md). The new test matrix
is explicitly a synthetic developer regression set, not an independent or
held-out evaluation. The DeepSeek paths use canned responses through a mocked
HTTP handler; no paid provider calls were made. Native SQL Server numerical
verification was not performed because no SQL Server test connection was
configured.

The upcoming GSD Head-authored query set and answer sheet were not opened,
searched, or used for implementation. This work does not establish GSD
acceptance, Filipino/Taglish accuracy, production-provider quality, or
calculation correctness beyond the existing report implementation.

## Deferred analytics

Feasibility and required review are recorded separately in
[`nla-v3-future-analytics-proposals.md`](../../planning/nla-v3-future-analytics-proposals.md).
Schedule coverage, recurrence, cross-year comparisons, non-operational
percentages, and WMS turnaround/root-cause reporting are proposals only.
