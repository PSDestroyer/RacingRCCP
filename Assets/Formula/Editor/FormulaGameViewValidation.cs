using System;
using System.Reflection;
using FormulaGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Uses an existing Editor resolution preset and restores the user's selection afterwards.
public static class FormulaGameViewValidation
{
    private const string Previous = "Formula.Experience.PreviousGameViewSize";
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;

    [MenuItem("Tools/Validate Display Aspect Layout")]
    public static void ValidateDisplayAspects()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var menu = (RectTransform)LoadUi(scene, FormulaMenuHierarchyBuilder.PrefabPath).transform;
            var menuPresentation = menu.GetComponent<FormulaPresentation>();
            var raceCanvas = new GameObject("Validation Canvas", typeof(RectTransform)).GetComponent<RectTransform>();
            SceneManager.MoveGameObjectToScene(raceCanvas.gameObject, scene);
            var racePresentation = raceCanvas.gameObject.AddComponent<FormulaPresentation>();
            var race = LoadUi(scene, "Assets/Formula/Resources/Formula/UI/FormulaRace.prefab", raceCanvas).transform;
            var loading = LoadUi(scene, "Assets/Formula/Resources/Formula/UI/FormulaLoading.prefab", raceCanvas).transform;
            typeof(FormulaPresentation).GetMethod("Awake", Flags).Invoke(menuPresentation, null);
            typeof(FormulaPresentation).GetMethod("Awake", Flags).Invoke(racePresentation, null);
            var capture = typeof(FormulaPresentation).GetMethod("Capture", Flags);
            var layout = typeof(FormulaPresentation).GetMethod("Layout", Flags);

            // Results are registered after the HUD has already been fitted to the viewport.
            layout.Invoke(racePresentation, new object[] { new Rect(0, 0, 3440, 1440), new Vector2(3440, 1440), 1f });
            var results = LoadUi(scene, "Assets/Formula/Resources/Formula/UI/FormulaResults.prefab", raceCanvas);
            capture.Invoke(racePresentation, new object[] { results.transform });
            foreach (var size in new[] { new Vector2(3440, 1440), new Vector2(1280, 720), new Vector2(1280, 800),
                new Vector2(1024, 768), new Vector2(5120, 1440), new Vector2(720, 1280), new Vector2(1280, 720) })
            {
                foreach (float inset in new[] { 1f, .95f })
                {
                    var safe = inset < 1 ? new Rect(20, 10, size.x - 50, size.y - 35) : new Rect(Vector2.zero, size);
                    layout.Invoke(menuPresentation, new object[] { safe, size, inset });
                    layout.Invoke(racePresentation, new object[] { safe, size, inset });
                    Covers(menu, menu.Find("Header/Formula Header Shade"), size, true, false);
                    Covers(menu, menu.Find("Footer/Formula Footer Shade"), size, false, true);
                    Covers(menu, menu.Find("Screens/Career/Figma Background"), size, true, true);
                    Covers(menu, menu.Find("Dialogs"), size, true, true);
                    Covers(raceCanvas, race.Find("Pause"), size, true, true);
                    Covers(raceCanvas, race.Find("Audio Options"), size, true, true);
                    Covers(raceCanvas, race.Find("Save Error"), size, true, true);
                    Covers(raceCanvas, loading.Find("Backdrop"), size, true, true);
                    Covers(raceCanvas, results.transform, size, true, true);
                    Inside(menu, menu.Find("Screens/Home/Navigation"), size, safe);
                    Inside(raceCanvas, race.Find("HUD/Session"), size, safe);
                    Inside(raceCanvas, race.Find("HUD/Vehicle"), size, safe);
                    Inside(raceCanvas, race.Find("HUD/Minimap"), size, safe);
                    Inside(raceCanvas, loading.Find("Backdrop/Progress Bar"), size, safe);
                    Inside(raceCanvas, results.transform.Find("retry"), size, safe);
                }
            }
            Debug.Log("FORMULA_DISPLAY_ASPECT_LAYOUT_PASSED: menu, HUD, dialogs, results and loading.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static GameObject LoadUi(Scene scene, string path, Transform parent = null)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        if (parent != null) instance.transform.SetParent(parent, false);
        return instance;
    }

    private static Rect DisplayBounds(RectTransform root, Transform target, Vector2 display)
    {
        if (!(target is RectTransform rect)) throw new InvalidOperationException("Missing authored UI rectangle.");
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2 origin = new Vector2(root.rect.xMin, root.rect.yMax - display.y);
        Vector2 min = (Vector2)root.InverseTransformPoint(corners[0]) - origin;
        Vector2 max = (Vector2)root.InverseTransformPoint(corners[2]) - origin;
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static void Covers(RectTransform root, Transform target, Vector2 display, bool top, bool bottom)
    {
        Rect bounds = DisplayBounds(root, target, display);
        if (bounds.xMin > .1f || bounds.xMax < display.x - .1f ||
            top && bounds.yMax < display.y - .1f || bottom && bounds.yMin > .1f)
            throw new InvalidOperationException($"{target.name} leaves a gap at {display}: {bounds}.");
    }

    private static void Inside(RectTransform root, Transform target, Vector2 display, Rect safe)
    {
        Rect bounds = DisplayBounds(root, target, display);
        if (bounds.xMin < safe.xMin - .1f || bounds.xMax > safe.xMax + .1f ||
            bounds.yMin < safe.yMin - .1f || bounds.yMax > safe.yMax + .1f)
            throw new InvalidOperationException($"{target.name} is outside the safe area at {display}: {bounds}.");
    }

    public static void Select1080()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var assembly = typeof(Editor).Assembly;
        var gameView = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        var selected = gameView.GetType().GetProperty("selectedSizeIndex", Flags);
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = sizesType.GetProperty("instance", Flags).GetValue(null);
        var groupType = sizesType.GetProperty("currentGroupType", Flags).GetValue(sizes);
        var group = sizesType.GetMethod("GetGroup", Flags).Invoke(sizes, new[] { groupType });
        int count = (int)group.GetType().GetMethod("GetTotalCount", Flags).Invoke(group, null);
        for (int i = 0; i < count; i++)
        {
            var size = group.GetType().GetMethod("GetGameViewSize", Flags).Invoke(group, new object[] { i });
            int width = (int)size.GetType().GetProperty("width", Flags).GetValue(size), height = (int)size.GetType().GetProperty("height", Flags).GetValue(size);
            if (width != 1920 || height != 1080) continue;
            SessionState.SetInt(Previous, (int)selected.GetValue(gameView)); selected.SetValue(gameView, i); gameView.Focus(); return;
        }
        throw new InvalidOperationException("No existing 1920 × 1080 Game View preset is available.");
    }
    public static void Restore()
    {
        int previous = SessionState.GetInt(Previous, -1); if (previous < 0) return;
        var type = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        type.GetProperty("selectedSizeIndex", Flags).SetValue(EditorWindow.GetWindow(type), previous);
        SessionState.EraseInt(Previous);
    }
}
