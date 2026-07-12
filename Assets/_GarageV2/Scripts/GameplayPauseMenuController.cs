using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

public class GameplayPauseMenuController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GamePlayManager gameplayManager;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private GameplayPauseMenuView pauseMenuPrefab;
    [SerializeField] private GameObject settingsPanelPrefab;

    [Header("Behavior")]
    [SerializeField] private bool allowPauseBeforeRaceStart = true;
    [SerializeField] private string homeSceneName = "Menu";
    [SerializeField] private string pauseTitle = "Paused";
    [SerializeField] private string settingsTitle = "Settings";
    [SerializeField] private float uiMoveRepeatDelay = 0.7f;
    [SerializeField] private float uiMoveRepeatRate = 0.2f;
    [SerializeField] private float menuMoveCooldown = 0.18f;
    [SerializeField] private float settingsMoveCooldown = 0.18f;

    private GameplayPauseMenuView pauseMenuInstance;
    private GameplaySettingsPanelView settingsPanelInstance;
    private bool isPaused;
    private bool settingsOpen;
    private readonly List<Selectable> pauseFocusables = new();
    private float lastMenuMoveTime = -10f;
    private float lastSettingsMoveTime = -10f;

    public bool IsPaused => isPaused;

    private void Awake()
    {
        ResolveReferences();
        ConfigureUiInputModule();
        EnsureMenuInstances();
        WireMenuActions();
        HideAll();
    }

    private void OnDestroy()
    {
        if (pauseMenuInstance != null)
        {
            pauseMenuInstance.ContinueButton.onClick.RemoveListener(ResumeGameplay);
            pauseMenuInstance.SettingsButton.onClick.RemoveListener(OpenSettings);
            pauseMenuInstance.HomeButton.onClick.RemoveListener(ReturnHome);
        }

        if (settingsPanelInstance != null)
            settingsPanelInstance.BackButton.onClick.RemoveListener(BackFromSettings);

        RestoreGameplayState();
    }

    private void Update()
    {
        if (!CanHandlePauseInput())
            return;

        if (settingsOpen)
            HandlePauseSettingsNavigationInput();
        else
            HandlePauseMenuNavigationInput();

        if (settingsOpen && WasBackPressed())
        {
            BackFromSettings();
            return;
        }

        if (WasPausePressed())
            HandlePausePressed();
    }

    public void TogglePause()
    {
        if (isPaused)
        {
            if (settingsOpen)
                BackFromSettings();
            else
                ResumeGameplay();
        }
        else
        {
            PauseGameplay();
        }
    }

    public void PauseGameplay()
    {
        if (isPaused)
            return;

        isPaused = true;
        settingsOpen = false;

        ApplyPausedState();
        ShowPauseMenu();
    }

    public void ResumeGameplay()
    {
        isPaused = false;
        settingsOpen = false;

        HideAll();
        RestoreGameplayState();
    }

    public void OpenSettings()
    {
        if (!isPaused)
            PauseGameplay();

        settingsOpen = true;

        if (pauseMenuInstance != null)
            pauseMenuInstance.Hide();

        if (settingsPanelInstance != null)
        {
            settingsPanelInstance.Show();
            settingsPanelInstance.RebuildNavigationCache();
            SetNavigationEventsEnabled(false);
            SelectObject(settingsPanelInstance.DefaultSelected);
        }
    }

    public void BackFromSettings()
    {
        settingsOpen = false;

        if (settingsPanelInstance != null)
            settingsPanelInstance.Hide();

        SetNavigationEventsEnabled(true);
        ShowPauseMenu();
    }

    public void ReturnHome()
    {
        Time.timeScale = 1f;
        isPaused = false;
        settingsOpen = false;

        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.LoadScene(homeSceneName);
            return;
        }

        SceneManager.LoadScene(homeSceneName);
    }

    private void HandlePausePressed()
    {
        if (!isPaused)
        {
            PauseGameplay();
            return;
        }

        if (settingsOpen)
        {
            BackFromSettings();
            return;
        }

        ResumeGameplay();
    }

    private bool CanHandlePauseInput()
    {
        if (pauseMenuInstance == null || settingsPanelInstance == null)
            return false;

        if (gameplayManager == null)
            return true;

        if (!allowPauseBeforeRaceStart && !gameplayManager.IsRaceStartedForPause())
            return false;

        if (gameplayManager.IsAnyResultScreenVisible())
            return false;

        return true;
    }

    private bool WasPausePressed()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            return true;

        if (Gamepad.current != null)
        {
            if (Gamepad.current.startButton.wasPressedThisFrame)
                return true;

            if (Gamepad.current.selectButton.wasPressedThisFrame)
                return true;
        }

        return false;
    }

    private bool WasBackPressed()
    {
        return Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
    }

    private bool WasSubmitPressed()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)
                return true;
        }

        return Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
    }

    private void ApplyPausedState()
    {
        Time.timeScale = 0f;

        if (gameplayManager != null && gameplayManager.CarController != null)
            gameplayManager.CarController.SetCanControl(false);
    }

    private void RestoreGameplayState()
    {
        Time.timeScale = 1f;

        if (gameplayManager != null && gameplayManager.CarController != null)
        {
            gameplayManager.CarController.externalControl = false;
            gameplayManager.CarController.SetCanControl(true);
        }
    }

    private void ShowPauseMenu()
    {
        if (settingsPanelInstance != null)
            settingsPanelInstance.Hide();

        if (pauseMenuInstance != null)
        {
            pauseMenuInstance.Show();
            SelectObject(pauseMenuInstance.DefaultSelected);
        }
    }

    private void HideAll()
    {
        if (pauseMenuInstance != null)
            pauseMenuInstance.Hide();

        if (settingsPanelInstance != null)
            settingsPanelInstance.Hide();

        SetNavigationEventsEnabled(true);
    }

    private void ResolveReferences()
    {
        if (gameplayManager == null)
            gameplayManager = GetComponent<GamePlayManager>() ?? GetComponentInChildren<GamePlayManager>(true);

        if (targetCanvas == null)
            targetCanvas = GetComponentInChildren<Canvas>(true);

        if (pauseMenuPrefab == null)
            pauseMenuPrefab = Resources.Load<GameplayPauseMenuView>("UI/GameplayPauseMenu");

        if (settingsPanelPrefab == null)
            settingsPanelPrefab = Resources.Load<GameObject>("UI/Settings");

        if (settingsPanelPrefab == null)
            settingsPanelPrefab = Resources.Load<GameObject>("UI/GameplaySettingsPanel");
    }

    private void EnsureMenuInstances()
    {
        if (targetCanvas == null)
        {
            Debug.LogWarning("GameplayPauseMenuController needs a target canvas.", this);
            return;
        }

        if (pauseMenuInstance == null && pauseMenuPrefab != null)
            pauseMenuInstance = Instantiate(pauseMenuPrefab, targetCanvas.transform);

        if (settingsPanelInstance == null && settingsPanelPrefab != null)
        {
            GameObject instance = Instantiate(settingsPanelPrefab, targetCanvas.transform);
            settingsPanelInstance = instance.GetComponent<GameplaySettingsPanelView>();

            if (settingsPanelInstance == null)
            {
                settingsPanelInstance = instance.AddComponent<GameplaySettingsPanelView>();
                settingsPanelInstance.AutoBindFromExistingRoot(instance);
            }
        }

        if (pauseMenuInstance != null)
        {
            pauseMenuInstance.SetTitle(pauseTitle);
            RebuildPauseNavigationCache();
        }

        if (settingsPanelInstance != null)
        {
            settingsPanelInstance.Initialize();
            settingsPanelInstance.SetTitle(settingsTitle);
        }
    }

    private void WireMenuActions()
    {
        if (pauseMenuInstance != null)
        {
            pauseMenuInstance.ContinueButton.onClick.RemoveListener(ResumeGameplay);
            pauseMenuInstance.SettingsButton.onClick.RemoveListener(OpenSettings);
            pauseMenuInstance.HomeButton.onClick.RemoveListener(ReturnHome);

            pauseMenuInstance.ContinueButton.onClick.AddListener(ResumeGameplay);
            pauseMenuInstance.SettingsButton.onClick.AddListener(OpenSettings);
            pauseMenuInstance.HomeButton.onClick.AddListener(ReturnHome);
        }

        if (settingsPanelInstance != null)
        {
            if (settingsPanelInstance.BackButton != null)
            {
                settingsPanelInstance.BackButton.onClick.RemoveListener(BackFromSettings);
                settingsPanelInstance.BackButton.onClick.AddListener(BackFromSettings);
            }
        }
    }

    private void SelectObject(GameObject selected)
    {
        if (selected == null || EventSystem.current == null)
            return;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(selected);
    }

    private void ConfigureUiInputModule()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            eventSystem = FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);

        if (eventSystem?.currentInputModule is not InputSystemUIInputModule inputModule)
            return;

        inputModule.moveRepeatDelay = uiMoveRepeatDelay;
        inputModule.moveRepeatRate = uiMoveRepeatRate;
    }

    private void RebuildPauseNavigationCache()
    {
        pauseFocusables.Clear();

        if (pauseMenuInstance == null)
            return;

        Button continueButton = pauseMenuInstance.ContinueButton;
        Button settingsButton = pauseMenuInstance.SettingsButton;
        Button homeButton = pauseMenuInstance.HomeButton;

        if (continueButton == null || settingsButton == null || homeButton == null)
            return;

        pauseFocusables.Add(continueButton);
        pauseFocusables.Add(settingsButton);
        pauseFocusables.Add(homeButton);

        for (int i = 0; i < pauseFocusables.Count; i++)
            SetNavigationNone(pauseFocusables[i]);
    }

    private void HandlePauseMenuNavigationInput()
    {
        if (!isPaused || settingsOpen || pauseFocusables.Count == 0)
            return;

        Vector2 direction = GetNavigationDirection();
        if (direction == Vector2.zero)
            return;

        if (Time.unscaledTime - lastMenuMoveTime < menuMoveCooldown)
            return;

        MoveSelection(pauseFocusables, direction);
        lastMenuMoveTime = Time.unscaledTime;
    }

    private void HandlePauseSettingsNavigationInput()
    {
        if (!isPaused || !settingsOpen || EventSystem.current == null)
            return;

        Vector2 direction = GetNavigationDirection();
        bool submitPressed = WasSubmitPressed();

        if (direction == Vector2.zero && !submitPressed)
            return;

        if (Time.unscaledTime - lastSettingsMoveTime < settingsMoveCooldown)
            return;

        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        if (selectedObject == null && settingsPanelInstance != null)
        {
            SelectObject(settingsPanelInstance.DefaultSelected);
            selectedObject = EventSystem.current.currentSelectedGameObject;
        }

        Selectable current = selectedObject != null ? selectedObject.GetComponent<Selectable>() : null;
        if (current == null)
            return;

        if (submitPressed)
        {
            ExecuteEvents.Execute(current.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            lastSettingsMoveTime = Time.unscaledTime;
            return;
        }

        if (current is Slider slider && (direction == Vector2.left || direction == Vector2.right))
        {
            AdjustSlider(slider, direction);
            lastSettingsMoveTime = Time.unscaledTime;
            return;
        }

        Selectable next = FindAdjacentSelectable(current, direction);
        if (next != null)
            SelectObject(next.gameObject);

        lastSettingsMoveTime = Time.unscaledTime;
    }

    private Vector2 GetNavigationDirection()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame)
                return Vector2.up;

            if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)
                return Vector2.down;

            if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame)
                return Vector2.left;

            if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)
                return Vector2.right;
        }

        if (Gamepad.current != null)
        {
            if (Gamepad.current.dpad.up.wasPressedThisFrame)
                return Vector2.up;

            if (Gamepad.current.dpad.down.wasPressedThisFrame)
                return Vector2.down;

            if (Gamepad.current.dpad.left.wasPressedThisFrame)
                return Vector2.left;

            if (Gamepad.current.dpad.right.wasPressedThisFrame)
                return Vector2.right;
        }

        return Vector2.zero;
    }

    private void MoveSelection(List<Selectable> focusables, Vector2 direction)
    {
        if (EventSystem.current == null || focusables == null || focusables.Count == 0)
            return;

        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
        Selectable current = focusables.Find(selectable => selectable != null && selectable.gameObject == currentSelected);
        Selectable next = FindNextSelectable(focusables, current, direction);

        if (next != null)
            SelectObject(next.gameObject);
    }

    private static Selectable FindNextSelectable(List<Selectable> focusables, Selectable current, Vector2 direction)
    {
        if (focusables == null || focusables.Count == 0)
            return null;

        if (current == null)
            return focusables[0];

        Vector2 currentPosition = ((RectTransform)current.transform).position;
        Selectable best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < focusables.Count; i++)
        {
            Selectable candidate = focusables[i];
            if (candidate == null || candidate == current)
                continue;

            Vector2 candidatePosition = ((RectTransform)candidate.transform).position;
            Vector2 delta = candidatePosition - currentPosition;

            if (!IsInDirection(delta, direction))
                continue;

            float score = ComputeDirectionalScore(delta, direction);
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best ?? current;
    }

    private static Selectable FindAdjacentSelectable(Selectable current, Vector2 direction)
    {
        if (current == null)
            return null;

        if (direction == Vector2.up)
            return current.FindSelectableOnUp();

        if (direction == Vector2.down)
            return current.FindSelectableOnDown();

        if (direction == Vector2.left)
            return current.FindSelectableOnLeft();

        if (direction == Vector2.right)
            return current.FindSelectableOnRight();

        return null;
    }

    private static void AdjustSlider(Slider slider, Vector2 direction)
    {
        if (slider == null)
            return;

        float range = slider.maxValue - slider.minValue;
        if (range <= 0f)
            return;

        float step = slider.wholeNumbers ? 1f : Mathf.Max(range * 0.05f, 0.01f);
        float delta = direction == Vector2.left ? -step : step;
        slider.SetValueWithoutNotify(Mathf.Clamp(slider.value + delta, slider.minValue, slider.maxValue));
        slider.onValueChanged?.Invoke(slider.value);
    }

    private static bool IsInDirection(Vector2 delta, Vector2 direction)
    {
        if (direction == Vector2.up)
            return delta.y > 2f;

        if (direction == Vector2.down)
            return delta.y < -2f;

        if (direction == Vector2.left)
            return delta.x < -2f;

        if (direction == Vector2.right)
            return delta.x > 2f;

        return false;
    }

    private static float ComputeDirectionalScore(Vector2 delta, Vector2 direction)
    {
        float primary;
        float secondary;

        if (direction == Vector2.up || direction == Vector2.down)
        {
            primary = Mathf.Abs(delta.y);
            secondary = Mathf.Abs(delta.x) * 4f;
        }
        else
        {
            primary = Mathf.Abs(delta.x);
            secondary = Mathf.Abs(delta.y) * 4f;
        }

        return primary + secondary;
    }

    private static void SetNavigationNone(Selectable selectable)
    {
        if (selectable == null)
            return;

        Navigation navigation = selectable.navigation;
        navigation.mode = Navigation.Mode.None;
        navigation.selectOnUp = null;
        navigation.selectOnDown = null;
        navigation.selectOnLeft = null;
        navigation.selectOnRight = null;
        selectable.navigation = navigation;
    }

    private static void SetNavigationEventsEnabled(bool enabled)
    {
        if (EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = enabled;
    }
}
