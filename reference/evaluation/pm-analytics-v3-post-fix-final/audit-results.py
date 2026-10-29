"""Offline audit of TEST-189. No endpoint or provider calls."""
import importlib.util
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('repeat189', HERE / 'run-evaluation.py')
repeat = importlib.util.module_from_spec(spec)
spec.loader.exec_module(repeat)
result = json.loads((HERE / 'results.json').read_text(encoding='utf-8'))
cases = repeat.frozen.load_cases()
assert result['corpusSha256'] == repeat.frozen.sha(repeat.ORIGINAL / 'cases.jsonl')
assert result['runnerSha256'] == repeat.frozen.sha(HERE / 'run-evaluation.py')
assert result['scoringRunnerSha256'] == repeat.frozen.sha(repeat.ORIGINAL / 'run-evaluation.py')
assert result['protocolSha256'] == repeat.frozen.sha(HERE / 'protocol.md')
assert len(result['attempts']) == 60
assert len({(a['caseId'], a['requestedInterpreter']) for a in result['attempts']}) == 60
for case in cases:
    for mode in repeat.frozen.MODES:
        attempt = next(a for a in result['attempts'] if a['caseId'] == case['caseId'] and a['requestedInterpreter'] == mode)
        assert attempt['question'] == case['question']
        assert attempt['sourceCommit'] == result['sourceCommit']
        assert attempt['score'] == repeat.frozen.score(case, attempt['httpStatus'], attempt['finalBackendResponse'])
        if attempt['httpStatus'] == 200:
            assert attempt['resolvedInterpreter'] in [mode, 'SafetyRules']
assert result['fixtureBefore'] == result['fixtureAfter']
assert all(result[k] for k in ['fixtureUnchanged', 'sourceUnchanged', 'historicalUnchanged', 'commitUnchanged'])
for path, sha in result['historicalHashes'].items():
    assert sha == repeat.frozen.sha(repeat.ROOT / path), path
recomputed = {'attempts': result['attempts']}
repeat.frozen.summarize(recomputed, cases)
assert recomputed['summary'] == result['summary']
assert recomputed['pairedValid'] == result['pairedValid']
print('PASS: all 60 frozen case/mode scores, summaries, identities and preserved historical files verified offline.')
