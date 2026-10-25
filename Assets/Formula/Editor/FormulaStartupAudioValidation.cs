using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;

// A bounded continuation of this task's already requested build validation.
// The request is consumed once on import, never watched or replayed on startup.
[InitializeOnLoad]
public static class FormulaStartupAudioValidation
{
    private const string Request = "Docs/AI/FormulaCircuits/StartupAudioBuildRequest.json";
    private static bool pending;
    static FormulaStartupAudioValidation()
    {
        if (!File.Exists(Request)) return;
        var data = JObject.Parse(File.ReadAllText(Request));
        if ((string)data["status"] != "pending") return;
        pending = true; EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        if (!pending || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        pending = false; EditorApplication.update -= Tick;
        var data = JObject.Parse(File.ReadAllText(Request));
        try
        {
            const string reportPath = "Docs/AI/FormulaCareer/BuildValidation.json";
            var previous = JObject.Parse(File.ReadAllText(reportPath));
            if ((string)previous["result"] != "Succeeded" || (int)previous["errors"] != 0) throw new InvalidOperationException("A successful full build is required before a scripts-only update.");
            const string fullReport = "Docs/AI/FormulaCircuits/FullDesktopBuildValidation.json";
            if (!((string)previous["options"] ?? "").Contains("BuildScriptsOnly"))
                File.Copy(reportPath, fullReport, true);
            else if (!File.Exists(fullReport))
                throw new InvalidOperationException("The full base build report must be preserved.");
            data["status"] = "building_scripts"; File.WriteAllText(Request, data.ToString());
            FormulaBuildValidation.RunScriptsOnly();
            var current = JObject.Parse(File.ReadAllText(reportPath));
            data["status"] = (string)current["result"] == "Succeeded" && (int)current["errors"] == 0 ? "passed" : "failed";
        }
        catch (Exception error) { data["status"] = "failed"; data["error"] = error.ToString(); }
        File.WriteAllText(Request, data.ToString());
    }
}
