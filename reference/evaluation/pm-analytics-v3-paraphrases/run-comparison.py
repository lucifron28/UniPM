"""Bounded synthetic comparison through the same loopback backend for both modes."""
import datetime
import hashlib
import json
import os
import pathlib
import statistics
import subprocess
import time
import urllib.error
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[3]
DIRECTORY = pathlib.Path(__file__).resolve().parent
BASE = 'http://localhost:5262'
MODES = ['DeterministicParser', 'DeepSeek']


def post(path, data, token=None):
    headers = {'Content-Type': 'application/json'}
    if token:
        headers['Authorization'] = 'Bearer ' + token
    request = urllib.request.Request(BASE + path, json.dumps(data).encode(), headers)
    try:
        with urllib.request.urlopen(request, timeout=75) as response:
            return response.status, json.load(response)
    except urllib.error.HTTPError as error:
        return error.code, json.load(error)
    except (TimeoutError, urllib.error.URLError):
        return 0, {'code': 'TransportFailure'}


def canonical(plan, semantic=False):
    if plan is None:
        return None
    value = json.loads(json.dumps(plan))
    period = value.get('period')
    if period:
        value['period'] = {k: v for k, v in period.items() if v is not None}
    value.setdefault('top', None)
    if semantic:
        # These cases do not request a particular presentation or column order.
        value.pop('presentation', None)
        for key in ['categories', 'measures', 'columns']:
            value[key] = sorted(value.get(key, []))
    return value


def grade(case, status, response):
    mode = response.get('interpreter')
    actual_status = response.get('status')
    status_correct = status == 200 and actual_status == case['expectedStatus']
    if case['expectedStatus'] == 'Valid':
        actual = response.get('plan')
        exact = status_correct and canonical(actual) == canonical(case['expectedPlan'])
        presentation_ok = actual is not None and (
            actual.get('presentation') == 'Table' or
            (not case['expectedPlan']['groupBy'] and actual.get('presentation') == 'Summary'))
        correct = bool(status_correct and presentation_ok and
                       canonical(actual, True) == canonical(case['expectedPlan'], True))
    else:
        exact = None
        correct = bool(status_correct and (
            response.get('plan') is None if actual_status == 'Unsupported'
            else bool(response.get('clarificationFields'))))
        if case['caseId'] == 'V3P-S02':
            correct = correct and 'Period' in response.get('clarificationFields', [])
    return dict(statusCorrect=status_correct, strictPlanCorrect=exact,
                semanticCorrect=correct, resolvedInterpreter=mode)


def main():
    payload = (DIRECTORY / 'cases.jsonl').read_bytes().replace(b'\r\n', b'\n')
    manifest = json.loads((DIRECTORY / 'manifest.json').read_text())
    assert hashlib.sha256(payload).hexdigest() == manifest['sha256']
    cases = [json.loads(line) for line in payload.splitlines()]
    assert len(cases) == 30
    password = os.environ['UNIPM_DEV_USER_PASSWORD']
    status, session = post('/api/v1/auth/login', dict(email='gsd@unipm.local', password=password))
    if status != 200:
        raise SystemExit('Synthetic login failed; no interpretation requests made.')
    token = session['accessToken']
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')
    path = ROOT / 'artifacts' / ('nla-paraphrase-comparison-' + stamp + '.json')
    result = dict(sourceCommit=subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
                  corpusHash=manifest['sha256'], startedUtc=stamp, attempts=[],
                  clock='Live server UTC clock, explicit 2025 periods; no relative-date cases.',
                  providerLogicalLimit=30, providerPhysicalUpperBound=90,
                  providerUsage='Unavailable through this endpoint; no cost estimate.',
                  model='deepseek-flash, temperature 0, configured backend default',
                  scoring='scoring-rules.md; exact and predeclared layout-equivalent scores separate')
    # Persist only selected safe metadata, never tokens or provider payloads.
    for index, case in enumerate(cases):
        order = MODES if index % 2 == 0 else list(reversed(MODES))
        for mode in order:
            start = time.perf_counter()
            status, response = post('/api/v1/analytics/pm/v3/interpret',
                                    dict(question=case['question'], interpreter=mode), token)
            elapsed = (time.perf_counter() - start) * 1000
            score = grade(case, status, response)
            if status == 200 and score['resolvedInterpreter'] not in [mode, 'SafetyRules']:
                raise SystemExit('Unexpected interpreter identity; stopping without additional calls.')
            result['attempts'].append(dict(caseId=case['caseId'], requestedInterpreter=mode,
                httpStatus=status, status=response.get('status'), code=response.get('code'),
                clarificationFields=response.get('clarificationFields', []), plan=response.get('plan'),
                elapsedMs=round(elapsed, 2), **score))
            path.write_text(json.dumps(result, indent=2) + '\n')
            print(case['caseId'], mode, status, response.get('status', response.get('code')),
                  'correct=' + str(score['semanticCorrect']), flush=True)
    supported = {c['caseId'] for c in cases if c['expectedStatus'] == 'Valid'}
    result['summary'] = {}
    for mode in MODES:
        attempts = [a for a in result['attempts'] if a['requestedInterpreter'] == mode]
        valid = [a for a in attempts if a['caseId'] in supported]
        controls = [a for a in attempts if a['caseId'] not in supported]
        latencies = sorted(a['elapsedMs'] for a in attempts)
        result['summary'][mode] = dict(supportedSemanticCorrect=sum(a['semanticCorrect'] for a in valid),
            supportedStrictPlanCorrect=sum(a['strictPlanCorrect'] for a in valid), supportedCount=len(valid),
            controlsCorrect=sum(a['semanticCorrect'] for a in controls), controlsCount=len(controls),
            httpFailures=sum(a['httpStatus'] != 200 for a in attempts),
            safetyRulesResponses=sum(a['resolvedInterpreter'] == 'SafetyRules' for a in attempts),
            medianMs=statistics.median(latencies), p95Ms=latencies[int(0.95 * len(latencies) + 0.999) - 1])
    pairs = {name: [] for name in ['bothCorrect','deepSeekOnly','parserOnly','neitherCorrect']}
    for case_id in sorted(supported):
        values = {a['requestedInterpreter']: a['semanticCorrect'] for a in result['attempts'] if a['caseId'] == case_id}
        key = ('bothCorrect' if all(values.values()) else 'deepSeekOnly' if values['DeepSeek']
               else 'parserOnly' if values['DeterministicParser'] else 'neitherCorrect')
        pairs[key].append(case_id)
    result['paired'] = pairs
    path.write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps(dict(summary=result['summary'], paired=pairs, artifact=str(path)), indent=2))


if __name__ == '__main__':
    main()
