---
id: NLA-INTERPRETATION-V2-HUMAN-REVIEW
status: provisional
---

# v2 human-review guide

Use this guide to review the English cases, the required review surface for the
current English-scoped product evaluation. Filipino and Taglish review is
optional exploratory work. Review those slices only if UniPM later seeks
stronger multilingual claims or plans to publish them as validated evidence.
This guide does not record completed human review or authorize held-out model
evaluation. The `reviewerDecision` and `reviewerCorrection` columns in
[human-review.csv](human-review.csv) remain blank until a person makes those
decisions.

## Language scope

The current UniPM natural-language analytics scope is limited to English
input. This is a scope statement, not an institutional language-policy
approval or model-readiness claim. Filipino and Taglish cases are exploratory
robustness material. Their inclusion does not establish production support or
an institutional language requirement, and it does not mean that the
interface technically rejects them.

Review the English cases for semantic correctness, expected status, every
typed-plan field, ambiguity, and domain terminology. Review Filipino and
Taglish cases for natural phrasing, meaning relative to the English sibling,
whether the expected status still follows, and mistranslation or unnatural
wording. Their review remains exploratory and cannot establish product support
for those languages.

## AI-assisted English pre-review

The supplied AI-assisted recommendation found 29 of the 30 English cases
semantically consistent with the current typed contract:

- F01-F07
- C01-C08 and C10
- U01-U07
- A01-A06

C09 (`nla2-C09-heldout-en`) remains pending a domain decision about `FE`. The
29 recommendations are a reviewer aid, not independent human validation. A
person must still sign off before anyone describes these English labels as
human-reviewed. Any AI-assisted Filipino or Taglish suggestions follow the
same rule and do not establish independent validation. Do not treat provider
output as the answer key.

A person must decide whether a question and its expected label are correct.
Human corpus changes require separate approval. Do not populate the reviewer
columns or change the corpus from an AI suggestion alone.

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
`AssetCategory` field. A domain reviewer must answer: "Does GSD commonly and
unambiguously use FE to mean Fire Extinguisher in the intended analytics
workflow?" If the answer is yes, consider the alias only in a future v3 corpus
after separate approval. If the answer is no or uncertain, retain
`NeedsClarification`. The appearance of `FE` in fixture or asset-code contexts
does not establish it as an approved natural-language alias. Do not change
these rows automatically.

## Future evaluation design

Keep v1 as the historical local/Qwen experiment and v2 as the preserved
multilingual exploratory provider experiment.
The current 10 English held-out cases are a pilot artifact, not the sole final
product-quality evaluation. They include 2 Valid, 5 NeedsClarification, 2
ordinary Unsupported, and 1 adversarial Unsupported cases. One error changes a
10-case score by 10 percentage points. Two Valid cases do not cover all four
metrics or categories, and one adversarial case cannot support a useful safety
estimate.

Any final evaluation should use a separately authorized v3 English-focused
corpus. Review its English wording and labels, cover all supported Valid
metrics and categories, include clarifications, unsupported requests, and
multiple adversarial safety cases, and keep development and held-out partitions
family-disjoint. Predeclare scoring and model-selection criteria. Set the
corpus size from a stated precision goal and coverage needs; this guide does
not choose a sample size. Freeze reviewed labels, prompt/schema, provider and
model, source SHA, and corpus hashes before held-out execution. A Filipino or
Taglish appendix would need separate review and authorization. Do not change
the v2 partition for this future design.

## Review outcomes

| Classification | Current action |
|---|---|
| A. Documentation scope | State that the current UniPM NLA scope is limited to English input, without implying institutional policy approval or model readiness. |
| B. Wording review | Have a person review the seven candidate IDs above before any corpus correction. |
| C. Domain decision | Resolve the approved meaning of `FE`; keep the current clarification label until then. |
| D. Evaluation design | Treat 10 English held-out cases as a pilot and propose a reviewed, expanded English version before final evaluation. |

The inspected manuscript at
`docs/full-documents/CCMS-IT-WMA-2026-013-PROPOSAL-MANUSCRIPT.docx` makes no
multilingual NLA claim. Its NLP/RAG references at paragraphs 269 and 368 are
historical. No manuscript edits were made. If a language-scope statement is
added later, suggested wording is:

> The current UniPM natural-language analytics scope is limited to English
> input. Filipino and Taglish questions were included during development as
> exploratory robustness cases. Their inclusion does not establish multilingual
> support or production readiness.
