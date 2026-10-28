# TEST-182 instrumented supplementary evaluation

Completed 60 once-only requests using unchanged TEST-181 questions and scoring.
Parser semantic agreement was 19/30; DeepSeek was 21/30. Production rules and
prompts were not changed.

| Measurement | Parser | DeepSeek |
| --- | --- | --- |
| Actual provider calls | 0 | 28 |
| Retries / timeouts / provider HTTP failures | 0 / 0 / 0 | 0 / 0 / 0 |
| Endpoint failures | 0 | 1 backend InvalidPlan rejection |
| Reported input / output / total tokens | not applicable | 120712 / 4286 / 124998 |
| Reported cached / uncached input | not applicable | 114688 / 6024 |
| Estimated USD API cost | 0 | 0.003819264 |
| Median provider transport | not applicable | 1247.16 ms |
| Median backend processing residual | 3.12 ms | 3.26 ms |
| Median endpoint round trip | 23.49 ms | 1260.31 ms |

Provider medians cover 28 calls; other medians cover 30 requests per mode.
Backend residual includes observation overhead and is not CPU time. Invoice
cost, pure inference time and exact weight revision are unavailable. Replies
report deepseek-flash and fingerprint bc19ba97cbe016479903222bb1578db6.

Both raw V3D-006 proposals use North Block; catalog normalization produces
NORTH BLOCK. Two correct model proposals, V3U-020 and V3U-026, are removed by a
backend Boolean OR guard. V3D-012 receives provider HTTP 200 but backend schema
rejection for GroupBy/Columns. Its tokens and cost are included.

[Full TEST-182 report](../../evidence/test-runs/TEST-182-instrumented-nla-development-evaluation.md)

## Offline reproduction

From the recorded Windows repository checkout:

```powershell
python reference/evaluation/pm-analytics-v3-instrumented/audit-results.py
```

No endpoint/provider calls occur. Byte-level source checks assume the recorded
checkout line endings. The script independently checks scores, cost and timings.

## Measurement-method reproduction

Use a fresh checkout, native SQL Server 2019, .NET 10 and Python. The tested
protocol commit is a37571d0aa15333012c86152dddbaccff9e6d3ac; publication/audit
files were added afterward. The observer build's business-source parent is
06e8827182037fa1dbd2bcaf41a3fc6d2a40ef37. Source hashes and the patch are in results.
Supply UNIPM_DEV_USER_PASSWORD and DEEPSEEK_API_KEY through the existing ignored
local .env. Never print or commit their values. Do not overwrite existing
artifacts/test-182 measurements or committed results.

```powershell
python reference/evaluation/pm-analytics-v3-instrumented/instrument.py
dotnet build artifacts/test-182/runtime/server.csproj
New-Item -ItemType Directory -Force artifacts/test-182/telemetry
& reference/evaluation/pm-analytics-v3-instrumented/start-fixture.ps1
```

Wait for the owned fixture's startup schedule, then run run-instrumented.py with
UNIPM_DEV_USER_PASSWORD supplied securely in its process environment. The runner
imports TEST-181 scoring and sends only interpretation requests. It saves local
artifacts/test-182/results.json. Stop only the owned API PID from the script.
Use a new evidence ID for later executions; do not replace these results. A new
provider run may produce different outputs. Recheck documented prices for a
later run rather than treating the recorded pricing snapshot as permanent.

The published startup script matches the executed script. Protocol, pricing,
observer and runner were frozen before execution. No raw prompts, provider
requests, authorization headers, credentials, token maps or reasoning traces
are published. Raw provider content is synthetic interpretation output only.

This is a repeat development evaluation with assistant-authored full labels.
It does not establish independent accuracy or generalization.
