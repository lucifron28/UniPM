# UniPM Retrieval Benchmark

- Evaluation manifest: `1.1.0`
- Operational dataset: `1.1.0`
- Generated at UTC: `2026-07-25T16:14:14.2693335+00:00`
- Queries: `24`
- Channels: `fused, lexical, semantic`

> Synthetic benchmark results are pipeline evidence only and do not prove production GSD performance.

## Embedding execution

| Provider | Model | Dimensions | Documents | Batches | Query embeddings | Expected requests | Actual requests | Query cache hits | Provider duration (ms) |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|
| `granite-local` | `ibm-granite/granite-embedding-97m-multilingual-r2` | 384 | 30 | 2 | 23 | 25 | 25 | 25 | 2267.131 |

## fused

Result limit: `10`; queries: `24`
Fusion: `rrf`; RRF K: `60`; candidate limit: `20`
Semantic degradation policy: Semantic unavailable or failed returns lexical-only results and marks the run degraded.

### Overall

| Slice | Hit@1 | Hit@5 | Precision@5 | Recall@5 | Recall@10 | MRR |
|---|---:|---:|---:|---:|---:|---:|
| `overall` | 1.000 | 1.000 | 0.458 | 0.965 | 1.000 | 1.000 |

### Execution

| Median latency (ms) | P95 latency (ms) | Zero results | Failed queries |
|---:|---:|---:|---:|
| 133.630 | 247.830 | 0 | 0 |

Semantic candidates: `93` across `24` queries; candidate-cap hits: `0`.

### By language

| Slice | Hit@1 | Hit@5 | Precision@5 | Recall@5 | Recall@10 | MRR |
|---|---:|---:|---:|---:|---:|---:|
| `english` | 1.000 | 1.000 | 0.380 | 1.000 | 1.000 | 1.000 |
| `tagalog` | 1.000 | 1.000 | 0.629 | 1.000 | 1.000 | 1.000 |
| `taglish` | 1.000 | 1.000 | 0.400 | 0.879 | 1.000 | 1.000 |

### By asset category

| Slice | Hit@1 | Hit@5 | Precision@5 | Recall@5 | Recall@10 | MRR |
|---|---:|---:|---:|---:|---:|---:|
| `emergency-light` | 1.000 | 1.000 | 0.467 | 0.958 | 1.000 | 1.000 |
| `fire-alarm` | 1.000 | 1.000 | 0.400 | 1.000 | 1.000 | 1.000 |
| `fire-extinguisher` | 1.000 | 1.000 | 0.600 | 0.900 | 1.000 | 1.000 |
| `water-drinking-station` | 1.000 | 1.000 | 0.367 | 1.000 | 1.000 | 1.000 |

### By scenario tag

| Slice | Hit@1 | Hit@5 | Precision@5 | Recall@5 | Recall@10 | MRR |
|---|---:|---:|---:|---:|---:|---:|
| `cold-start` | 1.000 | 1.000 | 0.300 | 0.850 | 1.000 | 1.000 |
| `cross-language` | 1.000 | 1.000 | 0.514 | 0.939 | 1.000 | 1.000 |
| `distractor-resistance` | 1.000 | 1.000 | 0.560 | 0.830 | 1.000 | 1.000 |
| `lexicon-covered` | 1.000 | 1.000 | 0.500 | 0.975 | 1.000 | 1.000 |
| `resolved-history` | 1.000 | 1.000 | 0.622 | 0.972 | 1.000 | 1.000 |
| `same-asset-history` | 1.000 | 1.000 | 0.520 | 1.000 | 1.000 | 1.000 |
| `same-building-context` | 1.000 | 1.000 | 0.667 | 1.000 | 1.000 | 1.000 |
| `semantic-paraphrase` | 1.000 | 1.000 | 0.373 | 0.960 | 1.000 | 1.000 |
| `similar-asset-fallback` | 1.000 | 1.000 | 0.514 | 0.939 | 1.000 | 1.000 |
| `unresolved-history` | 1.000 | 1.000 | 0.440 | 0.988 | 1.000 | 1.000 |

### Weakest queries

- `Q001` (`mahina ang pressure`; fire-extinguisher, taglish): MRR `1.000`, Recall@5 `1.000`
- `Q002` (`kulang ang pressure`; fire-extinguisher, tagalog): MRR `1.000`, Recall@5 `1.000`
- `Q003` (`gauge near red zone`; fire-extinguisher, english): MRR `1.000`, Recall@5 `1.000`
- `Q004` (`kailangan mag refill`; fire-extinguisher, tagalog): MRR `1.000`, Recall@5 `1.000`
- `Q005` (`missing safety pin`; fire-extinguisher, english): MRR `1.000`, Recall@5 `1.000`

## lexical

Result limit: `10`; queries: `24`

### Overall

| Slice | Hit@1 | Hit@5 | Precision@5 | Recall@5 | Recall@10 | MRR |
|---|---:|---:|---:|---:|---:|---:|
| `overall` | 0.583 | 0.583 | 0.125 | 0.267 | 0.267 | 0.583 |

### Execution

| Median latency (ms) | P95 latency (ms) | Zero results | Failed queries |
|---:|---:|---:|---:|
| 7.196 | 26.683 | 10 | 0 |

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

## semantic

Result limit: `10`; queries: `24`

### Overall

| Slice | Hit@1 | Hit@5 | Precision@5 | Recall@5 | Recall@10 | MRR |
|---|---:|---:|---:|---:|---:|---:|
| `overall` | 1.000 | 1.000 | 0.458 | 0.965 | 1.000 | 1.000 |

### Execution

| Median latency (ms) | P95 latency (ms) | Zero results | Failed queries |
|---:|---:|---:|---:|
| 6.085 | 14.374 | 0 | 0 |

Semantic candidates: `93` across `24` queries; candidate-cap hits: `0`.

### By language

| Slice | Hit@1 | Hit@5 | Precision@5 | Recall@5 | Recall@10 | MRR |
|---|---:|---:|---:|---:|---:|---:|
| `english` | 1.000 | 1.000 | 0.380 | 1.000 | 1.000 | 1.000 |
| `tagalog` | 1.000 | 1.000 | 0.629 | 1.000 | 1.000 | 1.000 |
| `taglish` | 1.000 | 1.000 | 0.400 | 0.879 | 1.000 | 1.000 |

### By asset category

| Slice | Hit@1 | Hit@5 | Precision@5 | Recall@5 | Recall@10 | MRR |
|---|---:|---:|---:|---:|---:|---:|
| `emergency-light` | 1.000 | 1.000 | 0.467 | 0.958 | 1.000 | 1.000 |
| `fire-alarm` | 1.000 | 1.000 | 0.400 | 1.000 | 1.000 | 1.000 |
| `fire-extinguisher` | 1.000 | 1.000 | 0.600 | 0.900 | 1.000 | 1.000 |
| `water-drinking-station` | 1.000 | 1.000 | 0.367 | 1.000 | 1.000 | 1.000 |

### By scenario tag

| Slice | Hit@1 | Hit@5 | Precision@5 | Recall@5 | Recall@10 | MRR |
|---|---:|---:|---:|---:|---:|---:|
| `cold-start` | 1.000 | 1.000 | 0.300 | 0.850 | 1.000 | 1.000 |
| `cross-language` | 1.000 | 1.000 | 0.514 | 0.939 | 1.000 | 1.000 |
| `distractor-resistance` | 1.000 | 1.000 | 0.560 | 0.830 | 1.000 | 1.000 |
| `lexicon-covered` | 1.000 | 1.000 | 0.500 | 0.975 | 1.000 | 1.000 |
| `resolved-history` | 1.000 | 1.000 | 0.622 | 0.972 | 1.000 | 1.000 |
| `same-asset-history` | 1.000 | 1.000 | 0.520 | 1.000 | 1.000 | 1.000 |
| `same-building-context` | 1.000 | 1.000 | 0.667 | 1.000 | 1.000 | 1.000 |
| `semantic-paraphrase` | 1.000 | 1.000 | 0.373 | 0.960 | 1.000 | 1.000 |
| `similar-asset-fallback` | 1.000 | 1.000 | 0.514 | 0.939 | 1.000 | 1.000 |
| `unresolved-history` | 1.000 | 1.000 | 0.440 | 0.988 | 1.000 | 1.000 |

### Weakest queries

- `Q001` (`mahina ang pressure`; fire-extinguisher, taglish): MRR `1.000`, Recall@5 `1.000`
- `Q002` (`kulang ang pressure`; fire-extinguisher, tagalog): MRR `1.000`, Recall@5 `1.000`
- `Q003` (`gauge near red zone`; fire-extinguisher, english): MRR `1.000`, Recall@5 `1.000`
- `Q004` (`kailangan mag refill`; fire-extinguisher, tagalog): MRR `1.000`, Recall@5 `1.000`
- `Q005` (`missing safety pin`; fire-extinguisher, english): MRR `1.000`, Recall@5 `1.000`

## Channel comparison

The comparison below uses per-query reciprocal rank only; scores are not normalized or fused.

### Lexical outperformed semantic


### Semantic outperformed lexical

- `Q004` (`kailangan mag refill`)
- `Q006` (`gauge malapit sa red zone`)
- `Q009` (`detector communication problem`)
- `Q010` (`bell not sounding`)
- `Q016` (`dim during test`)
- `Q017` (`charging problem`)
- `Q018` (`may charging problem ang emergency light`)
- `Q020` (`mahina ang daloy`)
- `Q023` (`UV light not working`)
- `Q024` (`UV light not functioning sa water station`)

### Neither channel found a relevant result


## Limitations

- RRF is applied without maintenance-context boosts, thresholds, or an insufficient-evidence policy.
- Results are measured on fictional synthetic maintenance data and do not prove production GSD performance.
- Source selection and summary behavior are not evaluated.
- Timing is diagnostic only and is not a statistically valid performance comparison from one local run.
