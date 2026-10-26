using System;
using System.IO;
using System.Linq;
using FormulaGame;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FormulaCameraRainBuilder
{
    public const string ScenePath = "Assets/Formula/Scenes/Formula_Circuit_06.unity";
    public const string MaterialPath = "Assets/Formula/Materials/Camera Rain Lite.mat";
    public const string ReportFolder = "Docs/AI/FormulaCameraRain";

    [MenuItem("Tools/Formula Career/Apply Lightweight Camera Rain")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var shader = Shader.Find("Formula/Camera Rain Lite");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/WindshieldRainAsset/Common/DemoResources/Textures/rain_normal.png");
        if (shader == null || !shader.isSupported || normal == null) throw new InvalidOperationException("Camera rain shader or imported rain normal is unavailable.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (!opened && scene.isDirty) throw new InvalidOperationException("Save the rain circuit before applying camera rain.");
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var circuits = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FormulaCircuit>(true)).ToArray();
            if (circuits.Length != 1 || circuits[0].trackId != "track_06") throw new InvalidOperationException("Unexpected rain circuit wiring.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, MaterialPath); }
            Undo.RecordObject(material, "Configure economical lens rain");
            material.shader = shader; material.SetTexture("_MainTex", normal);
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            var rain = circuits[0].GetComponent<FormulaCameraRain>();
            if (rain == null) rain = Undo.AddComponent<FormulaCameraRain>(circuits[0].gameObject);
            var serialized = new SerializedObject(rain);
            serialized.FindProperty("rainMaterial").objectReferenceValue = material;
            serialized.ApplyModifiedProperties();
            if (PrefabUtility.IsPartOfPrefabInstance(rain)) PrefabUtility.RecordPrefabInstancePropertyModifications(rain);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Rain circuit could not be saved.");
            Directory.CreateDirectory(ReportFolder);
            File.WriteAllText(ReportFolder + "/Authoring.json", JsonConvert.SerializeObject(new
            {
                status = "applied", unity = Application.unityVersion, scene = scene.path,
                component = rain.GetType().FullName, material = MaterialPath,
                texture = AssetDatabase.GetAssetPath(normal), textureSize = new[] { normal.width, normal.height },
                shaderPasses = material.passCount, textureLookupsPerPixel = 1,
                dropCount = FormulaLensDroplets.DropCount, fullScreenQuad = false,
                extraSceneColorCopies = 0, extraRenderTextures = 0,
                blur = false, refraction = false, fullWindshieldSimulation = false,
                switchDeviceProfiled = false
            }, Formatting.Indented));
            Debug.Log("FORMULA_CAMERA_RAIN_APPLIED: one lens overlay below HUD, rain circuit only.");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
}
