---
id: TEST-157
type: test-run
title: NLA routing and deterministic evaluation remediation
status: executed
recordedAtUtc: 2026-10-25
testedCommit: b8f5522206dd7722a6649d12fc72da73f421c087
sourceBranch: feature/capstone-remediation
evidenceLevel: locally-executed
---

# NLA routing remediation

Commit `f050c211` removes the two implicit `ReviewedInterpretations` runtime
shortcuts. The questions remain regression cases. A bounded parser grammar
handles supported late-inspection phrases and excludes conjunctions and
negations that require clarification. The frozen corpus and labels are unchanged.

Default routing remains DeepSeek only when Development, enabled and configured
with a key. Otherwise the parser runs. An explicit parser request is independent
of provider availability. Provider errors remain visible errors, with no silent
parser substitution. This record does not enable an external provider.

At `b8f5522206dd7722a6649d12fc72da73f421c087`:

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --filter "FullyQualifiedName~PmAnalyticsV3AcceptanceTests|FullyQualifiedName~PmAnalyticsV3EndpointTests|FullyQualifiedName~PmAnalyticsV3RegressionTests|FullyQualifiedName~PmAnalyticsV3EvaluationHarnessTests|FullyQualifiedName~NaturalLanguageAnalyticsModelClientTests" --logger "trx;LogFileName=phase4-nla-backend.trx" --results-directory artifacts/remediation/backend
dotnet run --project tools/UniPM.PmAnalytics.V3Evaluation --no-build -- score
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --filter "FullyQualifiedName~PmAnalyticsV3SqlServerTests.V3_quarter_drilldown_and_matching_denominators_translate_on_sql_server_2019" --logger "trx;LogFileName=phase4-native-sql.trx" --results-directory artifacts/remediation/backend
```

Commands ran separately. Exit 0 for each. The backend filter passed 69 tests,
with no failures or skips. The deterministic scorer matched 18/18 reviewed
development outcomes and 69/69 plan assertions, with zero pending reviews or
unsafe acceptances. This is development-set agreement, not held-out accuracy.

The isolated native SQL test passed 1/1, asserting SQL Server major version 15,
compatibility 150, quarterly drilldown and matching denominators. Both SQL test
variables were process-scoped to local Windows-authenticated master. The fixture
created and dropped a fresh synthetic GUID database. This test does not assert
Full-Text Search and did not select an application database.

Provider-mode tests use fake HTTP handlers. Non-Development mode makes zero
requests even with a synthetic key and enabled flag. Development default routing
makes one fake request and identifies DeepSeek in the typed response. These
tests prove adapter selection and validation, not live model quality. Earlier
commit `8ecf2857` failed compilation because the new test used `using` with
`IConfigurationRoot`; `b8f5522` removes those modifiers before the passing run.

At client source `0087179d1741ca28fa563f1b3773783ce4840980`, the existing
NLA UI test file passed 19/19 with Node 22.23.4. Source inspection confirms that
normal requests omit an interpreter override, explicit parser requests name it,
and displayed provenance comes from the server. Client routing needed no edit.

Raw logs, TRX and command details remain ignored under
`artifacts/remediation/backend/phase4*`, `backend/phase4.md` and
`clients/phase4-nla-ui-test.log`. No live provider, new numerical benchmark,
institutional acceptance or browser execution is established by this record.
