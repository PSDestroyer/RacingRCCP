using System;
using System.IO;
using System.Linq;
using FormulaGame;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Refines the existing authored prefab in place, preserving its controls and serialized references.
public static class FormulaMenuPremiumBuilder
{
    private const string Folder = "Assets/Formula/Resources/Formula/UI/";
    private static readonly Color Ink = new Color32(15, 2, 7, 245);
    private static readonly Action NoAction = () => { };
    [MenuItem("Tools/Formula Career/Refine Formula UX and UI")]
    public static void Refine()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var original = SceneManager.GetActiveScene();
        foreach (string path in new[] { "Assets/Scenes/Menu.unity", FormulaGarageBuilder.GarageScene })
        {
            var scene = SceneManager.GetSceneByPath(path);
            if (scene.IsValid() && scene.isDirty) throw new InvalidOperationException("Save scene changes first: " + path);
        }
        Directory.CreateDirectory("Docs/AI/FormulaUI/BeforePremium");
        string backup = "Docs/AI/FormulaUI/BeforePremium/Formula_Menu.prefab.txt";
        if (!File.Exists(backup)) File.Copy(FormulaMenuHierarchyBuilder.PrefabPath, backup);
        RefinePrefab();
        RefineStage();
        foreach (string path in new[] { "Assets/Scenes/Menu.unity", FormulaGarageBuilder.GarageScene })
        {
            FormulaMenuHierarchyBuilder.Install(path);
            RefineScene(path);
        }
        AssetDatabase.SaveAssets();
        if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
        FormulaMenuHierarchyBuilder.Validate(); Debug.Log("FORMULA_PREMIUM_UX_UI_APPLIED");
    }
    private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var s = new SerializedObject(target); s.FindProperty(field).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Rect(Transform t, float x, float y, float w, float h)
    {
        var r = (RectTransform)t; r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
    }
    private static TMP_Text Text(Transform p, string name, string text, float x, float y, float w, float h, int size, bool title = false, Color? color = null)
    {
        var t = p.Find(name)?.GetComponent<TMP_Text>();
        if (t == null) t = title ? FormulaMenuStyle.Title(p, text, x, y, w, h, size, color) : FormulaMenuStyle.Text(p, text, x, y, w, h, size, color);
        t.name = name; t.text = FormulaEnglishBuilder.Translate(text); Rect(t.transform, x, y, w, h); Font(t, size, title, color ?? Color.white); return t;
    }
    private static void Font(TMP_Text t, int size, bool title = false, Color? color = null)
    {
        t.font = title ? FormulaMenuAssets.Current.titleFont : FormulaMenuAssets.Current.strongFont;
        t.fontSize = t.fontSizeMax = size; t.fontSizeMin = size * .85f; t.enableAutoSizing = !title;
        // Teko's ascender/descender metrics exceed its visible capital height.
        // Ellipsis treats compact title slots as a whole-line overflow and hides them.
        t.color = color ?? Color.white; t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = title ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
    }
    private static UnityEngine.UI.Button NewButton(Transform p, string id, string text, float x, float y, float w, float h)
    {
        var b = p.Find(id)?.GetComponent<UnityEngine.UI.Button>() ?? FormulaMenuStyle.Button(p, id, text, x, y, w, h, NoAction);
        MoveButton(b, x, y, w, h); b.GetComponentInChildren<TMP_Text>(true).text = FormulaEnglishBuilder.Translate(text); return b;
    }
    private static void MoveButton(UnityEngine.UI.Button button, float x, float y, float w, float h, bool primary = false)
    {
        Rect(button.transform, x, y, w, h);
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) { Rect(label.transform, 18, 4, w - 36, h - 8); Font(label, h >= 58 ? 26 : 21); label.verticalAlignment = VerticalAlignmentOptions.Middle; }
        var icon = button.transform.Find("Figma Icon");
        if (icon != null)
        {
            float iw = ((RectTransform)icon).sizeDelta.x, ih = ((RectTransform)icon).sizeDelta.y;
            Rect(icon, 18, (h - ih) * .5f, iw, ih); if (label != null) Rect(label.transform, w < 220 ? 56 : 84, 4, w - (w < 220 ? 68 : 98), h - 8);
        }
        var image = button.GetComponent<UnityEngine.UI.Image>(); image.color = Color.white;
        var c = button.colors; c.normalColor = primary ? FormulaUI.Red : new Color32(18, 2, 8, 226); c.highlightedColor = new Color32(85, 8, 26, 250); c.selectedColor = primary ? new Color32(227, 20, 52, 255) : new Color32(87, 7, 25, 245); c.pressedColor = new Color32(160, 8, 30, 255); c.disabledColor = new Color32(20, 3, 9, 220); button.colors = c;
        foreach (Transform child in button.transform)
        {
            if (child.name == "Focus Top") Rect(child, 8, 0, w - 16, 1);
            else if (child.name == "Focus Bottom") Rect(child, 8, h - 1, w - 16, 1);
            else if (child.name == "Focus Left") Rect(child, 0, 8, 2, h - 16);
            else if (child.name == "Focus Right") Rect(child, w - 2, 8, 1, h - 16);
            else if (child.name == "Separator") Rect(child, 0, 0, w, 1);
            else if (child.name == "ActivePage") Rect(child, 0, h - 3, w, 3);
        }
    }
    private static RectTransform Panel(Transform p, string name, float x, float y, float w, float h, Color? color = null)
    {
        var t = p.Find(name) as RectTransform;
        if (t == null) t = FormulaMenuStyle.Surface(p, name, x, y, w, h, color ?? Ink);
        Rect(t, x, y, w, h); var image = t.GetComponent<UnityEngine.UI.Image>(); if (image != null) image.color = color ?? Ink; return t;
    }
    private static FormulaTrackMap Map(Transform p, string track, float x, float y, float w, float h)
    {
        var t = p.Find("Circuit Outline") as RectTransform ?? FormulaUI.Rect(p, "Circuit Outline", x, y, w, h); Rect(t, x, y, w, h);
        var map = t.GetComponent<FormulaTrackMap>() ?? t.gameObject.AddComponent<FormulaTrackMap>(); map.color = new Color(1, 1, 1, .8f); map.raycastTarget = false; map.SetTrack(track); return map;
    }
    private static void PrepareArt()
    {
        var art = FormulaMenuAssets.Current;
        if (art.strongFont == null)
        {
            const string path = Folder + "Rajdhani-SemiBold.asset";
            art.strongFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (art.strongFont == null)
            {
                var font = AssetDatabase.LoadAssetAtPath<UnityEngine.Font>("Assets/_GarageV2/UI/Fonts/Rajdhani/Rajdhani-SemiBold.ttf");
                var f = TMP_FontAsset.CreateFontAsset(font, 64, 8, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
                f.name = "Rajdhani SemiBold";
                f.TryAddCharacters(new string(Enumerable.Range(32, 95).Select(i => (char)i).ToArray()) + "ăâîșțĂÂÎȘȚşţŞŢ→←−×·≤–—…", out string missing);
                AssetDatabase.CreateAsset(f, path); AssetDatabase.AddObjectToAsset(f.material, f); foreach (var texture in f.atlasTextures) AssetDatabase.AddObjectToAsset(texture, f); art.strongFont = f;
            }
        }
        if (art.trackMaps == null || art.trackMaps.Length != 2)
        {
            art.trackMaps = FormulaAssets.Current.tracks.Select(track =>
            {
                var scene = SceneManager.GetSceneByPath(track.scenePath); bool loaded = scene.IsValid() && scene.isLoaded;
                if (!loaded) scene = EditorSceneManager.OpenScene(track.scenePath, OpenSceneMode.Additive);
                try
                {
                    var route = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RCCP_AIWaypointsContainer>(true)).First();
                    int stride = Mathf.Max(1, Mathf.CeilToInt(route.waypoints.Count / 180f));
                    var p = route.waypoints.Where((_, i) => i % stride == 0).Select(w => new Vector2(w.transform.position.x, w.transform.position.z)).ToArray();
                    float xMin = p.Min(v => v.x), xMax = p.Max(v => v.x), yMin = p.Min(v => v.y), yMax = p.Max(v => v.y);
                    float width = xMax - xMin, height = yMax - yMin, span = Mathf.Max(width, height * 1.8f);
                    return new FormulaMenuAssets.TrackGraphic { id = track.id, points = p.Select(v => new Vector2((v.x - xMin - width * .5f) / span + .5f, (v.y - yMin - height * .5f) / (span / 1.8f) + .5f)).ToArray() };
                }
                finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
            }).ToArray();
        }
        EditorUtility.SetDirty(art); AssetDatabase.SaveAssets();
    }
    public static void RefinePrefab()
    {
        PrepareArt(); var root = PrefabUtility.LoadPrefabContents(FormulaMenuHierarchyBuilder.PrefabPath);
        try
        {
            var view = root.GetComponent<FormulaMenuView>();
            if (view.UpgradeTheme) { FormulaMenuUnifiedBuilder.Style(view); PrefabUtility.SaveAsPrefabAsset(root, FormulaMenuHierarchyBuilder.PrefabPath); return; }
            foreach (var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                var r = (RectTransform)button.transform; MoveButton(button, r.anchoredPosition.x, -r.anchoredPosition.y, r.sizeDelta.x, r.sizeDelta.y);
            }
            var screens = root.transform.Find("Screens"); Rect(screens, 32, 104, 1216, 520);
            Header(root.transform, view); Home(view.Home); Cards(view.Career.transform, false); Cards(view.Quick.transform, true); Garage(view.Garage); Settings(view.Settings); Briefing(view.Dialogs);
            var footer = root.transform.Find("Footer"); Rect(footer, 0, 648, 1280, 72); footer.GetComponent<UnityEngine.UI.Image>().color = Ink;
            var title = footer.Find("Page Title").GetComponent<TMP_Text>(); Rect(title.transform, 40, 9, 440, 50); Font(title, 42, true);
            Rect(footer.Find("Title Rule"), 480, 39, 225, 1); Rect(footer.Find("Input Hints"), 750, 12, 330, 48); Font(footer.Find("Input Hints").GetComponent<TMP_Text>(), 17);
            MoveButton(footer.Find("nav-0").GetComponent<UnityEngine.UI.Button>(), 1090, 14, 150, 44);
            var shade = root.transform.Find("Screen Shade") as RectTransform ?? FormulaUI.Box(root.transform, "Screen Shade", 0, 84, 1280, 564, Color.clear);
            shade.SetAsLastSibling(); shade.SetSiblingIndex(screens.GetSiblingIndex()); Set(view, "screenShade", shade.GetComponent<UnityEngine.UI.Image>());
            FormulaEnglishBuilder.TranslateHierarchy(root);
            PrefabUtility.SaveAsPrefabAsset(root, FormulaMenuHierarchyBuilder.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void Header(Transform root, FormulaMenuView view)
    {
        var top = root.Find("Header"); Rect(top, 0, 0, 1280, 84); top.GetComponent<UnityEngine.UI.Image>().color = new Color32(12, 1, 5, 234);
        Rect(top.Find("Brand Stripe"), 24, 19, 3, 40);
        var brand = top.Find("Brand").GetComponent<TMP_Text>(); Rect(brand.transform, 38, -4, 126, 80); brand.text = "F27"; Font(brand, 82, true, FormulaUI.Red); brand.fontStyle = FontStyles.Italic;
        var season = top.Find("Season"); Rect(season, 167, 14, 230, 32); season.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
        var year = season.Find("Year").GetComponent<TMP_Text>(); Rect(year.transform, 0, 0, 225, 33); year.text = "FORMULA 2027"; Font(year, 29, true);
        var subtitle = top.Find("Subtitle").GetComponent<TMP_Text>(); Rect(subtitle.transform, 170, 46, 230, 23); Font(subtitle, 16, false, FormulaUI.Accent);
        var account = top.Find("Driver and Wallet"); Rect(account, 816, 12, 416, 58); account.GetComponent<UnityEngine.UI.Image>().color = new Color32(26, 2, 9, 180);
        Rect(account.Find("Helmet Icon"), 6, 6, 44, 42);
        var wallet = account.Find("Wallet").GetComponent<TMP_Text>(); Rect(wallet.transform, 254, 7, 144, 44); Font(wallet, 28, false); wallet.text = "0 <size=16>CR</size>"; wallet.alignment = TextAlignmentOptions.MidlineRight;
        Set(view, "driver", Text(account, "Driver", "Pilot / NIVEL 1", 59, 5, 205, 30, 20));
        var track = Panel(account, "XP Track", 59, 40, 168, 3, new Color32(80, 8, 27, 255));
        var fill = Panel(track, "XP Progress", 0, 0, 168, 3, FormulaUI.Accent).GetComponent<UnityEngine.UI.Image>(); fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; fill.fillAmount = 0; Set(view, "xpFill", fill);
    }
    [MenuItem("Tools/Formula Career/Simplify Home")]
    public static void SimplifyHome()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit Play Mode first.");
        var root = PrefabUtility.LoadPrefabContents(FormulaMenuHierarchyBuilder.PrefabPath);
        try
        {
            var home = root.GetComponentInChildren<FormulaHomeView>(true).transform;
            foreach (string name in new[] { "Next Stage", "Next Event", "Event Objective", "Current Vehicle" })
            {
                var item = home.Find(name);
                if (item != null) UnityEngine.Object.DestroyImmediate(item.gameObject);
            }
            PrefabUtility.SaveAsPrefabAsset(root, FormulaMenuHierarchyBuilder.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("FORMULA_HOME_SIMPLIFIED: removed event details and vehicle/reward caption from Home.");
    }
    [MenuItem("Tools/Formula Career/Arrange Home Navigation")]
    public static void ArrangeHomeNavigation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var root = PrefabUtility.LoadPrefabContents(FormulaMenuHierarchyBuilder.PrefabPath);
        try
        {
            Home(root.GetComponentInChildren<FormulaHomeView>(true));
            PrefabUtility.SaveAsPrefabAsset(root, FormulaMenuHierarchyBuilder.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("FORMULA_HOME_NAVIGATION: one Career entry, aligned navigation.");
    }
    private static void Home(FormulaHomeView view)
    {
        if (view.GetComponentInParent<FormulaMenuView>().UpgradeTheme) { FormulaMenuUnifiedBuilder.StyleHome(view); return; }
        var nav = view.transform.Find("Navigation");
        var previous = nav.Find("continue-career");
        if (previous != null)
        {
            var duplicate = nav.Find("home-career");
            if (duplicate != null) UnityEngine.Object.DestroyImmediate(duplicate.gameObject);
            previous.name = "home-career";
        }
        var career = nav.Find("home-career").GetComponent<UnityEngine.UI.Button>();
        Set(view, "career", career); career.GetComponentInChildren<TMP_Text>(true).text = "CAREER";
        MoveButton(career, 12, 60, 360, 64, true);
        MoveButton(nav.Find("home-garage").GetComponent<UnityEngine.UI.Button>(), 12, 136, 360, 60);
        var development = nav.Find("home-development"); if (development != null) MoveButton(development.GetComponent<UnityEngine.UI.Button>(), 12, 208, 360, 60);
        MoveButton(nav.Find("home-quick").GetComponent<UnityEngine.UI.Button>(), 12, 280, 360, 60);
        MoveButton(nav.Find("home-profile").GetComponent<UnityEngine.UI.Button>(), 12, 364, 174, 48);
        MoveButton(nav.Find("home-settings").GetComponent<UnityEngine.UI.Button>(), 198, 364, 174, 48);
        foreach (var button in nav.GetComponentsInChildren<UnityEngine.UI.Button>(true)) StyleHomeButton(button);
    }
    private static void StyleHomeButton(UnityEngine.UI.Button button)
    {
        var root = (RectTransform)button.transform; float w = root.sizeDelta.x, h = root.sizeDelta.y;
        var plate = button.GetComponent<UnityEngine.UI.Image>(); plate.sprite = null; plate.type = UnityEngine.UI.Image.Type.Simple;
        button.transition = UnityEngine.UI.Selectable.Transition.None;
        foreach (string name in new[] { "Focus Top", "Focus Bottom", "Focus Left", "Focus Right", "Separator" })
        {
            var old = root.Find(name); if (old != null) old.gameObject.SetActive(false);
        }
        var label = button.GetComponentInChildren<TMP_Text>(true);
        Font(label, w > 220 ? 27 : 21); label.characterSpacing = w > 220 ? 1.5f : .5f;
        Rect(label.transform, w > 220 ? 78 : 55, 4, w > 220 ? w - 122 : w - 64, h - 8);
        var rail = Panel(root, "Selection Rail", 0, 0, 4, h, FormulaUI.Red).GetComponent<UnityEngine.UI.Image>(); rail.raycastTarget = false; rail.sprite = null; rail.type = UnityEngine.UI.Image.Type.Simple;
        var marker = Panel(root, "Selection Underline", 4, h - 2, w - 4, 2, Color.clear).GetComponent<UnityEngine.UI.Image>(); marker.raycastTarget = false; marker.sprite = null; marker.type = UnityEngine.UI.Image.Type.Simple;
        var arrow = Text(root, "Selection Arrow", w > 220 ? "→" : "", w - 40, 4, 28, h - 8, 25); arrow.raycastTarget = false; arrow.verticalAlignment = VerticalAlignmentOptions.Middle;
        var icon = root.Find("Figma Icon")?.GetComponent<UnityEngine.UI.RawImage>();
        var focus = button.GetComponent<FormulaMenuFocus>(); focus.Initialize(marker); focus.StyleHome(plate, label, icon, rail, arrow);
        EditorUtility.SetDirty(focus);
    }
    [MenuItem("Tools/Formula Career/Arrange Career Cards")]
    public static void ArrangeCareerCards()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var root = PrefabUtility.LoadPrefabContents(FormulaMenuHierarchyBuilder.PrefabPath);
        try { FormulaCareerFigmaBuilder.Style(root.GetComponentInChildren<FormulaCareerView>(true)); PrefabUtility.SaveAsPrefabAsset(root, FormulaMenuHierarchyBuilder.PrefabPath); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }
    private static void Cards(Transform root, bool quick)
    {
        if (!quick) { FormulaCareerFigmaBuilder.Style(root.GetComponent<FormulaCareerView>()); return; }
        foreach (var card in root.GetComponentsInChildren<FormulaEventCard>(true))
        {
            var e = FormulaCatalog.Current.Event(card.EventId); var t = card.transform;
            Map(t, e.trackId, 388, 14, 174, 84); Rect(t.Find("Mode"), 20, 12, 364, 44); Font(t.Find("Mode").GetComponent<TMP_Text>(), 36, true);
            Rect(t.Find("Circuit"), 20, 61, 350, 31); Font(t.Find("Circuit").GetComponent<TMP_Text>(), 22);
            Rect(t.Find("Reward Rule"), 20, 110, 550, 28); Font(t.Find("Reward Rule").GetComponent<TMP_Text>(), 17, false, FormulaUI.Accent);
        }
        Text(root, "Career Policy", "ANTRENAMENT LIBER  /  FĂRĂ IMPACT ASUPRA PROGRESULUI ÎN CARIERĂ", 0, 506, 1200, 28, 18, false, FormulaUI.Accent);
    }
    private static void Garage(FormulaGarageView view)
    {
        var root = view.transform;
        foreach (var card in root.GetComponentsInChildren<FormulaDevelopmentCard>(true))
        {
            var r = (RectTransform)card.transform; int index = card.transform.GetSiblingIndex(); Rect(r, 0, 62 + index * 110, 360, 104);
            var branch = r.Find("Branch").GetComponent<TMP_Text>(); Rect(branch.transform, 16, 3, 220, 34); Font(branch, 31, true);
            var equip = card.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name.StartsWith("equip-")); MoveButton(equip, 232, 5, 114, 34);
            var buy = card.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name.StartsWith("buy-")); MoveButton(buy, 10, 60, 340, 38);
            Set(card, "effect", Text(r, "Next Level Effect", "480 → 494.4 Nm", 16, 34, 328, 24, 21));
            var bars = r.Find("Development Levels"); Rect(bars, 16, 100, 328, 3);
            for (int i = 0; i < bars.childCount; i++) Rect(bars.GetChild(i), i * 110, 0, 104, 3);
        }
        foreach (var stats in root.GetComponentsInChildren<FormulaStatisticsView>(true)) Statistics(stats);
        var setupCards = root.Find("Setup/Setup Cards");
        for (int i = 0; i < setupCards.childCount; i++)
        {
            var button = setupCards.GetChild(i).GetComponent<UnityEngine.UI.Button>(); MoveButton(button, 0, 62 + i * 138, 360, 126);
            var label = button.GetComponentInChildren<TMP_Text>(true); Rect(label.transform, 18, 13, 324, 99); Font(label, 23); label.textWrappingMode = TextWrappingModes.Normal;
        }
        var details = root.Find("Livree/Livery Details"); Rect(details, 0, 64, 360, 272); details.GetComponent<UnityEngine.UI.Image>().color = Ink;
        Rect(details.Find("Category"), 18, 13, 324, 25); Font(details.Find("Category").GetComponent<TMP_Text>(), 17, false, FormulaUI.Accent);
        Rect(details.Find("Livery Name"), 18, 46, 324, 59); Font(details.Find("Livery Name").GetComponent<TMP_Text>(), 45, true);
        Rect(details.Find("Ownership"), 18, 111, 324, 28); Font(details.Find("Ownership").GetComponent<TMP_Text>(), 20, false, FormulaUI.Accent);
        Rect(details.Find("Unlock Condition"), 18, 148, 324, 52); Font(details.Find("Unlock Condition").GetComponent<TMP_Text>(), 21); details.Find("Unlock Condition").GetComponent<TMP_Text>().textWrappingMode = TextWrappingModes.Normal;
        MoveButton(details.Find("livery-apply").GetComponent<UnityEngine.UI.Button>(), 18, 211, 324, 46, true);
        foreach (var card in root.GetComponentsInChildren<FormulaLiveryCard>(true))
        {
            int index = card.transform.GetSiblingIndex(); MoveButton(card.GetComponent<UnityEngine.UI.Button>(), index * 153, 386, 145, 128);
            Rect(card.transform.Find("Livery Name"), 9, 3, 127, 30); Font(card.transform.Find("Livery Name").GetComponent<TMP_Text>(), 24, true);
            var thumb = card.transform.Find("Generic Livery Thumbnail").GetComponent<UnityEngine.UI.RawImage>(); Rect(thumb.transform, 5, 34, 135, 68.6f); thumb.uvRect = new Rect(.14f, .14f, .72f, .72f);
            Rect(card.transform.Find("Ownership"), 9, 104, 127, 23); Font(card.transform.Find("Ownership").GetComponent<TMP_Text>(), 17, false, FormulaUI.Accent);
        }
        if (view.Upgrade != null) FormulaUpgradeFigmaBuilder.Style(view);
    }
    private static void Statistics(FormulaStatisticsView view)
    {
        var root = view.transform; Rect(root, 420, 396, 780, 114); root.GetComponent<UnityEngine.UI.Image>().color = new Color32(12, 1, 5, 230);
        Rect(root.Find("Heading"), 0, -28, 600, 24); Font(root.Find("Heading").GetComponent<TMP_Text>(), 17);
        string[] fields = { "torque", "brakes", "spring", "aero" }, ids = { "engine", "brakes", "suspension", "aero" }, names = { "Cuplu motor", "Cuplu frână față", "Arc suspensie", "Coef. apăsare aero" };
        var s = new SerializedObject(view); var development = s.FindProperty("development");
        for (int i = 0; i < 4; i++)
        {
            float x = 18 + i * 192; var label = root.Find("Label " + names[i]).GetComponent<TMP_Text>(); Rect(label.transform, x, 10, 178, 24); Font(label, 16, false, FormulaUI.Accent);
            development.GetArrayElementAtIndex(i).FindPropertyRelative("label").objectReferenceValue = label;
            var value = s.FindProperty(fields[i]).objectReferenceValue as TMP_Text; Rect(value.transform, x, 32, 178, 46); Font(value, i == 2 ? 32 : 38, true); value.alignment = TextAlignmentOptions.MidlineLeft;
            var bars = root.Find("Stats-" + ids[i]); Rect(bars, x, 77, 174, 4); for (int j = 0; j < bars.childCount; j++) Rect(bars.GetChild(j), j * 58, 0, 52, 4);
        }
        s.ApplyModifiedPropertiesWithoutUndo(); Rect(root.Find("Drag and Mass"), 18, 87, 744, 24); Font(root.Find("Drag and Mass").GetComponent<TMP_Text>(), 16);
    }
    private static void Settings(FormulaSettingsView view)
    {
        var overview = view.transform.Find("Assists"); var heading = overview.Find("Assist Information"); heading.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
        var information = heading.Find("Information").GetComponent<TMP_Text>(); Rect(information.transform, 0, 5, 590, 50); information.text = "CONTROLUL MAȘINII"; Font(information, 36, true);
        Text(heading, "Assistance Policy", "Asistențe gratuite · Aceleași recompense", 620, 11, 580, 38, 21, false, FormulaUI.Accent);
        string[] ids = { "abs", "tcs", "esp" }, descriptions = { "Previne blocarea roților la frânare", "Limitează patinarea la accelerare", "Ajută la stabilizarea mașinii în viraje" };
        for (int i = 0; i < 3; i++)
        {
            var b = overview.Find("assist-" + ids[i]).GetComponent<UnityEngine.UI.Button>(); MoveButton(b, 0, 86 + i * 108, 566, 94);
            var label = b.GetComponentInChildren<TMP_Text>(true); Rect(label.transform, 20, 9, 526, 34); Font(label, 27);
            Text(b.transform, "Description", descriptions[i], 20, 50, 526, 30, 21);
        }
        var audio = overview.Find("existing-settings").GetComponent<UnityEngine.UI.Button>(); MoveButton(audio, 610, 86, 590, 310);
        var title = audio.GetComponentInChildren<TMP_Text>(true); Rect(title.transform, 24, 80, 542, 54); title.text = "AUDIO ȘI COMENZI"; Font(title, 44, true);
        var icon = audio.transform.Find("Settings Icon"); if (icon == null) icon = FormulaMenuStyle.Art(audio.transform, "Settings Icon", FormulaMenuAssets.Current.settings, 24, 20, 38, 40).transform;
        var description = Text(audio.transform, "Description", "Volum și vibrații\nTastatură, gamepad și volan", 24, 151, 542, 90, 25); description.textWrappingMode = TextWrappingModes.Normal;
        Text(audio.transform, "Action", "PERSONALIZEAZĂ  →", 24, 253, 542, 35, 22, false, FormulaUI.Accent);
    }
    [MenuItem("Tools/Formula Career/Arrange Event Briefing")]
    public static void ArrangeEventBriefing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var root = PrefabUtility.LoadPrefabContents(FormulaMenuHierarchyBuilder.PrefabPath);
        try { Briefing(root.GetComponentInChildren<FormulaMenuDialogs>(true)); PrefabUtility.SaveAsPrefabAsset(root, FormulaMenuHierarchyBuilder.PrefabPath); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }
    private static void Briefing(FormulaMenuDialogs view)
    {
        var root = Panel(view.transform, "Race Briefing", 110, 80, 1060, 560, new Color32(24, 2, 10, 255)); Set(view, "eventBriefing", root.gameObject);
        Set(view, "eventState", Text(root, "Event State", "CAREER / EVENT 01", 32, 20, 996, 24, 17, false, FormulaUI.Accent));
        Set(view, "eventTitle", Text(root, "Event Title", "SHAKEDOWN", 32, 50, 996, 64, 52, true));
        var circuit = Panel(root, "Circuit", 32, 130, 276, 250, new Color32(49, 4, 17, 245));
        Set(view, "eventMap", Map(circuit, "track_01", 26, 16, 224, 124));
        var track = Text(circuit, "Circuit Name and Format", "Grand Prix Circuit\n1 lap · Solo", 20, 160, 236, 70, 22); track.textWrappingMode = TextWrappingModes.Normal; Set(view, "eventTrack", track);
        Text(root, "Objective Heading", "OBJECTIVE", 338, 126, 690, 22, 15, false, FormulaUI.Accent);
        Set(view, "eventGoal", Text(root, "Goal", "Complete a valid lap", 338, 151, 690, 44, 28));
        Text(root, "Targets Heading", "MEDAL / TARGET", 352, 207, 360, 22, 15);
        Set(view, "eventReward", Text(root, "Reward", "TOTAL REWARD", 750, 207, 264, 22, 15, false, FormulaUI.Accent));
        var goals = new TMP_Text[3]; var tiers = new TMP_Text[3]; var rewards = new TMP_Text[3]; string[] names = { "BRONZ", "ARGINT", "AUR" };
        for (int i = 0; i < 3; i++)
        {
            var medal = Panel(root, "Medal " + names[i], 338, 238 + i * 55, 690, 49, new Color32(44, 3, 14, 255));
            tiers[i] = Text(medal, "Tier", new[] { "BRONZE", "SILVER", "GOLD" }[i], 14, 13, 85, 24, 17, false, FormulaUI.Accent);
            goals[i] = Text(medal, "Target", "Complete a valid lap", 110, 11, 285, 28, 21);
            rewards[i] = Text(medal, "Total Reward", "1,200 CR · 1,000 XP", 412, 11, 264, 28, 21);
        }
        var serialized = new SerializedObject(view);
        void SetTexts(string field, TMP_Text[] texts) { var p = serialized.FindProperty(field); p.arraySize = texts.Length; for (int i = 0; i < texts.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = texts[i]; serialized.ApplyModifiedPropertiesWithoutUndo(); }
        SetTexts("medalGoals", goals); SetTexts("medalTiers", tiers); SetTexts("medalRewards", rewards);
        var vehicle = Text(root, "Vehicle Rule", "STOCK CAR\nBalanced setup", 32, 398, 276, 70, 19); vehicle.textWrappingMode = TextWrappingModes.Normal; Set(view, "eventVehicle", vehicle);
        var note = Text(root, "Note", "Bronze unlocks the next event.", 338, 410, 690, 62, 17); note.textWrappingMode = TextWrappingModes.Normal; Set(view, "eventNote", note);
        var cancel = NewButton(root, "cancel", "BACK", 338, 486, 176, 50); Set(view, "eventCancel", cancel);
        var accept = NewButton(root, "confirm", "START EVENT", 530, 486, 498, 50); Set(view, "eventAccept", accept); Set(view, "eventActionText", accept.GetComponentInChildren<TMP_Text>(true));
        foreach (var button in new[] { cancel, accept })
        {
            StyleHomeButton(button);
            var label = button.GetComponentInChildren<TMP_Text>(true); Rect(label.transform, 20, 4, button == accept ? 420 : 140, 42);
            button.GetComponent<FormulaMenuFocus>().StyleHome(button.GetComponent<UnityEngine.UI.Image>(), label, null, button.transform.Find("Selection Rail").GetComponent<UnityEngine.UI.Image>(), button.transform.Find("Selection Arrow").GetComponent<TMP_Text>());
        }
        root.gameObject.SetActive(false);
        var generic = view.transform.Find("Confirmation"); generic.GetComponent<UnityEngine.UI.Image>().color = new Color32(24, 2, 10, 255);
        Font(generic.Find("Title").GetComponent<TMP_Text>(), 44, true);
        var description = generic.Find("Details").GetComponent<TMP_Text>(); Font(description, 24); description.textWrappingMode = TextWrappingModes.Normal;
        var acceptGeneric = generic.Find("confirm").GetComponent<UnityEngine.UI.Button>(); var r = (RectTransform)acceptGeneric.transform; MoveButton(acceptGeneric, r.anchoredPosition.x, -r.anchoredPosition.y, r.sizeDelta.x, r.sizeDelta.y, true);
    }
    private static void RefineStage()
    {
        const string path = "Assets/Formula/Garage/Formula_Paddock_Garage.prefab";
        Material Material(string name, Color color, float metal, float smooth)
        {
            string assetPath = "Assets/Formula/Garage/Materials/" + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, assetPath); }
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metal); material.SetFloat("_Smoothness", smooth); EditorUtility.SetDirty(material); return material;
        }
        var floor = Material("Display Floor", new Color32(29, 4, 12, 255), .35f, .42f);
        var ground = Material("Studio Floor", new Color32(15, 2, 7, 255), .2f, .35f);
        var wall = Material("Display Team Red", new Color32(85, 4, 22, 255), .15f, .32f);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.transform.Find("Architecture/Epoxy Floor").GetComponent<Renderer>().sharedMaterial = ground;
            root.transform.Find("Service Bay Markings/Service Pad").GetComponent<Renderer>().sharedMaterial = floor;
            root.transform.Find("Architecture/Rear Team Stripe").GetComponent<Renderer>().sharedMaterial = wall;
            foreach (var light in root.GetComponentsInChildren<Light>(true))
                if (light.name == "Main Softbox") light.intensity = 1.8f;
                else if (light.name == "Front Fill") light.intensity = 16;
                else if (light.name == "Rear Fill") light.intensity = 9;
            var probe = root.GetComponentInChildren<UnityEngine.ReflectionProbe>();
            const string reflectionPath = "Assets/Formula/Garage/Garage Reflection.exr";
            FormulaGarageBuilder.BakeStageReflection(root, probe, reflectionPath);
            AssetDatabase.ImportAsset(reflectionPath);
            probe.bakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(reflectionPath);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void RefineScene(string path)
    {
        var scene = SceneManager.GetSceneByPath(path); bool loaded = scene.IsValid() && scene.isLoaded;
        if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); SceneManager.SetActiveScene(scene);
        try
        {
            RenderSettings.ambientLight = new Color(.18f, .12f, .13f);
            var preview = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FormulaGarageVehiclePreview>(true)).Single();
            var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Single(c => c.CompareTag("MainCamera"));
            var cameraData = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (cameraData != null) cameraData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            Set(preview, "presentationCamera", camera); preview.Present(path == FormulaGarageBuilder.GarageScene ? "Garaj" : "Acasă", "Livree", true);
            var view = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FormulaMenuView>(true)).Single();
            var title = new SerializedObject(view).FindProperty("title").objectReferenceValue as TMP_Text; Font(title, 42, true); Rect(title.transform, 40, 9, 440, 50); PrefabUtility.RecordPrefabInstancePropertyModifications(title); PrefabUtility.RecordPrefabInstancePropertyModifications(title.rectTransform);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
    }
}
