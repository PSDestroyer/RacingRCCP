# Three-lap AI driving validation — 2026-10-31

Two complete physical races passed on `track_01` (3,539.6 m), with three opponents and a stock player car driven automatically by RCCP. Each run followed the normal Quick Race launch, checkpoint/lap accounting, three-lap finish and result screen. An isolated temporary save was used. Physics fixedDeltaTime was unchanged; timeScale 2 accelerated wall-clock execution. No checkpoint/lap injection or reference-player transport was used. Opponent production recovery was monitored and was never triggered in either passing run.

## Finding and correction

The initial probe after the previous radius/prediction corrections still produced substantial path deviations and opponent recovery transports. `Attempt1` retains that evidence and records why the run was aborted.

`FormulaCircuit.ConfigureDriver` returned early on regular circuits before adding `FormulaAIPace`. The precomputed curvature/braking plan already existed on these circuits but was not applied. Consequently, fixing the old low-speed radius calculation exposed corner entries at excessive speed, and the catch-up system had no authored speed target to adjust there.

The driver now uses the existing braking plan on every Formula circuit, with the existing proportional speed controller and integral term disabled to avoid straight-line wind-up. Compact/RTG circuits retain their additional steering look-ahead configuration. The prior fix keeps catch-up independent of steering gain/horizon. No scene, vehicle physics parameters, save format, rendering or frame-limit settings were changed. All 18 scenes have positive pre-authored speed-plan data (`SpeedPlanAudit.json`); only track_01 was driven in this task.

## Results

| Scenario | Race time | Valid player laps | Opponent distance behind at player finish | Recovery transports | Repeated steering oscillation windows |
| --- | ---: | ---: | ---: | ---: | ---: |
| Normal reference | 260.382 s | 3 | approximately 14 m maximum | 0 | 0 |
| Faster reference | 243.665 s | 3 | 288.6–300.5 m (8.2–8.5% of a lap) | 0 | 0 |

The faster reference uses the production `FormulaAIPace.PacingMultiplier` of 1.25, yielding up to 10% higher planned straight speed and 2.5% higher tight-corner speed; it does not receive increased torque or teleportation. Opponent catch-up multipliers rose to 1.235–1.242. This is a deliberately faster benchmark; a remaining gap of about 300 m is reported, not hidden as equal pace.

Quick Race stops participants when the player finishes. Opponents still approaching the final line therefore report two **completed** laps even though they are almost through lap three. The reported continuous progress gaps establish that they were not a lap behind. The normal reference finished P2; the faster reference finished P1.

## Oscillation measurement

Telemetry is sampled approximately every 0.1 simulation seconds. The in-Editor check records speed, steering input, yaw rate, path error, progress and recoveries. `Analyze.py` independently reconstructs the authored waypoint geometry from the scene, avoiding a straightness classification based on the car's own heading. It checks road segments with less than 8 degrees of local heading change ahead and speed at least 60 km/h for four steering-sign reversals exceeding 0.12 input magnitude within two seconds. Both passing runs had zero such windows. Opponent lateral RMS in these samples was approximately 0.79–1.04 m.

This is a quantitative check for repetitive wobbling, not a claim that steering input never changes sign. Video/visual inspection, a human-driven reference, other circuits' full races, and Nintendo hardware were not exercised. Editor simulation does not establish Switch frame-time performance.

## Artifacts and repeatability

- `Summary.json`: both passing results, independent metrics and runtime-source hashes.
- `StockReference/` and `FasterReference/`: full reports and CSV telemetry.
- `Attempt1/`: initial failed tracking evidence.
- `Analyze.py`: independent geometry/telemetry analysis; requires Python with NumPy. Optional first argument selects a run folder.
- `FormulaAIThreeLapValidation.Run` / `.RunCatchUp`: repeatable Editor entry points, also available under Tools → Formula Career. For a separate batch Editor, use `-batchmode -projectPath <project> -executeMethod <entry> -logFile <log>` without `-quit`; the validator exits after Play Mode with code 0 on pass.

Both passing Unity 6000.3.19f1 processes exited with code 0 and recorded zero runtime errors. The runner restores its save-path override, Play Mode start scene, background execution setting, audio pause and time scale before exit.
