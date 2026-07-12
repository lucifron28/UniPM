# UniPM Retrieval Benchmark

- Evaluation manifest: `1.1.0`
- Operational dataset: `1.1.0`
- Generated at UTC: `2026-07-12T01:56:11.3476778+00:00`
- Queries: `24`
- Channels: `lexical`

> Synthetic benchmark results are pipeline evidence only and do not prove production GSD performance.

## lexical

Result limit: `10`; queries: `24`

### Overall

| Slice | Hit@1 | Hit@5 | Precision@5 | Recall@5 | Recall@10 | MRR |
|---|---:|---:|---:|---:|---:|---:|
| `overall` | 0.583 | 0.583 | 0.125 | 0.267 | 0.267 | 0.583 |

### By language

| Slice | Hit@1 | Hit@5 | Precision@5 | Recall@5 | Recall@10 | MRR |
|---|---:|---:|---:|---:|---:|---:|
| `english` | 0.500 | 0.500 | 0.120 | 0.320 | 0.320 | 0.500 |
| `tagalog` | 0.714 | 0.714 | 0.143 | 0.231 | 0.231 | 0.714 |
| `taglish` | 0.571 | 0.571 | 0.114 | 0.226 | 0.226 | 0.571 |

### By asset category

| Slice | Hit@1 | Hit@5 | Precision@5 | Recall@5 | Recall@10 | MRR |
|---|---:|---:|---:|---:|---:|---:|
| `emergency-light` | 0.500 | 0.500 | 0.100 | 0.131 | 0.131 | 0.500 |
| `fire-alarm` | 0.667 | 0.667 | 0.167 | 0.417 | 0.417 | 0.667 |
| `fire-extinguisher` | 0.667 | 0.667 | 0.133 | 0.297 | 0.297 | 0.667 |
| `water-drinking-station` | 0.500 | 0.500 | 0.100 | 0.222 | 0.222 | 0.500 |

### By scenario tag

| Slice | Hit@1 | Hit@5 | Precision@5 | Recall@5 | Recall@10 | MRR |
|---|---:|---:|---:|---:|---:|---:|
| `cold-start` | 0.250 | 0.250 | 0.050 | 0.125 | 0.125 | 0.250 |
| `cross-language` | 0.643 | 0.643 | 0.129 | 0.229 | 0.229 | 0.643 |
| `distractor-resistance` | 0.600 | 0.600 | 0.120 | 0.190 | 0.190 | 0.600 |
| `lexicon-covered` | 0.800 | 0.800 | 0.180 | 0.350 | 0.350 | 0.800 |
| `resolved-history` | 0.778 | 0.778 | 0.156 | 0.239 | 0.239 | 0.778 |
| `same-asset-history` | 1.000 | 1.000 | 0.240 | 0.500 | 0.500 | 1.000 |
| `same-building-context` | 0.667 | 0.667 | 0.133 | 0.192 | 0.192 | 0.667 |
| `semantic-paraphrase` | 0.400 | 0.400 | 0.093 | 0.247 | 0.247 | 0.400 |
| `similar-asset-fallback` | 0.571 | 0.571 | 0.114 | 0.207 | 0.207 | 0.571 |
| `unresolved-history` | 0.600 | 0.600 | 0.130 | 0.285 | 0.285 | 0.600 |

### Weakest queries

- `Q004` (`kailangan mag refill`; fire-extinguisher, tagalog): MRR `0.000`, Recall@5 `0.000`
- `Q006` (`gauge malapit sa red zone`; fire-extinguisher, taglish): MRR `0.000`, Recall@5 `0.000`
- `Q009` (`detector communication problem`; fire-alarm, english): MRR `0.000`, Recall@5 `0.000`
- `Q010` (`bell not sounding`; fire-alarm, english): MRR `0.000`, Recall@5 `0.000`
- `Q016` (`dim during test`; emergency-light, english): MRR `0.000`, Recall@5 `0.000`

## Limitations

- No fusion, RRF, score normalization, retrieval threshold, or insufficient-evidence policy is applied.
- Results are measured on fictional synthetic maintenance data and do not prove production GSD performance.
- Timing is diagnostic only and is not a statistically valid performance comparison from one local run.
