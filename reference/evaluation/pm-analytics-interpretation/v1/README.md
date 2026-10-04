---
id: NLA-INTERPRETATION-V1
status: provisional
---

# PM analytics interpretation evaluation v1

This versioned corpus evaluates whether a constrained interpreter maps synthetic English, Filipino, and Taglish questions to the existing PM analytics contract. It does not add a metric, calculation, category, maintenance rule, or workflow.

The 100 questions cover only `Progress`, `OnTimeCompliance`, `CompletedLate`, and `NonOperational`; the four existing asset-category codes; a single explicit CPMP month and four-digit year; optional department filtering; and grouping by `None` or `Department`. A Progress count is a presentation of the existing progress numerator, not a fifth metric. The expected presentation is `Percent` for Progress by default, `Count` only when the wording explicitly asks for a count, `Percent` for OnTimeCompliance, and `Count` for CompletedLate and NonOperational.

## Files and split

- `cases.jsonl`: one synthetic question and expected structured label per line.
- `split-manifest.md`: frozen family assignments, planned distributions, and corpus digest.
- 60 cases are in development and 40 are held out. The `familyId` is the unit of splitting; language variants of a family stay together.
- Each class has 25 cases: 25 Valid, 25 NeedsClarification, 25 ordinary Unsupported, and 25 Adversarial. Adversarial is a labeled subset of expected Unsupported, so the aggregate expected Unsupported total is 50. Report both class and status denominators.
- Language totals are balanced approximately, with at least 13 cases for each language in the held-out partition.
- Five English Valid cases are tagged `caseTag: strict-template-control` and match the existing strict question grammar: three development controls and two held-out controls. The other 20 Valid cases are free phrasing. Report strict-template controls separately from free-phrasing cases; both remain in the aggregate Valid denominator.

The strict-template controls are `nla1-V01-dev-en-1`, `nla1-V04-dev-en-1`, `nla1-V05-dev-en-1`, `nla1-V09-heldout-en-1`, and `nla1-V10-heldout-en-1`. Their inclusion provides a constrained baseline comparison; it does not make the strict English grammar the expected form for the multilingual free-phrasing cases.

## Label schema

Each `expected` object records `status`, `plan`, `clarificationFields`, and `presentation`. A Valid case has a complete plan and no clarification fields. A NeedsClarification or Unsupported case has `plan: null` and `presentation: null`; no partial plan is executable. Clarification fields use the exact contract enum names: `Metric`, `AssetCategory`, `Year`, `Month`, `Department`, and `GroupBy`. Unsupported cases have an empty clarification-field list. Provider/runtime failures are not language labels and must be reported separately.

Plans use the existing canonical metric and category codes, `yyyy-MM` cycle, uppercase-normalized fictional department label or null, and `None|Department` grouping. Synthetic department labels are `CAMPUS WORKS`, `LEARNING COMMONS`, and `UTILITIES`; they do not imply an official university department list. Explicit but conflicting alternatives are clarification cases; requests to compare or aggregate beyond the supported schema are Unsupported. A relative period never supplies an implicit current date: ask for missing year/month instead of guessing. Explicit CPMP-incompatible months are Unsupported.

## Evaluation and leakage controls

The development and held-out partitions are fixed by family before model evaluation. Prompt, alias, parser, or model-configuration changes may use development results only. Freeze the evaluator source/configuration and record their fingerprints before a single held-out evaluation; do not inspect held-out failures to tune and rerun. If a later change is justified, version a new corpus/evaluation protocol rather than reusing the held-out set as development data.

Report exact status accuracy, exact plan accuracy across all plan fields (including nulls), per-field accuracy, exact clarification-field accuracy, unsupported/adversarial rejection, and counts by split/language/class. Keep provider errors separate. Record the model identifier/digest, runtime version, prompt/config fingerprint, sample denominators, and latency percentiles; record cost/token counts only when the local runtime exposes them. Do not put questions, prompts, model responses, credentials, endpoints, or raw provider payloads into committed evidence.

## Limitations

These are synthetic developer-authored labels for implementation evaluation, not a human-validated language corpus and not evidence of real GSD terminology, institutional acceptance, or production quality. Filipino/Taglish phrasing is provisional and requires independent fluent-speaker review before any language-quality claim. A passing result establishes performance only on this frozen synthetic sample and configuration. No real maintenance records or personal data are included. No external provider is involved in this corpus.
