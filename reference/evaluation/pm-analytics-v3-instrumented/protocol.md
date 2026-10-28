# TEST-182 supplementary instrumented evaluation

Use the same 30 TEST-181 questions, expectations and scoring by importing its
frozen runner functions. Never alter TEST-181 corpus, labels, protocol, runner or
results. Keep TEST-139, TEST-146, TEST-179 and TEST-180 unchanged. New record
TEST-182 is a development-set supplementary run, not independent accuracy,
held-out evaluation or evidence of generalization. New instrumented results do
not replace historical scores. Label provenance remains assistant-authored.

Run 30 questions once per explicit DeterministicParser/DeepSeek condition, 60
endpoint requests, alternating the first mode. Same controlled synthetic native
SQL Server fixture, same running instrumented binary, same backend endpoint
POST /api/v1/analytics/pm/v3/interpret. No preview, execution, normal GSD database
access, or production-record mutation. No fallback, adapter retries, selective
reruns or tuning. Unchanged backend retries remain bounded by two per logical
provider call, 90 physical attempts maximum for the 30 DeepSeek-mode requests.
Record actual dispatches, not just this budget.

## Observer-only runtime

instrument.py copies tracked server files into ignored artifacts/test-182/runtime
and adds observation hooks to that copy only. Program middleware correlates a
fixed per-case X-Test182-Request header with AsyncLocal events. Raw structured
model output, parsed candidates, question constraints, schema rejection/pass,
catalog resolution, provider ledger observations and wall clocks are recorded.
No new endpoint, prompt, request payload, model setting, parser rule, validation
condition, retry policy or response is changed. An in-memory reverse-patch check
must reconstruct every original business source exactly. Preserve the patch,
original source hashes, observer hash, binary hash and tested commit identities.
Instrumentation adds timing overhead; do not equate its latency with an
uninstrumented performance benchmark. A binary with observers is explicitly an
evaluation build, not an unchanged production binary.

Observe each stage only if reached. A shared SafetyRules or parser early return
may have no interpreter proposal. Mark proposal unavailable with its reason;
never invoke the parser/provider a second time to invent a pre-validation
candidate. DeepSeek response content is captured before the backend output
parser, then the typed candidate before constraints. Parser proposal is the
actual Parse return before common validation. Final response is recorded from
the HTTP endpoint. NeedsClarification plans are drafts, not fully validated
executable plans; expose a finalValidatedPlan only for Valid plus schema/catalog
validation success. Keep stage availability explicit.

The unchanged provider ledger supplies latency, HTTP success/failure and retry
metadata. A hook directly before SendAsync counts actual client dispatches.
Server receipt and billing of a failed dispatch cannot be independently proven.
Only allowlisted response usage/model/fingerprint/created metadata are extracted
from successful bounded bodies. No requests, credentials, authorization headers,
raw prompts, token maps or reasoning traces are persisted. Inputs and fixture
contain synthetic data only. Never log exception messages or provider error
bodies that might contain sensitive configuration.

## Tokens, pricing and reliability

Retain provider-reported prompt_tokens, completion_tokens, total_tokens,
prompt_cache_hit_tokens, prompt_cache_miss_tokens and reasoning details if present.
Do not synthesize a missing reported token value. Check cache hit plus miss equals
input, and input plus output equals reported total when all are available.
For no-provider-call requests, reported token fields are not applicable; observed
provider API spend is zero, not an invented provider usage report.

Use the official documented rates in pricing.json, separating cached input,
uncached input and output. Estimate USD = (hit*hitRate + miss*missRate +
output*outputRate)/1e6 with decimal arithmetic. A successful usage response is
not a paid invoice. Retain known-cost subtotal and mark aggregate cost incomplete
if any actual call lacks sufficient usage/pricing evidence. Missing cache split
is not assumed uncached. Peak holiday ambiguity produces a rate range, not a
fabricated holiday decision. Preserve source URL, retrieval time and page hash.
Model version is the exact provider-reported model and fingerprint; do not infer
a weight revision merely from a configured alias or release note.

Report actual logical calls, dispatches, retries, provider HTTP failures,
transport timeouts/failures, endpoint failures and terminal failures per request.
Distinguish HTTP/provider transport errors from HTTP-200 model-output contract
errors, backend question constraints, catalog effects and interpretation
mismatches. A bad final plan alone cannot identify an LLM error. Compare raw,
parsed, constrained and final status/fields/plans without dropping scope.

## Latency definitions

- Provider transport latency: ledger wall time from actual SendAsync through
  bounded successful body read, or until failure detection. Failure timings may
  stop at headers rather than reading an error body, matching unchanged behavior.
- Provider logical stage: elapsed GenerateAsync time, including request creation,
  response parsing, retries and backoff. It is not pure remote inference time.
- Backend processing residual: measured server pipeline elapsed minus the sum of
  provider transport latencies. Includes authentication, SQL, validation, JSON,
  observer work and any backoff; it is not CPU time. Also retain server elapsed
  minus provider logical stage to separate retry/backoff from local remainder.
- Endpoint total: client HTTP round trip to the complete final body. Retain
  server elapsed separately; client/server residual includes loopback transport,
  response completion and telemetry persistence outside the server stopwatch.

Do not call any of these server-side model inference time or TTFT; those are
unavailable. Never clamp a negative timing residual silently. Missing telemetry
remains unavailable. Provider calls and cost on the parser path are observed,
not assumed zero before measurement.

## Fixture and audit

Fresh owned SQL Server 2019 fixture: one Active fire-extinguisher asset,
building North Block, department Synthetic Department, null location. The
unchanged startup scheduler creates one unassigned November 2026 schedule.
No inspection records/forms. Save full before/after catalog, schedule and counts;
assert equality. Use live clock, explicit dates, synthetic authenticated GSD user.
No real credentials or connection strings in committed evidence.

Before requests, commit protocol, pricing, observer generator and runner, check
frozen TEST-181 hashes, compile the overlay and inspect the reversible patch.
After all 60 once-only attempts, audit saved telemetry and score/cost arithmetic
offline without provider reruns. Compare deterministic responses with TEST-181
as an additional observer conformance check, not independent model validation.
Record any setup failures separately. Stop only the owned evaluation API.
