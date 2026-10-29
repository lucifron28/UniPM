"""TEST-182: 60 once-only endpoint requests with observer measurements."""
import collections
import datetime
from decimal import Decimal
import hashlib
import importlib.util
import json
import math
import os
from pathlib import Path
import statistics
import subprocess
import time
import urllib.request
import urllib.error

ROOT=Path(__file__).resolve().parents[3]
HERE=Path(__file__).resolve().parent
ART=ROOT/'artifacts/test-182'
FROZEN=HERE.parent/'pm-analytics-v3-unified'
spec=importlib.util.spec_from_file_location('frozen181',FROZEN/'run-evaluation.py')
frozen=importlib.util.module_from_spec(spec);spec.loader.exec_module(frozen)
MODES=['DeterministicParser','DeepSeek']
TOKEN_FIELDS=dict(input='prompt_tokens',output='completion_tokens',total='total_tokens',cacheHit='prompt_cache_hit_tokens',cacheMiss='prompt_cache_miss_tokens')


def hashes():
    return {p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(FROZEN.iterdir()) if p.is_file()}


def post(path,body,token=None,request_id=None):
    headers={'Content-Type':'application/json'}
    if token:headers['Authorization']='Bearer '+token
    if request_id:headers['X-Test182-Request']=request_id
    request=urllib.request.Request('http://localhost:5263'+path,json.dumps(body).encode(),headers)
    try:
        with urllib.request.urlopen(request,timeout=75) as response:return response.status,json.load(response)
    except urllib.error.HTTPError as error:
        try:return error.code,json.load(error)
        except (ValueError,UnicodeError):return error.code,dict(code='NonJsonEndpointFailure')
    except (TimeoutError,urllib.error.URLError):return 0,dict(code='TransportFailure')


def snapshot():
    database=(ART/'database.txt').read_text().strip()
    assert database.startswith('UniPMDb_Test182_') and database[len('UniPMDb_Test182_'):].isalnum()
    sql="SET NOCOUNT ON; SELECT Id,AssetCode,AssetCategory,Building,Department,Location,Status,CreatedAt,UpdatedAt FROM Assets ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES; SELECT (SELECT COUNT(*) FROM PreventiveMaintenanceSchedules) AS schedules,(SELECT COUNT(*) FROM InspectionRecords) AS inspections,(SELECT COUNT(*) FROM PreventiveMaintenanceForms) AS forms FOR JSON PATH; SELECT * FROM PreventiveMaintenanceSchedules ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES; SELECT CAST(SERVERPROPERTY('ProductVersion') AS varchar(50)) AS engineVersion,compatibility_level FROM sys.databases WHERE name=DB_NAME() FOR JSON PATH;"
    raw=subprocess.check_output(['sqlcmd','-S','localhost','-E','-d',database,'-b','-w','65535','-y','0','-Q',sql],text=True)
    items=[json.loads(line) for line in raw.splitlines() if line.strip().startswith('[')]
    return dict(catalog=items[0],counts=items[1],schedules=items[2],sqlServer=items[3],sha256=hashlib.sha256(raw.encode()).hexdigest())


def cost(usage,dispatch,pricing):
    tokens={k:usage.get(v) if isinstance(usage,dict) else None for k,v in TOKEN_FIELDS.items()}
    numbers=all(isinstance(v,int) and not isinstance(v,bool) and v>=0 for v in tokens.values())
    consistent=bool(numbers and tokens['input']==tokens['cacheHit']+tokens['cacheMiss'] and tokens['total']==tokens['input']+tokens['output'])
    if not consistent:return dict(tokens=tokens,usageConsistent=False,estimatedCostUsd=None,costRangeUsd=None,reason='Missing or inconsistent reported usage')
    utc=datetime.datetime.fromisoformat(dispatch['utc']).astimezone(datetime.timezone.utc)
    hour=utc.hour+utc.minute/60+utc.second/3600
    peak_window=utc.weekday()<5 and any(lo<=hour<hi for lo,hi in pricing['peakUtcIntervals'])
    def amount(rates):
        return str((Decimal(tokens['cacheHit'])*Decimal(rates['cacheHit'])+Decimal(tokens['cacheMiss'])*Decimal(rates['cacheMiss'])+Decimal(tokens['output'])*Decimal(rates['output']))/Decimal(1000000))
    if dispatch.get('model')!='deepseek-flash':return dict(tokens=tokens,usageConsistent=True,estimatedCostUsd=None,costRangeUsd=None,reason='No documented price for requested alias')
    if peak_window:return dict(tokens=tokens,usageConsistent=True,estimatedCostUsd=None,costRangeUsd=[amount(pricing['offPeak']),amount(pricing['peak'])],reason='Weekday peak window; Chinese public holiday status not independently checked')
    return dict(tokens=tokens,usageConsistent=True,estimatedCostUsd=amount(pricing['offPeak']),costRangeUsd=None,rateTier='offPeak',ratesPerMillion=pricing['offPeak'],reason='Observed UTC dispatch outside documented peak windows; invoice unavailable')


def measurements(trace,endpoint_ms,pricing):
    if trace is None:return dict(available=False,reason='Observer file unavailable; no calls or tokens inferred')
    events=trace['events']
    select=lambda name:[e['value'] for e in events if e['stage']==name]
    positions=[i for i,e in enumerate(events) if e['stage']=='providerDispatch']
    calls=[]
    for n,start in enumerate(positions):
        dispatch=events[start]['value'];end=positions[n+1] if n+1<len(positions) else len(events)
        part=events[start:end]
        by=lambda name:[e['value'] for e in part if e['stage']==name]
        metadata=by('providerReportedMetadata')
        replies=by('providerHttpSuccess');failures=by('providerFailure')
        meta=metadata[0] if len(metadata)==1 else {}
        ledger=replies+failures
        usage=meta.get('usage')
        calls.append(dict(attemptNumber=dispatch['attemptNumber'],isRetry=dispatch['attemptNumber']>1,dispatchUtc=dispatch['utc'],requestedModel=dispatch['model'],reportedModel=meta.get('model'),systemFingerprint=meta.get('systemFingerprint'),providerCreated=meta.get('created'),reportedUsage=usage,providerHttpStatuses=[l.get('httpStatusCode') for l in ledger],transportElapsedMs=sum(l['latencyMilliseconds'] for l in ledger) if ledger else None,httpSucceeded=bool(replies),failures=failures,**cost(usage,dispatch,pricing)))
    durations=[c['transportElapsedMs'] for c in calls]
    provider_ms=sum(durations) if all(v is not None for v in durations) else None
    logical=select('providerLogicalStage')
    logical_ms=sum(x['elapsedMs'] for x in logical) if calls else 0
    if calls and not logical:logical_ms=None
    server=trace['serverElapsedMs']
    tokens={k:sum(c['tokens'][k] for c in calls) if calls and all(c['tokens'][k] is not None for c in calls) else None for k in TOKEN_FIELDS}
    known_cost=sum(Decimal(c['estimatedCostUsd']) for c in calls if c['estimatedCostUsd'] is not None)
    cost_complete=all(c['estimatedCostUsd'] is not None for c in calls)
    return dict(available=True,observerErrors=trace['observerErrors'],providerLogicalCalls=len(select('providerLogicalCall')),actualProviderDispatches=len(calls),providerRetries=sum(c['isRetry'] for c in calls),providerHttpFailures=sum(any(s is not None and s>=400 for s in c['providerHttpStatuses']) for c in calls),providerFailureEvents=select('providerFailure'),providerTimeoutEvents=[e for e in select('providerFailure')+select('providerTerminalFailure') if 'Timeout' in e.get('safeCode','')],providerTerminalFailures=select('providerTerminalFailure'),calls=calls,reportedTokens=tokens,tokenApplicability='provider-reported' if calls else 'not-applicable; observed no provider dispatch',estimatedCostUsd=str(known_cost) if cost_complete else None,knownCostSubtotalUsd=str(known_cost),costComplete=cost_complete,providerTransportMs=provider_ms,providerLogicalStageMs=logical_ms,serverElapsedMs=server,backendProcessingResidualMs=server-provider_ms if provider_ms is not None else None,backendExcludingProviderLogicalMs=server-logical_ms if logical_ms is not None else None,totalEndpointMs=endpoint_ms,clientServerResidualMs=endpoint_ms-server)


def proposal(trace):
    if trace is None:return dict(rawProviderText=None,rawProviderJson=None,parsedInterpreterProposal=None,reason='Telemetry unavailable',stages=[])
    events=trace['events'];select=lambda name:[e['value'] for e in events if e['stage']==name]
    raw=select('rawProviderProposal');text=raw[-1]['text'] if raw else None
    try:raw_json=json.loads(text) if text is not None else None
    except ValueError:raw_json=None
    parsed=select('parsedInterpreterProposal')
    return dict(rawProviderText=text,rawProviderJson=raw_json,parsedInterpreterProposal=parsed[-1] if parsed else None,reason=None if parsed else 'No typed proposal reached; inspect shared/early guard or provider/output failure',stages=[e for e in events if e['stage'] in ['questionConstraints','schemaValidationPassed','schemaValidationRejected','catalogResolution']])


def summary(result,cases):
    result['summary']={}
    for mode in MODES:
        rows=[a for a in result['attempts'] if a['requestedInterpreter']==mode]
        measured=[a['measurements'] for a in rows if a['measurements']['available']]
        def stats(key,provider_only=False):
            v=sorted(m[key] for m in measured if m.get(key) is not None and (not provider_only or m['actualProviderDispatches']>0))
            return dict(count=len(v),mean=statistics.mean(v),median=statistics.median(v),p95=v[math.ceil(.95*len(v))-1],minimum=min(v),maximum=max(v)) if v else None
        costs=[m['estimatedCostUsd'] for m in measured]
        sums={k:sum(c['tokens'][k] for m in measured for c in m['calls'] if c['tokens'][k] is not None) for k in TOKEN_FIELDS}
        data=dict(count=len(rows),strictCaseCorrect=sum(a['finalScore']['strictCaseCorrect'] for a in rows),semanticCaseCorrect=sum(a['finalScore']['semanticCaseCorrect'] for a in rows),httpFailures=sum(a['httpStatus']!=200 for a in rows),telemetryAvailable=len(measured),observerErrors=sum(m['observerErrors'] for m in measured),providerDispatches=sum(m['actualProviderDispatches'] for m in measured),providerLogicalCalls=sum(m['providerLogicalCalls'] for m in measured),providerRetries=sum(m['providerRetries'] for m in measured),providerHttpFailures=sum(m['providerHttpFailures'] for m in measured),providerFailureEvents=sum(len(m['providerFailureEvents']) for m in measured),timeoutEvents=sum(len(m['providerTimeoutEvents']) for m in measured),reportedTokenTotals=sums,reportedTokenCoverage={k:sum(c['tokens'][k] is not None for m in measured for c in m['calls']) for k in TOKEN_FIELDS},estimatedCostUsd=str(sum(Decimal(v) for v in costs)) if len(measured)==len(rows) and all(v is not None for v in costs) else None,knownCostSubtotalUsd=str(sum(Decimal(m['knownCostSubtotalUsd']) for m in measured)),rawProposalAvailable=sum(a['interpretation']['parsedInterpreterProposal'] is not None for a in rows),rawProviderTextAvailable=sum(a['interpretation']['rawProviderText'] is not None for a in rows),backendChangedProposal=sum(bool(a['backendChanges']) for a in rows),providerModels=sorted({c['reportedModel'] for m in measured for c in m['calls'] if c['reportedModel'] is not None}),latencies={k:stats(k,k in ['providerTransportMs','providerLogicalStageMs']) for k in ['providerTransportMs','providerLogicalStageMs','serverElapsedMs','backendProcessingResidualMs','backendExcludingProviderLogicalMs','totalEndpointMs','clientServerResidualMs']})
        data['subsets']={}
        for subset in ['original','additional','Valid','NeedsClarification','Unsupported']:
            ids={c['caseId'] for c in cases if c['subset']==subset or c['kind']==subset};sub=[a for a in rows if a['caseId'] in ids]
            data['subsets'][subset]=dict(count=len(sub),strict=sum(a['finalScore']['strictCaseCorrect'] for a in sub),semantic=sum(a['finalScore']['semanticCaseCorrect'] for a in sub))
        result['summary'][mode]=data


def main():
    cases=frozen.load_cases();before_hashes=hashes();pricing=json.loads((HERE/'pricing.json').read_text())
    before=snapshot();assert before['counts']==[dict(schedules=1,inspections=0,forms=0)]
    assert len(before['catalog'])==1 and before['catalog'][0]['Building']=='North Block' and before['catalog'][0]['Department']=='Synthetic Department'
    assert not list((ART/'telemetry').glob('*.json')),'Refusing to reuse telemetry from an earlier evaluation'
    status,session=post('/api/v1/auth/login',dict(email='gsd@unipm.local',password=os.environ['UNIPM_DEV_USER_PASSWORD']))
    assert status==200,'Synthetic login failed before cases'
    source=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
    result=dict(evidenceRecord='TEST-182',sourceCommit=source,startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),test181HashesBefore=before_hashes,fixtureBefore=before,pricing=pricing,instrumentationManifest=json.loads((ART/'instrumentation-manifest.json').read_text()),instrumentedAssemblySha256=hashlib.sha256((ART/'runtime/bin/Debug/net10.0/UniPM.Api.dll').read_bytes()).hexdigest(),protocolSha256=hashlib.sha256((HERE/'protocol.md').read_bytes()).hexdigest(),runnerSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),attempts=[])
    output=ART/'results.json'
    output.write_text(json.dumps(result,indent=2)+'\n')
    for i,case in enumerate(cases):
        for mode in (MODES if i%2==0 else list(reversed(MODES))):
            request_id=case['caseId']+'-'+mode
            start=time.perf_counter();http,response=post('/api/v1/analytics/pm/v3/interpret',dict(question=case['question'],interpreter=mode),session['accessToken'],request_id);elapsed=round((time.perf_counter()-start)*1000,3)
            trace_path=ART/'telemetry'/(request_id+'.json');deadline=time.monotonic()+3
            while not trace_path.exists() and time.monotonic()<deadline:time.sleep(.02)
            trace=None
            if trace_path.exists():
                try:trace=json.loads(trace_path.read_text())
                except (ValueError,OSError):pass
            p=proposal(trace);candidate=p['parsedInterpreterProposal']
            view=lambda x:{k:x.get(k) for k in ['status','clarificationFields','plan']}
            changes=frozen.differences(view(candidate),view(response),'proposal') if candidate is not None else None
            validated=bool(response.get('status')=='Valid' and any(e['stage']=='catalogResolution' and e['value'].get('plan') is not None for e in p['stages']))
            row=dict(caseId=case['caseId'],subset=case['subset'],kind=case['kind'],question=case['question'],sourceCommit=source,requestedInterpreter=mode,resolvedInterpreter=response.get('interpreter'),httpStatus=http,finalBackendResponse=response,finalValidatedPlan=response.get('plan') if validated else None,finalPlanIsValidatedExecutable=validated,interpretation=p,backendChanges=changes,measurements=measurements(trace,elapsed,pricing),finalScore=frozen.score(case,http,response),proposalScore=frozen.score(case,200,candidate) if candidate is not None else None,observerTrace=trace)
            result['attempts'].append(row);output.write_text(json.dumps(result,indent=2)+'\n')
            print(case['caseId'],mode,http,response.get('status',response.get('code')),'calls='+str(row['measurements'].get('actualProviderDispatches')),'changed='+str(bool(changes)),flush=True)
            if http==200 and row['resolvedInterpreter'] not in [mode,'SafetyRules']:raise SystemExit('Unexpected identity; fallback not accepted. No reruns.')
    result['endedUtc']=datetime.datetime.now(datetime.timezone.utc).isoformat();result['fixtureAfter']=snapshot();result['fixtureUnchanged']=result['fixtureAfter']==before
    result['test181HashesAfter']=hashes();result['test181Unchanged']=before_hashes==result['test181HashesAfter']
    prior=json.loads((FROZEN/'results.json').read_text());conformance=[]
    for a in result['attempts']:
        if a['requestedInterpreter']!='DeterministicParser':continue
        old=next(x for x in prior['attempts'] if x['caseId']==a['caseId'] and x['requestedInterpreter']=='DeterministicParser')
        conformance.append(dict(caseId=a['caseId'],sameHttp=a['httpStatus']==old['httpStatus'],sameFinalResponse=a['finalBackendResponse']==old['finalBackendResponse']))
    result['deterministicConformanceWithTest181']=conformance
    summary(result,cases);output.write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(dict(summary=result['summary'],fixtureUnchanged=result['fixtureUnchanged'],test181Unchanged=result['test181Unchanged']),indent=2))


if __name__=='__main__':main()
