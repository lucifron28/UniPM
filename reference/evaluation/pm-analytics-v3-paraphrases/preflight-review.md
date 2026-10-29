# Supplementary comparison preflight review

Recorded October 28, 2026 by the coding assistant. This is source and label
preparation, not independent researcher approval or an interpretation result.

## Gold plans

All 24 proposed executable plans passed the unchanged V3 plan validator.
All 30 case IDs are unique, questions satisfy the 512-character limit, the six
controls have null executable gold plans, and the canonical LF corpus hash
matches the manifest. No corpus labels or questions were changed.

```powershell
dotnet run --project tools/UniPM.PmAnalytics.V3Evaluation -- validate-paraphrases
```

The command exited zero on `16dea58efccf327969a7e6061f0284f6e1bc936b` plus the
new ParaphraseCorpusValidation implementation and Program dispatch. No question
was interpreted. Raw output is ignored in
`artifacts/nla-paraphrases-gold-validation.log`. The validator canonicalizes
CRLF to LF before hashing to handle Windows checkout settings.

## Semantic review

The eight intentions use supported categories, scheduled months and catalog
measures. Completion counts include late completions. Progress uses scheduled
inspections as denominator. Late and missed work use original PM month-end;
the last intention uses the separate first Inspector assignment window.
Department grouping uses current asset metadata.

Several questions do not specify Table presentation. The proposed gold plans
use that preferred layout, so exact full-plan agreement must be distinguished
from semantic intent agreement. An ungrouped Summary with the same scope and
measures is not a language misunderstanding solely because the layout differs.
Predeclare allowed layout differences before scoring; additional filters,
measures, categories or periods must count as semantic mismatches.

## Overlap check

An automated comparison checked these 30 questions against 218 earlier question
fields in JSONL files under `reference/evaluation`. It found zero exact matches
after case, punctuation and whitespace normalization. Maximum SequenceMatcher
character similarity was 0.757. This is a wording diagnostic, not a semantic
novelty or model-quality score. Per-case nearest references are retained in
ignored `artifacts/nla-paraphrases-overlap.json`.

The scan is not exhaustive of source-code test strings, demo Markdown or earlier
external questions. The report intentions intentionally reuse supported
capabilities. No independent held-out claim is justified.

## Clarification controls need scoring decisions

- S01 leaves subject, category, period and meaning unresolved. The backend can
  ask for one missing concept first. Requiring every concept in one response
  would penalize appropriate staged clarification.
- S02 omits the year. The API clarification identifier is `Period`, not the
  conceptual `period.year` label. An executable plan must not guess the year.
- S03 leaves an outcome union unresolved. Clarification must distinguish union
  from separate counts and must not silently substitute intersection.

The controls currently encode conceptual labels, not exact approved API field
sets. Resolve the scoring rules before running either condition. Unsupported
controls must return Unsupported without an executable plan. Pre-provider
SafetyRules rejection is system safety evidence, not remote-model reasoning.

## Paired execution boundary

The backend accepts explicit DeterministicParser and DeepSeek mode selection.
Explicit DeepSeek fails if unavailable rather than falling back. Both share
sanitization, safety and validation; model plans also pass backend question
constraints. The comparison evaluates the configured interpretation paths,
not isolated raw-model capability.

The earlier V3 diagnose command calls the rule parser directly. Do not compare
that path against the full DeepSeek endpoint. The paired adapter must call the
same full backend boundary with explicit selection and record the resolved
interpreter. Provider retries, physical-call limits and usage capture still
need inspection before real execution. No scored run or provider call was
performed in this preflight step.

The corpus remains author-prepared pending review. This document does not
attribute label approval to the owner or establish any model advantage.
