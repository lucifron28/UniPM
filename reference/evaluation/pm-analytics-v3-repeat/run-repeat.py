"""TEST-180: one paired full-endpoint repeat; no report execution."""
import datetime
import hashlib
import importlib.util
import json
import os
import pathlib
import subprocess
import time

ROOT = pathlib.Path(__file__).resolve().parents[3]
HERE = pathlib.Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('paired', HERE.parent / 'pm-analytics-v3-paraphrases/run-comparison.py')
paired = importlib.util.module_from_spec(spec)
spec.loader.exec_module(paired)
CORPUS = ROOT / 'reference/evaluation/pm-analytics-v3/dev-candidates.jsonl'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def catalog_fold(value):
    value = json.loads(json.dumps(value))
    if not isinstance(value, dict):
        return value
    for f in value.get('baseFilters', []):
        if f.get('field') in ('building', 'department'):
            known = 'NORTH BLOCK' if f['field'] == 'building' else 'SYNTHETIC DEPARTMENT'
            f['values'] = [v.strip().upper() if v.strip().upper() == known else v for v in f.get('values', [])]
    return value


def assertions(expected, actual, path='plan'):
    if expected is None:
        return []
    if isinstance(expected, dict):
        result = []
        for k, v in expected.items():
            candidate = actual.get(k) if isinstance(actual, dict) else None
            result += assertions(v, candidate, path + '.' + k)
        return result
    return [dict(field=path, correct=expected == actual, expected=expected, actual=actual)]


def disposition(response):
    if response.get('plan') is None:
        return 'None'
    return {'Valid':'Executable','NeedsClarification':'ClarificationDraft'}.get(response.get('status'), 'Invalid')


def score(case, http, response, equivalent=False):
    expected = case.get('expectedPlanAssertions')
    actual = response.get('plan')
    if equivalent:
        expected, actual = catalog_fold(expected), catalog_fold(actual)
    checks = assertions(expected, actual)
    status = http == 200 and response.get('status') == case['expectedStatus']
    fields = http == 200 and sorted(response.get('clarificationFields', [])) == sorted(case['expectedClarificationFields'])
    plan = http == 200 and disposition(response) == case['expectedPlanDisposition']
    return dict(statusCorrect=status, clarificationFieldsCorrect=fields,
        dispositionCorrect=plan, assertions=len(checks), assertionsCorrect=sum(c['correct'] for c in checks),
        allDeclaredCorrect=bool(status and fields and plan and all(c['correct'] for c in checks)),
        mismatches=[c for c in checks if not c['correct']])


def fixture_snapshot():
    database = (ROOT / 'artifacts/test-180/database.txt').read_text().strip()
    if not database.startswith('UniPMDb_Test180_') or not database[len('UniPMDb_Test180_'):].isalnum():
        raise RuntimeError('Not an owned TEST-180 fixture.')
    query = "SET NOCOUNT ON; SELECT Id,AssetCode,AssetCategory,Building,Department,Location,Status,CreatedAt,UpdatedAt FROM Assets ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES; SELECT (SELECT COUNT(*) FROM PreventiveMaintenanceSchedules) AS schedules,(SELECT COUNT(*) FROM InspectionRecords) AS inspections,(SELECT COUNT(*) FROM PreventiveMaintenanceForms) AS forms FOR JSON PATH; SELECT * FROM PreventiveMaintenanceSchedules ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES; SELECT CAST(SERVERPROPERTY('ProductVersion') AS varchar(50)) AS engineVersion,compatibility_level FROM sys.databases WHERE name=DB_NAME() FOR JSON PATH;"
    raw = subprocess.check_output(['sqlcmd','-S','localhost','-E','-d',database,'-b','-w','65535','-y','0','-Q',query], text=True)
    lines = [json.loads(l) for l in raw.splitlines() if l.strip().startswith('[')]
    return dict(catalog=lines[0], domainCounts=lines[1], schedules=lines[2], sqlServer=lines[3], sha256=hashlib.sha256(raw.encode()).hexdigest())


def main():
    raw = CORPUS.read_bytes().replace(b'\r\n', b'\n')
    corpus_hash = hashlib.sha256(raw).hexdigest()
    assert corpus_hash == 'dd313076a40ea5dd8eddfef610ff8db37ca6c5ba1934a286c6023a1586ca0e25'
    cases = [json.loads(line) for line in raw.splitlines()]
    assert [c['caseId'] for c in cases] == [f'V3D-{i:03}' for i in range(1,19)]
    before = fixture_snapshot()
    assert len(before['catalog']) == 1
    assert before['catalog'][0]['Building'] == 'North Block'
    assert before['catalog'][0]['Department'] == 'Synthetic Department'
    assert before['domainCounts'] == [dict(schedules=1, inspections=0, forms=0)]
    status, session = paired.post('/api/v1/auth/login',dict(email='gsd@unipm.local',password=os.environ['UNIPM_DEV_USER_PASSWORD']))
    if status != 200:
        raise SystemExit('Synthetic authentication failed; no cases executed.')
    source = subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
    production_hash = hashlib.sha256()
    for path in sorted((ROOT/'server').rglob('*.cs')):
        if 'obj' in path.parts or 'bin' in path.parts:
            continue
        production_hash.update(str(path.relative_to(ROOT)).replace('\\','/').encode())
        production_hash.update(path.read_bytes().replace(b'\r\n',b'\n'))
    started = datetime.datetime.now(datetime.timezone.utc).isoformat()
    result = dict(evidenceRecord='TEST-180',sourceCommit=source,
        productionSourceSha256=production_hash.hexdigest(),
        backendAssemblySha256=digest(ROOT/'server/bin/Debug/net10.0/UniPM.Api.dll'),
        interpreterSourceSha256=digest(ROOT/'server/Features/Reports/PmAnalyticsV3QuestionInterpreter.cs'),
        corpusSha256=corpus_hash, protocolSha256=digest(HERE/'protocol.md'),
        startedUtc=started,clock='Shared live server clock; explicit historical periods; no fixed-clock injection.',
        rawPreNormalizationAvailable=False, model='deepseek-flash; temperature zero',
        fixtureBefore=before, attempts=[])
    output = ROOT / 'artifacts/test-180/results.json'
    output.write_text(json.dumps(result,indent=2)+'\n')
    for i,case in enumerate(cases):
        order = ['DeterministicParser','DeepSeek'] if i%2==0 else ['DeepSeek','DeterministicParser']
        for requested in order:
            start=time.perf_counter()
            http,response=paired.post('/api/v1/analytics/pm/v3/interpret',dict(question=case['question'],interpreter=requested),session['accessToken'])
            row=dict(caseId=case['caseId'],question=case['question'],sourceCommit=source,
                requestedInterpreter=requested,resolvedInterpreter=response.get('interpreter'),
                httpStatus=http,elapsedMs=round((time.perf_counter()-start)*1000,3),
                status=response.get('status'),code=response.get('code'),
                clarificationFields=response.get('clarificationFields',[]),planDisposition=disposition(response),
                plan=response.get('plan'),finalBackendResponse=response,rawPreNormalizationAvailable=False,
                strict=score(case,http,response),catalogEquivalent=score(case,http,response,True))
            result['attempts'].append(row)
            output.write_text(json.dumps(result,indent=2)+'\n')
            print(case['caseId'],requested,http,response.get('status',response.get('code')),flush=True)
            if http==200 and row['resolvedInterpreter'] not in (requested,'SafetyRules'):
                raise SystemExit('Unexpected interpreter identity; no fallback accepted. Stopped.')
    result['fixtureAfter']=fixture_snapshot()
    result['fixtureUnchanged']=result['fixtureAfter']==before
    result['endedUtc']=datetime.datetime.now(datetime.timezone.utc).isoformat()
    result['summary']={}
    for mode in ['DeterministicParser','DeepSeek']:
        rows=[a for a in result['attempts'] if a['requestedInterpreter']==mode]
        result['summary'][mode]={}
        for rubric in ['strict','catalogEquivalent']:
            result['summary'][mode][rubric]={key:sum(r[rubric][key] for r in rows)
                for key in ['statusCorrect','clarificationFieldsCorrect','dispositionCorrect','assertions','assertionsCorrect','allDeclaredCorrect']}
    comparisons=[]
    for case in cases:
        p,d=[next(a for a in result['attempts'] if a['caseId']==case['caseId'] and a['requestedInterpreter']==m) for m in ['DeterministicParser','DeepSeek']]
        strict=p['plan']==d['plan']; equivalent=catalog_fold(p['plan'])==catalog_fold(d['plan'])
        envelope=(p['status']==d['status'] and sorted(p['clarificationFields'])==sorted(d['clarificationFields']) and p['planDisposition']==d['planDisposition'])
        comparisons.append(dict(caseId=case['caseId'],expectedValid=case['expectedStatus']=='Valid',bothPlansPresent=p['plan'] is not None and d['plan'] is not None,
            fullStrictPlanAgreement=strict,fullCatalogEquivalentPlanAgreement=equivalent,
            fullStrictResponseAgreement=strict and envelope,fullCatalogEquivalentResponseAgreement=equivalent and envelope))
    result['pairedFullPlanComparisons']=comparisons
    result['fullStrictGoldAccuracy']=None
    result['fullGoldLimitation']='Frozen expectations contain partial assertions, not full gold plans. No new gold labels introduced.'
    output.write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(dict(summary=result['summary'],fixtureUnchanged=result['fixtureUnchanged']),indent=2))


if __name__=='__main__':
    main()
