using System;
using System.IO;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

// Explicit, one-run Editor acceptance job; no file watcher or recurring schedule.
[InitializeOnLoad]
public static class FormulaCircuitFinalValidation
{
    private const string Key = "Formula.CircuitFinalValidation";
    private static readonly string[] Reports = { "Docs/AI/FormulaCircuits/DirectPlayValidation.json", "Docs/AI/FormulaUI/RuntimeValidation.json", "Docs/AI/FormulaUI/English/RaceValidation.json", "Docs/AI/FormulaCareer/BuildValidation.json" };
    static FormulaCircuitFinalValidation() { EditorApplication.update += Tick; }
    [MenuItem("Tools/Formula Career/Validate Circuit Integration and Desktop Build")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if ((string)JObject.Parse(File.ReadAllText("Docs/AI/FormulaCircuits/PlayValidation.json"))["status"] != "passed") throw new InvalidOperationException("Finish all-circuit driving validation first.");
        SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".Stage", 0); SessionState.SetBool(Key + ".Waiting", false); Write("running", null);
    }
    [MenuItem("Tools/Formula Career/Resume UI and Desktop Validation")]
    public static void ResumePresentation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if ((string)JObject.Parse(File.ReadAllText("Docs/AI/FormulaCircuits/PlayValidation.json"))["status"] != "passed" ||
            (string)JObject.Parse(File.ReadAllText(Reports[0]))["status"] != "passed") throw new InvalidOperationException("Complete circuit driving and natural race acceptance first.");
        FormulaCircuitSelectionBuilder.Apply();
        SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".Stage", 1); SessionState.SetBool(Key + ".Waiting", false); Write("running", null);
    }
    [MenuItem("Tools/Formula Career/Resume Circuits and Final Validation")]
    public static void ResumeAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".Stage", -1);
        SessionState.SetString(Key + ".CoreStarted", DateTime.UtcNow.ToString("O"));
        SessionState.SetBool(Key + ".Waiting", false); Write("awaiting_circuit_validation", null);
        try { FormulaCircuitExpansionBuilder.RebuildAndResume(); }
        catch (Exception error) { SessionState.SetBool(Key, false); Write("failed", error.ToString()); throw; }
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        try
        {
            int stage = SessionState.GetInt(Key + ".Stage", 0);
            if (stage == -1)
            {
                var since = DateTime.Parse(SessionState.GetString(Key + ".CoreStarted", ""), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                const string buildPath = "Docs/AI/FormulaCircuits/Build.json", drivePath = "Docs/AI/FormulaCircuits/PlayValidation.json";
                if (File.Exists(buildPath) && File.GetLastWriteTimeUtc(buildPath) >= since && (string)JObject.Parse(File.ReadAllText(buildPath))["phase"] == "failed") throw new InvalidOperationException("Circuit authoring failed; inspect " + buildPath);
                if (!File.Exists(drivePath) || File.GetLastWriteTimeUtc(drivePath) < since) return;
                string core = (string)JObject.Parse(File.ReadAllText(drivePath))["status"];
                if (core == "failed") throw new InvalidOperationException("Circuit driving failed; inspect " + drivePath);
                if (core != "passed") return;
                SessionState.SetInt(Key + ".Stage", 0); SessionState.SetBool(Key + ".Waiting", false); Write("running", null); return;
            }
            if (!SessionState.GetBool(Key + ".Waiting", false))
            {
                SessionState.SetString(Key + ".Started", DateTime.UtcNow.ToString("O")); SessionState.SetBool(Key + ".Waiting", true); Write("running", null);
                if (stage == 0) FormulaDirectCircuitValidation.Run();
                else if (stage == 1) FormulaMenuPreview.Run1080();
                else if (stage == 2) FormulaEnglishRacePreview.Run();
                else if (stage == 3) FormulaBuildValidation.Run();
                return;
            }
            var started = DateTime.Parse(SessionState.GetString(Key + ".Started", ""), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (DateTime.UtcNow - started > TimeSpan.FromHours(2)) throw new InvalidOperationException("Final validation timed out: stage " + stage);
            if (!File.Exists(Reports[stage]) || File.GetLastWriteTimeUtc(Reports[stage]) < started) return;
            var report = JObject.Parse(File.ReadAllText(Reports[stage]));
            string status = (string)(report["status"] ?? report["phase"] ?? report["result"]);
            bool errors = report["errors"] is JArray list && list.Count > 0;
            if ((stage == 3 && status != "Succeeded") || (stage != 3 && status != "passed" && status != "complete") || errors) throw new InvalidOperationException("Stage " + stage + " failed; inspect " + Reports[stage]);
            if (stage == 3)
            {
                bool buildErrors = ((int?)report["errors"] ?? 0) > 0;
                SessionState.SetBool(Key, false); Write(buildErrors ? "build_succeeded_with_reported_errors" : "passed", null); return;
            }
            SessionState.SetInt(Key + ".Stage", stage + 1); SessionState.SetBool(Key + ".Waiting", false);
        }
        catch (Exception error) { SessionState.SetBool(Key, false); Write("failed", error.ToString()); }
    }
    private static void Write(string status, string error) => File.WriteAllText("Docs/AI/FormulaCircuits/FinalValidation.json", JsonConvert.SerializeObject(new { status, stage = SessionState.GetInt(Key + ".Stage", 0), error, reports = Reports }, Formatting.Indented));
}
