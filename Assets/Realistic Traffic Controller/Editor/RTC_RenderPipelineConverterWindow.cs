//----------------------------------------------
//        Realistic Traffic Controller
//
// Copyright © 2014 - 2024 BoneCracker Games
// https://www.bonecrackergames.com
//
//----------------------------------------------

using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;

#if BCG_URP
using UnityEngine.Rendering.Universal;
#endif

#if BCG_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

public class RTC_RenderPipelineConverterWindow : EditorWindow {

    private RenderPipelineAsset activePipeline;
    private string pipelineName = "Built-in";

    /// <summary>
    /// Enum for supported pipelines
    /// </summary>
    private enum Pipeline { BuiltIn, URP, HDRP }

    private Vector2 scrollPosition;

    public static void Init() {

        RTC_RenderPipelineConverterWindow window = GetWindow<RTC_RenderPipelineConverterWindow>("RTC Pipeline Converter");
        window.Show();
        window.minSize = new Vector2(400, 920);

    }

    private void OnEnable() {

        activePipeline = GraphicsSettings.currentRenderPipeline;

        if (activePipeline == null) {

            pipelineName = "Built-in";

        } else if (activePipeline.GetType().ToString().Contains("Universal")) {

            pipelineName = "URP";

        } else if (activePipeline.GetType().ToString().Contains("HD")) {

            pipelineName = "HDRP";

        } else {

            pipelineName = "Unknown";

        }

    }

    private void OnGUI() {

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false);

        GUILayout.Space(10);
        EditorGUILayout.LabelField("RTC Render Pipeline Converter", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "This tool assists in converting RTC materials and lighting components for URP or HDRP.\n\n" +
            "Render Pipelines in Unity define how objects are drawn. Built-in is the default pipeline, " +
            "URP (Universal Render Pipeline) is optimized for performance, and HDRP (High Definition Render Pipeline) targets high-end visuals.",
            MessageType.Info
        );

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Detected Render Pipeline:", EditorStyles.label);
        EditorGUILayout.LabelField(pipelineName, EditorStyles.boldLabel);

        if (EditorApplication.isCompiling)
            EditorGUILayout.HelpBox("Scripts are compiling… please wait.", MessageType.Warning);

        EditorGUI.BeginDisabledGroup(EditorApplication.isCompiling);

        GUILayout.Space(10);

        if (pipelineName == "Built-in") {

            EditorGUILayout.HelpBox("No conversion is needed. RTC is fully compatible with the Built-in Render Pipeline.", MessageType.Info);

        } else if (pipelineName == "URP" || pipelineName == "HDRP") {

            EditorGUILayout.HelpBox(
                $"{pipelineName} detected.\n\n" +
                "In order to work properly in this pipeline, materials and lens flare components must be converted.\n\n",
                MessageType.Warning
            );

            GUILayout.Space(10);
            EditorGUILayout.LabelField("1. Material Conversion", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Click the button below to automatically select all materials under the RTC folder.\n" +
                "After selection, go to:\nEdit > Rendering > Materials > Convert Selected Built-in Materials",
                MessageType.None
            );

            if (GUILayout.Button("1. Select All RTC Materials for Conversion")) {

                SelectRTCMaterials();

            }

            GUILayout.Space(15);
            EditorGUILayout.LabelField("2. Lens Flare Conversion", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "In Built-in RP, Unity uses a legacy LensFlare component which does not work in URP/HDRP.\n" +
                "Click the button below to scan all RTC vehicle prefabs and replace legacy LensFlares with SRP-compatible ones.",
                MessageType.None
            );

            if (GUILayout.Button("2. Convert RTC Lens Flares to SRP"))
                ConvertLensFlaresToSRP();

#if BCG_URP

            if (pipelineName == "URP") {

                // -. Camera URP components
                GUILayout.Space(15);
                EditorGUILayout.LabelField("- Camera Components", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Scan all demo scenes and update their cameras for URP,\n" +
                    "and add the URP components if missing.",
                    MessageType.None
                );

                if (GUILayout.Button("- Enable Post Processing on All RTC Cameras"))
                    EnablePostProcessingOnCameras();

            }

#endif

            if (pipelineName == "HDRP") {

                // 3. HDRP Demo Scene Setup
                GUILayout.Space(15);
                EditorGUILayout.LabelField("3. HDRP Demo Scene Setup", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Scan all demo scenes and update their directional lights for HDRP,\n" +
                    "and add the HDRP Volume Profile prefab if missing.",
                    MessageType.None
                );

                if (GUILayout.Button("Convert Demo Scenes for HDRP"))
                    ConvertDemoScenesForHDRP();

            }

        } else {

            EditorGUILayout.HelpBox("Unsupported or unknown render pipeline detected. Please check your project settings.", MessageType.Error);

        }

        GUILayout.Space(20);
        EditorGUILayout.LabelField("Need Help?", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "If you are unfamiliar with Render Pipelines or material conversion in Unity, please visit the official Unity documentation:\n\n" +
            "- URP Guide: https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal\n" +
            "- HDRP Guide: https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition",
            MessageType.None
        );

        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndScrollView();

    }

    public static void SelectRTCMaterials() {

        List<string> materialGuids = new List<string>(AssetDatabase.FindAssets("t:Material", new[] { RTC_AssetUtilities.BasePath }));
        List<Object> materials = new List<Object>();

        foreach (string guid in materialGuids) {

            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
                materials.Add(mat);

        }

        Selection.objects = materials.ToArray();

        EditorUtility.DisplayDialog(
            "RTC Material Selection",
            $"{materials.Count} material(s) found and selected.\n\nGo to:\nEdit > Rendering > Materials > Convert Selected Built-in Materials",
            "OK"
        );

    }

#if BCG_URP || BCG_HDRP
    public static void ConvertLensFlaresToSRP() {

        List<string> prefabGuids = new List<string>(AssetDatabase.FindAssets("t:Prefab", new[] { RTC_AssetUtilities.BasePath }));

        int convertedCount = 0;

        foreach (string guid in prefabGuids) {

            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
                continue;

            //if (prefab.GetComponentInChildren<RCCP_CarController>(true) == null)
            //    continue;

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

            if (instance == null)
                continue;

            Light[] lights = instance.GetComponentsInChildren<Light>(true);

            bool modified = false;

            foreach (Light light in lights) {

                LensFlare legacyFlare = light.GetComponent<LensFlare>();
                if (legacyFlare != null) {

                    DestroyImmediate(legacyFlare, true);
                    LensFlareComponentSRP lf = light.gameObject.AddComponent<LensFlareComponentSRP>();
                    lf.attenuationByLightShape = false;
                    lf.intensity = 0f;
                    lf.lensFlareData = RTC_Settings.Instance.lensFlareData as LensFlareDataSRP;
                    modified = true;

                }

            }

            if (modified) {

                PrefabUtility.SaveAsPrefabAsset(instance, path);
                convertedCount++;

            }

            DestroyImmediate(instance);

        }

        EditorUtility.DisplayDialog("RTC Lens Flare Conversion", $"Conversion completed.\n{convertedCount} prefab(s) updated.", "OK");

    }
#else

    private static void ConvertLensFlaresToSRP() {



    }
#endif

#if BCG_URP
    public static void EnablePostProcessingOnCameras() {

        List<string> prefabGuids = new List<string>(AssetDatabase.FindAssets("t:Prefab", new[] { RTC_AssetUtilities.BasePath }));

        int modifiedCount = 0;

        foreach (string guid in prefabGuids) {

            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
                continue;

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

            if (instance == null)
                continue;

            Camera[] cameras = instance.GetComponentsInChildren<Camera>(true);
            bool modified = false;

            foreach (Camera cam in cameras) {

                if (!cam.allowHDR)
                    cam.allowHDR = true;

                if (!cam.allowMSAA)
                    cam.allowMSAA = true;

#if UNITY_2021_2_OR_NEWER
                // Enables the Post Processing checkbox for URP/HDRP cameras.
                if (!cam.allowDynamicResolution)
                    cam.allowDynamicResolution = true;

                if (!cam.renderingPath.Equals(RenderingPath.UsePlayerSettings))
                    cam.renderingPath = RenderingPath.UsePlayerSettings;
#endif

                if (!cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>()) {

                    var additionalData = cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                    additionalData.renderPostProcessing = true;
                    modified = true;

                } else {

                    var additionalData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                    if (!additionalData.renderPostProcessing) {
                        additionalData.renderPostProcessing = true;
                        modified = true;
                    }

                }

            }

            if (modified) {

                PrefabUtility.SaveAsPrefabAsset(instance, path);
                modifiedCount++;

            }

            DestroyImmediate(instance);

        }

        EditorUtility.DisplayDialog("RTC Camera Post Processing", $"Completed.\n{modifiedCount} prefab(s) modified.", "OK");

    }

#endif

#if BCG_HDRP

    /// <summary>
    /// Scans every demo scene in the Demo Scenes folder,
    /// updates directional lights with HDRP components,
    /// and ensures a Volume Profile is present.
    /// </summary>
    public static void ConvertDemoScenesForHDRP() {

        // Get the HDRP Volume Profile prefab from RTC_Settings
        var settings = RTC_Settings.Instance;
        var volumeProfilePrefab = settings.hdrpVolumeProfilePrefab;
        if (volumeProfilePrefab == null) {
            Debug.LogError("[RTC] HDRP Volume Profile Prefab is not assigned in RTC_Settings.");
            return;
        }

        List<string> sceneGuids = new List<string>(AssetDatabase.FindAssets("t:SceneAsset", new[] { RTC_AssetUtilities.BasePath }));

        int processed = 0;

        string lastScene = UnityEditor.SceneManagement.EditorSceneManager
        .GetActiveScene().path;

        UnityEditor.SceneManagement.EditorSceneManager
                .NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        foreach (string guid in sceneGuids) {

            // Resolve path and open scene additively
            string scenePath = AssetDatabase.GUIDToAssetPath(guid);
            var scene = UnityEditor.SceneManagement.EditorSceneManager
                .OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);

            bool sceneModified = false;
            bool oldDirectionalLightFound = false;

            // --- REMOVE OLD DIRECTIONAL LIGHTS ---
            // Find every Light of type Directional
            var oldLights = scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<Light>(true))
                .Where(light => light.type == LightType.Directional && light.transform.name.Contains("Directional"))
                .ToArray();

            foreach (Light oldLight in oldLights) {
                oldDirectionalLightFound = true;
                // destroy the entire GameObject so we do not leave orphan components
                GameObject.DestroyImmediate(oldLight.gameObject);
                sceneModified = true;
            }

            if (oldDirectionalLightFound) {

                // --- CREATE NEW DIRECTIONAL LIGHT ---
                GameObject newDirLightGO = new GameObject("Sun");
                Light newLight = newDirLightGO.AddComponent<Light>();
                newLight.type = LightType.Directional;
                // orient the light at a nice default angle
                newDirLightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                // move into the demo scene
                EditorSceneManager.MoveGameObjectToScene(newDirLightGO, scene);
                sceneModified = true;

            }

            // 1) Update all Directional Lights
#if UNITY_2022_1_OR_NEWER
            Light[] lights = GameObject.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            Light[] lights = GameObject.FindObjectsOfType<Light>(true);
#endif

            foreach (Light light in lights) {

                if (light.GetComponentInParent<RTC_CarController>(true) != null)
                    continue;

                if (light.type != LightType.Directional) {
                    // In HDRP, lights require HDAdditionalLightData
#if UNITY_2021_2_OR_NEWER
                    if (light.GetComponent<global::UnityEngine.Rendering.HighDefinition
                        .HDAdditionalLightData>() == null) {
                        light.gameObject.AddComponent<global::UnityEngine.Rendering.HighDefinition
                            .HDAdditionalLightData>();
                        sceneModified = true;
                    }
#endif
                }
            }

            // 2) Ensure a Volume Profile exists
#if !UNITY_2022_1_OR_NEWER
            var volumes = GameObject.FindObjectsOfType<UnityEngine.Rendering.Volume>(true);
#else
            var volumes = GameObject.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#endif

            if (volumes.Length == 0) {
                // Instantiate the prefab and move it into this scene
                GameObject volumeGO = PrefabUtility
                    .InstantiatePrefab(volumeProfilePrefab) as GameObject;
                UnityEditor.SceneManagement.EditorSceneManager
                    .MoveGameObjectToScene(volumeGO, scene);
                sceneModified = true;
            }

            // 3) Save changes if needed
            if (sceneModified) {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                processed++;
            }

            // Close the additive scene before moving on
            if (EditorSceneManager.sceneCount > 1) {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        EditorUtility.DisplayDialog(
            "RTC HDRP Scene Conversion",
            $"Completed HDRP setup on {processed} demo scene(s).",
            "OK"
        );

        if (lastScene.Length > 0)
            EditorSceneManager.OpenScene(lastScene, OpenSceneMode.Single);

    }

#else

    private static void ConvertDemoScenesForHDRP() {



    }

#endif

    /// <summary>
    /// Helper to get a friendly pipeline label for dialogs.
    /// </summary>
    private static string GetPipelineLabel() {

        var pipeline = GraphicsSettings.currentRenderPipeline;
        if (pipeline == null) return "Built-in";
        var type = pipeline.GetType().ToString();
        if (type.Contains("Universal")) return "URP";
        if (type.Contains("HD")) return "HDRP";
        return "Unknown";
    }

    /// <summary>
    /// Deletes all assets at the path of the given content object
    /// </summary>
    private static void RemovePipelineContent(Object contentObject) {
        if (contentObject == null) {
            Debug.LogWarning("Content object is null, skipping removal.");
            return;
        }

        var path = AssetDatabase.GetAssetPath(contentObject);

        if (string.IsNullOrEmpty(path)) {
            Debug.LogWarning("Could not find asset path for content object: " + contentObject.name);
            return;
        }

        // If folder, delete folder, else delete asset file
        if (AssetDatabase.IsValidFolder(path)) {
            if (AssetDatabase.DeleteAsset(path)) {
                Debug.Log("Deleted folder at " + path);
            } else {
                Debug.LogError("Failed to delete folder at " + path);
            }
        } else {
            if (AssetDatabase.DeleteAsset(path)) {
                Debug.Log("Deleted asset at " + path);
            } else {
                Debug.LogError("Failed to delete asset at " + path);
            }
        }
    }

    /// <summary>
    /// Imports a .unitypackage from the path of the given package object
    /// </summary>
    private static void ImportPackage(Object packageObject) {
        if (packageObject == null) {
            Debug.LogError("Package object is null, cannot import.");
            return;
        }

        var packagePath = AssetDatabase.GetAssetPath(packageObject);

        if (string.IsNullOrEmpty(packagePath)) {
            Debug.LogError("Could not find package path for object: " + packageObject.name);
            return;
        }

        AssetDatabase.ImportPackage(packagePath, true);
        Debug.Log("Imported package: " + packagePath);
    }

}
