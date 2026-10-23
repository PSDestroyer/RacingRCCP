using System;
using System.Linq;
using FormulaGame;
using TMPro;
using UnityEditor;
using UnityEngine;

// The Career frame is authored from Figma 174:37 at its original 1280x720 size.
// Static artwork is exported per layer; live event and profile text stays editable.
public static class FormulaCareerFigmaBuilder
{
    private const string ArtFolder = "Assets/Formula/UI/FigmaCareer/";

    [MenuItem("Tools/Formula Career/Match Figma Career")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        foreach (string path in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder.TrimEnd('/') }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048; importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp; importer.SaveAndReimport();
        }
        var root = PrefabUtility.LoadPrefabContents(FormulaMenuHierarchyBuilder.PrefabPath);
        try
        {
            Style(root.GetComponentInChildren<FormulaCareerView>(true));
            PrefabUtility.SaveAsPrefabAsset(root, FormulaMenuHierarchyBuilder.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets(); Debug.Log("FORMULA_FIGMA_CAREER_APPLIED");
    }

    public static void Style(FormulaCareerView view)
    {
        var root = (RectTransform)view.transform;
        var canvas = root.GetComponentInParent<FormulaMenuView>();
        var parent = (RectTransform)root.parent;
        Place(root, -parent.anchoredPosition.x, parent.anchoredPosition.y, 1280, 720);
        // Keep the existing screen, card instances and serialized event bindings.
        var background = Art(root, "Figma Background", "Background", 0, 0, 1280, 720); background.transform.SetAsFirstSibling();
        float shadeAlpha = QualitySettings.activeColorSpace == ColorSpace.Linear ? 1 - Mathf.GammaToLinearSpace(.35f) : .65f;
        var dim = Box(root, "Figma Background Shade", 0, 0, 1280, 720, new Color(0, 0, 0, shadeAlpha)); dim.SetSiblingIndex(1);
        var headerShade = Art(root, "Figma Header Shade", "HeaderShade", 0, 0, 1280, 133);
        var footerShade = Art(root, "Figma Footer Shade", "FooterShade", 0, 612, 1280, 108);
        const string shadeMaterialPath = ArtFolder + "FigmaBlackOverlay.mat";
        var shadeMaterial = AssetDatabase.LoadAssetAtPath<Material>(shadeMaterialPath);
        if (shadeMaterial == null) { shadeMaterial = new Material(Shader.Find("Formula/UI/Figma Black Overlay")); AssetDatabase.CreateAsset(shadeMaterial, shadeMaterialPath); }
        headerShade.material = footerShade.material = shadeMaterial;

        // Preserve the source F artwork and replace its old season digits.
        var logo = Group(root, "Figma Logo 2027", 23, 0, 429, 147);
        var oldMark = logo.Find("Season Mark"); if (oldMark != null) oldMark.gameObject.SetActive(false);
        var fMark = Art(logo, "Original F Mark", "FMark", 0, 0, 368.656f, 147); fMark.transform.SetAsFirstSibling();
        var mark = Label(logo, "Season Digits", "27", 53, 20, 120, 64, 76, FormulaMenuAssets.Current.titleFont);
        mark.fontStyle = FontStyles.Italic; mark.transform.localScale = new Vector3(1.5f, 1, 1);
        Art(logo, "Formula Wordmark", "LogoWord", 173.397f, 37.042f, 155.086f, 19.824f);
        Art(logo, "Rise of Legends", "LogoSubtitle", 138.849f, 16.625f, 114.283f, 10.734f);
        var year = Label(logo, "Season", "2027", 251.856f, 57, 69.795f, 25, 25, FormulaMenuAssets.Current.bodyFont);
        year.color = new Color32(255, 30, 0, 255); year.fontStyle = FontStyles.Bold;
        mark.color = year.color;

        var account = Group(root, "Figma Driver and Wallet", 955, 27, 301, 46);
        var oldFrame = account.Find("Frame"); if (oldFrame != null) { oldFrame.gameObject.SetActive(false); oldFrame.GetComponent<UnityEngine.UI.RawImage>().texture = null; }
        var walletFill = Art(account, "Figma Multiply Fill", "WalletFill", 0, 0, 301, 46); walletFill.transform.SetAsFirstSibling();
        const string multiplyPath = ArtFolder + "FigmaMultiply.mat";
        var multiply = AssetDatabase.LoadAssetAtPath<Material>(multiplyPath);
        if (multiply == null) { multiply = new Material(Shader.Find("Formula/UI/Figma Multiply")); AssetDatabase.CreateAsset(multiply, multiplyPath); }
        walletFill.material = multiply;
        var walletStroke = Art(account, "Figma Border", "WalletStroke", -1.5f, -1.5f, 304, 49); walletStroke.transform.SetSiblingIndex(1);
        Art(account, "Helmet", "Helmet", 13, 4, 49, 47);
        var driver = Label(account, "Player Name", "Driver", 50, 10, 106, 26, 20, FormulaMenuAssets.Current.bodyFont);
        var wallet = Label(account, "Credits", "0", 182, 11, 81, 26, 20, FormulaMenuAssets.Current.bodyFont);
        wallet.alignment = TextAlignmentOptions.MidlineRight;
        var coin = Art(account, "Coin", "CoinMatte", 268, 11, 26, 26);
        coin.color = new Color(.72f, .72f, .72f, 1);
        Set(view, "driver", driver); Set(view, "wallet", wallet);

        var footer = Group(root, "Figma Career Footer", 0, 624, 1280, 96);
        Art(footer, "Figma Glow Left", "FooterGlow", 72, 0, 350, 22, true);
        Art(footer, "Figma Glow Right", "FooterGlow", 661, 4, 218, 14, true);
        Label(footer, "Page Heading", "SELECT YOUR CAREER", 79, 22, 387, 62, 60, FormulaMenuAssets.Current.titleFont);
        Box(footer, "Title Rule", 465.638f, 52, 496.362f, 1, new Color(1, 1, 1, .45f));
        var backRoot = Group(footer, "career-back", 1062, 37, 75, 31);
        var hit = backRoot.GetComponent<UnityEngine.UI.Image>() ?? backRoot.gameObject.AddComponent<UnityEngine.UI.Image>();
        hit.color = Color.clear; hit.raycastTarget = true;
        var back = backRoot.GetComponent<UnityEngine.UI.Button>() ?? backRoot.gameObject.AddComponent<UnityEngine.UI.Button>();
        back.transition = UnityEngine.UI.Selectable.Transition.None; back.targetGraphic = hit;
        Art(backRoot, "B Button", "BackPrompt", 0, 0, 30, 30);
        Label(backRoot, "Back Label", "Back", 30, 0, 45, 31, 24, FormulaMenuAssets.Current.bodyFont);
        var backFocus = backRoot.GetComponent<FormulaMenuFocus>() ?? backRoot.gameObject.AddComponent<FormulaMenuFocus>();
        backFocus.Initialize(Box(backRoot, "Focus", 0, 32, 75, 1, Color.clear).GetComponent<UnityEngine.UI.Image>());
        Set(view, "back", back);
        var rail = Box(root, "Figma Scroll Rail", 1263, 124, 5, 508, new Color32(217, 217, 217, 255));
        rail.GetComponent<UnityEngine.UI.Image>().sprite = FormulaMenuAssets.Current.panelSprite;
        var thumb = Box(root, "Figma Scroll Thumb", 1263, 141, 5, 115, new Color32(14, 15, 20, 255));
        thumb.GetComponent<UnityEngine.UI.Image>().sprite = FormulaMenuAssets.Current.panelSprite;

        var cards = root.GetComponentsInChildren<FormulaEventCard>(true).OrderBy(c => c.EventId).ToArray();
        for (int i = 0; i < cards.Length; i++) StyleCard(cards[i], i);
        var policy = root.Find("Career Policy"); if (policy != null) policy.gameObject.SetActive(false);
        var serialized = new SerializedObject(view); var chrome = serialized.FindProperty("standardChrome");
        chrome.arraySize = 3;
        string[] chromeNames = { "Header", "Footer", "Screen Shade" };
        for (int i = 0; i < chromeNames.Length; i++) chrome.GetArrayElementAtIndex(i).objectReferenceValue = canvas.transform.Find(chromeNames[i]).gameObject;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        if(canvas.UpgradeTheme)FormulaMenuUnifiedBuilder.StyleCareer(view);
    }

    private static void StyleCard(FormulaEventCard card, int index)
    {
        var root = (RectTransform)card.transform;
        float[] xs = { 23, 437, 852, 24, 437, 850 };
        Place(root, xs[index], index < 3 ? 124 : 374, 398, 216);
        foreach (string name in new[] { "Race Format", "Card Rule", "Objective Heading", "Pass Objective", "Available Reward", "Selection Rail", "Selection Underline", "Selection Arrow", "Focus Top", "Focus Bottom", "Focus Left", "Focus Right" })
        {
            var child = root.Find(name); if (child != null) child.gameObject.SetActive(false);
        }
        var normal = Art(root, "Figma Card Surface", "CardNormal", 0, 0, 398, 216); normal.transform.SetAsFirstSibling();
        var selected = Art(root, "Figma Selected Surface", "CardSelected", 0, 0, 398, 216); selected.transform.SetSiblingIndex(1);
        var oldFlag = root.Find("Country Badge"); if (oldFlag != null) { oldFlag.gameObject.SetActive(false); oldFlag.GetComponent<UnityEngine.UI.RawImage>().texture = null; }
        var iconRoot = Group(root, "Mission Type Icon", 19, 31, 33, 33);
        var icon = iconRoot.GetComponent<FormulaMissionIcon>() ?? iconRoot.gameObject.AddComponent<FormulaMissionIcon>();
        icon.color = Color.white; icon.raycastTarget = false; icon.SetEvent(FormulaCatalog.Current.Event(card.EventId)); Set(card, "missionIcon", icon);
        var title = root.Find("Event Name").GetComponent<TMP_Text>();
        Configure(title, 56, 27, 322, 41, 60, FormulaMenuAssets.Current.titleFont);
        title.enableAutoSizing = true; title.fontSizeMin = 52; title.fontSizeMax = 60;
        var stage = root.Find("Stage").GetComponent<TMP_Text>(); Configure(stage, 299, 9, 81, 20, 16, FormulaMenuAssets.Current.bodyFont); stage.alignment = TextAlignmentOptions.Center;
        var subtitle = root.Find("Circuit").GetComponent<TMP_Text>(); Configure(subtitle, 20, 76, 214, 26, 20, FormulaMenuAssets.Current.strongFont);
        var date = root.Find("Unlock and Medal").GetComponent<TMP_Text>(); Configure(date, 34, 160, 220, 33, 26, FormulaMenuAssets.Current.strongFont);
        date.horizontalAlignment = HorizontalAlignmentOptions.Left;
        var route = root.Find("Circuit Outline").GetComponent<FormulaTrackMap>(); route.gameObject.SetActive(true);
        Place(route.rectTransform, 181, 72, 188, 108); route.SetTrack(FormulaCatalog.Current.Event(card.EventId).trackId); route.color = Color.white;
        var action = Art(root, "Figma To Race", "ToRace", 285, 180, 84, 24);
        var plate = card.GetComponent<UnityEngine.UI.Image>(); plate.color = Color.clear;
        var button = card.GetComponent<UnityEngine.UI.Button>(); button.transition = UnityEngine.UI.Selectable.Transition.None; button.targetGraphic = plate;
        card.GetComponent<FormulaMenuFocus>().StyleFigma(selected, action);
        var s = new SerializedObject(card); s.FindProperty("figmaLayout").boolValue = true; s.ApplyModifiedPropertiesWithoutUndo();
        // The prefab presents meaningful text immediately in Edit Mode as well.
        title.text = FormulaAssets.Current.FindTrack(FormulaCatalog.Current.Event(card.EventId).trackId).displayName.Replace(" Circuit", "").ToUpperInvariant();
        stage.text = "Round " + (index + 1); subtitle.text = FormulaCatalog.Current.Event(card.EventId).name; date.text = FormulaSeasonCalendar.Current.Caption(FormulaCatalog.Current.Event(card.EventId));
    }

    private static void Set(UnityEngine.Object target, string name, UnityEngine.Object value)
    {
        var s = new SerializedObject(target); s.FindProperty(name).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Place(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
    }
    private static RectTransform Group(Transform p, string name, float x, float y, float w, float h)
    {
        var r = p.Find(name) as RectTransform ?? FormulaUI.Rect(p, name, x, y, w, h); Place(r, x, y, w, h); return r;
    }
    private static RectTransform Box(Transform p, string name, float x, float y, float w, float h, Color color)
    {
        var r = Group(p, name, x, y, w, h); var image = r.GetComponent<UnityEngine.UI.Image>() ?? r.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false; return r;
    }
    private static UnityEngine.UI.RawImage Art(Transform p, string name, string file, float x, float y, float w, float h, bool shared = false)
    {
        var r = Group(p, name, x, y, w, h); var image = r.GetComponent<UnityEngine.UI.RawImage>() ?? r.gameObject.AddComponent<UnityEngine.UI.RawImage>();
        image.texture = shared ? FormulaMenuAssets.Current.footerGlow : AssetDatabase.LoadAssetAtPath<Texture2D>(ArtFolder + file + ".png");
        if (image.texture == null) throw new InvalidOperationException("Missing Figma artwork: " + file);
        image.color = Color.white; image.raycastTarget = false; return image;
    }
    private static TMP_Text Label(Transform p, string name, string content, float x, float y, float w, float h, int size, TMP_FontAsset font)
    {
        var t = p.Find(name)?.GetComponent<TMP_Text>() ?? FormulaMenuStyle.Text(p, content, x, y, w, h, size);
        t.name = name; t.text = content; Configure(t, x, y, w, h, size, font); return t;
    }
    private static void Configure(TMP_Text t, float x, float y, float w, float h, int size, TMP_FontAsset font)
    {
        Place(t.rectTransform, x, y, w, h); t.font = font; t.fontSize = t.fontSizeMin = t.fontSizeMax = size;
        t.enableAutoSizing = false; t.color = Color.white; t.fontStyle = FontStyles.Normal;
        t.characterSpacing = 0; t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow;
        t.alignment = TextAlignmentOptions.MidlineLeft; t.raycastTarget = false;
    }
}
