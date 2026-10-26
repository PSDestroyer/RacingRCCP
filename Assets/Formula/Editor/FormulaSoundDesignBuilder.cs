using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public static class FormulaSoundDesignBuilder
{
    private const string BankPath = "Assets/Formula/Resources/Formula/Audio/SoundBank.asset";
    private const string Designed = "Assets/Formula/Audio/Designed/";
    private const string Vendor = "Assets/Realistic Car Controller Pro/Audio/";
    [MenuItem("Tools/Formula Career/Integrate Formula Sound Design")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before audio authoring.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save your open scene before audio authoring.");
        Directory.CreateDirectory("Docs/AI/FormulaAudio");
        Directory.CreateDirectory(Path.GetDirectoryName(BankPath)); AssetDatabase.Refresh();
        var bank = BuildBank();
        var prefabs = FormulaCatalog.Current.liveries.Select(l => l.prefabPath)
            .Concat(new[] { AssetDatabase.GetAssetPath(FormulaAssets.Current.vehiclePrefab) }).Distinct().ToArray();
        foreach (var path in prefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<FormulaEngineAudio>() == null) root.AddComponent<FormulaEngineAudio>();
                if (root.GetComponent<FormulaContactAudio>() == null) root.AddComponent<FormulaContactAudio>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var rows = new List<object>();
        try
        {
            foreach (var path in Directory.GetFiles("Assets/Formula/Scenes", "*.unity").Where(p => p.Contains("Formula_Circuit_") || p.Contains("Formula_RTG_")).OrderBy(p => p))
            {
                var scene = EditorSceneManager.OpenScene(path);
                string physicsBefore = PhysicsFingerprint(scene);
                var circuit = All<FormulaCircuit>(scene).Single(); bool wet = circuit.trackId == "track_06";
                var roads = new HashSet<Collider>(circuit.roadColliders.Where(c => c != null));
                int surfaces = 0, terrains = 0;
                foreach (var collider in All<Collider>(scene))
                {
                    if (collider.isTrigger || collider.attachedRigidbody != null) continue;
                    FormulaAudioSurfaceKind kind;
                    bool known = Classify(collider.name, out kind);
                    if (roads.Contains(collider)) { kind = FormulaAudioSurfaceKind.Asphalt; known = true; }
                    // Explicit curbs can also be in the driveable-road registry.
                    if (collider.name.IndexOf("curb", StringComparison.OrdinalIgnoreCase) >= 0 || collider.name.IndexOf("kerb", StringComparison.OrdinalIgnoreCase) >= 0) { kind = FormulaAudioSurfaceKind.Kerb; known = true; }
                    var terrain = collider.GetComponent<Terrain>();
                    if (terrain != null) { kind = FormulaAudioSurfaceKind.Grass; known = true; }
                    if (!known) continue;
                    var surface = collider.GetComponent<FormulaAudioSurface>();
                    if (surface == null) surface = collider.gameObject.AddComponent<FormulaAudioSurface>();
                    surface.kind = kind; surface.wetness = wet && kind == FormulaAudioSurfaceKind.Asphalt ? 1 : 0;
                    if (terrain != null && terrain.terrainData != null)
                    {
                        var kinds = terrain.terrainData.terrainLayers.Select(layer =>
                        {
                            string hint = layer == null ? "" : layer.name + " " + (layer.diffuseTexture == null ? "" : layer.diffuseTexture.name);
                            return Classify(hint, out var value) ? value : FormulaAudioSurfaceKind.Grass;
                        }).ToArray();
                        surface.BakeTerrain(terrain, kinds); terrains++;
                    }
                    if (PrefabUtility.IsPartOfPrefabInstance(surface)) PrefabUtility.RecordPrefabInstancePropertyModifications(surface);
                    EditorUtility.SetDirty(surface); surfaces++;
                }
                int crowds = AuthorCrowds(scene, circuit, bank);
                if (physicsBefore != PhysicsFingerprint(scene)) throw new InvalidOperationException("Audio authoring changed physics: " + path);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                rows.Add(new { scene = path, circuit.trackId, acousticSurfaces = surfaces, terrainMaps = terrains, crowdSources = crowds, wetRoad = wet });
                File.WriteAllText("Docs/AI/FormulaAudio/Authoring.json", JsonConvert.SerializeObject(new { status = "authoring", prefabs, scenes = rows }, Formatting.Indented));
            }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        AssetDatabase.SaveAssets();
        File.WriteAllText("Docs/AI/FormulaAudio/Authoring.json", JsonConvert.SerializeObject(new { status = "complete", prefabs, scenes = rows, physicsChanged = false, vendorAssetsChanged = false, engineRecordings = "Existing RCCP Sport2 bank with Formula-owned blending; replacement field recordings remain an art dependency." }, Formatting.Indented));
        Debug.Log("FORMULA_SOUND_DESIGN_AUTHORED");
    }
    private static FormulaSoundBank BuildBank()
    {
        var bank = AssetDatabase.LoadAssetAtPath<FormulaSoundBank>(BankPath);
        if (bank == null) { bank = ScriptableObject.CreateInstance<FormulaSoundBank>(); AssetDatabase.CreateAsset(bank, BankPath); }
        bank.mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Formula/Audio/Formula Mixer.mixer");
        if (bank.mixer == null) throw new InvalidOperationException("Run Tools/Audio/build_formula_audio.py before authoring.");
        bank.playerEngine = Group(bank, "Player Engine"); bank.opponents = Group(bank, "Opponents");
        bank.tyres = Group(bank, "Tyres"); bank.contacts = Group(bank, "Contacts"); bank.world = Group(bank, "World");
        bank.ui = Group(bank, "UI"); bank.music = Group(bank, "Music");
        bank.rollAsphalt = Clip(Designed + "RollAsphalt.wav"); bank.rollKerb = Clip(Designed + "RollKerb.wav");
        bank.rollGrass = Clip(Designed + "RollGrass.wav"); bank.rollGravel = Clip(Designed + "RollGravel.wav"); bank.rollWet = Clip(Designed + "RollWet.wav"); bank.rollSnow = Clip(Designed + "RollSnow.wav");
        bank.scrapeAsphalt = Clip(Designed + "ScrapeAsphalt.wav"); bank.scrapeMetal = Clip(Designed + "ScrapeMetal.wav");
        bank.suspensionHit = Clip(Vendor + "Wheel/Wheel_Bump.wav");
        bank.skidAsphalt = Clip(Vendor + "Skid/Skid_Asphalt.wav"); bank.skidGrass = Clip(Vendor + "Skid/Skid_Grass.wav"); bank.skidSand = Clip(Vendor + "Skid/Skid_Sand.wav");
        bank.lightImpacts = Enumerable.Range(1, 4).Select(i => Clip(Designed + "LightImpact" + i + ".wav")).ToArray();
        bank.heavyImpacts = Enumerable.Range(2, 4).Select(i => Clip(Vendor + "Impact/Impact" + i + ".wav")).ToArray();
        bank.metalImpacts = Enumerable.Range(1, 4).Select(i => Clip(Designed + "MetalImpact" + i + ".wav")).ToArray();
        bank.bodyImpacts = Enumerable.Range(1, 4).Select(i => Clip(Designed + "BodyImpact" + i + ".wav")).ToArray();
        bank.crowd = Clip("Assets/RTG 1.3/Contents/GrandStands/Sound/Audience.wav");
        bank.rain = Clip(Designed + "RainOpenAir.wav"); bank.garageRoom = Clip(Designed + "GarageRoom.wav");
        bank.menuMusic = Clip(Designed + "PaddockTheme.wav");
        bank.cues = Enum.GetValues(typeof(FormulaAudio.Cue)).Cast<FormulaAudio.Cue>().Select(cue => new FormulaSoundBank.CueClip
        { cue = cue, clip = Clip((int)cue < 6 ? "Assets/Formula/Audio/UI/" + cue + ".wav" : Designed + cue + ".wav") }).ToArray();
        foreach (string path in Directory.GetFiles(Designed, "*.wav"))
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            bool music = path.EndsWith("PaddockTheme.wav", StringComparison.Ordinal);
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            settings.quality = .8f; settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings; importer.forceToMono = !music;
            importer.loadInBackground = music; importer.SaveAndReimport();
        }
        EditorUtility.SetDirty(bank); AssetDatabase.SaveAssetIfDirty(bank); return bank;
    }
    private static AudioMixerGroup Group(FormulaSoundBank bank, string name) => bank.mixer.FindMatchingGroups("").Single(g => g.name == name);
    private static AudioClip Clip(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>(path) ?? throw new InvalidOperationException("Missing sound: " + path);
    private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    private static string PhysicsFingerprint(Scene scene) => string.Join("\n", All<Collider>(scene).Cast<Component>().Concat(All<Rigidbody>(scene)).Select(c => EditorJsonUtility.ToJson(c) + "|" + EditorJsonUtility.ToJson(c.transform)));
    private static bool Classify(string hint, out FormulaAudioSurfaceKind kind)
    {
        hint = hint.ToLowerInvariant(); kind = FormulaAudioSurfaceKind.Asphalt;
        if (hint.Contains("curb") || hint.Contains("kerb")) kind = FormulaAudioSurfaceKind.Kerb;
        else if (hint.Contains("snow")) kind = FormulaAudioSurfaceKind.Snow;
        else if (hint.Contains("grass")) kind = FormulaAudioSurfaceKind.Grass;
        else if (hint.Contains("gravel") || hint.Contains("sand") || hint.Contains("dirt")) kind = FormulaAudioSurfaceKind.Gravel;
        else if (hint.Contains("guardrail") || hint.Contains("fence") || hint.Contains("metal")) kind = FormulaAudioSurfaceKind.Metal;
        else if (hint.Contains("wall") || hint.Contains("barrier") || hint.Contains("blocker")) kind = FormulaAudioSurfaceKind.Barrier;
        else if (!(hint.Contains("road") || hint.Contains("asphalt") || hint.Contains("pavement"))) return false;
        return true;
    }
    private static int AuthorCrowds(Scene scene, FormulaCircuit circuit, FormulaSoundBank bank)
    {
        var existing = All<AudioSource>(scene).Where(s => s.clip == bank.crowd).ToArray();
        foreach (var source in existing)
        {
            source.outputAudioMixerGroup = bank.world; source.priority = 210; source.volume = .12f;
            source.spatialBlend = 1; source.minDistance = 8; source.maxDistance = 85; source.dopplerLevel = 0;
            EditorUtility.SetDirty(source);
        }
        if (existing.Length > 0) return existing.Length;
        var stands = All<MeshRenderer>(scene).Where(r => r.name.IndexOf("tribune", StringComparison.OrdinalIgnoreCase) >= 0 || r.name.IndexOf("grandstand", StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(r => (r.bounds.center - circuit.startFinish.position).sqrMagnitude).ToArray();
        var positions = new List<Vector3>();
        foreach (var stand in stands)
        {
            var point = stand.bounds.center;
            if (positions.Any(p => (p - point).sqrMagnitude < 40 * 40)) continue;
            positions.Add(point);
            var node = new GameObject("Formula Crowd " + positions.Count); node.transform.SetParent(circuit.transform, true); node.transform.position = point;
            var source = node.AddComponent<AudioSource>(); source.clip = bank.crowd; source.loop = true; source.playOnAwake = true;
            source.outputAudioMixerGroup = bank.world; source.priority = 210; source.volume = .12f;
            source.spatialBlend = 1; source.minDistance = 8; source.maxDistance = 85; source.dopplerLevel = 0;
            if (positions.Count == 3) break;
        }
        return positions.Count;
    }
}
