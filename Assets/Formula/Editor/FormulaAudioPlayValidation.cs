using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FormulaAudioPlayValidation
{
    private const string Key = "Formula.Audio.Play", Folder = "Docs/AI/FormulaAudio";
    private static readonly List<string> checks = new List<string>(), errors = new List<string>();
    private static int phase, maxLoops, shifts, maxVoices, virtualVoices, warningBefore, backBefore, fixtureImpacts, maxGroundedWheels;
    private static double began, lastWrite;
    private static float peakSpeed, fixtureImpulse, fixtureGain;
    private static string status;
    private static GamePlayManager game;
    private static RCCP_AI driver;
    private static RCCP_CarController fixtureCar;
    private static FormulaContactAudio fixtureContact;
    private static GameObject fixtureRoot;
    private static PhysicsMaterial fixtureMaterial;
    private static Vector3 floorTop;
    private static FormulaAudioOutputProbe outputProbe;
    private static FormulaAudioOutputProbe.Metrics outputMetrics;
    private static FormulaEngineSyncProbe syncProbe;
    private static FormulaEngineSyncProbe.Metrics syncMetrics;
    private static bool requestedUpshift, requestedDownshift;
    static FormulaAudioPlayValidation()
    {
        EditorApplication.playModeStateChanged += Mode; EditorApplication.update += Tick; Application.logMessageReceived += Log;
    }
    [MenuItem("Tools/Formula Career/Play Validate Formula Sound Design")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        SessionState.SetString(Key + ".Previous", EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        SessionState.SetInt(Key + ".Fps", Application.targetFrameRate); SessionState.SetInt(Key + ".VSync", QualitySettings.vSyncCount);
        SessionState.SetString(Key + ".SaveOverride", Environment.GetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH") ?? "");
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaAudio-" + Guid.NewGuid().ToString("N"), "save.json"));
        Directory.CreateDirectory(Folder); SessionState.SetBool(Key, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Menu.unity"); EditorApplication.EnterPlaymode();
    }
    private static void Mode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear(); errors.Clear(); phase = maxLoops = shifts = maxVoices = virtualVoices = fixtureImpacts = 0;
            peakSpeed = fixtureImpulse = fixtureGain = 0; game = null; fixtureRoot = null; fixtureCar = null; fixtureContact = null;
            outputProbe = null; outputMetrics = null;
            syncProbe = null; syncMetrics = null;
            requestedUpshift = requestedDownshift = false;
            maxGroundedWheels = 0;
            Application.runInBackground = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60;
            Time.timeScale = 1; status = "running"; began = EditorApplication.timeSinceStartup;
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus(); Write();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            Time.timeScale = 1; AudioListener.pause = false;
            Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
            Application.targetFrameRate = SessionState.GetInt(Key + ".Fps", -1); QualitySettings.vSyncCount = SessionState.GetInt(Key + ".VSync", 0);
            string save = SessionState.GetString(Key + ".SaveOverride", ""); Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", string.IsNullOrEmpty(save) ? null : save);
            string previous = SessionState.GetString(Key + ".Previous", ""); EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            SessionState.SetBool(Key, false);
        }
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            double age = EditorApplication.timeSinceStartup - began;
            Require(age < 65, "Audio validation timed out in phase " + phase);
            Require(errors.Count == 0, "Runtime error during sound validation");
            if (EditorApplication.timeSinceStartup - lastWrite > 3) { lastWrite = EditorApplication.timeSinceStartup; Write(); }
            if (phase == 0)
            {
                if (UnityEngine.Object.FindFirstObjectByType<FormulaMainMenu>() == null || SoundManager.Instance == null || age < 2) return;
                var sound = SoundManager.Instance; var bank = FormulaSoundBank.Current;
                Require(sound.MusicSource != null && sound.MusicSource.clip == bank.menuMusic && sound.MusicSource.isPlaying, "Menu music not playing");
                Require(sound.UiSource != null && sound.UiSource.ignoreListenerPause && sound.UiSource.outputAudioMixerGroup == bank.ui, "UI source is not routed independently of listener pause");
                sound.SetSfxVolume(.5f);
                Require(bank.mixer.GetFloat("EffectsVolume", out float gain) && Mathf.Abs(gain + 6.0206f) < .01f, "Half SFX gain is not -6.02dB");
                Require(Mathf.Approximately(sound.UiSource.volume, 1), "UI source duplicates the slider attenuation");
                int clicks = FormulaAudio.GetPlayedCount(FormulaAudio.Cue.Confirm), purchases = FormulaAudio.GetPlayedCount(FormulaAudio.Cue.Purchase);
                FormulaAudio.Play(FormulaAudio.Cue.Confirm); FormulaAudio.Play(FormulaAudio.Cue.Purchase); FormulaAudio.Flush();
                Require(FormulaAudio.GetPlayedCount(FormulaAudio.Cue.Purchase) == purchases + 1 && FormulaAudio.GetPlayedCount(FormulaAudio.Cue.Confirm) == clicks, "Purchase and click were not coalesced");
                sound.SetSfxVolume(1);
                checks.Add("Menu music is scheduled; UI uses its own mixer route and one slider attenuation. Synthetic same-frame Confirm/Purchase dispatches only Purchase.");
                string error = FormulaSession.Launch("race:track_01", "quick"); Require(error == null, error); Next(1);
            }
            else if (phase == 1)
            {
                game = UnityEngine.Object.FindFirstObjectByType<GamePlayManager>();
                if (game == null || !game.FormulaRaceStarted || game.CarController == null || !game.CarController.GetComponent<FormulaEngineAudio>().Configured) return;
                Require(FormulaAudio.GetPlayedCount(FormulaAudio.Cue.Countdown) == 3 && FormulaAudio.GetPlayedCount(FormulaAudio.Cue.Go) == 1, "Actual3/2/1/GO sequence missed or duplicated cues");
                Require(game.FormulaParticipants == 4, "Quick-race grid is not four cars");
                FormulaSession.PrepareAgent(game.CarController.gameObject); driver = game.CarController.gameObject.AddComponent<RCCP_AI>();
                driver.behaviour = RCCP_AI.BehaviourType.RaceWaypoints; driver.waypointsContainer = game.raceWaypoints; game.GetComponent<FormulaCircuit>().ConfigureDriver(driver);
                var listener = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).First(l => l.isActiveAndEnabled);
                outputProbe = listener.gameObject.AddComponent<FormulaAudioOutputProbe>(); outputProbe.Begin(25);
                var engineAudio = game.CarController.GetComponent<FormulaEngineAudio>();
                var raceHud = UnityEngine.Object.FindFirstObjectByType<FormulaRaceView>();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                syncProbe = game.CarController.gameObject.AddComponent<FormulaEngineSyncProbe>();
                syncProbe.Begin(engineAudio, raceHud,
                    (FormulaEngineAudio.EngineLayer[])typeof(FormulaEngineAudio).GetField("layers", flags).GetValue(engineAudio),
                    (TMPro.TMP_Text)typeof(FormulaRaceView).GetField("rpm", flags).GetValue(raceHud),
                    (TMPro.TMP_Text)typeof(FormulaRaceView).GetField("gear", flags).GetValue(raceHud));
                checks.Add("Normal Menu to track01 quick-race flow emits exactly three countdown cues and one GO with four participants."); Next(2);
            }
            else if (phase == 2)
            {
                var engine = game.CarController.GetComponent<FormulaEngineAudio>();
                // Explicit public gearbox commands exercise both shift directions even when
                // this short AI route never reaches the automatic upshift threshold.
                var gearbox = game.CarController.Gearbox;
                if (!requestedUpshift && age >= 3 && game.CarController.absoluteSpeed > 20 && !gearbox.shiftingNow)
                { gearbox.ShiftUp(); requestedUpshift = true; }
                if (requestedUpshift && !requestedDownshift && age >= 7 && gearbox.currentGear > 0 && !gearbox.shiftingNow)
                { gearbox.ShiftDown(); requestedDownshift = true; }
                peakSpeed = Mathf.Max(peakSpeed, game.CarController.absoluteSpeed); maxLoops = Mathf.Max(maxLoops, engine.ActiveLoopCount); shifts = engine.ShiftCount;
                maxGroundedWheels = Mathf.Max(maxGroundedWheels, game.CarController.GetComponent<FormulaContactAudio>().GroundedWheels);
                var sources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
                maxVoices = Mathf.Max(maxVoices, sources.Count(s => s.isPlaying && !s.mute && s.volume > .001f));
                virtualVoices = Mathf.Max(virtualVoices, sources.Count(s => s.isPlaying && s.isVirtual));
                if (age < 20) return;
                Require(peakSpeed > 40 && shifts > 0 && maxLoops > 0 && engine.IsPlayerVehicle && engine.LegacySuppressed, "Real RCCP driving did not exercise Formula engine/gear audio");
                Require(maxGroundedWheels > 0, "No wheel contact telemetry throughout the driving run");
                Require(UnityEngine.Object.FindObjectsByType<FormulaEngineAudio>(FindObjectsSortMode.None).All(e => e.Configured && e.LegacySuppressed), "An opponent still uses legacy engine audio");
                outputMetrics = outputProbe.Write(Folder + "/GameplayAudio.wav");
                Require(outputMetrics.frames > 0 && outputMetrics.peak > .001f, "Listener DSP produced no measurable audio output");
                Require(outputMetrics.clippedSamples == 0, "Actual output clipped during the driving capture");
                syncProbe.Finish(Folder + "/EngineSynchronization.csv"); syncMetrics = syncProbe.Result;
                Require(syncMetrics.frames > 60 && syncMetrics.frameMismatches == 0 && syncMetrics.gearMismatches == 0 && syncMetrics.rpmTextMismatches == 0,
                    "HUD and rendered engine voices did not consume the same powertrain frame");
                Require(syncMetrics.maxAudioHudRpmDifference < .001f && syncMetrics.maxPlaybackPitchErrorCents < .1f,
                    "Actual source pitch or HUD RPM drifted from shared feedback");
                Require(syncMetrics.risingFrames > 0 && syncMetrics.fallingFrames > 0 && syncMetrics.shiftFrames > 0,
                    "Driving did not cover rising/falling RPM and an active gear change");
                Require(engine.UpshiftCount > 0 && engine.DownshiftCount > 0, "Both actual gearbox shift directions were not exercised");
                checks.Add("Per-rendered-frame CSV verifies rising/falling RPM and shifts: actual TMP labels, audio source pitch and HUD share one powertrain sample.");
                UnityEngine.Object.Destroy(syncProbe);
                UnityEngine.Object.Destroy(outputProbe);
                checks.Add("Twenty seconds of real RCCP autonomous input produces speed, shifts and engine/road voices; player and opponents suppress the legacy engine path.");
                warningBefore = FormulaAudio.GetPlayedCount(FormulaAudio.Cue.WrongWay); backBefore = FormulaAudio.GetPlayedCount(FormulaAudio.Cue.Back);
                Time.timeScale = 0; AudioListener.pause = true;
                FormulaAudio.Play(FormulaAudio.Cue.WrongWay); FormulaAudio.Play(FormulaAudio.Cue.Back); FormulaAudio.Flush(); Next(3);
            }
            else if (phase == 3)
            {
                if (age < .3) return;
                Require(FormulaAudio.GetPlayedCount(FormulaAudio.Cue.WrongWay) == warningBefore && FormulaAudio.GetPlayedCount(FormulaAudio.Cue.Back) == backBefore + 1, "Pause failed to suppress gameplay cue or blocked UI");
                checks.Add("Synthetic cue probe during an actual race pause suppresses Wrong Way and allows Back on the UI source.");
                Time.timeScale = 1; AudioListener.pause = false; Next(4);
            }
            else if (phase == 4)
            {
                if (age < 1) return;
                Require(game.CarController.GetComponent<FormulaEngineAudio>().ActiveLoopCount > 0, "Engine loops failed to resume");
                BeginContactFixture(); Next(5);
            }
            else if (phase == 5)
            {
                if (age < 1) return;
                var colliders = fixtureCar.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger && !(c is WheelCollider) && c.attachedRigidbody == fixtureCar.Rigid).ToArray();
                Require(colliders.Length > 0, "Fixture has no chassis colliders");
                Physics.SyncTransforms(); float bottom = colliders.Min(c => c.bounds.min.y);
                fixtureCar.transform.position += Vector3.up * (floorTop.y + .03f - bottom);
                Physics.SyncTransforms(); fixtureCar.Rigid.isKinematic = false; fixtureCar.Rigid.linearVelocity = Vector3.zero; Next(6);
            }
            else if (phase == 6)
            {
                if (fixtureContact.ImpactCount == 0) { Require(age < 5, "Controlled chassis drop produced no impact voice"); return; }
                fixtureImpulse = fixtureContact.LastImpactImpulse; fixtureGain = fixtureContact.LastImpactGain;
                Require(fixtureGain > 0 && fixtureImpulse < 800, "Fixture did not exercise a contact below the old800Ns gate: " + fixtureImpulse);
                checks.Add("Controlled Play-only cloned chassis: 3cm drop produces an actual PhysX contact and a positive impact voice below the old800Ns gate.");
                fixtureCar.Rigid.linearVelocity = Vector3.right * 8; Next(7);
            }
            else if (phase == 7)
            {
                if (!fixtureContact.ScrapeActive) { Require(age < 4, "Continuous actual chassis contact produced no scrape loop"); return; }
                if (age < .3) return;
                checks.Add("Controlled Play-only cloned chassis sliding at an initial8m/s activates sustained asphalt scrape audio from real CollisionStay contacts.");
                fixtureCar.Rigid.linearVelocity = Vector3.zero; fixtureCar.Rigid.angularVelocity = Vector3.zero; fixtureCar.Rigid.isKinematic = true;
                fixtureImpacts = fixtureContact.ImpactCount; Next(8);
            }
            else if (phase == 8)
            {
                if (age < 1.5) return;
                Require(!fixtureContact.ScrapeActive && fixtureContact.ImpactCount == fixtureImpacts && fixtureContact.ActiveVoices == 0, "Stopped fixture retained scrape audio or repeated impacts");
                checks.Add("Stopping the controlled contact fixture fades the scrape, leaves no contact voices and does not spam impacts.");
                UnityEngine.Object.Destroy(fixtureRoot); UnityEngine.Object.Destroy(fixtureMaterial); FormulaSession.ReturnToMenu(); Next(9);
            }
            else if (phase == 9)
            {
                if (UnityEngine.Object.FindFirstObjectByType<FormulaMainMenu>() == null || age < 2) return;
                Require(!FormulaSession.InRace && SoundManager.Instance.MusicSource.isPlaying, "Menu music did not recover after scene reload");
                Require(UnityEngine.Object.FindObjectsByType<FormulaEngineAudio>(FindObjectsSortMode.None).All(e => e.ActiveLoopCount == 0), "Vehicle voices leaked into menu");
                checks.Add("Returning to Menu restores music and leaves no racing engine loops."); status = "passed"; Write(); EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception failure) { errors.Add(failure.ToString()); status = "failed"; Write(); EditorApplication.ExitPlaymode(); }
    }
    private static void BeginContactFixture()
    {
        // This isolated fixture validates acoustic contact callbacks, not ordinary driving/handling.
        driver.enabled = false; game.enabled = false; game.CarController.SetCanControl(false);
        game.CarController.Rigid.linearVelocity = Vector3.zero; game.CarController.Rigid.isKinematic = true;
        foreach (var racer in game.aiRacers) if (racer?.racerTransform != null) racer.racerTransform.gameObject.SetActive(false);
        fixtureRoot = new GameObject("Validation Only - Chassis Audio Fixture");
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Validation Asphalt Contact Pad"; floor.transform.SetParent(fixtureRoot.transform);
        floor.transform.position = game.CarController.transform.position + Vector3.up * 20; floor.transform.localScale = new Vector3(40, 1, 40);
        floorTop = floor.transform.position + Vector3.up * .5f; floor.AddComponent<FormulaAudioSurface>().kind = FormulaAudioSurfaceKind.Asphalt;
        fixtureMaterial = new PhysicsMaterial("Validation Only - Low Friction") { dynamicFriction = 0, staticFriction = 0, bounciness = 0, frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum };
        floor.GetComponent<Collider>().sharedMaterial = fixtureMaterial;
        var scene = RCCP_SceneManager.Instance; scene.registerLastVehicleAsPlayer = false;
        var clone = UnityEngine.Object.Instantiate(FormulaAssets.Current.vehiclePrefab, floorTop + Vector3.up * 4, Quaternion.identity, fixtureRoot.transform);
        clone.name = "Validation Only - Generic Contact Probe"; fixtureCar = clone.GetComponent<RCCP_CarController>(); fixtureContact = clone.GetComponent<FormulaContactAudio>();
        fixtureCar.SetCanControl(false); fixtureCar.SetEngine(false); fixtureCar.Rigid.isKinematic = true; fixtureCar.Rigid.constraints = RigidbodyConstraints.FreezeRotation;
        foreach (var wheel in clone.GetComponentsInChildren<WheelCollider>()) wheel.enabled = false;
    }
    private static object FixtureSnapshot()
    {
        if (fixtureCar == null || fixtureContact == null) return null;
        object Field(string name) => typeof(FormulaContactAudio).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(fixtureContact);
        var body = fixtureCar.Rigid;
        return new { position = fixtureCar.transform.position.ToString("F3"), velocity = body.linearVelocity.ToString("F3"), body.isKinematic, body.useGravity,
            initialized = Field("initialized"), audible = Field("audible"), fixtureContact.enabled,
            age = Time.time - (float)Field("enabledAt"), fixtureContact.ImpactCount, fixtureContact.ActiveVoices,
            floorY = floorTop.y, floorLayerBlocked = Physics.GetIgnoreLayerCollision(fixtureCar.gameObject.layer, 0),
            colliders = fixtureCar.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger && !(c is WheelCollider)).Select(c => new { c.name, kind = c.GetType().Name, bottom = c.bounds.min.y, attached = c.attachedRigidbody == body }).ToArray() };
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Next(int next) { phase = next; began = EditorApplication.timeSinceStartup; Write(); }
    private static void Log(string message, string trace, LogType type) { if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying && (type == LogType.Error || type == LogType.Exception)) errors.Add(message + "\n" + trace); }
    private static void Write() => File.WriteAllText(Folder + "/PlayValidation.json", JsonConvert.SerializeObject(new { status, unity = Application.unityVersion, phase, isolatedSave = true,
        method = "Actual menu/race/countdown, 20 seconds autonomous RCCP driving with explicit public gearbox up/down commands, pause and scene return. Per-rendered-frame source/HUD telemetry; separate Play-only cloned-chassis PhysX contact fixture. Does not measure speaker latency or subjective sound quality.",
        peakSpeedKmh = peakSpeed, shifts, maxPlayerLoops = maxLoops, maxScheduledSources = maxVoices, maxVirtualSources = virtualVoices,
        listenerOutput = outputMetrics, synchronization = syncMetrics,
        fixtureImpulseNs = fixtureImpulse, fixtureImpactGain = fixtureGain, fixture = FixtureSnapshot(), checks, errors }, Formatting.Indented));
}
