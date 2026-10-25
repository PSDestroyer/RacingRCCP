using System;
using System.IO;
using System.Linq;
using FormulaGame;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class FormulaBuildValidation
{
    [Serializable] class Report { public string unityVersion, target, backend, architecture, output, result, options; public double seconds; public ulong bytes; public int errors, warnings; public string[] scenes, messages, errorMessages; }
    [MenuItem("Tools/Formula Career/Build Desktop Validation")]
    public static void Run() => Build(false);
    [MenuItem("Tools/Formula Career/Build Desktop Scripts Validation")]
    public static void RunScriptsOnly() => Build(true);
    private static void Build(bool scriptsOnly)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var target = EditorUserBuildSettings.activeBuildTarget;
        if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target)) throw new InvalidOperationException("The active target has no installed playback engine: " + target);
        string suffix = target == BuildTarget.StandaloneOSX ? ".app" : target == BuildTarget.StandaloneWindows64 ? ".exe" : "";
        string output = Path.Combine(Path.GetTempPath(), "FormulaG27Validation", "FormulaG27" + suffix); Directory.CreateDirectory(Path.GetDirectoryName(output));
        string[] scenes = new[] { "Assets/Scenes/Init.unity", "Assets/Scenes/Username.unity", "Assets/Scenes/Menu.unity", FormulaGarageBuilder.GarageScene }.Concat(FormulaAssets.Current.tracks.Select(t => t.scenePath)).Distinct().ToArray();
        var started = DateTime.UtcNow;
        var options = BuildOptions.Development | (scriptsOnly ? BuildOptions.BuildScriptsOnly : BuildOptions.None);
        var build = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = output, target = target, options = options });
        var report = new Report { unityVersion = Application.unityVersion, target = target.ToString(), backend = PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone).ToString(), architecture = PlayerSettings.GetArchitecture(BuildTargetGroup.Standalone).ToString(), output = output, scenes = scenes, options = options.ToString(), result = build.summary.result.ToString(), seconds = (DateTime.UtcNow - started).TotalSeconds, bytes = build.summary.totalSize, errors = (int)build.summary.totalErrors, warnings = (int)build.summary.totalWarnings, errorMessages = build.steps.SelectMany(s => s.messages).Where(m => m.type == LogType.Error || m.type == LogType.Exception).Select(m => m.content).Distinct().ToArray(), messages = build.steps.SelectMany(s => s.messages).Where(m => m.type == LogType.Error || m.type == LogType.Exception || m.type == LogType.Warning).Select(m => m.content).Distinct().ToArray() };
        Directory.CreateDirectory("Docs/AI/FormulaCareer"); File.WriteAllText("Docs/AI/FormulaCareer/BuildValidation.json", JsonUtility.ToJson(report, true));
        Debug.Log("FORMULA_DESKTOP_BUILD_" + report.result + " " + output);
    }
}
