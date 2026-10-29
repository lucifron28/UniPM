"""TEST-189: post-fix repeat; import the frozen TEST-181 rubric unchanged."""
import datetime
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import time

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
ART = ROOT / 'artifacts/test-189'
ORIGINAL = HERE.parent / 'pm-analytics-v3-unified'
spec = importlib.util.spec_from_file_location('frozen181', ORIGINAL / 'run-evaluation.py')
frozen = importlib.util.module_from_spec(spec)
spec.loader.exec_module(frozen)
frozen.http_adapter.BASE = 'http://localhost:5269'


def historical_hashes():
    roots = [ROOT / 'reference/evidence/test-runs', ROOT / 'reference/evaluation']
    return {str(p.relative_to(ROOT)).replace('\\', '/'): frozen.sha(p)
            for base in roots for p in sorted(base.rglob('*'))
            if p.is_file() and '__pycache__' not in p.parts
            and HERE not in p.parents}


def snapshot():
    database = (ART / 'database.txt').read_text().strip()
    import re
    assert re.fullmatch(r'UniPMDb_Test189_[a-f0-9]{12}', database)
    query = "SET NOCOUNT ON; SELECT Id,AssetCode,AssetCategory,Building,Department,Location,Status,CreatedAt,UpdatedAt FROM Assets ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES; SELECT (SELECT COUNT(*) FROM PreventiveMaintenanceSchedules) AS schedules,(SELECT COUNT(*) FROM InspectionRecords) AS inspections,(SELECT COUNT(*) FROM PreventiveMaintenanceForms) AS forms FOR JSON PATH; SELECT * FROM PreventiveMaintenanceSchedules ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES; SELECT CAST(SERVERPROPERTY('ProductVersion') AS varchar(50)) AS engineVersion,compatibility_level FROM sys.databases WHERE name=DB_NAME() FOR JSON PATH;"
    raw = subprocess.check_output(['sqlcmd', '-S', 'localhost', '-E', '-d', database,
                                   '-b', '-w', '65535', '-y', '0', '-Q', query], text=True)
    lines = [json.loads(line) for line in raw.splitlines() if line.strip().startswith('[')]
    return dict(catalog=lines[0], counts=lines[1], schedules=lines[2], sqlServer=lines[3],
                sha256=hashlib.sha256(raw.encode()).hexdigest())


def source_hashes():
    return {str(p.relative_to(ROOT)).replace('\\', '/'): frozen.sha(p)
            for p in sorted((ROOT / 'server').rglob('*.cs'))
            if 'obj' not in p.parts and 'bin' not in p.parts}


def main():
    ART.mkdir(parents=True, exist_ok=True)
    output = ART / 'results.json'
    assert not output.exists(), 'Refusing to overwrite an evaluation run.'
    cases = frozen.load_cases()
    before = snapshot()
    assert len(before['catalog']) == 1
    asset = before['catalog'][0]
    assert (asset['Building'], asset['Department'], asset['Location']) == ('North Block', 'Synthetic Department', None)
    assert before['counts'] == [dict(schedules=1, inspections=0, forms=0)]
    historical = historical_hashes()
    sources = source_hashes()
    http, session = frozen.http_adapter.post('/api/v1/auth/login', dict(
        email='gsd@unipm.local', password=os.environ['UNIPM_DEV_USER_PASSWORD']))
    assert http == 200, 'Synthetic login failed before evaluation.'
    commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    binary = ROOT / 'artifacts/guard-build/bin/server/debug/UniPM.Api.dll'
    result = dict(evidenceRecord='TEST-189', sourceCommit=commit,
        corpusSha256=frozen.sha(ORIGINAL / 'cases.jsonl'),
        protocolSha256=frozen.sha(HERE / 'protocol.md'),
        frozenProtocolSha256=frozen.sha(ORIGINAL / 'protocol.md'),
        scoringRunnerSha256=frozen.sha(ORIGINAL / 'run-evaluation.py'),
        runnerSha256=frozen.sha(Path(__file__)),
        backendAssemblySha256=hashlib.sha256(binary.read_bytes()).hexdigest(),
        startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        model='Configured deepseek-flash, temperature zero; served revision unavailable through endpoint.',
        rawPreNormalizationAvailable=False, fixtureBefore=before, sourceHashes=sources,
        historicalHashes=historical, attempts=[])
    output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    for index, case in enumerate(cases):
        modes = frozen.MODES if index % 2 == 0 else list(reversed(frozen.MODES))
        for mode in modes:
            start = time.perf_counter()
            http, response = frozen.http_adapter.post('/api/v1/analytics/pm/v3/interpret',
                dict(question=case['question'], interpreter=mode), session['accessToken'])
            code = response.get('code')
            classification = ('endpoint-failure-origin-unresolved' if http != 200
                else 'backend-plan-rejection-reported' if code == 'InvalidPlanNeedsClarification'
                else 'shared-pre-interpretation-guard' if response.get('interpreter') == 'SafetyRules'
                else 'catalog-resolution-reported' if code in ['UnknownCatalogValue', 'AmbiguousCatalogValue']
                else 'raw-origin-unavailable')
            result['attempts'].append(dict(caseId=case['caseId'], subset=case['subset'], kind=case['kind'],
                question=case['question'], sourceCommit=commit, requestedInterpreter=mode,
                resolvedInterpreter=response.get('interpreter'), httpStatus=http,
                elapsedMs=round((time.perf_counter() - start) * 1000, 3), finalBackendResponse=response,
                rawPreNormalizationAvailable=False, observedClassification=classification,
                score=frozen.score(case, http, response)))
            output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
            print(case['caseId'], mode, http, response.get('status', code),
                  result['attempts'][-1]['score']['semanticCaseCorrect'], flush=True)
            if http == 200 and response.get('interpreter') not in [mode, 'SafetyRules']:
                raise SystemExit('Unexpected interpreter; fallback forbidden.')
    result.update(endedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(), fixtureAfter=snapshot())
    result['fixtureUnchanged'] = before == result['fixtureAfter']
    result['sourceUnchanged'] = sources == source_hashes()
    result['historicalUnchanged'] = historical == historical_hashes()
    result['commitUnchanged'] = commit == subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    frozen.summarize(result, cases)
    output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    assert all(result[key] for key in ['fixtureUnchanged', 'sourceUnchanged', 'historicalUnchanged', 'commitUnchanged'])
    print(json.dumps(result['summary'], indent=2))


if __name__ == '__main__':
    main()
