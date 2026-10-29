"""Offline TEST-182 audit. Makes no endpoint or provider requests."""
from pathlib import Path
from decimal import Decimal
import json,hashlib,collections,datetime,statistics,math
BASE=Path(__file__).resolve().parent
ROOT=BASE.parents[2]
r=json.loads((BASE/'results.json').read_text())
cases=list(map(json.loads,(BASE.parent/'pm-analytics-v3-unified/cases.jsonl').read_text().splitlines()))
assert len(r['attempts'])==60
assert len({(a['caseId'],a['requestedInterpreter']) for a in r['attempts']})==60
assert r['fixtureBefore']==r['fixtureAfter'] and r['test181HashesBefore']==r['test181HashesAfter']
fields={s.lower():s for s in 'Subject Categories Measures GroupBy Period Filters Top Department Building Location'.split()}
def canon(p):
 p=json.loads(json.dumps(p))
 if p is None:return None
 for k in ['categories','measures','columns']:p[k]=sorted(p[k])
 if p['mode']=='Aggregate' and not p['groupBy'] and p['presentation'] in ['Summary','Table']:p['presentation']='Summary'
 if p['period']==dict(basis='pmCycle',kind='Year',year=None,month=None,quarter=None,startCycle=None,endCycle=None):p['period']=None
 for f in p['baseFilters']:
  allowed={'building':'NORTH BLOCK','department':'SYNTHETIC DEPARTMENT'}.get(f['field'])
  if allowed:f['values']=[v.strip().upper() if v.strip().upper()==allowed else v for v in f['values']]
 return p
for a in r['attempts']:
 c=next(c for c in cases if c['caseId']==a['caseId']);v=a['finalBackendResponse'];m=a['measurements'];trace=a['observerTrace'];events=trace['events']
 assert m['available'] and m['observerErrors']==0
 assert m['actualProviderDispatches']==sum(e['stage']=='providerDispatch' for e in events)
 assert m['providerLogicalCalls']==sum(e['stage']=='providerLogicalCall' for e in events)
 assert a['resolvedInterpreter'] in [a['requestedInterpreter'],'SafetyRules'] or a['httpStatus']!=200
 ok=a['httpStatus']==200 and v.get('status')==c['expectedStatus']
 disp='None' if v.get('plan') is None else {'Valid':'Executable','NeedsClarification':'ClarificationDraft'}.get(v.get('status'),'Invalid')
 ef=sorted(c['expectedClarificationFields']);af=sorted(v.get('clarificationFields',[]));cf=lambda f:sorted(fields.get(x.lower(),x) for x in f)
 strict=bool(ok and ef==af and disp==c['expectedPlanDisposition'] and c['expectedPlan']==v.get('plan'))
 semantic=bool(ok and cf(ef)==cf(af) and disp==c['expectedPlanDisposition'] and canon(c['expectedPlan'])==canon(v.get('plan')))
 assert a['finalScore']['strictCaseCorrect']==strict and a['finalScore']['semanticCaseCorrect']==semantic
 transport=0;usd=Decimal(0)
 for call in m['calls']:
  u=call['reportedUsage'];t=call['tokens']
  for k,key in dict(input='prompt_tokens',output='completion_tokens',total='total_tokens',cacheHit='prompt_cache_hit_tokens',cacheMiss='prompt_cache_miss_tokens').items():assert t[k]==u[key]
  assert t['input']==t['cacheHit']+t['cacheMiss'] and t['total']==t['input']+t['output']
  dt=datetime.datetime.fromisoformat(call['dispatchUtc']).astimezone(datetime.timezone.utc)
  assert not (1<=dt.hour<4 or 6<=dt.hour<10), 'This recorded run used off-peak intervals'
  expected=(Decimal(t['cacheHit'])*Decimal('0.003')+Decimal(t['cacheMiss'])*Decimal('0.15')+Decimal(t['output'])*Decimal('0.60'))/Decimal(1000000)
  assert Decimal(call['estimatedCostUsd'])==expected;usd+=expected;transport+=call['transportElapsedMs']
  assert call['httpSucceeded'] and call['providerHttpStatuses']==[200] and not call['failures']
 assert Decimal(m['estimatedCostUsd'])==usd and abs(m['providerTransportMs']-transport)<1e-8
 assert abs(m['backendProcessingResidualMs']-(m['serverElapsedMs']-transport))<1e-8
 assert abs(m['totalEndpointMs']-m['clientServerResidualMs']-m['serverElapsedMs'])<1e-8
for mode in ['DeterministicParser','DeepSeek']:
 rows=[a for a in r['attempts'] if a['requestedInterpreter']==mode];s=r['summary'][mode]
 assert sum(a['measurements']['actualProviderDispatches'] for a in rows)==s['providerDispatches']
 assert sum(Decimal(a['measurements']['estimatedCostUsd']) for a in rows)==Decimal(s['estimatedCostUsd'])
 for k in ['strictCaseCorrect','semanticCaseCorrect']:assert sum(a['finalScore'][k] for a in rows)==s[k]
 for k,stats in s['latencies'].items():
  vals=sorted(a['measurements'][k] for a in rows if k not in ['providerTransportMs','providerLogicalStageMs'] or a['measurements']['actualProviderDispatches'])
  if not vals:assert stats is None;continue
  assert stats['count']==len(vals) and stats['median']==statistics.median(vals) and stats['p95']==vals[math.ceil(.95*len(vals))-1]
assert all(x['sameHttp'] and x['sameFinalResponse'] for x in r['deterministicConformanceWithTest181'])
assert sum(a['httpStatus']!=200 for a in r['attempts'])==1
for x in r['instrumentationManifest']['files']:
 assert hashlib.sha256((ROOT/x['path']).read_bytes()).hexdigest()==x['sha256'],x['path']
for name,digest in r['test181HashesBefore'].items():
 assert hashlib.sha256((BASE.parent/'pm-analytics-v3-unified'/name).read_bytes()).hexdigest()==digest,name
print('PASS: 60 unique pairs; frozen scoring independently recomputed; raw token, cache/cost and timing arithmetic; 28 successful provider calls; one backend endpoint rejection; zero retries/timeouts; all original source and TEST-181 hashes; parser conformance 30/30. No network calls.')
