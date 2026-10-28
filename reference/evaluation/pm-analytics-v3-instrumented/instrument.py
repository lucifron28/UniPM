"""Generate an observer-only server copy under ignored artifacts; never edit server/."""
from pathlib import Path
import hashlib,json,subprocess,shutil,difflib
ROOT=Path(__file__).resolve().parents[3]
HERE=Path(__file__).resolve().parent
DEST=ROOT/'artifacts/test-182/runtime'
def sha(b):return hashlib.sha256(b).hexdigest()
def main():
    if DEST.exists():raise SystemExit('Refusing to overwrite an existing instrumented build.')
    paths=subprocess.check_output(['git','ls-files','server'],cwd=ROOT,text=True).splitlines()
    manifest=dict(sourceCommit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),files=[],changes=[])
    for name in paths:
        src=ROOT/name;dst=DEST/src.relative_to(ROOT/'server')
        dst.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(src,dst)
        manifest['files'].append(dict(path=name,sha256=sha(src.read_bytes())))
    patches=[]
    def patch(name,old,new):
        p=DEST/name;s=p.read_text();assert s.count(old)==1,(name,s.count(old),old[:70])
        t=s.replace(old,new);p.write_text(t)
        manifest['changes'].append(dict(path=name,old=old,new=new))
    patch('Program.cs','var app = builder.Build();','var app = builder.Build();\napp.Use((context, next) => UniPM.Api.Features.Reports.Test182Trace.Observe(context, next));')
    interp='Features/Reports/PmAnalyticsV3QuestionInterpreter.cs'
    patch(interp,'            var response = await _modelClient.GenerateAsync(modelInput, cancellationToken);',
        '            var observedProviderStart = System.Diagnostics.Stopwatch.GetTimestamp();\n            NaturalLanguageAnalyticsModelResponse response;\n            try { response = await _modelClient.GenerateAsync(modelInput, cancellationToken); }\n            finally { Test182Trace.Event("providerLogicalStage", new { elapsedMs = System.Diagnostics.Stopwatch.GetElapsedTime(observedProviderStart).TotalMilliseconds }); }\n            Test182Trace.Event("rawProviderProposal", new { text = response.Content });')
    patch(interp,'        if (candidate is null || candidate.Status is null || candidate.ClarificationFields is null)',
        '        Test182Trace.Event("parsedInterpreterProposal", candidate);\n        if (candidate is null || candidate.Status is null || candidate.ClarificationFields is null)')
    patch(interp,'            var constrained = ApplyInterpretedQuestionConstraints(sanitized, ref candidate, interpreter, interpretationTimeUtc);',
        '            var constrained = ApplyInterpretedQuestionConstraints(sanitized, ref candidate, interpreter, interpretationTimeUtc);\n            Test182Trace.Event("questionConstraints", new { candidate, decision = constrained });')
    patch(interp,'        var resolution = await _queryService.NormalizeAndResolveAsync(candidate.Plan, cancellationToken);',
        '        Test182Trace.Event("schemaValidationPassed", new { plan = candidate.Plan });\n        var resolution = await _queryService.NormalizeAndResolveAsync(candidate.Plan, cancellationToken);\n        Test182Trace.Event("catalogResolution", new { plan = resolution.Plan?.Plan, fields = resolution.ClarificationFields, errors = resolution.Errors });')
    patch(interp,'            if (candidate.Code is "UnsupportedTimeBasis" or "UnsupportedPmCycle")',
        '            Test182Trace.Event("schemaValidationRejected", new { errors = planErrors });\n            if (candidate.Code is "UnsupportedTimeBasis" or "UnsupportedPmCycle")')
    base='Features/Reports/NaturalLanguageAnalyticsApiModelClientBase.cs'
    patch(base,'                using var response = await _httpClient.SendAsync(',
        '                Test182Trace.Event("providerDispatch", new { attemptNumber, model = ModelId, utc = DateTimeOffset.UtcNow });\n                using var response = await _httpClient.SendAsync(')
    patch(base,'                NaturalLanguageAnalyticsModelResponse parsed;',
        '                Test182Trace.ProviderBody(body);\n                NaturalLanguageAnalyticsModelResponse parsed;')
    ledger='Features/Reports/NaturalLanguageAnalyticsModelClient.cs'
    patch(ledger,'            _logicalCalls++;','            _logicalCalls++;\n            Test182Trace.Event("providerLogicalCall", new { });')
    patch(ledger,'            _attempts++;','            _attempts++;\n            Test182Trace.Event("providerAttempt", new { isRetry });')
    patch(ledger,'            _httpSuccessResponses++;',
        '            Test182Trace.Event("providerHttpSuccess", new { usage, hasText, latencyMilliseconds, reportedModelVersion, systemFingerprint, httpStatusCode });\n            _httpSuccessResponses++;')
    patch(ledger,'            _providerFailures++;',
        '            Test182Trace.Event("providerFailure", new { safeCode, latencyMilliseconds, stopRun, terminalCode, httpStatusCode });\n            _providerFailures++;')
    patch(ledger,'            _terminalFailureCode ??= safeCode;',
        '            Test182Trace.Event("providerTerminalFailure", new { safeCode });\n            _terminalFailureCode ??= safeCode;')
    shutil.copyfile(HERE/'TraceObserver.cs',DEST/'Test182Trace.cs')
    # Reverse every insertion in memory to prove the business sources are unchanged.
    for name in dict.fromkeys(c['path'] for c in manifest['changes']):
        p=DEST/name;after=p.read_text();before=after
        for c in reversed([c for c in manifest['changes'] if c['path']==name]):
            assert before.count(c['new'])==1;before=before.replace(c['new'],c['old'])
        original=(ROOT/'server'/name).read_text();assert original==before,name
        patches.extend(difflib.unified_diff(original.splitlines(True),after.splitlines(True),fromfile='server/'+name,tofile='observer-overlay/'+name))
    manifest['observerSha256']=sha((HERE/'TraceObserver.cs').read_bytes())
    manifest['reversalCheck']='Every overlay edit reverses exactly to the original source text; no business source edits.'
    (ROOT/'artifacts/test-182/instrumentation-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    (ROOT/'artifacts/test-182/instrumentation.patch').write_text(''.join(patches))
    print('Generated reversible observer-only build from',len(paths),'tracked server files.')
if __name__=='__main__':main()
