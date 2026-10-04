---
id: NLA-INTERPRETATION-V1-SPLIT
status: provisional
---

# Split manifest

This manifest freezes the case-family split before any model evaluation. Cases in a family stay in one split; translated or paraphrased siblings never cross the development/held-out boundary.

- Dataset: `cases.jsonl`
- Cases SHA-256 (UTF-8, no BOM, LF line endings): `41656d73d9ec6d0bdbbc1b108d905025be0b59143e77d7721f692b6c95a34d35`
- Split: 60 development / 40 held-out cases
- Family split: 32 development / 20 held-out families
- Valid strict-template controls: 3 development / 2 held-out (5 total); 12 development / 8 held-out free-phrasing Valid cases
- Strict-template control case IDs: `nla1-V01-dev-en-1`, `nla1-V04-dev-en-1`, `nla1-V05-dev-en-1`, `nla1-V09-heldout-en-1`, `nla1-V10-heldout-en-1`
- Exact split family IDs: listed below

| Case class | Expected status | Dev families | Held-out families |
|---|---|---|---|
| valid | Valid | V01, V02, V03, V04, V05, V06, V07, V08 | V09, V10, V11, V12, V13 |
| needs-clarification | NeedsClarification | C01, C02, C03, C04, C05, C06, C07, C08 | C09, C10, C11, C12, C13 |
| unsupported | Unsupported | U01, U02, U03, U04, U05, U06, U07, U08 | U09, U10, U11, U12, U13 |
| adversarial | Unsupported | A01, A02, A03, A04, A05, A06, A07, A08 | A09, A10, A11, A12, A13 |

## Planned distributions

| Split | Valid | NeedsClarification | Unsupported | Adversarial | English | Filipino | Taglish | Total |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| development | 15 | 15 | 15 | 15 | 20 | 20 | 20 | 60 |
| held-out | 10 | 10 | 10 | 10 | 14 | 13 | 13 | 40 |
| total | 25 | 25 | 25 | 25 | 34 | 33 | 33 | 100 |
