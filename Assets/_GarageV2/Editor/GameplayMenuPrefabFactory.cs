using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class GameplayMenuPrefabFactory
{
    private const string ResourcesFolder = "Assets/_GarageV2/Resources/UI";
    private const string PausePrefabPath = ResourcesFolder + "/GameplayPauseMenu.prefab";
    private const string SettingsPrefabPath = ResourcesFolder + "/GameplaySettingsPanel.prefab";
    private const string ExistingSettingsPrefabPath = ResourcesFolder + "/Settings.prefab";
    private const string GameplayPrefabPath = "Assets/_GarageV2/Prefabs/GameplayPrefab.prefab";

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.delayCall += EnsureAssetsReady;
    }

    [MenuItem("Tools/RacingRCCP/Refresh Gameplay Menu Prefabs")]
    private static void RefreshPrefabs()
    {
        EnsureAssetsReady(forceRebuild: true);
    }

    private static void EnsureAssetsReady()
    {
        EnsureAssetsReady(false);
    }

    private static void EnsureAssetsReady(bool forceRebuild)
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        EnsureFolder(ResourcesFolder);

        if (forceRebuild || AssetDatabase.LoadAssetAtPath<GameObject>(PausePrefabPath) == null)
            CreatePausePrefab();

        if (AssetDatabase.LoadAssetAtPath<GameObject>(ExistingSettingsPrefabPath) == null &&
            (forceRebuild || AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath) == null))
            CreateSettingsPrefab();

        WireGameplayPrefab();
        AssetDatabase.SaveAssets();
    }

    private static void CreatePausePrefab()
    {
        var resources = new DefaultControls.Resources();
        var root = new GameObject("GameplayPauseMenu", typeof(RectTransform), typeof(GameplayPauseMenuView));
        var rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);

        var dimmer = CreateImage("Dimmer", root.transform, new Color(0f, 0f, 0f, 0.68f));
        Stretch(dimmer.rectTransform);

        var panel = CreateImage("Panel", root.transform, new Color(0.09f, 0.11f, 0.15f, 0.95f));
        var panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(460f, 360f);
        panelRect.anchoredPosition = Vector2.zero;
        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(32, 32, 32, 32);
        layout.spacing = 18f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var title = CreateTMPText("Title", panel.transform, "Paused", 42f, FontStyles.Bold);
        var titleLayout = title.gameObject.AddComponent<LayoutElement>();
        titleLayout.preferredHeight = 56f;

        var continueButton = CreateButton("ContinueButton", panel.transform, resources, "Continue");
        var settingsButton = CreateButton("SettingsButton", panel.transform, resources, "Settings");
        var homeButton = CreateButton("HomeButton", panel.transform, resources, "Home");

        ConfigureButtonLayout(continueButton);
        ConfigureButtonLayout(settingsButton);
        ConfigureButtonLayout(homeButton);

        var view = root.GetComponent<GameplayPauseMenuView>();
        AssignPauseView(view, root, continueButton, settingsButton, homeButton, title);

        PrefabUtility.SaveAsPrefabAsset(root, PausePrefabPath);
        Object.DestroyImmediate(root);
    }

    private static void CreateSettingsPrefab()
    {
        var resources = new DefaultControls.Resources();
        var root = new GameObject("GameplaySettingsPanel", typeof(RectTransform), typeof(GameplaySettingsPanelView));
        var settingsManager = root.AddComponent<SettingsManager>();
        var rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);

        var dimmer = CreateImage("Dimmer", root.transform, new Color(0f, 0f, 0f, 0.68f));
        Stretch(dimmer.rectTransform);

        var panel = CreateImage("Panel", root.transform, new Color(0.09f, 0.11f, 0.15f, 0.95f));
        var panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(560f, 420f);
        panelRect.anchoredPosition = Vector2.zero;
        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(32, 32, 32, 32);
        layout.spacing = 16f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var title = CreateTMPText("Title", panel.transform, "Settings", 38f, FontStyles.Bold);
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;

        var sfxRow = CreateSliderRow("Sfx", panel.transform, resources, out Slider sfxSlider);
        var vehicleRow = CreateSliderRow("Vehicle", panel.transform, resources, out Slider vehicleSlider);
        var musicRow = CreateSliderRow("Music", panel.transform, resources, out Slider musicSlider);

        ConfigureSliderDefaults(sfxSlider);
        ConfigureSliderDefaults(vehicleSlider);
        ConfigureSliderDefaults(musicSlider);

        var backButton = CreateButton("BackButton", panel.transform, resources, "Back");
        ConfigureButtonLayout(backButton);

        var view = root.GetComponent<GameplaySettingsPanelView>();
        ConfigureSettingsManager(settingsManager, sfxSlider, vehicleSlider, musicSlider);
        AssignSettingsView(view, root, sfxSlider, vehicleSlider, musicSlider, backButton, title);

        PrefabUtility.SaveAsPrefabAsset(root, SettingsPrefabPath);
        Object.DestroyImmediate(root);
    }

    private static void WireGameplayPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(GameplayPrefabPath) == null)
            return;

        var pausePrefab = AssetDatabase.LoadAssetAtPath<GameplayPauseMenuView>(PausePrefabPath);
        var settingsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExistingSettingsPrefabPath);
        if (settingsPrefab == null)
            settingsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath);

        if (pausePrefab == null || settingsPrefab == null)
            return;

        var root = PrefabUtility.LoadPrefabContents(GameplayPrefabPath);
        bool changed = false;

        var controller = root.GetComponent<GameplayPauseMenuController>();
        if (controller == null)
        {
            controller = root.AddComponent<GameplayPauseMenuController>();
            changed = true;
        }

        var gameplayManager = root.GetComponent<GamePlayManager>() ?? root.GetComponentInChildren<GamePlayManager>(true);
        var targetCanvas = root.GetComponentInChildren<Canvas>(true);

        SerializedObject serializedController = new SerializedObject(controller);
        changed |= SetObjectReference(serializedController, "gameplayManager", gameplayManager);
        changed |= SetObjectReference(serializedController, "targetCanvas", targetCanvas);
        changed |= SetObjectReference(serializedController, "pauseMenuPrefab", pausePrefab);
        changed |= SetObjectReference(serializedController, "settingsPanelPrefab", settingsPrefab);

        if (changed)
        {
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, GameplayPrefabPath);
        }

        PrefabUtility.UnloadPrefabContents(root);
    }

    private static GameObject CreateSliderRow(string label, Transform parent, DefaultControls.Resources resources, out Slider slider)
    {
        var row = new GameObject(label + "Row", typeof(RectTransform), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        var rowRect = row.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, 72f);
        row.GetComponent<LayoutElement>().preferredHeight = 72f;

        var labelText = CreateTMPText(label + "Label", row.transform, label, 26f, FontStyles.Normal);
        var labelRect = labelText.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = new Vector2(0f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(0f, 12f);
        labelRect.sizeDelta = new Vector2(180f, 36f);

        var sliderObject = DefaultControls.CreateSlider(resources);
        sliderObject.name = label + "Slider";
        sliderObject.transform.SetParent(row.transform, false);
        slider = sliderObject.GetComponent<Slider>();

        var sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0f);
        sliderRect.anchorMax = new Vector2(1f, 0f);
        sliderRect.offsetMin = new Vector2(0f, -24f);
        sliderRect.offsetMax = new Vector2(0f, 12f);

        return row;
    }

    private static void ConfigureSliderDefaults(Slider slider)
    {
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;

        var background = slider.transform.Find("Background")?.GetComponent<Image>();
        if (background != null)
            background.color = new Color(0.23f, 0.27f, 0.34f, 1f);

        var fill = slider.transform.Find("Fill Area/Fill")?.GetComponent<Image>();
        if (fill != null)
            fill.color = new Color(0.18f, 0.63f, 1f, 1f);

        var handle = slider.transform.Find("Handle Slide Area/Handle")?.GetComponent<Image>();
        if (handle != null)
            handle.color = Color.white;
    }

    private static Button CreateButton(string name, Transform parent, DefaultControls.Resources resources, string label)
    {
        var buttonObject = DefaultControls.CreateButton(resources);
        buttonObject.name = name;
        buttonObject.transform.SetParent(parent, false);

        var image = buttonObject.GetComponent<Image>();
        if (image != null)
            image.color = new Color(0.18f, 0.63f, 1f, 1f);

        var buttonText = buttonObject.GetComponentInChildren<Text>();
        if (buttonText != null)
        {
            buttonText.text = label;
            buttonText.fontSize = 24;
            buttonText.color = Color.white;
        }

        return buttonObject.GetComponent<Button>();
    }

    private static void ConfigureButtonLayout(Button button)
    {
        var rect = button.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 56f);

        var layout = button.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 56f;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI CreateTMPText(string name, Transform parent, string text, float size, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void AssignPauseView(GameplayPauseMenuView view, GameObject root, Button continueButton, Button settingsButton, Button homeButton, TextMeshProUGUI title)
    {
        SerializedObject serializedObject = new SerializedObject(view);
        serializedObject.FindProperty("root").objectReferenceValue = root;
        serializedObject.FindProperty("continueButton").objectReferenceValue = continueButton;
        serializedObject.FindProperty("settingsButton").objectReferenceValue = settingsButton;
        serializedObject.FindProperty("homeButton").objectReferenceValue = homeButton;
        serializedObject.FindProperty("titleText").objectReferenceValue = title;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignSettingsView(GameplaySettingsPanelView view, GameObject root, Slider sfx, Slider vehicle, Slider music, Button backButton, TextMeshProUGUI title)
    {
        SerializedObject serializedObject = new SerializedObject(view);
        serializedObject.FindProperty("root").objectReferenceValue = root;
        serializedObject.FindProperty("sfxSlider").objectReferenceValue = sfx;
        serializedObject.FindProperty("vehicleSlider").objectReferenceValue = vehicle;
        serializedObject.FindProperty("musicSlider").objectReferenceValue = music;
        serializedObject.FindProperty("backButton").objectReferenceValue = backButton;
        serializedObject.FindProperty("titleText").objectReferenceValue = title;
        serializedObject.FindProperty("settingsManager").objectReferenceValue = view.GetComponent<SettingsManager>();
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureSettingsManager(SettingsManager settingsManager, Slider sfx, Slider vehicle, Slider music)
    {
        SerializedObject serializedObject = new SerializedObject(settingsManager);
        serializedObject.FindProperty("sfx").objectReferenceValue = sfx;
        serializedObject.FindProperty("vehicle").objectReferenceValue = vehicle;
        serializedObject.FindProperty("music").objectReferenceValue = music;
        serializedObject.FindProperty("managePreviewVehicleState").boolValue = false;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool SetObjectReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue == value)
            return false;

        property.objectReferenceValue = value;
        return true;
    }

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
    }

    private static void EnsureFolder(string assetPath)
    {
        string[] parts = assetPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
}
