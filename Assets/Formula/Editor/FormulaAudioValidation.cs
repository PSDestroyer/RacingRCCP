using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FormulaAudioValidation
{
    private static readonly List<string> passed = new List<string>(), failed = new List<string>();
    [MenuItem("Tools/Formula Career/Validate Formula Sound Design")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        passed.Clear(); failed.Clear();
        var bank = FormulaSoundBank.Current;
        Check("Sound bank, mixer groups and presentation snapshots", () =>
        {
            Require(bank != null && bank.mixer != null, "Sound bank/mixer missing");
            foreach (FormulaAudioBus bus in Enum.GetValues(typeof(FormulaAudioBus)))
                Require(FormulaAudioMix.Group(bus) != null && FormulaAudioMix.Group(bus).audioMixer == bank.mixer, "Unrouted bus: " + bus);
            foreach (string name in new[] { "Menu", "Race", "Pause", "Results" }) Require(bank.mixer.FindSnapshot(name) != null, "Missing snapshot " + name);
            foreach (string name in new[] { "VehicleVolume", "EffectsVolume", "MusicVolume" }) Require(bank.mixer.GetFloat(name, out _), "Missing user-volume parameter " + name);
        });
        Check("Every semantic UI and race cue has an authored clip", () =>
        {
            foreach (FormulaAudio.Cue cue in Enum.GetValues(typeof(FormulaAudio.Cue)))
            {
                var clip = bank.GetCue(cue); Require(clip != null && clip.samples > 0 && clip.length > 0, "Missing cue " + cue);
                Require(bank.cues.Count(c => c.cue == cue) == 1, "Duplicate cue " + cue);
            }
        });
        Check("Road, contact, environment and music assets are assigned", () =>
        {
            var clips = new[] { bank.rollAsphalt, bank.rollKerb, bank.rollGrass, bank.rollGravel, bank.rollWet, bank.rollSnow,
                bank.scrapeAsphalt, bank.scrapeMetal, bank.suspensionHit, bank.skidAsphalt, bank.skidGrass,
                bank.skidSand, bank.crowd, bank.rain, bank.garageRoom, bank.menuMusic };
            Require(clips.All(c => c != null && c.samples > 0), "Missing/empty audio clip");
            Require(bank.lightImpacts.Length >= 2 && bank.heavyImpacts.Length >= 2, "Impact variations missing");
            Require(bank.bodyImpacts.Length >= 2 && bank.metalImpacts.Length >= 2, "Body/metal impact variations missing");
            Require(bank.lightImpacts.Concat(bank.heavyImpacts).Concat(bank.bodyImpacts).Concat(bank.metalImpacts).All(c => c != null && c.samples > 0), "Missing impact variation");
        });
        var layers = new[] { 0f, .22f, .58f, 1f }.Select(a => new FormulaEngineAudio.EngineLayer { rpmAnchor = a, pitchAtAnchor = 1.2f }).ToArray();
        Check("Engine crossfades maintain equal power and use at most two adjacent bands", () =>
        {
            var weights = new float[layers.Length];
            for (int i = 0; i <= 1000; i++)
            {
                FormulaEngineAudio.CalculateLayerWeights(layers, i / 1000f, weights);
                Require(Mathf.Abs(weights.Sum(w => w * w) - 1) < .0001f, "Energy discontinuity at " + i);
                Require(weights.All(w => !float.IsNaN(w) && w >= 0 && w <= 1) && weights.Count(w => w > 0) <= 2, "Invalid active bands");
            }
        });
        Check("Engine band boundaries and former 6000/11000 RPM transitions are continuous", () =>
        {
            var a = new float[4]; var b = new float[4];
            foreach (float point in new[] { .22f, .58f, Mathf.InverseLerp(1800, 12000, 6000), Mathf.InverseLerp(1800, 12000, 11000) })
            {
                FormulaEngineAudio.CalculateLayerWeights(layers, point - .0001f, a);
                FormulaEngineAudio.CalculateLayerWeights(layers, point + .0001f, b);
                Require(a.Zip(b, (x, y) => Mathf.Abs(x - y)).Max() < .005f, "Band jump at " + point);
            }
        });
        Check("Engine smoothing is frame-rate independent and respects zero delta", () =>
        {
            float[] end = new float[3]; int[] rates = { 30, 60, 120 };
            for (int i = 0; i < rates.Length; i++)
                for (int frame = 0; frame < rates[i]; frame++) end[i] = FormulaEngineAudio.Damp(end[i], 1, .11f, 1f / rates[i]);
            Require(end.Max() - end.Min() < .00001f, "Different response at30/60/120fps");
            Require(FormulaEngineAudio.Damp(.4f, 1, .05f, 0) == .4f, "Paused smoothing advances");
        });
        Check("Engine pitch stays finite and within the authored playback range", () =>
        {
            foreach (var layer in layers)
                for (float rpm = 0; rpm < 15000; rpm += 100)
                {
                    float pitch = FormulaEngineAudio.PitchForLayer(layer, rpm, 1800, 12000);
                    Require(!float.IsNaN(pitch) && pitch >= .25f && pitch <= 3f, "Invalid pitch");
                }
        });
        Check("Active engine bands follow RPM without pitch plateaus", () =>
        {
            var weights = new float[layers.Length];
            for (float rpm = 1800; rpm < 12000; rpm += 10)
            {
                FormulaEngineAudio.CalculateLayerWeights(layers, Mathf.InverseLerp(1800, 12000, rpm), weights);
                for (int band = 0; band < layers.Length; band++)
                {
                    if (weights[band] < .001f) continue;
                    float pitch = FormulaEngineAudio.PitchForLayer(layers[band], rpm, 1800, 12000);
                    float next = FormulaEngineAudio.PitchForLayer(layers[band], rpm + 10, 1800, 12000);
                    Require(next > pitch, "Audible pitch plateau at " + rpm + " RPM, band " + band);
                    Require(Mathf.Abs(next / pitch - (rpm + 10) / rpm) < .00001f, "Pitch does not track the RPM ratio");
                }
            }
            var calibrated = new FormulaEngineAudio.EngineLayer { referenceRpm = 4000, pitchAtAnchor = 1, rpmAnchor = .8f };
            Require(FormulaEngineAudio.PitchForLayer(calibrated, 8000, 1800, 12000) == 2, "Measured recording RPM ignored");
        });
        Check("Shared RPM dezipper reaches 90 percent within 50ms at30/60/120fps", () =>
        {
            foreach (int rate in new[] { 30, 60, 120 })
            {
                float value = 0; int frames = 0;
                while (value < .9f && frames < rate)
                { value = FormulaEngineAudio.Damp(value, 1, FormulaEngineAudio.RpmResponseSeconds, 1f / rate); frames++; }
                Require((float)frames / rate <= .05f, "RPM feedback reacts too slowly at " + rate);
            }
        });
        Check("Forward gear indices display1through8; reverse and neutral stay distinct", () =>
        {
            for (int i = 0; i < 8; i++) Require(FormulaEngineAudio.DisplayGearFor(i, false, false) == i + 1, "Incorrect displayed gear");
            Require(FormulaEngineAudio.DisplayGearFor(0, true, false) == -1 && FormulaEngineAudio.DisplayGearFor(0, false, true) == 0, "Reverse/neutral mapping incorrect");
        });
        Check("Last shift light reaches the gearbox shift RPM with ordered thresholds", () =>
        {
            Require(FormulaEngineAudio.ShiftLightThreshold(11, 12) == 1, "Last LED disagrees with shift point");
            for (int i = 1; i < 12; i++) Require(FormulaEngineAudio.ShiftLightThreshold(i, 12) > FormulaEngineAudio.ShiftLightThreshold(i - 1, 12), "Unordered LED thresholds");
            var sample = new FormulaEngineAudio.PowertrainFrame { idleRpm = 1800, maxRpm = 12000, shiftRpm = 11400, rpm = 11400, hasHigherGear = true };
            Require(sample.ShiftDue && sample.ShiftFraction == 1, "Shift prompt misses its point");
            sample.shifting = true; Require(!sample.ShiftDue, "Prompt remains active during a shift");
            sample.shifting = false; sample.hasHigherGear = false; Require(!sample.ShiftDue, "Top gear requests a nonexistent gear");
        });
        Check("Light contacts below the old800Ns gate are audible; rest stays silent", () =>
        {
            Require(FormulaContactAudio.ImpactGain(0, 820, 0) == 0, "Static contact produces a hit");
            float light = FormulaContactAudio.ImpactGain(250, 820, .4f), heavy = FormulaContactAudio.ImpactGain(6000, 820, 8);
            Require(light > 0 && heavy > light && heavy <= 1, "Light/heavy contact range is not meaningful");
            Require(Mathf.Abs(FormulaContactAudio.ImpactGain(250, 820, .4f) - FormulaContactAudio.ImpactGain(500, 1640, .4f)) < .0001f, "Impact response is not mass normalized");
        });
        Check("Slider gains have a mute endpoint and correct half-gain conversion", () =>
        {
            Require(FormulaAudioMix.Decibels(0) == -80 && FormulaAudioMix.Decibels(1) == 0, "Volume endpoints incorrect");
            Require(Mathf.Abs(FormulaAudioMix.Decibels(.5f) + 6.0206f) < .001f, "Half volume is not a single -6.02dB attenuation");
        });
        var prefabs = Directory.GetFiles("Assets/Formula/RCCP/Prefabs/Generic", "*.prefab").Concat(new[] { "Assets/Formula/Resources/Formula/Vehicles/Formula_Generic.prefab" }).ToArray();
        Check("Exactly nine Generic prefab assets are covered", () => Require(prefabs.Length == 9, "Unexpected Generic prefab count"));
        foreach (string path in prefabs) Check("Authored vehicle audio: " + Path.GetFileNameWithoutExtension(path), () =>
        {
            var car = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(car != null && car.GetComponent<FormulaEngineAudio>() != null && car.GetComponent<FormulaContactAudio>() != null, "Missing Formula audio components");
            var legacy = car.GetComponentInChildren<RCCP_Audio>(true);
            Require(legacy != null && legacy.engineSounds.All(s => s != null && s.audioClipOn != null), "Engine source bank missing");
        });
        Check("Career/quick-race circuit inventory remains18", () => Require(FormulaAssets.Current.tracks.Length == 18, "Unexpected circuit count"));
        foreach (var track in FormulaAssets.Current.tracks) Check("Saved acoustic mapping: " + track.id, () =>
        {
            var scene = EditorSceneManager.OpenPreviewScene(track.scenePath);
            try
            {
                var circuit = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FormulaCircuit>(true)).Single();
                Require(circuit.roadColliders != null && circuit.roadColliders.Length > 0, "No road colliders");
                Require(circuit.roadColliders.All(c => c != null && c.GetComponentInParent<FormulaAudioSurface>() != null), "Road without acoustic metadata");
                Require(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FormulaAudioSurface>(true)).Any(), "No surface metadata");
                foreach (var terrain in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>(true)))
                    Require((terrain.GetComponentInParent<FormulaAudioSurface>()?.TerrainResolution ?? 0) >= 8, "Terrain acoustic map not baked");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        });
        Directory.CreateDirectory("Docs/AI/FormulaAudio");
        File.WriteAllText("Docs/AI/FormulaAudio/Validation.json", JsonConvert.SerializeObject(new { status = failed.Count == 0 ? "passed" : "failed", unity = Application.unityVersion,
            method = "Deterministic math, authored references and18saved scenes opened as isolated previews without saving. Playback and subjective timbre require separate validation.", passed, failed }, Formatting.Indented));
        Debug.Log("FORMULA_AUDIO: " + passed.Count + " passed, " + failed.Count + " failed");
        if (failed.Count > 0) throw new InvalidOperationException(string.Join("\n", failed));
    }
    private static void Check(string name, Action action) { try { action(); passed.Add(name); } catch (Exception e) { failed.Add(name + ": " + e.Message); } }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
