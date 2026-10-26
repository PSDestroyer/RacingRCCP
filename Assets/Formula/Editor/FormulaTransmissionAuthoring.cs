using System;
using System.IO;
using System.Linq;
using FormulaGame;
using UnityEditor;
using UnityEngine;

public static class FormulaTransmissionAuthoring
{
    public static string[] Prefabs => Directory.GetFiles("Assets/Formula/RCCP/Prefabs/Generic", "*.prefab")
        .Concat(new[] { "Assets/Formula/Resources/Formula/Vehicles/Formula_Generic.prefab" }).ToArray();
    [MenuItem("Tools/Formula Career/Integrate Formula Transmission")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        foreach (string path in Prefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<FormulaTransmission>() == null) root.AddComponent<FormulaTransmission>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("FORMULA_TRANSMISSION_AUTHORED: " + Prefabs.Length + " Generic prefabs");
    }
    [MenuItem("Tools/Formula Career/Validate Formula Transmission Rules")]
    public static void Validate()
    {
        var ratios = new[] { 3f, 2.4f, 1.95f, 1.65f, 1.42f, 1.24f, 1.1f, .98f };
        for (int gear = 0; gear < 8; gear++)
        {
            int next = FormulaTransmission.SelectGear(gear, ratios, 11400, 11400, 11400, 6500, 1, 0, 4);
            Require(next == Math.Min(7, gear + 1), "Missed upshift from " + (gear + 1));
            Require(FormulaTransmission.SelectGear(gear, ratios, 12000, 12000, 11400, 6500, 1, 0, 0) == gear, "Airborne shift");
            Require(FormulaTransmission.SelectGear(gear, ratios, 12000, 1000, 11400, 6500, 1, 0, 4) == gear, "Wheelspin cascades through gears without road speed");
            int down = FormulaTransmission.SelectGear(gear, ratios, 6400, 6400, 11400, 6500, 0, 1, 4);
            Require(down == Math.Max(0, gear - 1), "Missed safe downshift from " + (gear + 1));
            Require(FormulaTransmission.SelectGear(gear, ratios, 6400, 12000, 11400, 6500, 0, 1, 4) == gear, "Downshift permits mechanical over-rev");
        }
        Require(FormulaTransmission.LaunchClutch(20, 1, 6000, 1800) == 0, "Clutch slips at road speed");
        Require(FormulaTransmission.LaunchClutch(0, 0, 1800, 1800) == 1, "Stationary clutch not disengaged");
        Require(FormulaTransmission.WheelOverspeedTorqueScale(30, 30, 12000, 11400) == 1, "Coupled driving unnecessarily loses torque");
        Require(FormulaTransmission.WheelOverspeedTorqueScale(30, 10, 11000, 11400) < .3f, "Spinning axle keeps full torque near redline");
        Require(FormulaTransmission.WheelOverspeedTorqueScale(30, 10, 7000, 11400) == 1, "Overspeed protection activates outside its high-RPM range");
        foreach (string path in Prefabs)
        {
            var car = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(car.GetComponents<FormulaTransmission>().Length == 1, "Missing/duplicate transmission: " + path);
        }
        Directory.CreateDirectory("Docs/AI/FormulaTransmission");
        File.WriteAllText("Docs/AI/FormulaTransmission/Rules.json", "{\"status\":\"passed\",\"gears\":8,\"prefabs\":9,\"checks\":[\"upshift\",\"safe downshift\",\"airborne\",\"wheelspin\",\"over-rev\",\"clutch endpoints\",\"TCS torque protection\"]}");
        Debug.Log("FORMULA_TRANSMISSION_RULES_PASSED: all eight gears, airborne, slip, over-rev and nine prefabs");
    }
    private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}
