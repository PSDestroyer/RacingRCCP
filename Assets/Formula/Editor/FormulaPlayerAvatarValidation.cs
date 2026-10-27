using System;
using System.Collections.Generic;
using System.IO;
using FormulaGame;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FormulaPlayerAvatarValidation
{
    private const string Key = "Formula.PlayerAvatarValidation";
    private const string Folder = "Docs/AI/NintendoNickname";
    private static int phase, frame;
    private static double started;
    private static float enteredAt;
    private static Texture2D fixture;
    private static FormulaMainMenu menu;
    private static readonly List<string> checks = new List<string>(), errors = new List<string>();
    static FormulaPlayerAvatarValidation()
    {
        EditorApplication.playModeStateChanged += Mode;
        EditorApplication.update += Tick;
        Application.logMessageReceived += Log;
    }
    [MenuItem("Tools/Formula Career/Validate Nintendo Profile Photos")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        SessionState.SetString(Key + ".Scene", EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        string previous = Environment.GetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH");
        SessionState.SetBool(Key + ".HadSave", previous != null); SessionState.SetString(Key + ".Save", previous ?? "");
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        string path = Path.Combine(Path.GetTempPath(), "FormulaAvatar-" + Guid.NewGuid().ToString("N"), "save.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, "{\"PlayerName\":\"NintendoTest\",\"money\":1234}");
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", path);
        SessionState.SetBool(Key, true);
        Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/AvatarValidation.json", "{\"status\":\"running\"}");
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Menu.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void Mode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            phase = frame = 0; checks.Clear(); errors.Clear(); fixture = null;
            started = EditorApplication.timeSinceStartup; Application.runInBackground = true;
        }
        if (state == PlayModeStateChange.ExitingPlayMode && fixture != null) UnityEngine.Object.Destroy(fixture);
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            Application.runInBackground = SessionState.GetBool(Key + ".Background", false);
            Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", SessionState.GetBool(Key + ".HadSave", false) ? SessionState.GetString(Key + ".Save", "") : null);
            string scene = SessionState.GetString(Key + ".Scene", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(scene) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
            SessionState.SetBool(Key, false);
        }
    }
    private static void Next() { phase++; frame = Time.frameCount; enteredAt = Time.unscaledTime; }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            Require(EditorApplication.timeSinceStartup - started < 90, "Avatar validation timed out.");
            if (phase == 0)
            {
                menu = UnityEngine.Object.FindFirstObjectByType<FormulaMainMenu>();
                if (menu == null || menu.View == null || Time.timeSinceLevelLoad < 1) return;
                var avatars = menu.View.GetComponentsInChildren<FormulaPlayerAvatar>(true);
                Require(avatars.Length >= 3, "Missing menu/career/upgrade avatar wiring.");
                foreach (var avatar in avatars) Require(!avatar.GetComponent<UnityEngine.UI.RawImage>().enabled, "Desktop fallback unexpectedly displays a photo.");
                var results = AssetDatabase.LoadAssetAtPath<GameObject>(FormulaPlayerAvatarBuilder.ResultsPath);
                Require(results.GetComponentsInChildren<FormulaPlayerAvatar>(true).Length == 1, "Results photo is missing.");
                checks.Add("Menu, career, upgrade and results have authored photo slots; desktop retains its existing fallback.");
                fixture = new Texture2D(64, 64, TextureFormat.RGB24, false);
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                {
                    bool head = new Vector2(x - 32, y - 41).sqrMagnitude < 100;
                    bool shoulders = (x - 32) * (x - 32) / 650f + (y - 9) * (y - 9) / 240f < 1;
                    fixture.SetPixel(x, y, head || shoulders ? new Color(.93f, .78f, .15f) : new Color(.08f, .14f, .2f));
                }
                fixture.Apply(false, true);
                foreach (var avatar in avatars)
                {
                    avatar.gameObject.SetActive(true); // All slots are active within their page.
                    if (!avatar.gameObject.activeInHierarchy) continue;
                    avatar.Present(fixture);
                    Require(avatar.GetComponent<UnityEngine.UI.RawImage>().enabled && !avatar.GetComponent<UnityEngine.UI.RawImage>().raycastTarget, "Photo visibility or input blocking is incorrect.");
                }
                checks.Add("A representative test photo displays without receiving UI raycasts.");
                Next();
            }
            else if (phase == 1 && Time.frameCount - frame > 4)
            {
                menu.Show("Carieră"); Next();
            }
            else if (phase == 2 && Time.frameCount - frame > 4)
            {
                foreach (var avatar in menu.View.GetComponentsInChildren<FormulaPlayerAvatar>())
                {
                    avatar.Present(fixture); avatar.enabled = false;
                    Require(avatar.GetComponent<UnityEngine.UI.RawImage>().texture == null, "Disable retained the runtime photo reference.");
                    avatar.enabled = true;
                    Require(!avatar.GetComponent<UnityEngine.UI.RawImage>().enabled, "Re-enable did not restore desktop fallback.");
                    avatar.Present(fixture);
                }
                checks.Add("Page changes and disable/re-enable clear the photo and restore fallback correctly.");
                menu.Show("Acasă"); Next();
            }
            else if (phase == 3 && Time.unscaledTime - enteredAt > .7f)
            {
                foreach (var avatar in menu.View.GetComponentsInChildren<FormulaPlayerAvatar>())
                {
                    avatar.Present(fixture);
                    var name = avatar.transform.parent.Find("Player Name").GetComponent<TMP_Text>();
                    var a = (RectTransform)avatar.transform; var n = (RectTransform)name.transform;
                    Require(a.rect.width == 38 && a.rect.height == 38 && a.anchoredPosition.x + 38 <= n.anchoredPosition.x, "Photo overlaps the nickname.");
                    Require(name.text == "NintendoTest" && !name.richText, "Nickname is not displayed literally.");
                }
                checks.Add("Photo fits beside the actual nickname with a gap and preserves literal name rendering.");
                ScreenCapture.CaptureScreenshot(Folder + "/AvatarPreview.png"); Next();
            }
            else if (phase == 4 && Time.frameCount - frame > 4)
            {
                Require(errors.Count == 0, "Runtime errors occurred."); Write("passed", null); EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception e) { Write("failed", e.ToString()); EditorApplication.ExitPlaymode(); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Write(string status, string failure) => File.WriteAllText(Folder + "/AvatarValidation.json", JsonConvert.SerializeObject(new { status, failure, checks, errors, unity = Application.unityVersion, photoIsTestFixture = true, switchDeviceTested = false }, Formatting.Indented));
    private static void Log(string message, string trace, LogType type)
    {
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) errors.Add(message);
    }
}
