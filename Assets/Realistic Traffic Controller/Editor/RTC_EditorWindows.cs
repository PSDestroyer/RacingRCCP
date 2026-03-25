//----------------------------------------------
//        Realistic Traffic Controller
//
// Copyright � 2014 - 2024 BoneCracker Games
// https://www.bonecrackergames.com
//
//----------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class RTC_EditorWindows : EditorWindow {

    [MenuItem("Tools/BoneCracker Games/Realistic Traffic Controller/Edit Settings", false, 0)]
    [MenuItem("GameObject/BoneCracker Games/Realistic Traffic Controller/Edit Settings", false, 0)]
    public static void OpenSettings() {

        Selection.activeObject = RTC_Settings.Instance;

    }

    [MenuItem("Tools/BoneCracker Games/Realistic Traffic Controller/Add Traffic Controller To Vehicle", false, 100)]
    [MenuItem("GameObject/BoneCracker Games/Realistic Traffic Controller/Add Traffic Controller To Vehicle", false, 100)]
    public static void AddRTC() {

        GameObject vehicleModel = Selection.activeGameObject;

        if (!vehicleModel.GetComponentInParent<RTC_CarController>(true)) {

            bool isPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(vehicleModel);

            if (isPrefab) {

                bool isModelPrefab = PrefabUtility.IsPartOfModelPrefab(vehicleModel);
                bool unpackPrefab = EditorUtility.DisplayDialog("Unpack Prefab", "This gameobject is connected to a " + (isModelPrefab ? "model" : "") + " prefab. Would you like to unpack the prefab completely? If you don't unpack it, you won't be able to move, reorder, or delete any children instance of the prefab.", "Unpack", "Don't Unpack");

                if (unpackPrefab)
                    PrefabUtility.UnpackPrefabInstance(vehicleModel, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            }

            if (EditorUtility.DisplayDialog("Fix Pivot", "Do you want to fix the pivot position of the vehicle model? If your model has correct pivot position, choose no.", "Fix Pivot", "No")) {

                GameObject pivot = new GameObject(vehicleModel.name);
                pivot.transform.position = RTC_GetBounds.GetBoundsCenter(vehicleModel.transform);
                pivot.transform.rotation = vehicleModel.transform.rotation;

                pivot.AddComponent<RTC_CarController>();

                vehicleModel.transform.SetParent(pivot.transform);
                Selection.activeGameObject = pivot;

            } else {

                vehicleModel.AddComponent<RTC_CarController>();
                Selection.activeGameObject = vehicleModel;

            }

            Rigidbody rig = Selection.activeGameObject.GetComponent<Rigidbody>();

            if (rig != null && rig.mass == 1) {

                rig.mass = 1350;
                rig.linearDamping = 0f;
                rig.angularDamping = .1f;
                rig.interpolation = RigidbodyInterpolation.Interpolate;

            }

        } else {

            EditorUtility.DisplayDialog("Your Gameobject Already Has Realistic Traffic Controller", "Your Gameobject Already Has Realistic Traffic Controller", "Close");
            Selection.activeGameObject = vehicleModel;

        }

    }

    [MenuItem("Tools/BoneCracker Games/Realistic Traffic Controller/Add Traffic Controller To Vehicle", true, 100)]
    [MenuItem("GameObject/BoneCracker Games/Realistic Traffic Controller/Add Traffic Controller To Vehicle", true, 100)]
    public static bool CheckAddRTC() {

        if (Selection.gameObjects.Length > 1)
            return false;

        if (!Selection.activeGameObject)
            return false;

        if (Selection.activeGameObject.scene == default)
            return false;

        return true;

    }

    [MenuItem("Tools/BoneCracker Games/Realistic Traffic Controller/Create Scene Manager", false, 200)]
    [MenuItem("GameObject/BoneCracker Games/Realistic Traffic Controller/RTC Scene Manager", false, 200)]
    public static void CreateSceneManager() {

        Selection.activeGameObject = RTC.CreateSceneManager();

    }

    [MenuItem("Tools/BoneCracker Games/Realistic Traffic Controller/Create Traffic Spawner", false, 200)]
    [MenuItem("GameObject/BoneCracker Games/Realistic Traffic Controller/RTC Traffic Spawner", false, 200)]
    public static void CreateSpawner() {

#if !UNITY_2022_1_OR_NEWER
        RTC_TrafficSpawner spawner = FindObjectOfType<RTC_TrafficSpawner>(true);
#else
        RTC_TrafficSpawner spawner = FindFirstObjectByType<RTC_TrafficSpawner>(FindObjectsInactive.Include);
#endif


        if (spawner) {

            Selection.activeGameObject = spawner.gameObject;
            spawner.gameObject.SetActive(true);
            return;

        }

        Selection.activeGameObject = (GameObject)PrefabUtility.InstantiatePrefab(RTC_Settings.Instance.trafficSpawner.gameObject.gameObject, null);
        Selection.activeGameObject.name = RTC_Settings.Instance.trafficSpawner.name;

    }

    [MenuItem("Tools/BoneCracker Games/Realistic Traffic Controller/Create Demo Main Camera", false, 200)]
    [MenuItem("GameObject/BoneCracker Games/Realistic Traffic Controller/RTC Demo Camera", false, 200)]
    public static void CreateDemoCamera() {

#if !UNITY_2022_1_OR_NEWER
        RTC_DemoCamera demoCamera = FindObjectOfType<RTC_DemoCamera>(true);
#else
        RTC_DemoCamera demoCamera = FindFirstObjectByType<RTC_DemoCamera>(FindObjectsInactive.Include);
#endif

        if (demoCamera) {

            Selection.activeGameObject = demoCamera.gameObject;
            demoCamera.gameObject.SetActive(true);
            return;

        }

        Selection.activeGameObject = (GameObject)PrefabUtility.InstantiatePrefab(RTC_Settings.Instance.demoCamera.gameObject, null);
        Selection.activeGameObject.name = RTC_Settings.Instance.demoCamera.name;

    }

    [MenuItem("Tools/BoneCracker Games/Realistic Traffic Controller/Create Demo UI Canvas", false, 200)]
    [MenuItem("GameObject/BoneCracker Games/Realistic Traffic Controller/RTC Demo UI Canvas", false, 200)]
    public static void CreateUICanvas() {

#if !UNITY_2022_1_OR_NEWER
        RTC_Demo demoUICanvas = FindObjectOfType<RTC_Demo>(true);
#else
        RTC_Demo demoUICanvas = FindFirstObjectByType<RTC_Demo>(FindObjectsInactive.Include);
#endif

        if (demoUICanvas) {

            Selection.activeGameObject = demoUICanvas.gameObject;
            demoUICanvas.gameObject.SetActive(true);
            return;

        }

        Selection.activeGameObject = (GameObject)PrefabUtility.InstantiatePrefab(RTC_Settings.Instance.demoUICanvas.gameObject, null);
        Selection.activeGameObject.name = RTC_Settings.Instance.demoUICanvas.name;

    }

    [MenuItem("Tools/BoneCracker Games/Realistic Traffic Controller/Render Pipeline Converter", false, 300)]
    [MenuItem("GameObject/BoneCracker Games/Realistic Traffic Controller/Render Pipeline Converter", false, 300)]
    public static void RenderPipelineConverter() {

        RTC_RenderPipelineConverterWindow.Init();

    }

}
