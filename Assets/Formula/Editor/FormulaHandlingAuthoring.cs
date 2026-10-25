using System;
using System.Linq;
using FormulaGame;
using UnityEditor;
using UnityEngine;

public static class FormulaHandlingAuthoring
{
    // Enough sustained cornering lock for every input device. FormulaSteering
    // softens the initial response without removing the player's turning range.
    public static AnimationCurve SteeringCurve()
    {
        var curve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(40, .75f),
            new Keyframe(80, .36f), new Keyframe(100, .24f), new Keyframe(140, .14f),
            new Keyframe(200, .07f), new Keyframe(260, .048f), new Keyframe(340, .034f));
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
        }
        return curve;
    }

    // The imported tyre mesh has a small lateral offset. Copying its bounds
    // directly put the front physics axle off the vehicle centreline, causing
    // measurable launch yaw even with zero steering. Preserve the track width.
    public static void CenterAxleWheelColliders(RCCP_Axle axle)
    {
        var left = axle.leftWheelCollider.transform;
        var right = axle.rightWheelCollider.transform;
        Vector3 l = axle.transform.InverseTransformPoint(left.position);
        Vector3 r = axle.transform.InverseTransformPoint(right.position);
        float halfTrack = (r.x - l.x) * .5f;
        if (halfTrack <= 0) throw new InvalidOperationException("Invalid left/right wheel ordering on " + axle.name);
        l.x = -halfTrack; r.x = halfTrack;
        left.position = axle.transform.TransformPoint(l);
        right.position = axle.transform.TransformPoint(r);
    }

    // Calibrated with twelve-second stock and level-three launches on level and
    // banked roads. Development continues to derive from these prefab baselines.
    public static void ApplyChassisCalibration(GameObject root)
    {
        var aero = root.GetComponentInChildren<RCCP_AeroDynamics>(true);
        if (aero == null) throw new InvalidOperationException("Missing Formula aerodynamics on " + root.name);
        Transform com = aero.COM;
        var duplicates = aero.transform.Cast<Transform>().Where(t => t.name == "COM" && t != com).ToArray();
        foreach (var duplicate in duplicates)
            if (duplicate.childCount != 0 || duplicate.GetComponents<Component>().Length != 1)
                throw new InvalidOperationException("Duplicate COM contains authored content on " + root.name);

        // RCCP resolves COM by child name after reloading. Keep the existing
        // selected transform and remove only redundant empty direct children.
        foreach (var duplicate in duplicates) UnityEngine.Object.DestroyImmediate(duplicate.gameObject);
        com.position = root.transform.TransformPoint(new Vector3(0, .25f, -.15f));
        aero.COM = com;
        foreach (var wheel in root.GetComponentsInChildren<WheelCollider>(true))
        {
            var spring = wheel.suspensionSpring;
            spring.damper = 4500f;
            wheel.suspensionSpring = spring;
            wheel.forceAppPointDistance = .2f;
        }
    }

    [MenuItem("Tools/Formula Career/Align Generic Front Wheel Physics")]
    public static void AlignFrontWheelPhysics()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var paths = FormulaCatalog.Current.liveries.Select(l => l.prefabPath).Concat(new[] { AssetDatabase.GetAssetPath(FormulaAssets.Current.vehiclePrefab) }).Distinct();
        foreach (string path in paths)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var axle = root.GetComponentsInChildren<RCCP_Axle>(true).Single(a => a.isSteer);
                CenterAxleWheelColliders(axle);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Debug.Log("FORMULA_GENERIC_FRONT_AXLE_ALIGNED");
    }

    [MenuItem("Tools/Formula Career/Apply Generic Handling Correction")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var paths = FormulaCatalog.Current.liveries.Select(l => l.prefabPath).Concat(new[] { AssetDatabase.GetAssetPath(FormulaAssets.Current.vehiclePrefab) }).Distinct();
        foreach (string path in paths)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var input = root.GetComponentInChildren<RCCP_Input>(true);
                if (input == null) throw new InvalidOperationException("Missing RCCP input on " + path);
                input.steeringCurve = SteeringCurve();
                if (root.GetComponent<FormulaSteering>() == null) root.AddComponent<FormulaSteering>();
                if (root.GetComponent<FormulaTractionControl>() == null) root.AddComponent<FormulaTractionControl>();
                CenterAxleWheelColliders(root.GetComponentsInChildren<RCCP_Axle>(true).Single(a => a.isSteer));
                ApplyChassisCalibration(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets(); Debug.Log("FORMULA_GENERIC_HANDLING_CORRECTED");
    }
}
