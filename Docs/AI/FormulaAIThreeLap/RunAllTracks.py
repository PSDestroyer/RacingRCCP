"""Run the other 17 circuits sequentially in isolated Unity batch processes."""
import os, json, subprocess, time, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent / 'AllTracks'
UNITY = '/Applications/Unity/Hub/Editor/6000.3.19f1/Unity.app/Contents/MacOS/Unity'
TRACKS = [f'track_{i:02}' for i in range(2, 9)] + [f'rtg_{i:02}' for i in range(1, 11)]
results = []
for track in TRACKS:
    folder = OUT / track
    folder.mkdir(parents=True, exist_ok=True)
    env = dict(os.environ, FORMULA_AI_TEST_TRACK=track, FORMULA_AI_REPORT_PATH=str(folder))
    command = [UNITY, '-batchmode', '-projectPath', str(ROOT), '-executeMethod',
               'FormulaAIThreeLapValidation.RunCatchUp', '-logFile', str(folder / 'Unity.log')]
    started = time.time()
    print('START ' + track, flush=True)
    try:
        run = subprocess.run(command, cwd=ROOT, env=env, timeout=1020)
        code = run.returncode
    except subprocess.TimeoutExpired:
        code = 'process timeout'
    report = json.loads((folder / 'Validation.json').read_text()) if (folder / 'Validation.json').exists() else {}
    analysis = None
    if (folder / 'RuntimeWaypoints.json').exists() and (folder / 'Telemetry.csv').exists():
        check = subprocess.run([sys.executable, str(OUT.parent / 'Analyze.py'), str(folder)], capture_output=True, text=True)
        if check.returncode == 0:
            analysis = json.loads((folder / 'IndependentAnalysis.json').read_text())
        else:
            (folder / 'AnalysisError.txt').write_text(check.stderr)
    entry = dict(track=track, exitCode=code, wallSeconds=round(time.time()-started, 1),
                 status=report.get('status', 'no report'), result=report.get('result'),
                 errors=report.get('errors'), independentAnalysis=analysis)
    results.append(entry)
    (OUT / 'Summary.json').write_text(json.dumps(dict(referencePace=1.25, switchHardwareTested=False,
        completed=len(results), requested=len(TRACKS), results=results), indent=2))
    print('DONE ' + track + ' ' + entry['status'] + ' ' + str(entry['result']), flush=True)
