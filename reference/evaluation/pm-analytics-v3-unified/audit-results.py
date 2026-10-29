import json,copy,collections,hashlib
from pathlib import Path
base=Path('reference/evaluation/pm-analytics-v3-unified')
cases=list(map(json.loads,(base/'cases.jsonl').read_text().splitlines()))
r=json.load(open(base/'results.json'))
assert len(r['attempts'])==60
assert len({(a['caseId'],a['requestedInterpreter']) for a in r['attempts']})==60
known={s.lower():s for s in 'Subject Categories Measures GroupBy Period Filters Top Department Building Location'.split()}
def canonical(x):
 p=copy.deepcopy(x)
 if p is None:return None
 if p['period']=={'basis':'pmCycle','kind':'Year','year':None,'month':None,'quarter':None,'startCycle':None,'endCycle':None}:p['period']=None
 for prop in ('categories','measures','columns'):p[prop]=sorted(p[prop])
 if p['mode']=='Aggregate' and not p['groupBy'] and p['presentation'] in ('Summary','Table'):p['presentation']='Summary'
 for f in p['baseFilters']:
  allowed={'building':'NORTH BLOCK','department':'SYNTHETIC DEPARTMENT'}.get(f['field'])
  if allowed:f['values']=[v.strip().upper() if isinstance(v,str) and v.strip().upper()==allowed else v for v in f['values']]
 return p
for a in r['attempts']:
 c=next(c for c in cases if c['caseId']==a['caseId']);v=a['finalBackendResponse'];s=a['score'];ok=a['httpStatus']==200
 assert a['resolvedInterpreter'] in [a['requestedInterpreter'],'SafetyRules']
 disp='None' if v.get('plan') is None else {'Valid':'Executable','NeedsClarification':'ClarificationDraft'}.get(v.get('status'),'Invalid')
 exact_fields=sorted(v.get('clarificationFields',[]))==sorted(c['expectedClarificationFields'])
 eqfields=lambda fs:sorted(known.get(x.lower(),x) for x in fs)
 semfields=eqfields(v.get('clarificationFields',[]))==eqfields(c['expectedClarificationFields'])
 status=ok and v.get('status')==c['expectedStatus'];d=disp==c['expectedPlanDisposition']
 strict=v.get('plan')==c['expectedPlan'];semantic=canonical(v.get('plan'))==canonical(c['expectedPlan'])
 expected=dict(statusCorrect=status,clarificationFieldsStrictCorrect=ok and exact_fields,clarificationFieldsSemanticCorrect=ok and semfields,dispositionCorrect=ok and d,fullStrictPlanCorrect=ok and strict,fullSemanticPlanCorrect=ok and semantic,strictCaseCorrect=bool(status and exact_fields and d and strict),semanticCaseCorrect=bool(status and semfields and d and semantic),clarificationAppropriate=bool(status and c['expectedStatus']=='NeedsClarification' and semfields and disp!='Executable'))
 assert all(s[k]==v for k,v in expected.items()),a['caseId']
for c in cases:
 if c['kind']!='Valid':continue
 e=canonical(c['expectedPlan'])
 altered=copy.deepcopy(c['expectedPlan']);altered['period']['year']+=1
 assert canonical(altered)!=e
 altered=copy.deepcopy(c['expectedPlan']);altered['columns'].append('scheduleId')
 assert canonical(altered)!=e
 altered=copy.deepcopy(c['expectedPlan']);altered['measures'].append('unrequestedMeasure')
 assert canonical(altered)!=e
assert r['fixtureBefore']==r['fixtureAfter']
assert hashlib.sha256((base/'cases.jsonl').read_bytes().replace(b'\r\n',b'\n')).hexdigest()==r['corpusSha256']
print('Independent offline audit passed: 60 unique paired attempts, all scoring booleans, fixture equality, corpus hash, scope/column/measure mutation rejection. No endpoint calls.')
