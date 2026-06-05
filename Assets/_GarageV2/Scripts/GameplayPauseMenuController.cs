using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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

    private GameplayPauseMenuView pauseMenuInstance;
    private GameplaySettingsPanelView settingsPanelInstance;
    private bool isPaused;
    private bool settingsOpen;

    public bool IsPaused => isPaused;

    private void Awake()
    {
        ResolveReferences();
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
            SelectObject(settingsPanelInstance.DefaultSelected);
        }
    }

    public void BackFromSettings()
    {
        settingsOpen = false;

        if (settingsPanelInstance != null)
            settingsPanelInstance.Hide();

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
            pauseMenuInstance.SetTitle(pauseTitle);

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
}
