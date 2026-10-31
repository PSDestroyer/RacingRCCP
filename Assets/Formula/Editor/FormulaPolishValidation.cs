using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public static class FormulaPolishValidation
{
    [MenuItem("Tools/Formula Career/Polish/Validate Controls Pacing and Music")]
    public static void Validate()
    {
        int checks = 0;
        void Require(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
        Require(RCCP_AI.PathTurnRadius(Vector3.zero, Vector3.forward * 10, Vector3.forward * 20) == float.MaxValue, "Straight path incorrectly imposes corner braking");
        foreach (float radius in new[] { 5f, 25f, 100f })
            Require(Mathf.Abs(RCCP_AI.PathTurnRadius(new Vector3(radius, 0, 0), new Vector3(0, 0, radius), new Vector3(-radius, 0, 0)) - radius) < .001f, "Path circumradius is incorrect");
        Require(RCCP_AI.PathTurnRadius(Vector3.zero, Vector3.zero, Vector3.forward) == float.MaxValue, "Duplicate path sample must not impose a speed limit");
        foreach (float speed in new[] { 0f, 15, 35, 65, 100, 200, 300 })
        {
            Require(FormulaSteering.ShapeStick(0, .05f, speed) == 0, "Neutral stick drift");
            Require(Mathf.Approximately(FormulaSteering.ShapeStick(1, .05f, speed), 1), "Full steering lock lost");
            float previous = 0;
            for (int i = 1; i <= 20; i++)
            {
                float value = i / 20f; float shaped = FormulaSteering.ShapeStick(value, .05f, speed);
                Require(shaped >= previous && shaped <= value + .0001f, "Stick response is discontinuous or amplified");
                Require(Mathf.Approximately(shaped, -FormulaSteering.ShapeStick(-value, .05f, speed)), "Left/right response mismatch");
                if (speed >= 100 && value > .05f) Require(Mathf.Abs(shaped - value) < .0001f, "High-speed stick calibration changed");
                previous = shaped;
            }
        }
        Require(FormulaSteering.ShapeStick(.3f, .05f, 15) < .2f, "Low-speed center response is not gentler");
        Require(FormulaRacePacing.Multiplier(-18, 1) == 1 && FormulaRacePacing.Multiplier(25, 1) == 1, "Pacing deadband changed");
        Require(FormulaRacePacing.Multiplier(-600, 1) == 1.25f, "Catch-up exceeds the torque budget");
        Require(FormulaRacePacing.Multiplier(600, 1) == .95f, "Leader slowdown exceeds five percent");
        for (int gap = -1000; gap <= 1000; gap += 10)
        {
            float multiplier = FormulaRacePacing.Multiplier(gap, 1);
            Require(multiplier >= .95f && multiplier <= 1.25f, "Unbounded race pacing");
            Require(FormulaRacePacing.Multiplier(gap, 0) == 1, "Finish fade did not remove assistance");
        }
        Require(FormulaRacePacing.SpeedFactor(1.25f, 40) <= 1.026f, "Hairpin boost is too high");
        Require(FormulaRacePacing.SpeedFactor(1.25f, 170) <= 1.101f, "Straight boost is too high");
        foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Formula/RCCP/Prefabs/Generic", "Assets/Formula/Resources/Formula/Vehicles" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(root.GetComponent<FormulaSteering>() != null, "Missing progressive steering: " + path);
        }
        var bank = FormulaSoundBank.Current;
        Require(bank != null && bank.menuMusic != null && bank.menuMusic.length >= 119 && bank.menuMusic.length < 121, "New music is not assigned");
        var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(bank.menuMusic));
        Require(importer.defaultSampleSettings.loadType == AudioClipLoadType.Streaming, "Music is not streamed");
        Require(bank.menuMusic.channels == 2 && bank.menuMusic.frequency == 48000, "Music channel/sample budget changed");
        File.WriteAllText(FormulaPolishTrackAudit.Folder + "/LogicValidation.json", JsonConvert.SerializeObject(new { status = "passed", checks, music = bank.menuMusic.name, musicSeconds = bank.menuMusic.length, musicLoadType = importer.defaultSampleSettings.loadType.ToString(), switchHardwareMeasured = false }, Formatting.Indented));
        Debug.Log("FORMULA_POLISH_LOGIC_PASSED: " + checks);
    }
}
