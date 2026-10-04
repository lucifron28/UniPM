---
id: NLA-INTERPRETATION-V2-SPLIT
status: provisional
---

# Proposed split manifest

This manifest records the current proposed family partition and the digest of the current `cases.jsonl` snapshot. It is not a final freeze. The language labels have not received human review, the held-out partition is not authorized for evaluation, and no held-out evaluation has run.

- Dataset: `cases.jsonl`
- Current cases SHA-256 (UTF-8 without BOM, LF line endings): 510bf8c4998f33828b2b58f7a00d74d47c32333ba6c1ccaa184e1bacdee3cfa6
- Proposed split: 60 development / 30 held-out cases
- Proposed family split: 20 development / 10 held-out families
- Family is the split unit; each family has one English, one Filipino, and one Taglish case.
- Strict-template controls: 3 development and 1 held-out, all tagged only on the English Valid sibling.
- Current snapshot counts do not freeze labels, family membership, prompt/schema, providers, or source commit.
- Held-out status: reserved, not human-reviewed, not evaluated, not authorized.

## Proposed family assignments

| Class | Expected status | Development families | Held-out families |
|---|---|---|---|
| valid | Valid | F01, F02, F03, F04, F05 | F06, F07 |
| needs-clarification | NeedsClarification | C01, C02, C03, C04, C05 | C06, C07, C08, C09, C10 |
| unsupported | Unsupported | U01, U02, U03, U04, U05 | U06, U07 |
| adversarial | Unsupported | A01, A02, A03, A04, A05 | A06 |

## Proposed counts

| Split | Valid | NeedsClarification | Ordinary Unsupported | Adversarial Unsupported | Expected Unsupported total | English | Filipino | Taglish | Total |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| development | 15 | 15 | 15 | 15 | 30 | 20 | 20 | 20 | 60 |
| held-out | 6 | 15 | 6 | 3 | 9 | 10 | 10 | 10 | 30 |
| total | 21 | 30 | 21 | 18 | 39 | 30 | 30 | 30 | 90 |

Expected status counts within each language:

| Proposed split | Language | Valid | NeedsClarification | Ordinary Unsupported | Adversarial Unsupported | Expected Unsupported total | Cases |
|---|---|---:|---:|---:|---:|---:|---:|
| Development | Each of English, Filipino, and Taglish | 5 | 5 | 5 | 5 | 10 | 20 |
| Held-out, not evaluated | Each of English, Filipino, and Taglish | 2 | 5 | 2 | 1 | 3 | 10 |

The current SHA-256 is a provenance aid for review and later change detection. After human review, the corpus owner must decide whether to retain or revise this split, freeze the reviewed cases and evaluation inputs, and record a new digest before any held-out run.
