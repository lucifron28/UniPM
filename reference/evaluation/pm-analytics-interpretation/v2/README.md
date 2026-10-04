---
id: NLA-INTERPRETATION-V2
status: provisional
---

# PM analytics interpretation evaluation v2

This directory contains a new synthetic draft corpus for comparing interpretation of natural English, Filipino, and Taglish PM analytics questions. It uses only the current typed analytics contract. It adds no metric, category, grouping, reporting formula, scheduling rule, database behavior, or workflow.

**Review status: provisional.** No Filipino or Taglish question or label has received human review. This coding pass does not validate language quality. The corpus and the family partition remain proposed, and the held-out set has not been evaluated. Do not cite results from this provisional corpus as unbiased held-out evidence.

## Size and proposed split

The corpus has 90 questions in 30 intent families. Each family has one English, one Filipino, and one Taglish draft. The three language siblings always share a proposed split. This gives exactly 30 questions per language and makes leakage checks simple.

The proposed partition has 20 development families (60 questions) and 10 held-out families (30 questions). Sixty development questions are small enough to run repeatedly across the rule baseline and two API providers, while 90 individual questions remain practical for a person to review. The held-out portion is reserved for a future one-time evaluation after the human review and freeze conditions in the experiment plan are met.

| Proposed split | Valid | NeedsClarification | Ordinary Unsupported | Adversarial Unsupported | Total |
|---|---:|---:|---:|---:|---:|
| Development | 15 | 15 | 15 | 15 | 60 |
| Held-out, not evaluated | 6 | 15 | 6 | 3 | 30 |
| Total | 21 | 30 | 21 | 18 | 90 |

Adversarial is a class label for a subset of expected Unsupported cases. Expected status totals are 21 Valid, 30 NeedsClarification, and 39 Unsupported.

Every split contains all three languages in equal numbers:

| Proposed split | English | Filipino | Taglish |
|---|---:|---:|---:|
| Development | 20 | 20 | 20 |
| Held-out, not evaluated | 10 | 10 | 10 |

The family assignment and current data-file digest appear in [split-manifest.md](split-manifest.md). They describe a proposed partition, not a final freeze. Human corrections require a new reviewed snapshot and recalculated digest before any held-out use.

## Case format and expected contract

[cases.jsonl](cases.jsonl) follows the v1 evaluator's JSONL input shape: `caseId`, `familyId`, `split`, `language`, `caseClass`, `question`, `expected`, and the optional `caseTag`. The expected object contains `status`, `plan`, `clarificationFields`, and `presentation`.

A Valid case has the complete existing plan:
- metric: `Progress`, `OnTimeCompliance`, `CompletedLate`, or `NonOperational`;
- asset category: `fire-extinguisher`, `fire-alarm`, `emergency-light`, or `water-drinking-station`;
- PM cycle in `yyyy-MM` form;
- fictional department label or null;
- grouping: `None` or `Department`.

The existing response presentation is `Count` or `Percent`. Progress defaults to Percent unless the question explicitly asks for a count. OnTimeCompliance uses Percent; CompletedLate and NonOperational use Count. No new metric was made for progress counts.

A NeedsClarification case has a null plan and presentation. Its clarification fields use the existing names `Metric`, `AssetCategory`, `Year`, `Month`, `Department`, and `GroupBy`. An Unsupported case also has a null plan and presentation, with no clarification fields. No non-valid case should produce an executable plan.

The valid development cases cover all four metrics and categories. Their periods use existing CPMP months: fire extinguishers and water drinking stations use February, May, August, or November; fire alarm systems and emergency lights use June or December. The department names `CAMPUS WORKS` and `UTILITIES` are fictional test labels, not an official university department list.

## Vocabulary and label limits

The four category codes above are the complete supported category set. A clearly named outside category such as elevators is expected to be Unsupported. A missing category, or a phrase that cannot be grounded, is expected to ask for clarification rather than silently choose a nearby category.

`FE` appears only as an unapproved abbreviation in the clarification cases. It is not added to the category vocabulary. Its meaning needs domain confirmation before any future experiment treats it as an approved alias.

The expected labels are synthetic and developer-authored. In particular, the Filipino and Taglish variants need fluent-speaker review. [human-review.csv](human-review.csv) contains one row per question with the question, expected status, every plan field, clarification fields, notes, and blank reviewer-decision and reviewer-correction columns. No reviewer decision has been entered.

## Use limits

- Use the development partition for any future tuning before freeze.
- Do not evaluate the held-out partition in this goal. It has not been human-reviewed, frozen, or authorized for evaluation.
- Never split a family's language siblings across partitions.
- Do not treat provider output as authoritative analytics. The model interprets a question; the existing validator and deterministic analytics remain authoritative.
- Do not claim Filipino or Taglish validation, institutional terminology coverage, or production suitability from this synthetic corpus.
- This corpus contains no operational PM records, personal data, prompts, credentials, or provider responses.

The phase-5-to-case mapping is in [coverage-map.md](coverage-map.md).
