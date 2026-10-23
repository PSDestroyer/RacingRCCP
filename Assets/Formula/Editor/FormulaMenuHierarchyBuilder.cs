using System;
using System.IO;
using System.Linq;
using FormulaGame;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit authoring command. It saves actual uGUI objects; no runtime layout generation.
public static class FormulaMenuHierarchyBuilder
{
    public const string PrefabPath = "Assets/Formula/UI/Formula_Menu.prefab";
    private const string ArtFolder = "Assets/Formula/Resources/Formula/UI";
    private static readonly Action NoAction = () => { };
    [MenuItem("Tools/Formula Career/Author Menu Hierarchy")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var original = SceneManager.GetActiveScene();
        foreach (string path in new[] { "Assets/Scenes/Menu.unity", FormulaGarageBuilder.GarageScene })
        {
            var scene = SceneManager.GetSceneByPath(path);
            if (scene.IsValid() && scene.isDirty) throw new InvalidOperationException("Save the scene before authoring its UI: " + path);
        }
        Directory.CreateDirectory("Assets/Formula/UI"); AssetDatabase.Refresh(); PersistStyleAssets();
        if (!File.Exists(PrefabPath)) { BuildPrefab(); FormulaMenuPremiumBuilder.RefinePrefab(); }
        PolishControls();
        foreach (string path in new[] { "Assets/Scenes/Menu.unity", FormulaGarageBuilder.GarageScene }) Install(path);
        AssetDatabase.SaveAssets();
        if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
        Validate(); Debug.Log("FORMULA_MENU_HIERARCHY_AUTHORED");
    }
    private static void Set(UnityEngine.Object target, string field, object value)
    {
        var serialized = new SerializedObject(target); var p = serialized.FindProperty(field);
        if (p == null) throw new InvalidOperationException(target.GetType().Name + "." + field);
        Assign(p, value); serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Assign(SerializedProperty p, object value)
    {
        if (value is Array array) { p.arraySize = array.Length; for (int i = 0; i < array.Length; i++) Assign(p.GetArrayElementAtIndex(i), array.GetValue(i)); }
        else if (value is string text) p.stringValue = text;
        else if (value is bool flag) p.boolValue = flag;
        else p.objectReferenceValue = value as UnityEngine.Object;
    }
    private static RectTransform Group(Transform parent, string name) => FormulaUI.Rect(parent, name, 0, 0, 1216, 500);
    private static TMP_Text Text(Transform p, string name, string value, float x, float y, float w, float h, int size = 24, bool title = false, Color? color = null)
    {
        value = FormulaEnglishBuilder.Translate(value);
        var t = title ? FormulaMenuStyle.Title(p, value, x, y, w, h, size, color) : FormulaMenuStyle.Text(p, value, x, y, w, h, size, color); t.name = name; return t;
    }
    private static UnityEngine.UI.Button Button(Transform p, string name, string value, float x, float y, float w, float h) => FormulaMenuStyle.Button(p, name, FormulaEnglishBuilder.Translate(value), x, y, w, h, NoAction);
    private static TMP_Text Label(UnityEngine.UI.Button b) { var label = b.GetComponentInChildren<TMP_Text>(); label.name = "Label"; return label; }
    private static GameObject Marker(UnityEngine.UI.Button b, bool active)
    {
        FormulaUI.MarkActive(b, active); return b.transform.GetChild(b.transform.childCount - 1).gameObject;
    }
    private static UnityEngine.UI.Image[] Segments(Transform p, string name, float x, float y, float width)
    {
        var group = FormulaUI.Rect(p, name, x, y, width, 6);
        return Enumerable.Range(0, 3).Select(i => FormulaUI.Box(group, "Level " + (i + 1), i * width / 3, 0, width / 3 - 5, 6, FormulaUI.Red).GetComponent<UnityEngine.UI.Image>()).ToArray();
    }
    private static void PersistStyleAssets()
    {
        var art = FormulaMenuAssets.Current;
        if (art == null) throw new InvalidOperationException("Build Figma Menu Assets first.");
        if (art.panelSprite == null)
        {
            var t = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float dx = Mathf.Max(7.5f - x, x - 23.5f), dy = Mathf.Max(7.5f - y, y - 23.5f);
                float distance = new Vector2(Mathf.Max(dx, 0), Mathf.Max(dy, 0)).magnitude;
                t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(8.5f - distance)));
            }
            t.Apply(); string path = ArtFolder + "/RoundedPanel.png"; File.WriteAllBytes(path, t.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(t); AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(10, 10, 10, 10); importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            art.panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        if (art.vignette == null)
        {
            var t = new Texture2D(64, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++) for (int x = 0; x < 64; x++)
            {
                float radius = new Vector2((x / 63f - .70f) / .52f, (y / 31f - .55f) / .34f).magnitude;
                float frame = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.30f, 1.05f, radius)); t.SetPixel(x, y, new Color(.07f, .003f, .014f, Mathf.Lerp(.08f, .96f, frame)));
            }
            t.Apply(); string path = ArtFolder + "/MenuVignette.png"; File.WriteAllBytes(path, t.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(t); AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.wrapMode = TextureWrapMode.Clamp; importer.SaveAndReimport();
            art.vignette = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        EditorUtility.SetDirty(art); AssetDatabase.SaveAssets();
    }
    private static void BuildPrefab()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
        try
        {
            var root = new GameObject("Formula Menu", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster), typeof(FormulaMenuView));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            var view = root.GetComponent<FormulaMenuView>(); Set(view, "menuCanvas", canvas);
            FormulaMenuStyle.Backdrop(root.transform);
            var header = FormulaUI.Box(root.transform, "Header", 0, 0, 1280, 96, FormulaMenuStyle.SurfaceColor);
            FormulaUI.Box(header, "Brand Stripe", 24, 20, 8, 57, FormulaUI.Red);
            Text(header, "Brand", "FORMULA", 44, 7, 255, 58, 56, true);
            Text(header, "Subtitle", "RISE OF LEGENDS", 47, 64, 260, 28, 20, false, FormulaUI.Accent);
            var season = FormulaUI.Box(header, "Season", 304, 24, 90, 44, FormulaUI.Red); Text(season, "Year", "2027", 10, 0, 78, 44, 38, true);
            var account = FormulaMenuStyle.Surface(header, "Driver and Wallet", 842, 20, 390, 56); var art = FormulaMenuAssets.Current;
            FormulaMenuStyle.Art(account, "Helmet Icon", art.helmet, 10, 10, 37, 35.5f);
            var wallet = Text(account, "Wallet", "Pilot · NIVEL 1\n0 CR / 0/3.000 XP", 52, 0, 322, 56, 21); wallet.alignment = TextAlignmentOptions.MidlineRight; Set(view, "wallet", wallet);
            var screens = FormulaUI.Rect(root.transform, "Screens", 32, 118, 1216, 500); Set(view, "screens", screens); Set(view, "screenTransition", screens.gameObject.AddComponent<CanvasGroup>());
            Set(view, "home", BuildHome(screens)); Set(view, "career", BuildEvents(screens, false)); Set(view, "garage", BuildGarage(screens)); Set(view, "quick", BuildEvents(screens, true)); Set(view, "profile", BuildProfile(screens)); Set(view, "settings", BuildSettings(screens));
            var footer = FormulaUI.Box(root.transform, "Footer", 0, 632, 1280, 88, FormulaMenuStyle.SurfaceColor);
            FormulaMenuStyle.Art(footer, "Footer Glow", art.footerGlow, 56, -8, 350, 22);
            Set(view, "title", Text(footer, "Page Title", "MENIU PRINCIPAL", 56, 12, 520, 65, 52, true));
            FormulaUI.Box(footer, "Title Rule", 560, 48, 160, 1, Color.white);
            Set(view, "hints", Text(footer, "Input Hints", "Enter · Confirmă\nEsc · Înapoi", 750, 4, 320, 76, 19));
            var back = Button(footer, "nav-0", "Înapoi", 1090, 22, 150, 46); Set(view, "back", back); back.gameObject.SetActive(false);
            Set(view, "dialogs", BuildDialogs(root.transform));
            foreach (Transform child in screens) child.gameObject.SetActive(child.name == "Home");
            FormulaEnglishBuilder.TranslateHierarchy(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }
    [MenuItem("Tools/Formula Career/Polish Authored UI Controls")]
    public static void PolishControls()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            foreach (var slider in root.GetComponentsInChildren<UnityEngine.UI.Slider>(true))
            {
                // Slider stretches its handle vertically; extra sizeDelta would double its height.
                slider.handleRect.sizeDelta = new Vector2(12, 0);
                slider.transform.Find("Track").GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            }
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                if (label.name == "Text")
                {
                    if (string.IsNullOrEmpty(label.text)) UnityEngine.Object.DestroyImmediate(label.gameObject);
                    else label.name = "Label";
                }
            FormulaEnglishBuilder.TranslateHierarchy(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static FormulaHomeView BuildHome(Transform screens)
    {
        var root = Group(screens, "Home"); var view = root.gameObject.AddComponent<FormulaHomeView>(); var art = FormulaMenuAssets.Current;
        Set(view, "nextStage", Text(root, "Next Stage", "CARIERA TA / ETAPA 01", 24, 12, 370, 28, 20, false, FormulaUI.Accent));
        Set(view, "nextEvent", Text(root, "Next Event", "ACOMODARE", 24, 42, 390, 50, 40, true));
        var nav = Group(root, "Navigation");
        Set(view, "continueCareer", FormulaMenuStyle.MenuButton(nav, "continue-career", "CONTINUĂ CARIERA", art.career, 12, 110, 380, 66, NoAction));
        Set(view, "garage", FormulaMenuStyle.MenuButton(nav, "home-garage", "GARAJ", art.garage, 12, 177, 380, 66, NoAction));
        Set(view, "development", FormulaMenuStyle.MenuButton(nav, "home-development", "DEZVOLTARE", art.upgrade, 12, 244, 380, 66, NoAction));
        Set(view, "quick", FormulaMenuStyle.MenuButton(nav, "home-quick", "CURSE RAPIDE", art.career, 12, 311, 380, 66, NoAction));
        Set(view, "profile", FormulaMenuStyle.MenuButton(nav, "home-profile", "PROFIL", art.helmet, 12, 378, 182, 60, NoAction));
        Set(view, "settings", FormulaMenuStyle.MenuButton(nav, "home-settings", "SETĂRI", art.settings, 200, 378, 192, 60, NoAction));
        var caption = FormulaMenuStyle.Surface(root, "Current Vehicle", 650, 440, 550, 55); Text(caption, "Chassis", "FORMULA G27", 18, 0, 240, 50, 34, true);
        Set(view, "currentLivery", Text(caption, "Equipped Livery", "Apex Orange", 260, 8, 270, 35, 23, false, FormulaUI.Accent)); return view;
    }
    private static Component BuildEvents(Transform screens, bool quick)
    {
        var root = Group(screens, quick ? "Quick Race" : "Career");
        Component view = quick ? (Component)root.gameObject.AddComponent<FormulaQuickView>() : root.gameObject.AddComponent<FormulaCareerView>();
        var cards = new FormulaEventCard[6]; string[] quickIds = { "g03", "g04", "g02", "g05", "g01", "g05" };
        var list = Group(root, "Event Cards");
        for (int i = 0; i < cards.Length; i++)
        {
            var e = FormulaCatalog.Current.Event(quick ? quickIds[i] : "g0" + (i + 1));
            var b = Button(list, quick ? "quick-" + i : e.id, "", quick ? i % 2 * 610 : i % 3 * 410, quick ? i / 2 * 164 : 6 + i / 3 * 246, quick ? 594 : 394, quick ? 148 : 224);
            cards[i] = b.gameObject.AddComponent<FormulaEventCard>(); var card = cards[i]; Set(card, "eventId", e.id); Set(card, "mode", quick ? (i == 2 || i == 3 ? "time_trial" : "quick") : "career"); Set(card, "button", b);
            if (quick)
            {
                Text(b.transform, "Mode", i < 2 ? "CURSĂ RAPIDĂ" : i < 4 ? "TIME ATTACK" : "ANTRENAMENT", 20, 10, 554, 53, 38, true);
                Set(card, "trackName", Text(b.transform, "Circuit", FormulaAssets.Current.FindTrack(e.trackId).displayName, 20, 67, 554, 34, 24));
                Set(card, "state", Text(b.transform, "Reward Rule", "FĂRĂ RECOMPENSE DE CARIERĂ", 20, 111, 554, 28, 19, false, FormulaUI.Accent));
            }
            else
            {
                Set(card, "stage", Text(b.transform, "Stage", "ETAPA " + (i + 1).ToString("00") + (i == 0 ? "" : " / BLOCATĂ"), 18, 12, 358, 28, 19, false, FormulaUI.Accent));
                Set(card, "eventName", Text(b.transform, "Event Name", e.name, 18, 49, 358, 56, 38, true));
                Set(card, "trackName", Text(b.transform, "Circuit", FormulaAssets.Current.FindTrack(e.trackId).displayName, 18, 111, 358, 31, 22));
                Set(card, "format", Text(b.transform, "Race Format", e.laps + " TURURI / " + e.opponentCount + " ADVERSARI", 18, 146, 358, 28, 18));
                FormulaUI.Box(b.transform, "Card Rule", 18, 181, 358, 1, FormulaUI.Red);
                Set(card, "state", Text(b.transform, "Unlock and Medal", i == 0 ? "Fără medalie" : "Continuă cariera", 18, 186, 358, 31, 20, false, FormulaUI.Accent));
            }
        }
        Set(view, "events", cards); return view;
    }
    private static FormulaGarageView BuildGarage(Transform screens)
    {
        var root = Group(screens, "Garage"); var view = root.gameObject.AddComponent<FormulaGarageView>();
        var tabRoot = Group(root, "Tabs"); string[] names = { "Dezvoltare", "Setup", "Livree" }; var tabs = new UnityEngine.UI.Button[3]; var markers = new GameObject[3]; var sections = new GameObject[3];
        for (int i = 0; i < 3; i++) { tabs[i] = Button(tabRoot, "garage-" + i, names[i].ToUpperInvariant(), i * 260, 0, 248, 44); markers[i] = Marker(tabs[i], i == 0); sections[i] = Group(root, names[i]).gameObject; }
        Set(view, "tabs", tabs); Set(view, "tabMarkers", markers); Set(view, "sections", sections);
        var upgrades = new FormulaDevelopmentCard[4];
        var development = sections[0].transform; var list = Group(development, "Upgrade Cards");
        for (int i = 0; i < upgrades.Length; i++)
        {
            var branch = FormulaCatalog.Current.upgrades[i]; var row = FormulaMenuStyle.Surface(list, "Development-" + branch.id, 0, 70 + i * 104, 360, 96);
            var card = row.gameObject.AddComponent<FormulaDevelopmentCard>(); upgrades[i] = card; Set(card, "branchId", branch.id);
            Text(row, "Branch", branch.name.ToUpperInvariant(), 16, 4, 230, 35, 30, true); Set(card, "segments", Segments(row, "Development Levels", 16, 46, 190));
            var equip = Button(row, "equip-" + branch.id, "NIVEL 0 >", 224, 8, 122, 40); Set(card, "equip", equip); Set(card, "equippedLevel", Label(equip)); equip.interactable = false;
            var buy = Button(row, "buy-" + branch.id, "DEZVOLTĂ / " + branch.levels[0].incrementalPriceCredits.ToString("N0") + " CR", 10, 57, 340, 33); Set(card, "buy", buy); Set(card, "purchase", Label(buy));
        }
        Set(view, "development", upgrades);
        var stats = new[] { BuildStatistics(development), BuildStatistics(sections[1].transform) }; Set(view, "statistics", stats);
        var setupList = Group(sections[1].transform, "Setup Cards"); var s = new SerializedObject(view); var setupEntries = s.FindProperty("setups"); setupEntries.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            var setup = FormulaCatalog.Current.freeSetups[i]; var b = Button(setupList, "setup-" + setup.id, setup.name.ToUpperInvariant() + " · GRATUIT", 0, 76 + i * 118, 650, 102);
            var entry = setupEntries.GetArrayElementAtIndex(i); entry.FindPropertyRelative("id").stringValue = setup.id; entry.FindPropertyRelative("button").objectReferenceValue = b;
            entry.FindPropertyRelative("label").objectReferenceValue = Label(b); entry.FindPropertyRelative("equippedMarker").objectReferenceValue = Marker(b, i == 0);
        }
        s.ApplyModifiedPropertiesWithoutUndo();
        var skinRoot = sections[2].transform; var details = FormulaMenuStyle.Surface(skinRoot, "Livery Details", 0, 74, 360, 264);
        Text(details, "Category", "LIVREE / FORMULA G27", 18, 12, 324, 28, 19, false, FormulaUI.Accent);
        Set(view, "liveryName", Text(details, "Livery Name", "Apex Orange", 18, 47, 324, 49, 37, true)); Set(view, "liveryStatus", Text(details, "Ownership", "ECHIPATĂ", 18, 103, 324, 32, 23, false, FormulaUI.Accent));
        Set(view, "liveryCondition", Text(details, "Unlock Condition", "Aspect cosmetic. Dezvoltarea se păstrează.", 18, 144, 324, 51, 21));
        var apply = Button(details, "livery-apply", "Echipată", 18, 204, 324, 46); Set(view, "liveryAction", apply); Set(view, "liveryActionText", Label(apply)); apply.interactable = false;
        var liveryCards = new FormulaLiveryCard[8]; var carousel = Group(skinRoot, "Livery Cards");
        for (int i = 0; i < liveryCards.Length; i++)
        {
            var l = FormulaCatalog.Current.liveries[i]; var b = Button(carousel, l.id, "", i * 153, 366, 145, 128); var card = b.gameObject.AddComponent<FormulaLiveryCard>(); liveryCards[i] = card;
            Set(card, "liveryId", l.id); Set(card, "button", b); Text(b.transform, "Livery Name", l.displayName, 9, 3, 127, 30, 23, true);
            FormulaMenuStyle.Art(b.transform, "Generic Livery Thumbnail", FormulaMenuAssets.Current.liveries[i], 7, 37, 131, 66.54f);
            Set(card, "ownership", Text(b.transform, "Ownership", i == 0 ? "ECHIPATĂ" : i == 3 ? "DEȚINUTĂ" : "BLOCATĂ", 9, 105, 127, 21, 17, false, FormulaUI.Accent)); Set(card, "equippedMarker", Marker(b, i == 0));
        }
        Set(view, "liveries", liveryCards); for (int i = 0; i < sections.Length; i++) sections[i].SetActive(i == 0); return view;
    }
    private static FormulaStatisticsView BuildStatistics(Transform parent)
    {
        var root = FormulaUI.Box(parent, "RCCP Statistics", 668, 274, 532, 218, FormulaMenuStyle.SurfaceColor); var view = root.gameObject.AddComponent<FormulaStatisticsView>();
        Text(root, "Heading", "CONFIGURAȚIA ECHIPATĂ", 16, 7, 490, 24, 18, false, FormulaUI.Accent);
        string[] titles = { "Cuplu motor", "Cuplu frână față", "Arc suspensie", "Coef. apăsare aero" }, ids = { "engine", "brakes", "suspension", "aero" }, fields = { "torque", "brakes", "spring", "aero" }, values = { "480 Nm", "1400 Nm", "70000 N/m", "6" };
        var s = new SerializedObject(view); var rows = s.FindProperty("development"); rows.arraySize = 4;
        for (int i = 0; i < titles.Length; i++)
        {
            int y = 38 + i * 32; Text(root, "Label " + titles[i], titles[i], 16, y, 215, 30, 21); var bars = Segments(root, "Stats-" + ids[i], 238, y + 22, 116);
            var value = Text(root, "Value " + titles[i], values[i], 362, y, 145, 30, 22, false, FormulaUI.Accent); value.alignment = TextAlignmentOptions.MidlineRight;
            s.FindProperty(fields[i]).objectReferenceValue = value; var row = rows.GetArrayElementAtIndex(i); row.FindPropertyRelative("branchId").stringValue = ids[i]; Assign(row.FindPropertyRelative("segments"), bars);
        }
        s.ApplyModifiedPropertiesWithoutUndo(); Set(view, "drag", Text(root, "Drag and Mass", "Rezistență aero 12 · Masă 820 kg", 16, 180, 490, 30, 18)); return view;
    }
    private static FormulaProfileView BuildProfile(Transform screens)
    {
        var root = Group(screens, "Profile"); var view = root.gameObject.AddComponent<FormulaProfileView>(); var panel = FormulaMenuStyle.Surface(root, "Pilot Summary", 0, 0, 400, 488);
        var name = Text(panel, "Pilot Name", "Pilot", 24, 24, 352, 64, 48, true); name.enableAutoSizing = true; name.fontSizeMin = 26; name.fontSizeMax = 48; name.overflowMode = TextOverflowModes.Ellipsis; Set(view, "pilotName", name);
        Set(view, "summary", Text(panel, "Progress", "NIVEL 1\n0 XP\n2/8 LIVREE\n0/6 PROBE TERMINATE", 24, 114, 352, 265, 28));
        var medalRoot = Group(root, "Career Medals"); var s = new SerializedObject(view); var rows = s.FindProperty("medals"); rows.arraySize = 6;
        for (int i = 0; i < 6; i++)
        {
            var e = FormulaCatalog.Current.careerEvents[i]; var row = FormulaMenuStyle.Surface(medalRoot, e.id + "-medal", 424, i * 81, 776, 72);
            Text(row, "Event", e.name, 16, 12, 560, 40, 25); var medal = Text(row, "Medal", "Fără medalie", 586, 12, 190, 40, 24, false, FormulaUI.Accent);
            rows.GetArrayElementAtIndex(i).FindPropertyRelative("eventId").stringValue = e.id; rows.GetArrayElementAtIndex(i).FindPropertyRelative("medal").objectReferenceValue = medal;
        }
        s.ApplyModifiedPropertiesWithoutUndo(); return view;
    }
    private static FormulaSettingsView BuildSettings(Transform screens)
    {
        var root = Group(screens, "Settings"); var view = root.gameObject.AddComponent<FormulaSettingsView>();
        var overview = Group(root, "Assists"); Set(view, "overview", overview.gameObject);
        var heading = FormulaMenuStyle.Surface(overview, "Assist Information", 0, 0, 1200, 65); Text(heading, "Information", "Asistențe gratuite · Recompensele rămân aceleași", 16, 8, 1178, 49, 28);
        string[] ids = { "abs", "tcs", "esp" }; var assists = new UnityEngine.UI.Button[3];
        for (int i = 0; i < 3; i++) assists[i] = Button(overview, "assist-" + ids[i], ids[i].ToUpperInvariant() + " · Activ", i == 1 ? 620 : 0, i == 2 ? 205 : 90, 590, 90);
        Set(view, "assists", assists); Set(view, "assistLabels", assists.Select(Label).ToArray()); Set(view, "openDetails", Button(overview, "existing-settings", "Audio și remaparea comenzilor", 620, 205, 590, 90));
        var details = Group(root, "Audio and Controls"); Set(view, "details", details.gameObject);
        var tabs = Group(details, "Tabs"); Set(view, "audioTab", Button(tabs, "settings-audio", "AUDIO", 0, 0, 300, 44)); Set(view, "controlsTab", Button(tabs, "settings-controls", "COMENZI", 312, 0, 300, 44));
        var audio = Group(details, "Audio"); Set(view, "audioPage", audio.gameObject); var sliders = new UnityEngine.UI.Slider[3]; var values = new TMP_Text[3];
        string[] names = { "Efecte sonore", "Motor", "Muzică" };
        for (int i = 0; i < 3; i++)
        {
            var row = FormulaMenuStyle.Surface(audio, "Volume " + names[i], 0, 72 + i * 94, 1200, 78); Text(row, "Channel", names[i], 22, 18, 270, 42, 28);
            var sliderRoot = FormulaUI.Rect(row, "volume-" + i, 328, 22, 650, 34); var slider = sliderRoot.gameObject.AddComponent<UnityEngine.UI.Slider>();
            FormulaUI.Box(sliderRoot, "Track", 0, 12, 650, 10, FormulaUI.Ink);
            var fillArea = FormulaUI.Rect(sliderRoot, "Fill Area", 0, 12, 650, 10); var fill = FormulaUI.Box(fillArea, "Fill", 0, 0, 650, 10, FormulaUI.Red);
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero;
            var handleArea = FormulaUI.Rect(sliderRoot, "Handle Area", 0, 0, 650, 34); var handle = FormulaUI.Box(handleArea, "Handle", 0, 0, 12, 34, FormulaUI.Accent);
            handle.GetComponent<UnityEngine.UI.Image>().raycastTarget = true; slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>(); slider.value = .8f;
            sliders[i] = slider; values[i] = Text(row, "Percentage", "80%", 1008, 18, 166, 42, 28, false, FormulaUI.Accent);
        }
        Set(view, "volumes", sliders); Set(view, "volumeValues", values);
        var vibration = Button(audio, "settings-vibration", "Vibrații · Active", 0, 374, 590, 76); Set(view, "vibration", vibration); Set(view, "vibrationLabel", Label(vibration));
        var controls = BuildControls(details); Set(view, "controls", controls); controls.gameObject.SetActive(false); details.gameObject.SetActive(false); return view;
    }
    private static FormulaControlsView BuildControls(Transform parent)
    {
        var root = Group(parent, "Controls"); var view = root.gameObject.AddComponent<FormulaControlsView>();
        var list = Group(root, "Bindings"); Set(view, "listInteraction", list.gameObject.AddComponent<CanvasGroup>());
        Set(view, "keyboard", Button(list, "controls-keyboard", "Tastatură", 0, 60, 245, 44)); Set(view, "gamepad", Button(list, "controls-gamepad", "Gamepad", 257, 60, 245, 44)); Set(view, "wheel", Button(list, "controls-wheel", "Volan", 514, 60, 245, 44));
        Set(view, "pageLabel", Text(list, "Binding Page", "TASTATURĂ / 1", 790, 60, 410, 44, 24, false, FormulaUI.Accent));
        var s = new SerializedObject(view); var rows = s.FindProperty("rows"); rows.arraySize = 6;
        for (int i = 0; i < 6; i++)
        {
            var b = Button(list, "rebind-" + i, "", i % 2 * 610, 122 + i / 2 * 94, 590, 80); var row = rows.GetArrayElementAtIndex(i); row.FindPropertyRelative("button").objectReferenceValue = b;
            row.FindPropertyRelative("action").objectReferenceValue = Text(b.transform, "Action", "Comandă", 18, 6, 554, 35, 22);
            row.FindPropertyRelative("binding").objectReferenceValue = Text(b.transform, "Binding", "Apasă pentru remapare", 18, 41, 554, 32, 22, false, FormulaUI.Accent);
        }
        s.ApplyModifiedPropertiesWithoutUndo();
        Set(view, "previous", Button(list, "controls-previous", "< Anterior", 0, 420, 245, 56)); Set(view, "next", Button(list, "controls-next", "Următor >", 257, 420, 245, 56)); Set(view, "reset", Button(list, "controls-reset", "Restabilește comenzile dispozitivului", 610, 420, 590, 56));
        var waiting = FormulaUI.Box(root, "Waiting for Input", 0, 52, 1216, 448, new Color(.08f, 0, .02f, .97f)); waiting.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
        Set(view, "waiting", waiting.gameObject); Set(view, "prompt", Text(waiting, "Prompt", "Apasă noua comandă\nEsc · Anulează", 60, 65, 1096, 180, 34));
        Set(view, "cancelRebind", Button(waiting, "rebind-cancel", "Anulează", 60, 294, 450, 64)); waiting.gameObject.SetActive(false); return view;
    }
    private static FormulaMenuDialogs BuildDialogs(Transform parent)
    {
        var root = FormulaUI.Box(parent, "Dialogs", 0, 0, 1280, 720, new Color(.04f, 0, .01f, .94f)); root.GetComponent<UnityEngine.UI.Image>().raycastTarget = true; var view = root.gameObject.AddComponent<FormulaMenuDialogs>();
        var confirm = FormulaMenuStyle.Surface(root, "Confirmation", 210, 140, 860, 440, FormulaMenuStyle.RaisedColor); Set(view, "confirmation", confirm.gameObject);
        FormulaUI.Box(confirm, "Accent", 28, 18, 80, 3, FormulaUI.Red); Set(view, "title", Text(confirm, "Title", "Confirmare", 28, 34, 804, 61, 40, true)); Set(view, "details", Text(confirm, "Details", "Detaliile acțiunii", 28, 109, 804, 218, 24));
        Set(view, "cancel", Button(confirm, "cancel", "Înapoi", 28, 354, 246, 58)); var accept = Button(confirm, "confirm", "Confirmă", 292, 354, 540, 58); Set(view, "accept", accept); Set(view, "acceptText", Label(accept));
        var levels = FormulaMenuStyle.Surface(root, "Upgrade Level Selection", 220, 104, 840, 506, FormulaMenuStyle.RaisedColor); Set(view, "levelSelection", levels.gameObject); Set(view, "levelTitle", Text(levels, "Title", "MOTOR / ECHIPARE", 28, 15, 780, 55, 42, true));
        Text(levels, "Information", "Alege nivelul deținut. Schimbarea este gratuită.", 28, 76, 780, 34, 23); var choices = new UnityEngine.UI.Button[4];
        for (int i = 0; i < 4; i++) choices[i] = Button(levels, "level-engine-" + i, i == 0 ? "CONFIGURAȚIA DE BAZĂ / DEȚINUT" : "NIVEL " + i + " / NECUMPĂRAT", 28, 124 + i * 66, 784, 56);
        Set(view, "levels", choices); Set(view, "levelLabels", choices.Select(Label).ToArray()); Set(view, "levelCancel", Button(levels, "cancel", "Înapoi", 28, 426, 240, 52));
        levels.gameObject.SetActive(false); root.gameObject.SetActive(false); return view;
    }
    public static void Install(string path)
    {
        var scene = SceneManager.GetSceneByPath(path); bool loaded = scene.IsValid() && scene.isLoaded;
        if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var roots = scene.GetRootGameObjects(); var menu = roots.SelectMany(r => r.GetComponentsInChildren<FormulaMainMenu>(true)).Single();
            var view = roots.SelectMany(r => r.GetComponentsInChildren<FormulaMenuView>(true)).SingleOrDefault();
            bool garageEntry = path == FormulaGarageBuilder.GarageScene;
            if (view == null)
            {
                Directory.CreateDirectory("Docs/AI/FormulaUI/SceneMigration"); string backup = "Docs/AI/FormulaUI/SceneMigration/" + Path.GetFileNameWithoutExtension(path) + ".before.unity.txt";
                if (!File.Exists(backup)) File.Copy(path, backup);
                var ui = new GameObject("UI"); SceneManager.MoveGameObjectToScene(ui, scene);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), ui.transform); view = instance.GetComponent<FormulaMenuView>();
                // Remove the old presenters after the replacement is fully serialized and referenced.
                if (menu.legacyCanvas != null) Undo.DestroyObjectImmediate(menu.legacyCanvas.gameObject);
                foreach (var root in roots.Where(r => r != null && new[] { "RCCP_Canvas", "MoneyManger", "CinemachineCameras" }.Contains(r.name))) Undo.DestroyObjectImmediate(root);
            }
            Set(menu, "view", view); Set(menu, "startInGarage", garageEntry); menu.legacyCanvas = null; menu.legacyNavigation = null; menu.legacyBack = null; menu.legacyDisplayCars = Array.Empty<GameObject>();
            var presentation = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Presentation") ?? new GameObject("Presentation"); SceneManager.MoveGameObjectToScene(presentation, scene);
            var systems = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Scene Systems") ?? new GameObject("Scene Systems"); SceneManager.MoveGameObjectToScene(systems, scene);
            var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Single(c => c.CompareTag("MainCamera")); camera.transform.SetParent(presentation.transform, true);
            var stage = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Formula Paddock Garage"); if (stage != null) stage.transform.SetParent(presentation.transform, true);
            var preview = presentation.GetComponentInChildren<FormulaGarageVehiclePreview>();
            if (preview == null)
            {
                var previewRoot = new GameObject("Vehicle Preview"); previewRoot.transform.SetParent(presentation.transform, false); preview = previewRoot.AddComponent<FormulaGarageVehiclePreview>();
            }
            var placeholder = roots.FirstOrDefault(r => r != null && r.name == "Editor Garage Vehicle") ?? preview.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Editor Garage Vehicle")?.gameObject;
            if (placeholder != null) placeholder.transform.SetParent(preview.transform, true);
            menu.spawnPoint.SetParent(preview.transform, true); menu.spawnPoint.name = "Vehicle Anchor";
            Set(preview, "vehicleAnchor", menu.spawnPoint); Set(preview, "editorVehicle", placeholder); Set(preview, "presentationCamera", camera);
            Set(menu, "vehiclePreview", preview); menu.transform.SetParent(systems.transform, true);
            foreach (var root in scene.GetRootGameObjects().Where(r => new[] { "EventSystem", "PlayerInput", "RCCP_SceneManager" }.Contains(r.name))) root.transform.SetParent(systems.transform, true);
            Vector3 position = menu.spawnPoint.position; camera.transform.position = position + (garageEntry ? new Vector3(3.8f, 1.8f, 8.3f) : new Vector3(3.3f, 1.7f, 8.2f));
            Vector3 right = Vector3.Cross(Vector3.up, position - camera.transform.position).normalized; camera.transform.LookAt(position + Vector3.up * .42f - right * 1.9f); camera.fieldOfView = 34;
            view.Activate(garageEntry ? "Garaj" : "Acasă"); view.Garage.ShowSection(garageEntry ? 2 : 0);
            view.Back.gameObject.SetActive(garageEntry);
            var title = new SerializedObject(view).FindProperty("title").objectReferenceValue as TMP_Text; title.text = garageEntry ? "GARAGE / LIVERIES" : "MAIN MENU";
            view.Transition.alpha = 1; view.Dialogs.gameObject.SetActive(false); EditorUtility.SetDirty(menu);
            PrefabUtility.RecordPrefabInstancePropertyModifications(view.Back.gameObject); PrefabUtility.RecordPrefabInstancePropertyModifications(title);
            foreach (var t in view.GetComponentsInChildren<Transform>(true)) PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
    }
    [MenuItem("Tools/Formula Career/Validate Authored Menu Hierarchy")]
    public static void Validate()
    {
        var report = new System.Text.StringBuilder();
        foreach (string path in new[] { "Assets/Scenes/Menu.unity", FormulaGarageBuilder.GarageScene })
        {
            var scene = SceneManager.GetSceneByPath(path); bool loaded = scene.IsValid() && scene.isLoaded;
            if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects(); var view = roots.SelectMany(r => r.GetComponentsInChildren<FormulaMenuView>(true)).Single();
                if (!view.Canvas.isActiveAndEnabled || view.Transition.alpha != 1) throw new InvalidOperationException("New UI is hidden in Edit Mode: " + path);
                if (roots.SelectMany(r => r.GetComponentsInChildren<GarageUIController>(true)).Any()) throw new InvalidOperationException("Legacy UI presenter remains: " + path);
                if (roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).Count() != 1) throw new InvalidOperationException("Expected one serialized menu Canvas: " + path);
                if (roots.SelectMany(r => r.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true)).Count() != 1) throw new InvalidOperationException("Expected one EventSystem: " + path);
                foreach (var t in roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0) throw new InvalidOperationException("Missing script: " + t.name);
                report.AppendLine(path); foreach (var root in roots) AppendTree(report, root.transform, 0, 3);
            }
            finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
        }
        Directory.CreateDirectory("Docs/AI/FormulaUI"); File.WriteAllText("Docs/AI/FormulaUI/AuthoredHierarchy.txt", report.ToString()); Debug.Log("FORMULA_MENU_HIERARCHY_VALIDATED");
    }
    private static void AppendTree(System.Text.StringBuilder output, Transform t, int depth, int maxDepth)
    {
        output.AppendLine(new string(' ', depth * 2) + t.name + " [" + (t.gameObject.activeSelf ? "active" : "inactive") + "]");
        if (depth >= maxDepth) return; foreach (Transform child in t) AppendTree(output, child, depth + 1, maxDepth);
    }
}
