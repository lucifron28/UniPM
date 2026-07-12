---
id: EVIDENCE-HANDBOOK
type: handbook
title: UniPM engineering evidence handbook
status: reviewed
evidenceLevel: source-inspected
---

# UniPM Engineering Evidence

## Purpose

UniPM preserves engineering evidence so implementation progress, architecture
decisions, test execution, retrieval experiments, approved baselines, known
limitations, and verification gaps remain traceable. This record system
supports engineering review and can inform the manuscript, but it is not a
substitute for institutional validation or fabricated manuscript proof.

## Evidence Boundary

```text
artifacts/             raw, generated, local, ignored by Git
reference/evidence/    selected, reviewed, sanitized, traceable, committed
```

Raw command output belongs under ignored `artifacts/`. A record may cite a raw
artifact, but only after its contents have been inspected and sensitive values
have been removed. Approved baseline files may be copied into
`reference/evidence/baselines/` only after checking for secrets, connection
strings, private endpoints, raw production records, provider credentials,
prompts, token maps, and embedding vectors. Synthetic query text and synthetic
inspection IDs are allowed in reviewed retrieval baselines because they are
fictional test-only data.

## Record Types

- **Implementation record**: source-inspected chronology for an implemented
  feature, including commits, files, contracts, tests present, and limitations.
- **Test-run record**: one actual execution with commit identity, commands,
  exit codes, counts, environment, artifacts, and skipped verification.
- **Experiment record**: one retrieval or lexicon experiment with a dataset,
  method, controlled variables, metrics, interpretation, and decision.
- **Decision record**: an architecture decision with alternatives,
  consequences, security/privacy implications, and supporting evidence.

Use the templates in `templates/`. Every record has stable front matter:

```yaml
---
id: TEST-001
type: test-run
title: Current backend verification baseline
status: executed
recordedAtUtc: 2026-07-12T00:00:00Z
testedCommit: abcdef1234567890
sourceBranch: chore/engineering-evidence
evidenceLevel: locally-executed
---
```

Allowed `evidenceLevel` values:

- `source-inspected`
- `locally-executed`
- `ci-executed`
- `real-provider-executed`
- `deterministic-provider-executed`

Allowed `status` values:

- `draft`
- `executed`
- `reviewed`
- `superseded`
- `rejected`
- `pending`

Record IDs and filenames remain stable after review. Update `INDEX.md` whenever
an evidence record is added or superseded. Do not rewrite an approved record to
make later results look better; create a new experiment or test-run record.

## Source And Execution Claims

Every executed record identifies the exact tested commit SHA, branch, UTC time,
commands, and environment facts needed to interpret the result. Source paths,
Git history, committed tests, and existing artifacts can support a
`source-inspected` record, but they cannot be described as historical test
execution. Use factual phrases such as "the repository contains tests covering"
and "no retained execution log was found" when execution evidence is absent.
Use "not executed", "not retained", "source-inspected only", or "pending
verification" for gaps. Do not use "verified" for source inspection alone.

## Retrieval And Provider Evidence

Synthetic benchmark scores do not prove production GSD performance. A
deterministic embedding provider can validate migration, indexing, persistence,
retriever, and report orchestration, but it cannot establish semantic model
quality. Real-provider records must identify the non-secret provider key, model,
dimensions, and embedding profile without recording endpoints, API keys,
payloads, or vectors. A disabled or unavailable provider is recorded as a
limitation, not as a zero-quality result.

The current repository has no independent labeled lexicon-accuracy evaluator.
Unit tests and retrieval annotations are behavioral/source evidence, not a
precision, recall, or F1 baseline. Do not create those metrics until a focused
evaluation dataset and evaluator exist.

## Sanitization

Evidence must not contain secrets, passwords, connection strings, API keys,
tokens, authorization headers, raw external-provider payloads, embedding
vectors, raw production prompts, request-scoped token maps, real institutional
records, personal data, or avoidable absolute user-profile paths. Inspect copied
baseline files directly; text replacement alone is not proof of sanitization.

## Capture Workflow

Use the Windows-first capture script:

```powershell
.\scripts\evidence\Invoke-BackendVerification.ps1
.\scripts\evidence\Invoke-BackendVerification.ps1 -Configuration Release -RunSqlServerTests
.\scripts\evidence\Invoke-BackendVerification.ps1 -RunSqlServerTests -BenchmarkChannels lexical
.\scripts\evidence\Invoke-BackendVerification.ps1 -RunSqlServerTests -BenchmarkChannels lexical,semantic
.\scripts\evidence\Invoke-BackendVerification.ps1 -RunSqlServerTests -BenchmarkChannels fused
```

The script writes raw logs and generated reports under ignored
`artifacts/evidence/<utc-timestamp>-<short-sha>/`. It records safe metadata,
parses TRX counters when present, writes `verification-summary.json`, and
generates `SHA256SUMS.txt`. It never records the SQL connection value, API key,
embedding endpoint, request body, or provider response body. SQL tests require
`UNIPM_SQLSERVER_TEST_CONNECTION`. Semantic benchmark execution requires the
existing real-provider configuration and never silently falls back to lexical
or deterministic mode. Fused benchmarking requires both channels and fails
when semantic retrieval degrades.

For the optional local observability profile, use:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\evidence\Invoke-ObservabilityVerification.ps1
```

This process-scoped verification enables metrics without editing `.env`,
requires a clean worktree, validates Compose, starts the `observability`
profile, polls API root, liveness, readiness, metrics, and the Prometheus
target, checks Grafana provisioning, and stops the stack in `finally`. It
resolves the API, Prometheus, and Grafana ports from `UNIPM_*_PORT` variables
with the documented defaults. Its raw artifacts are ignored. It does not prove
IIS deployment, production monitoring, retention, alert delivery, or real user
traffic.

## Index And Baselines

`INDEX.md` is the entry point for committed records and pending evidence. A
retrieval baseline is copied only when its channel actually executed and its
sanitized JSON/Markdown artifacts are hash-recorded. Baseline directories are
versioned by dataset and method; later runs receive new experiment IDs and do
not overwrite approved files.
