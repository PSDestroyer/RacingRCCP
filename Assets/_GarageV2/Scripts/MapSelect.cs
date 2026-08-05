using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HalvaStudio.Save;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One-screen level carousel. Every MapSO found in Resources/SO/MapSo is one level.
/// </summary>
public class MapSelect : MonoBehaviour
{
    private const string MapResourcesPath = "SO/MapSo";

    [Header("Header")]
    [SerializeField] private bool autoBuildInterfaceIfMissing = true;
    [SerializeField] private string championshipTitle = "Bronze Championship";
    [SerializeField] private TMP_Text championshipTitleText;

    [Header("Selected level")]
    [SerializeField] private TMP_Text levelNameText;
    [SerializeField] private TMP_Text gameModeText;
    [SerializeField] private TMP_Text gameTargetText;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private Image levelImage;

    [Header("Navigation")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button startButton;
    [SerializeField] private bool wrapNavigation = true;

    [Header("Page indicators")]
    [SerializeField] private Transform indicatorContainer;
    [SerializeField] private Image indicatorPrefab;
    [SerializeField] private Color indicatorColor = new Color(0.35f, 0.75f, 0.95f, 1f);
    [SerializeField] private Color selectedIndicatorColor = new Color(0f, 0.55f, 0.8f, 1f);

    [Header("Progress")]
    [SerializeField] private bool unlockAllLevelsForTesting;

    private readonly List<MapSO> levels = new List<MapSO>();
    private readonly List<Image> indicators = new List<Image>();
    private int selectedLevelIndex;
    private bool isStartingLevel;

    public IReadOnlyList<MapSO> Levels => levels;
    public MapSO SelectedLevel => IsValidIndex(selectedLevelIndex) ? levels[selectedLevelIndex] : null;

    private void Awake()
    {
        if (autoBuildInterfaceIfMissing && !HasRequiredInterface())
            BuildRuntimeInterface();

        LoadLevels();
        RegisterButtonListeners();
    }

    private void Start()
    {
        // InputManager can initialize after this component's OnEnable depending on scene order.
        RegisterInputActions();
    }

    private void OnEnable()
    {
        RegisterInputActions();
        EnsureProgressInitialized();

        if (levels.Count == 0)
            LoadLevels();

        RestoreSelectedLevel();
        RebuildIndicators();
        RefreshView();
        RequestSelection(startButton != null ? startButton.gameObject : null);
    }

    private void OnDestroy()
    {
        UnregisterButtonListeners();
    }

    private void OnDisable()
    {
        UnregisterInputActions();
        isStartingLevel = false;
    }

    public void PreviousLevel()
    {
        ChangeLevel(-1);
    }

    public void NextLevel()
    {
        ChangeLevel(1);
    }

    public void SelectLevel(int index)
    {
        if (!IsValidIndex(index))
            return;

        selectedLevelIndex = index;
        SaveSelectedLevel();
        RefreshView();
        PlayClick();
    }

    public void StartSelectedLevel()
    {
        if (isStartingLevel)
            return;

        MapSO level = SelectedLevel;

        if (level == null || !IsLevelUnlocked(selectedLevelIndex))
            return;

        if (string.IsNullOrWhiteSpace(level.sceneName))
        {
            Debug.LogError($"MapSO '{level.name}' has no Scene Name assigned.", level);
            return;
        }

        isStartingLevel = true;
        GlobalCarData.thismap = level;
        SetUpRaceStyle(level.raceType);
        SaveSelectedLevel();
        PlayClick();

        if (LoadingManager.Instance != null)
            LoadingManager.Instance.LoadScene(level.sceneName);
        else
            SceneManager.LoadScene(level.sceneName);
    }

    /// <summary>
    /// The carousel has no internal submenu, so GarageUIController handles Back directly.
    /// </summary>
    public bool HandleBack()
    {
        return false;
    }

    private void LoadLevels()
    {
        levels.Clear();
        levels.AddRange(Resources.LoadAll<MapSO>(MapResourcesPath)
            .Where(level => level != null)
            .OrderBy(level => level.id)
            .ThenBy(level => level.name));

        GlobalCarData._maplists = new List<MapSO>(levels);
        selectedLevelIndex = Mathf.Clamp(selectedLevelIndex, 0, Mathf.Max(0, levels.Count - 1));
    }

    private void ChangeLevel(int direction)
    {
        if (levels.Count == 0 || direction == 0)
            return;

        int nextIndex = selectedLevelIndex + direction;

        if (wrapNavigation)
            nextIndex = (nextIndex % levels.Count + levels.Count) % levels.Count;
        else
            nextIndex = Mathf.Clamp(nextIndex, 0, levels.Count - 1);

        SelectLevel(nextIndex);
        RequestSelection(startButton != null ? startButton.gameObject : null);
    }

    private void OnNavigate(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        Vector2 navigation = context.ReadValue<Vector2>();

        if (Mathf.Abs(navigation.x) < .5f || Mathf.Abs(navigation.x) <= Mathf.Abs(navigation.y))
            return;

        if (navigation.x < 0f)
            PreviousLevel();
        else
            NextLevel();
    }

    private void OnSubmit(InputAction.CallbackContext context)
    {
        if (context.performed)
            StartSelectedLevel();
    }

    private void RefreshView()
    {
        MapSO level = SelectedLevel;
        bool hasLevel = level != null;
        bool unlocked = hasLevel && IsLevelUnlocked(selectedLevelIndex);

        if (championshipTitleText != null)
            championshipTitleText.text = championshipTitle;

        if (levelNameText != null)
            levelNameText.text = hasLevel ? GetLevelName(level) : "No levels found";

        if (gameModeText != null)
            gameModeText.text = hasLevel ? GetModeText(level.raceType) : "-";

        if (gameTargetText != null)
            gameTargetText.text = hasLevel ? GetTargetText(level) : "-";

        if (rewardText != null)
            rewardText.text = hasLevel ? level.price.ToString() : "0";

        if (levelImage != null)
        {
            levelImage.sprite = hasLevel ? level.mapsprite : null;
            levelImage.enabled = levelImage.sprite != null;
        }

        if (startButton != null)
            startButton.interactable = unlocked && !string.IsNullOrWhiteSpace(level.sceneName);

        if (previousButton != null)
            previousButton.interactable = levels.Count > 1 && (wrapNavigation || selectedLevelIndex > 0);

        if (nextButton != null)
            nextButton.interactable = levels.Count > 1 && (wrapNavigation || selectedLevelIndex < levels.Count - 1);

        RefreshIndicators();
    }

    private void RebuildIndicators()
    {
        indicators.Clear();

        if (indicatorContainer == null)
            return;

        for (int i = indicatorContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = indicatorContainer.GetChild(i);

            if (indicatorPrefab == null || child.gameObject != indicatorPrefab.gameObject)
                Destroy(child.gameObject);
        }

        if (indicatorPrefab != null)
            indicatorPrefab.gameObject.SetActive(false);

        for (int i = 0; i < levels.Count; i++)
        {
            int capturedIndex = i;
            Image indicator;

            if (indicatorPrefab != null)
            {
                indicator = Instantiate(indicatorPrefab, indicatorContainer);
            }
            else
            {
                GameObject indicatorObject = new GameObject($"Level Indicator {i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                indicatorObject.transform.SetParent(indicatorContainer, false);
                indicator = indicatorObject.GetComponent<Image>();
                RectTransform indicatorRect = indicatorObject.GetComponent<RectTransform>();
                indicatorRect.sizeDelta = new Vector2(24f, 24f);
            }

            indicator.gameObject.SetActive(true);
            indicators.Add(indicator);

            Button indicatorButton = indicator.GetComponent<Button>();
            if (indicatorButton != null)
                indicatorButton.onClick.AddListener(() => SelectLevel(capturedIndex));
        }
    }

    private void RefreshIndicators()
    {
        for (int i = 0; i < indicators.Count; i++)
        {
            if (indicators[i] != null)
                indicators[i].color = i == selectedLevelIndex ? selectedIndicatorColor : indicatorColor;
        }
    }

    private bool IsLevelUnlocked(int index)
    {
        if (unlockAllLevelsForTesting || index == 0)
            return true;

        return SaveManager.Instance == null || SaveManager.Instance.IsMissionUnlocked(0, 0, index);
    }

    private void RestoreSelectedLevel()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.saveData == null || levels.Count == 0)
        {
            selectedLevelIndex = 0;
            return;
        }

        int savedId = SaveManager.Instance.saveData.currentMissionMapId;
        int foundIndex = levels.FindIndex(level => level.id == savedId);
        selectedLevelIndex = foundIndex >= 0 ? foundIndex : 0;
    }

    private void SaveSelectedLevel()
    {
        MapSO level = SelectedLevel;

        if (level == null || SaveManager.Instance == null || SaveManager.Instance.saveData == null)
            return;

        SaveManager.SaveData data = SaveManager.Instance.saveData;
        data.selectedMapName = GetLevelName(level);
        data.selectedTrackName = level.sceneName;
        // The existing save progression is reused as one virtual map, one virtual track,
        // and one mission entry per MapSO level.
        data.selectedMapIndex = 0;
        data.selectedTrackIndex = 0;
        data.selectedMissionIndex = selectedLevelIndex;
        data.currentMapTrackCount = 1;
        data.currentTrackMissionCount = levels.Count;
        data.currentMap = level.id;
        data.currentMissionMapId = level.id;
        data.currentMissionRaceType = (int)level.raceType;
        SaveManager.Instance.Save();
    }

    private void EnsureProgressInitialized()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.EnsureMissionProgressInitialized();
    }

    private static string GetLevelName(MapSO level)
    {
        return !string.IsNullOrWhiteSpace(level.mapName) ? level.mapName : level.name;
    }

    private static string GetModeText(RaceType raceType)
    {
        return raceType.ToString();
    }

    private static string GetTargetText(MapSO level)
    {
        switch (level.raceType)
        {
            case RaceType.Racing:
                return $"{Mathf.Max(1, level.raceLaps)} laps";
            case RaceType.Elimination:
                return $"Survive every {level.eliminationInterval:0}s";
            case RaceType.NoBrakeChallenge:
                return $"Finish under {level.limitedBrakeBronzeTime:0}s";
            case RaceType.TimeAttack:
                return $"Finish under {level.timeAttackBronzeTime}s";
            case RaceType.ChaseRace:
                return $"Catch the target in {Mathf.Max(1, level.chaseLapLimit)} laps";
            case RaceType.DriftScore:
                return $"Score {level.driftBronzeTarget}";
            case RaceType.PerfectDrift:
                return $"Drift for {level.perfectDriftBronzeTime:0}s";
            case RaceType.TargetDrift:
                return $"Score {level.targetDriftScore}";
            case RaceType.ComboMaster:
                return $"Reach x{level.comboBronzeTarget:0.#}";
            case RaceType.FreeDrift:
                return "Free drive";
            default:
                return level.target.ToString();
        }
    }

    private void SetUpRaceStyle(RaceType raceType)
    {
        int behaviorIndex = GetDrivingStyleIndex(raceType);
        RCCP_Settings settings = RCCP_RuntimeSettings.RCCPSettingsInstance;

        if (settings == null || settings.behaviorTypes == null || settings.behaviorTypes.Length == 0)
            return;

        behaviorIndex = Mathf.Clamp(behaviorIndex, 0, settings.behaviorTypes.Length - 1);
        settings.behaviorSelectedIndex = behaviorIndex;

        if (RCCP_SceneManager.Instance != null)
            RCCP_SceneManager.Instance.SetBehavior(behaviorIndex);
    }

    private static int GetDrivingStyleIndex(RaceType raceType)
    {
        switch (raceType)
        {
            case RaceType.FreeDrift:
            case RaceType.DriftScore:
            case RaceType.PerfectDrift:
            case RaceType.TargetDrift:
            case RaceType.ComboMaster:
                return 1;
            case RaceType.Racing:
            case RaceType.Elimination:
            case RaceType.NoBrakeChallenge:
            case RaceType.TimeAttack:
            case RaceType.ChaseRace:
                return 2;
            default:
                return 0;
        }
    }

    private bool IsValidIndex(int index)
    {
        return index >= 0 && index < levels.Count;
    }

    private void RegisterButtonListeners()
    {
        if (previousButton != null)
            previousButton.onClick.AddListener(PreviousLevel);
        if (nextButton != null)
            nextButton.onClick.AddListener(NextLevel);
        if (startButton != null)
            startButton.onClick.AddListener(StartSelectedLevel);
    }

    private void UnregisterButtonListeners()
    {
        if (previousButton != null)
            previousButton.onClick.RemoveListener(PreviousLevel);
        if (nextButton != null)
            nextButton.onClick.RemoveListener(NextLevel);
        if (startButton != null)
            startButton.onClick.RemoveListener(StartSelectedLevel);
    }

    private void RegisterInputActions()
    {
        InputAction navigateAction = GetInputAction("Navigate");
        InputAction submitAction = GetInputAction("Submit");

        if (navigateAction != null)
        {
            navigateAction.performed -= OnNavigate;
            navigateAction.performed += OnNavigate;
        }

        if (submitAction != null)
        {
            submitAction.performed -= OnSubmit;
            submitAction.performed += OnSubmit;
        }
    }

    private void UnregisterInputActions()
    {
        InputAction navigateAction = GetInputAction("Navigate");
        InputAction submitAction = GetInputAction("Submit");

        if (navigateAction != null)
            navigateAction.performed -= OnNavigate;

        if (submitAction != null)
            submitAction.performed -= OnSubmit;
    }

    private InputAction GetInputAction(string actionName)
    {
        if (playerInput == null && InputManager.Instance != null)
            playerInput = InputManager.Instance.GetPlayerInput();

        if (playerInput == null || playerInput.actions == null)
            return null;

        return playerInput.actions.FindAction(actionName, false);
    }

    private void RequestSelection(GameObject target)
    {
        if (target != null)
            StartCoroutine(SelectNextFrame(target));
    }

    private static IEnumerator SelectNextFrame(GameObject target)
    {
        yield return null;

        if (target == null || !target.activeInHierarchy || EventSystem.current == null)
            yield break;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(target);
    }

    private static void PlayClick()
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayButtonClick();
    }

    private bool HasRequiredInterface()
    {
        return levelNameText != null && gameModeText != null && gameTargetText != null &&
               rewardText != null && levelImage != null && previousButton != null &&
               nextButton != null && startButton != null && indicatorContainer != null;
    }

    private void BuildRuntimeInterface()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            transform.GetChild(i).gameObject.SetActive(false);

        GameObject root = CreateUIObject("Level Carousel", transform);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);

        Image background = root.AddComponent<Image>();
        background.color = new Color(0.08f, 0.08f, 0.08f, 0.96f);

        championshipTitleText = CreateText("Championship Title", root.transform, "Championship", 42f, TextAlignmentOptions.Center);
        SetAnchors(championshipTitleText.rectTransform, new Vector2(.22f, .84f), new Vector2(.78f, .97f));

        GameObject card = CreateUIObject("Selected Level Card", root.transform);
        Image cardImage = card.AddComponent<Image>();
        cardImage.color = new Color(0.10f, 0.10f, 0.10f, .98f);
        SetAnchors(card.GetComponent<RectTransform>(), new Vector2(.15f, .20f), new Vector2(.85f, .82f));

        levelNameText = CreateText("Level Name", card.transform, "Level", 34f, TextAlignmentOptions.Center);
        SetAnchors(levelNameText.rectTransform, new Vector2(.05f, .84f), new Vector2(.95f, .98f));

        levelImage = CreateUIObject("Level Picture", card.transform).AddComponent<Image>();
        levelImage.preserveAspect = true;
        SetAnchors(levelImage.rectTransform, new Vector2(.37f, .20f), new Vector2(.72f, .80f));

        CreateInfoRow(card.transform, "Game Mode Label", "GAME MODE", .66f, out gameModeText);
        CreateInfoRow(card.transform, "Game Target Label", "GAME TARGET", .48f, out gameTargetText);
        CreateInfoRow(card.transform, "Reward Label", "REWARD", .30f, out rewardText);

        previousButton = CreateButton("Previous Level", root.transform, "<", null);
        SetAnchors(previousButton.GetComponent<RectTransform>(), new Vector2(.05f, .42f), new Vector2(.13f, .60f));

        nextButton = CreateButton("Next Level", root.transform, ">", null);
        SetAnchors(nextButton.GetComponent<RectTransform>(), new Vector2(.87f, .42f), new Vector2(.95f, .60f));

        startButton = CreateButton("Start Level", card.transform, "START", null);
        SetAnchors(startButton.GetComponent<RectTransform>(), new Vector2(.75f, .28f), new Vector2(.94f, .72f));

        GameObject indicatorRoot = CreateUIObject("Level Indicators", root.transform);
        indicatorContainer = indicatorRoot.transform;
        SetAnchors(indicatorRoot.GetComponent<RectTransform>(), new Vector2(.18f, .07f), new Vector2(.82f, .16f));
        HorizontalLayoutGroup layout = indicatorRoot.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    private static void CreateInfoRow(Transform parent, string objectName, string label, float verticalPosition, out TMP_Text valueText)
    {
        TMP_Text labelText = CreateText(objectName, parent, label, 22f, TextAlignmentOptions.Left);
        SetAnchors(labelText.rectTransform, new Vector2(.05f, verticalPosition), new Vector2(.22f, verticalPosition + .13f));

        valueText = CreateText($"{label} Value", parent, "-", 24f, TextAlignmentOptions.Left);
        SetAnchors(valueText.rectTransform, new Vector2(.21f, verticalPosition), new Vector2(.36f, verticalPosition + .13f));
    }

    private static GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform));
        result.layer = parent.gameObject.layer;
        result.transform.SetParent(parent, false);
        return result;
    }

    private static TextMeshProUGUI CreateText(string objectName, Transform parent, string value, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUIObject(objectName, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = fontSize;
        return text;
    }

    private static Button CreateButton(string objectName, Transform parent, string label, UnityEngine.Events.UnityAction callback)
    {
        GameObject buttonObject = CreateUIObject(objectName, parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0f, .55f, .8f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        if (callback != null)
            button.onClick.AddListener(callback);

        TextMeshProUGUI text = CreateText("Label", buttonObject.transform, label, 28f, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        return button;
    }

    private static void SetAnchors(RectTransform rect, Vector2 minimum, Vector2 maximum)
    {
        rect.anchorMin = minimum;
        rect.anchorMax = maximum;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rect)
    {
        SetAnchors(rect, Vector2.zero, Vector2.one);
    }
}
