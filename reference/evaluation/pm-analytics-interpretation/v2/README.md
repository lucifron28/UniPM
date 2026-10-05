---
id: NLA-INTERPRETATION-V2
status: provisional
---

# PM analytics interpretation evaluation v2

This directory contains a synthetic draft corpus for evaluating English, Filipino, and Taglish questions against the current typed analytics contract. It adds no metric, category, grouping, reporting formula, scheduling rule, database behavior, or workflow.

The current UniPM natural-language analytics scope is limited to English input. This is a scope statement, not an institutional approval or a model-readiness claim. Filipino and Taglish appear in v2 as exploratory robustness cases. Their inclusion does not claim production support, and it does not mean that the interface technically rejects those inputs.

**Review status: provisional.** Labels are developer-authored. An AI-assisted English pre-review recommended 29 of 30 English cases as semantically consistent with the current contract; C09 remains pending a domain decision about `FE`. This is a review aid, not independent human validation. A person must sign off before anyone describes the corpus as human-reviewed. No independent Filipino or Taglish review is recorded. The corpus and family partition remain proposed, and the held-out set has not been evaluated. Do not cite results from this provisional corpus as unbiased held-out evidence. See the [human-review guide](human-review-guide.md) before reviewing or annotating any row.

The held-out partition was frozen before model comparison and was not submitted to candidate models, scored, inspected for model-specific failures, or used for prompt/model tuning before final held-out evaluation. This is a protocol statement, not a claim that nobody has ever seen the held-out wording.

## Size and proposed split

The corpus has 90 questions in 30 intent families across separate `dev.jsonl` and `heldout.jsonl` files. Each family has one English, one Filipino, and one Taglish draft. The three language siblings always share a proposed split. This gives exactly 30 questions per language and makes leakage checks simple.

The proposed partition has 20 development families (60 questions) and 10 held-out families (30 questions). English cases are the required review surface for the current English-scoped product evaluation. Filipino and Taglish are optional exploratory review slices, relevant only if UniPM later seeks stronger multilingual claims or plans to publish those slices as validated evidence. The held-out portion is reserved for future work after human review and a new evaluation freeze.

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

The family assignment and per-file digests appear in [split-manifest.md](split-manifest.md). They describe the preserved v2 partition, not a final English evaluation freeze. Keep v2 as the historical multilingual exploratory development experiment. Do not revise its questions, labels, hashes, or scores. Any corrected or expanded final English evaluation corpus belongs in a new version with reviewed labels and new digests.

## Case format and expected contract

Both [dev.jsonl](dev.jsonl) and [heldout.jsonl](heldout.jsonl) follow the v1 evaluator's JSONL input shape: `caseId`, `familyId`, `split`, `language`, `caseClass`, `question`, `expected`, and the optional `caseTag`. The expected object contains `status`, `plan`, `clarificationFields`, and `presentation`. The evaluator reads and hashes only the selected split file. A development run does not open the held-out file.

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

The expected labels are synthetic and developer-authored. [human-review.csv](human-review.csv) contains one row per question with the question, expected status, every plan field, clarification fields, notes, and reviewer-decision and reviewer-correction columns. Both reviewer columns are blank. The [human-review guide](human-review-guide.md) records the AI-assisted English pre-review, the English sign-off requirement, optional exploratory Filipino/Taglish review, wording candidates, and the unresolved `FE` domain question.

## Use limits

- Preserve v2 as the multilingual exploratory development experiment used by the historical provider comparison. Do not tune, correct, or re-score its cases as if it were the future final English corpus. Record any future language or label changes in a separately versioned corpus.
- Do not evaluate the held-out partition in this goal. Its file has not been human-reviewed or authorized for evaluation, and the CLI gate remains closed.
- Never split a family's language siblings across partitions.
- Do not treat provider output as authoritative analytics. The model interprets a question; the existing validator and deterministic analytics remain authoritative.
- Do not claim Filipino or Taglish validation, institutional terminology coverage, or production suitability for any model from this synthetic corpus.
- This corpus contains no operational PM records, personal data, prompts, credentials, or provider responses.

## Future English evaluation corpus

Use `v1` for the historical local/Qwen experiment, `v2` for the preserved multilingual exploratory provider experiment, and reserve `v3` for a future reviewed English-focused final evaluation corpus. Do not create v3 cases or choose its size in this goal. A separately authorized v3 should cover all supported Valid metrics and categories, clarifications, ordinary unsupported requests, and multiple adversarial safety cases. Keep development and held-out partitions family-disjoint, predeclare scoring and model-selection criteria, and freeze reviewed labels, prompt/schema, providers/models, source SHA, and corpus hashes before held-out use. The current 10 English v2 held-out cases are a pilot artifact, not a sufficient final product-quality evaluation.

The phase-5-to-case mapping is in [coverage-map.md](coverage-map.md).
