using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// UI and scene-entry acceptance only. Test prerequisites are seeded in a temporary
// profile; these checks do not claim that races were driven or won.
[InitializeOnLoad]
public static class FormulaCareerExpansionValidation
{
    private const string Key = "Formula.FullCareer.Validation", Folder = "Docs/AI/FormulaCareerExpansion";
    private static readonly List<object> results = new List<object>();
    private static readonly List<string> checks = new List<string>(), errors = new List<string>();
    private static FormulaCatalog.CareerEvent[] entries;
    private static Gamepad pad;
    private static int phase, index;
    private static double began;
    private static string status;
    static FormulaCareerExpansionValidation()
    {
        EditorApplication.playModeStateChanged += Mode; EditorApplication.update += Tick; Application.logMessageReceived += Log;
    }
    [MenuItem("Tools/Formula Career/Preview and Validate Full Career")]
    public static void Run() => Begin(false);
    [MenuItem("Tools/Formula Career/Preview Full Career Pages")]
    public static void RunPages() => Begin(true);
    private static void Begin(bool pagesOnly)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        SessionState.SetBool(Key + ".PagesOnly", pagesOnly);
        SessionState.SetString(Key + ".Previous", EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Key + ".Background", Application.runInBackground);
        SessionState.SetString(Key + ".Save", Environment.GetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH") ?? "");
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", Path.Combine(Path.GetTempPath(), "FormulaFullCareer-" + Guid.NewGuid().ToString("N"), "save.json"));
        Directory.CreateDirectory(Folder); SessionState.SetBool(Key, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Menu.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void Mode(PlayModeStateChange value)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (value == PlayModeStateChange.EnteredPlayMode)
        {
            results.Clear(); checks.Clear(); errors.Clear(); phase = index = 0; status = "running"; began = EditorApplication.timeSinceStartup;
            entries = FormulaCatalog.Current.careerEvents.GroupBy(e => e.trackId).Select(g => g.First()).ToArray();
            Application.runInBackground = true; pad = InputSystem.AddDevice<Gamepad>();
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus(); Write();
        }
        if (value == PlayModeStateChange.EnteredEditMode)
        {
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad); pad = null;
            string previous = SessionState.GetString(Key + ".Previous", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            previous = SessionState.GetString(Key + ".Save", ""); Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH", string.IsNullOrEmpty(previous) ? null : previous);
            Application.runInBackground = SessionState.GetBool(Key + ".Background", false); SessionState.SetBool(Key, false);
        }
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            double age = EditorApplication.timeSinceStartup - began;
            Require(age < 180, "Full Career acceptance timed out in phase " + phase);
            Require(errors.Count == 0, "Runtime error during Career acceptance");
            if (age < .6) return;
            var menu = UnityEngine.Object.FindFirstObjectByType<FormulaMainMenu>();
            if (phase == 0)
            {
                if (menu == null || menu.CurrentPage != "Acasă") return;
                menu.Show("Carieră"); Require(menu.View.Career.PageCount == 4, "Career needs four pages.");
                var seen = new List<string>(); var initialCards = menu.View.Career.GetComponentsInChildren<FormulaEventCard>(true).Select(c => c.GetInstanceID()).OrderBy(n => n).ToArray();
                for (int p = 0; p < menu.View.Career.PageCount; p++)
                {
                    Require(menu.View.Career.Page == p, "Career pager did not advance.");
                    var cards = menu.View.Career.GetComponentsInChildren<FormulaEventCard>();
                    foreach (var card in cards)
                    {
                        seen.Add(card.EventId); var nav = card.GetComponent<UnityEngine.UI.Button>().navigation;
                        foreach (var target in new[] { nav.selectOnUp, nav.selectOnDown, nav.selectOnLeft, nav.selectOnRight })
                            Require(target != null && target.gameObject.activeInHierarchy, "Career navigation targets an inactive object: " + card.EventId);
                    }
                    Require(cards.Length == (p == 3 ? 4 : 6), "Wrong active card count.");
                    Button(menu.View.Career.transform, "career-next").onClick.Invoke();
                }
                Require(seen.SequenceEqual(FormulaCatalog.Current.careerEvents.Select(e => e.id)), "Career pages omit or repeat event IDs.");
                Require(initialCards.SequenceEqual(menu.View.Career.GetComponentsInChildren<FormulaEventCard>(true).Select(c => c.GetInstanceID()).OrderBy(n => n)), "Paging cloned the card hierarchy.");
                Require(menu.View.Career.Page == 0, "Last Career page did not wrap.");
                Check("22 events are reachable through four authored pages; hidden slots cannot receive focus and card instances are reused");
                Capture("Career-Opening"); InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.RightShoulder)); Next(1);
            }
            else if (phase == 1)
            {
                Require(menu.View.Career.Page == 1, "Controller shoulder did not change Career page.");
                InputSystem.QueueStateEvent(pad, new GamepadState()); Check("Gamepad shoulder changes Career pages through production input");
                Capture("Career-Expansion"); Next(10);
            }
            else if (phase == 10)
            {
                Button(menu.View.Career.transform, "g07").onClick.Invoke();
                Require(menu.View.Dialogs.IsOpen && !Button(menu.View.Dialogs.transform, "confirm").interactable, "A fresh profile can start a locked expansion event.");
                Require(menu.View.Dialogs.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("Generic Finale")), "Expansion briefing omits the missing Bronze prerequisite.");
                Capture("Career-Locked"); Next(11);
            }
            else if (phase == 11)
            {
                menu.Back();
                Require(EventSystem.current.currentSelectedGameObject.name == "g07", "Closing the briefing lost card focus.");
                Require(FormulaProgression.Data.money == 0 && FormulaProgression.Data.exp == 0, "Inspecting Career changed the wallet.");
                Check("Locked expansion details show their prerequisite and restore the selected card without charging the player");
                Require(FormulaProgression.Commit(d =>
                {
                    foreach (var entry in FormulaCatalog.Current.careerEvents.Take(21)) d.formula.medals[entry.id] = Medal.Bronze;
                    d.formula.bestLapSeconds["g22"] = 87.654;
                    return null;
                }, out string error), error);
                menu.Show("Profil"); Require(menu.View.Profile.PageCount == 4, "Profile needs four pages.");
                for (int p = 0; p < 4; p++)
                {
                    Require(menu.View.Profile.Page == p, "Profile pager did not advance.");
                    Button(menu.View.Profile.transform, "profile-next").onClick.Invoke();
                }
                menu.View.Profile.SetPage(3);
                Require(menu.View.Profile.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("21/22 EVENTS COMPLETE")), "Profile total is still hard-coded to six events.");
                Require(menu.View.Profile.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("01:27.654")), "Final-event record is absent from Profile.");
                Check("Profile pages expose all 22 medals and saved lap records with an accurate total"); Next(111);
            }
            else if (phase == 111)
            {
                Capture("Profile-Expansion"); Next(12);
            }
            else if (phase == 12)
            {
                menu.Show("Carieră"); menu.View.Career.ShowEvent("g22"); Next(121);
            }
            else if (phase == 121)
            {
                Capture("Career-Final-Page"); Next(SessionState.GetBool(Key + ".PagesOnly", false) ? 5 : 2);
            }
            else if (phase == 2)
            {
                // Exercise the actual card and confirmation for the first scene.
                menu.View.Career.ShowEvent(entries[0].id); Button(menu.View.Career.transform, entries[0].id).onClick.Invoke();
                Require(Button(menu.View.Dialogs.transform, "confirm").interactable, "Seeded Career entry cannot start.");
                Button(menu.View.Dialogs.transform, "confirm").onClick.Invoke(); Next(3);
            }
            else if (phase == 3)
            {
                var game = UnityEngine.Object.FindFirstObjectByType<GamePlayManager>(); var entry = entries[index];
                if (game == null || game.CarController == null || !game.FormulaRaceStarted || game.GetComponent<FormulaCircuit>()?.trackId != entry.trackId || game.FormulaElapsed < (index == entries.Length - 1 ? 4 : 1)) return;
                Require(FormulaProgression.State.activeMode == "career" && FormulaSession.CurrentEvent.id == entry.id, "Scene entered a free session instead of Career.");
                Require(game.gameObject.scene.path == FormulaAssets.Current.FindTrack(entry.trackId).scenePath && Application.CanStreamedLevelBeLoaded(game.gameObject.scene.path), "Career loaded the wrong build scene.");
                Require(game.totalRaceLaps == entry.laps && game.aiRacers.Length == entry.opponentCount, "Career laps or AI count differ from its briefing.");
                Require(game.CarController.GetComponent<FormulaVehicle>().IsReady, "Career car did not initialize.");
                Require(game.aiRacers.All(r => r.aiDriver != null && r.aiDriver.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>().isOnNavMesh), "Career AI is not attached to road navigation.");
                Require(game.FormulaCompletedLaps == 0, "A lap was awarded at race start.");
                results.Add(new { entry.id, entry.trackId, scene = game.gameObject.scene.path, laps = game.totalRaceLaps, opponents = game.aiRacers.Length, mode = FormulaProgression.State.activeMode, elapsed = game.FormulaElapsed }); Write();
                index++;
                if (index < entries.Length) { string error = FormulaSession.Launch(entries[index].id, "career"); Require(error == null, error); began = EditorApplication.timeSinceStartup; }
                else { Capture("Career-Finale-Gameplay"); Next(30); }
            }
            else if (phase == 30)
            {
                FormulaSession.ReviewEvent("g22"); Next(4);
            }
            else if (phase == 4)
            {
                if (menu == null || menu.CurrentPage != "Carieră" || !menu.View.Dialogs.IsOpen) return;
                Require(menu.View.Career.Page == 3, "Returning from Results opened the wrong Career page.");
                Require(menu.View.Dialogs.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "SEASON FINALE"), "Returning from gameplay lost the reviewed event.");
                menu.Back(); Require(EventSystem.current.currentSelectedGameObject.name == "g22", "Return from gameplay did not restore the final event card.");
                Check("All 18 scenes launch in Career with the authored laps and AI; returning to review event 22 restores page 4 and card focus");
                Capture("Career-Return"); Next(5);
            }
            else if (phase == 5) { status = "passed"; Write(); EditorApplication.ExitPlaymode(); }
        }
        catch (Exception failure) { errors.Add(failure.Message); status = "failed"; Write(); EditorApplication.ExitPlaymode(); }
    }
    private static UnityEngine.UI.Button Button(Transform parent, string name) => parent.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == name);
    private static void Capture(string name) { Canvas.ForceUpdateCanvases(); ScreenCapture.CaptureScreenshot(Folder + "/" + name + ".png"); }
    private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    private static void Next(int next) { phase = next; began = EditorApplication.timeSinceStartup; Write(); }
    private static void Check(string value) { checks.Add(value); Write(); }
    private static void Log(string message, string trace, LogType type) { if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying && (type == LogType.Error || type == LogType.Exception)) errors.Add(message); }
    private static void Write() => File.WriteAllText(Folder + (SessionState.GetBool(Key + ".PagesOnly", false) ? "/PageValidation.json" : "/PlayValidation.json"), JsonConvert.SerializeObject(new { status, unity = Application.unityVersion, phase, isolatedSave = true, method = "Normal Career scene entry, UI paging and controller input. Bronze prerequisites seeded only in an isolated test profile. No race completion, driving-quality or platform-performance claim.", checks, results, errors }, Formatting.Indented));
}
