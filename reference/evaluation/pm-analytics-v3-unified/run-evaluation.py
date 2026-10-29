"""TEST-181: fixed 30-case paired endpoint evaluation; no report execution."""
import collections
import datetime
import hashlib
import importlib.util
import json
import math
import os
from pathlib import Path
import statistics
import subprocess
import time

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
ART = ROOT / 'artifacts/test-181'
spec = importlib.util.spec_from_file_location('http_adapter', HERE.parent/'pm-analytics-v3-paraphrases/run-comparison.py')
http_adapter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(http_adapter)
MODES = ['DeterministicParser','DeepSeek']
PLAN_KEYS = set('schemaVersion catalogVersion subject mode period categories measures groupBy baseFilters matchingFilters aggregateFilters orderBy top presentation columns'.split())
FIELD_NAMES = 'Subject Categories Measures GroupBy Period Filters Top Department Building Location'.split()


def sha(path):
    return hashlib.sha256(path.read_bytes().replace(b'\r\n',b'\n')).hexdigest()


def semantic(plan):
    p = json.loads(json.dumps(plan))
    if p is None:
        return None
    for f in p.get('baseFilters',[]):
        known = {'building':'NORTH BLOCK','department':'SYNTHETIC DEPARTMENT'}.get(f.get('field'))
        if known:
            f['values'] = [v.strip().upper() if isinstance(v,str) and v.strip().upper()==known else v for v in f.get('values',[])]
    for key in ['categories','measures','columns']:
        if isinstance(p.get(key),list):
            p[key] = sorted(p[key])
    if p.get('mode')=='Aggregate' and p.get('groupBy')==[] and p.get('presentation') in ['Summary','Table']:
        p['presentation']='Summary'
    period=p.get('period')
    if period==dict(basis='pmCycle',kind='Year',year=None,month=None,quarter=None,startCycle=None,endCycle=None):
        p['period']=None
    return p


def fields(values,equivalent=False):
    mapping = {x.lower():x for x in FIELD_NAMES}
    return sorted(mapping.get(x.lower(),x) if equivalent else x for x in values)


def disposition(response):
    if response.get('plan') is None:
        return 'None'
    return {'Valid':'Executable','NeedsClarification':'ClarificationDraft'}.get(response.get('status'),'Invalid')


def differences(expected,actual,path='plan'):
    if isinstance(expected,dict) and isinstance(actual,dict):
        return [d for k in sorted(expected.keys()|actual.keys()) for d in differences(expected.get(k),actual.get(k),path+'.'+k)]
    if expected==actual:
        return []
    return [dict(field=path,expected=expected,actual=actual)]


def score(case,status,response):
    ok=status==200
    status_ok=ok and response.get('status')==case['expectedStatus']
    actual_fields=response.get('clarificationFields',[])
    strict_fields=fields(actual_fields)==fields(case['expectedClarificationFields'])
    equivalent_fields=fields(actual_fields,True)==fields(case['expectedClarificationFields'],True)
    disposition_ok=disposition(response)==case['expectedPlanDisposition']
    expected,actual=case['expectedPlan'],response.get('plan')
    exact=expected==actual
    equiv=semantic(expected)==semantic(actual)
    return dict(statusCorrect=status_ok,clarificationFieldsStrictCorrect=ok and strict_fields,
        clarificationFieldsSemanticCorrect=ok and equivalent_fields,dispositionCorrect=ok and disposition_ok,
        fullStrictPlanCorrect=ok and exact,fullSemanticPlanCorrect=ok and equiv,
        strictCaseCorrect=bool(status_ok and strict_fields and disposition_ok and exact),
        semanticCaseCorrect=bool(status_ok and equivalent_fields and disposition_ok and equiv),
        clarificationAppropriate=bool(status_ok and case['expectedStatus']=='NeedsClarification' and equivalent_fields and disposition(response)!='Executable'),
        strictPlanDifferences=differences(expected,actual),semanticPlanDifferences=differences(semantic(expected),semantic(actual)))


def load_cases():
    manifest=json.loads((HERE/'manifest.json').read_text())
    assert sha(HERE/'cases.jsonl')==manifest['corpusSha256']
    cases=list(map(json.loads,(HERE/'cases.jsonl').read_text().splitlines()))
    assert len(cases)==30 and len({c['caseId'] for c in cases})==30
    assert collections.Counter(c['kind'] for c in cases)==dict(Valid=16,NeedsClarification=9,Unsupported=5)
    assert collections.Counter(c['subset'] for c in cases)==dict(original=18,additional=12)
    original_path=ROOT/'reference/evaluation/pm-analytics-v3/dev-candidates.jsonl'
    assert sha(original_path)==manifest['originalCorpusSha256']
    original=list(map(json.loads,original_path.read_text().splitlines()))
    def contains(e,a):
        if e is None:return True
        if isinstance(e,dict):return isinstance(a,dict) and all(contains(v,a.get(k)) for k,v in e.items())
        return e==a
    for c,o in zip(cases,original):
        for key in ['caseId','question','expectedStatus','expectedClarificationFields','expectedPlanDisposition']:
            assert c[key]==o[key],(c['caseId'],key)
        assert contains(o['expectedPlanAssertions'],c['expectedPlan'])
    for c in cases:
        if c['expectedPlan'] is not None:
            assert set(c['expectedPlan'])==PLAN_KEYS
        if c['subset']=='additional' and c['expectedStatus']=='Valid':
            parent=next(x for x in cases if x['caseId']==c['sourceCaseId'])
            assert parent['expectedPlan']==c['expectedPlan']
    return cases


def snapshot():
    database=(ART/'database.txt').read_text().strip()
    assert database.startswith('UniPMDb_Test181_') and database[len('UniPMDb_Test181_'):].isalnum()
    query="SET NOCOUNT ON; SELECT Id,AssetCode,AssetCategory,Building,Department,Location,Status,CreatedAt,UpdatedAt FROM Assets ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES; SELECT (SELECT COUNT(*) FROM PreventiveMaintenanceSchedules) AS schedules,(SELECT COUNT(*) FROM InspectionRecords) AS inspections,(SELECT COUNT(*) FROM PreventiveMaintenanceForms) AS forms FOR JSON PATH; SELECT * FROM PreventiveMaintenanceSchedules ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES; SELECT CAST(SERVERPROPERTY('ProductVersion') AS varchar(50)) AS engineVersion,compatibility_level FROM sys.databases WHERE name=DB_NAME() FOR JSON PATH;"
    raw=subprocess.check_output(['sqlcmd','-S','localhost','-E','-d',database,'-b','-w','65535','-y','0','-Q',query],text=True)
    lines=[json.loads(l) for l in raw.splitlines() if l.strip().startswith('[')]
    return dict(catalog=lines[0],counts=lines[1],schedules=lines[2],sqlServer=lines[3],sha256=hashlib.sha256(raw.encode()).hexdigest())


def summarize(result,cases):
    result['summary']={}
    for mode in MODES:
        all_rows=[a for a in result['attempts'] if a['requestedInterpreter']==mode]
        result['summary'][mode]={}
        for subset in ['original','additional','overall','Valid','NeedsClarification','Unsupported']:
            ids={c['caseId'] for c in cases if subset=='overall' or c['subset']==subset or c['kind']==subset}
            rows=[a for a in all_rows if a['caseId'] in ids]
            times=sorted(a['elapsedMs'] for a in rows)
            result['summary'][mode][subset]=dict(count=len(rows),**{key:sum(a['score'][key] for a in rows) for key in ['statusCorrect','clarificationFieldsStrictCorrect','clarificationFieldsSemanticCorrect','dispositionCorrect','fullStrictPlanCorrect','fullSemanticPlanCorrect','strictCaseCorrect','semanticCaseCorrect','clarificationAppropriate']},httpFailures=sum(a['httpStatus']!=200 for a in rows),safetyRulesResponses=sum(a['resolvedInterpreter']=='SafetyRules' for a in rows),medianMs=statistics.median(times),p95Ms=times[math.ceil(.95*len(times))-1])
    result['pairedValid']={k:[] for k in ['bothCorrect','deepSeekOnly','parserOnly','neitherCorrect']}
    for c in cases:
        if c['kind']!='Valid':continue
        p,d=[next(a for a in result['attempts'] if a['caseId']==c['caseId'] and a['requestedInterpreter']==m)['score']['semanticCaseCorrect'] for m in MODES]
        key='bothCorrect' if p and d else 'deepSeekOnly' if d else 'parserOnly' if p else 'neitherCorrect'
        result['pairedValid'][key].append(c['caseId'])


def main():
    cases=load_cases()
    before=snapshot()
    assert len(before['catalog'])==1 and before['catalog'][0]['Building']=='North Block' and before['catalog'][0]['Department']=='Synthetic Department'
    assert before['counts']==[dict(schedules=1,inspections=0,forms=0)]
    status,session=http_adapter.post('/api/v1/auth/login',dict(email='gsd@unipm.local',password=os.environ['UNIPM_DEV_USER_PASSWORD']))
    assert status==200,'Synthetic authentication failed before cases.'
    source=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
    production=hashlib.sha256()
    for p in sorted((ROOT/'server').rglob('*.cs')):
        if 'obj' in p.parts or 'bin' in p.parts:continue
        production.update(str(p.relative_to(ROOT)).replace(chr(92),'/').encode());production.update(p.read_bytes().replace(b'\r\n',b'\n'))
    result=dict(evidenceRecord='TEST-181',sourceCommit=source,corpusSha256=sha(HERE/'cases.jsonl'),protocolSha256=sha(HERE/'protocol.md'),runnerSha256=sha(Path(__file__)),productionSourceSha256=production.hexdigest(),backendAssemblySha256=hashlib.sha256((ROOT/'server/bin/Debug/net10.0/UniPM.Api.dll').read_bytes()).hexdigest(),interpreterSourceSha256=sha(ROOT/'server/Features/Reports/PmAnalyticsV3QuestionInterpreter.cs'),startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),clock='Shared live clock; all question dates explicit.',model='Configured deepseek-flash, temperature zero; provider revision unavailable through endpoint.',rawPreNormalizationAvailable=False,fixtureBefore=before,attempts=[])
    output=ART/'results.json'
    output.write_text(json.dumps(result,indent=2)+'\n')
    for i,c in enumerate(cases):
        for mode in (MODES if i%2==0 else list(reversed(MODES))):
            start=time.perf_counter()
            http,response=http_adapter.post('/api/v1/analytics/pm/v3/interpret',dict(question=c['question'],interpreter=mode),session['accessToken'])
            code=response.get('code') or ''
            classification='http-failure-origin-unresolved' if http!=200 else 'catalog-resolution' if code in ['UnknownCatalogValue','AmbiguousCatalogValue'] else 'backend-constraint-reported' if code in ['QuestionConstraintNeedsClarification','QuestionConstraintNotSupported','NegatedOutcomeNeedsClarification','UnsafeRequestNotSupported'] else 'origin-unresolved' if response.get('status')!=c['expectedStatus'] else 'no-origin-attribution'
            a=dict(caseId=c['caseId'],subset=c['subset'],kind=c['kind'],question=c['question'],sourceCommit=source,requestedInterpreter=mode,resolvedInterpreter=response.get('interpreter'),httpStatus=http,elapsedMs=round((time.perf_counter()-start)*1000,3),finalBackendResponse=response,rawPreNormalizationAvailable=False,observedClassification=classification,score=score(c,http,response))
            result['attempts'].append(a)
            output.write_text(json.dumps(result,indent=2)+'\n')
            print(c['caseId'],mode,http,response.get('status',code),'semantic='+str(a['score']['semanticCaseCorrect']),flush=True)
            if http==200 and a['resolvedInterpreter'] not in [mode,'SafetyRules']:
                raise SystemExit('Unexpected identity: no fallback accepted; stopped.')
    result['endedUtc']=datetime.datetime.now(datetime.timezone.utc).isoformat()
    result['fixtureAfter']=snapshot();result['fixtureUnchanged']=before==result['fixtureAfter']
    summarize(result,cases)
    output.write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(dict(summary=result['summary'],pairedValid=result['pairedValid'],fixtureUnchanged=result['fixtureUnchanged']),indent=2))


if __name__=='__main__':
    main()
