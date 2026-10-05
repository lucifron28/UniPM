---
id: NLA-INTERPRETATION-V2-HUMAN-REVIEW
status: provisional
---

# v2 human-review guide

Review all 90 synthetic v2 cases using this guide. It does not record completed
review or authorize held-out model evaluation. The
`reviewerDecision` and `reviewerCorrection` columns in [human-review.csv](human-review.csv)
remain blank until a person makes those decisions.

## Language scope

English is the formally supported natural-language analytics input for the
current UniPM scope. Filipino and Taglish cases are exploratory robustness
material. Their inclusion does not establish production support or an
institutional language requirement, and it does not mean that the interface
technically rejects them. This language-scope statement does not establish
production readiness for any model or analytics implementation.

Review the English cases for semantic correctness, expected status, every
typed-plan field, ambiguity, and domain terminology. Review Filipino and
Taglish cases for natural phrasing, meaning relative to the English sibling,
whether the expected status still follows, and mistranslation or unnatural
wording. Their review remains exploratory and cannot establish product support
for those languages.

AI-generated suggestions may help a reviewer find a possible issue, but they
are not independent human review. A person must decide whether a question and
its expected label are correct. Do not treat provider output as the answer key.

## Wording review candidates

These IDs are candidates for human review, not confirmed errors. Do not change
the question or expected label from an AI suggestion alone.

- `nla2-F01-dev-fil`
- `nla2-F01-dev-taglish`
- `nla2-F03-dev-fil`
- `nla2-F04-dev-fil`
- `nla2-F06-heldout-fil`
- `nla2-U02-dev-fil`
- `nla2-A06-heldout-fil`

## `FE` domain decision

The three C09 cases use `FE`, whose status as an approved GSD abbreviation is
unknown:

- `nla2-C09-heldout-en`
- `nla2-C09-heldout-fil`
- `nla2-C09-heldout-taglish`

The current expected result remains `NeedsClarification` with the
`AssetCategory` field. A domain reviewer must answer: "Is `FE` an officially
understood or approved abbreviation for Fire Extinguisher in the intended GSD
workflow?" If the answer is yes, a later corpus revision may label these cases
`Valid` with `fire-extinguisher`. If the answer is no or uncertain, retain
`NeedsClarification`. Any corpus mutation requires separate approval; do not
change these rows automatically.

## Future evaluation design

The product-facing held-out result should use English cases. The current
proposed English held-out slice has 10 cases: 2 Valid, 5 NeedsClarification,
2 ordinary Unsupported, and 1 adversarial Unsupported. One error changes an
overall 10-case score by 10 percentage points. Its two Valid cases cannot
cover all four metrics or all four asset categories, and one adversarial case
cannot support a useful safety estimate. Treat this slice as a pilot, not a
convincing final product evaluation.

After human review, consider a separately authorized English-focused corpus
version with a family-disjoint development and held-out split. Set its size
from a predeclared precision goal and coverage requirements for metrics,
categories, clarifications, unsupported requests, and safety cases; this guide
does not prescribe a sample-size target. Before held-out execution, freeze the
reviewed labels, prompt and schema, provider and model, source SHA, and corpus
hashes. Keep the current multilingual v2 results as exploratory evidence. A
Filipino/Taglish robustness appendix would need its own review and
authorization. Do not edit the current partition for this recommendation.

## Review outcomes

| Classification | Current action |
|---|---|
| A. Documentation scope | State English as formal product input and Filipino/Taglish as exploratory robustness cases. |
| B. Wording review | Have a person review the seven candidate IDs above before any corpus correction. |
| C. Domain decision | Resolve the approved meaning of `FE`; keep the current clarification label until then. |
| D. Evaluation design | Treat 10 English held-out cases as a pilot and propose a reviewed, expanded English version before final evaluation. |

The inspected manuscript at
`docs/full-documents/CCMS-IT-WMA-2026-013-PROPOSAL-MANUSCRIPT.docx` makes no
multilingual NLA claim. Its NLP/RAG references at paragraphs 269 and 368 are
historical. No manuscript edits were made. If a language-scope statement is
added later, suggested wording is:

> UniPM's natural-language analytics interface formally supports English
> input. Filipino and Taglish questions were also included during development
> as exploratory robustness tests, but multilingual support was not treated
> as a production requirement.
