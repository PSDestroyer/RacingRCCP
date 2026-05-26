using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseGameplay : MonoBehaviour
{
    public static PauseGameplay Instance { get; private set; }

    [Header("Panels")]
    public GameObject pausePanel;
    public GameObject settingsPanel;

    [Header("Scene Flow")]
    public string menuSceneName = "Menu";
    public bool pauseAudioListener = true;

    private bool isPaused = false;
    private bool canPause = true;
    private bool wasVehicleControllableBeforePause = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        RCCP_InputManager.OnOptions += TogglePause;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    private void OnDisable()
    {
        RCCP_InputManager.OnOptions -= TogglePause;

        if (Instance == this)
            Instance = null;

        if (Time.timeScale == 0f)
            ResumeGameplay();
    }

    public void TogglePause()
    {
        if (!canPause)
            return;

        if (settingsPanel != null && settingsPanel.activeSelf)
        {
            CloseSettingsPanel();
            return;
        }

        if (isPaused)
            ResumeGameplay();
        else
            PauseGame();
    }

    public void PauseGame()
    {
        if (isPaused || !canPause)
            return;

        isPaused = true;
        Time.timeScale = 0f;

        if (pauseAudioListener)
            AudioListener.pause = true;

        if (pausePanel != null)
            pausePanel.SetActive(true);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        SetGameplayVehicleControl(false);
    }

    public void ResumeGameplay()
    {
        isPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        RestoreGameplayVehicleControl();
    }

    public void OpenSettingsPanel()
    {
        if (!isPaused)
            PauseGame();

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    public void CloseSettingsPanel()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (isPaused && pausePanel != null)
            pausePanel.SetActive(true);
    }

    public void RestartGameplay()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        string currentSceneName = SceneManager.GetActiveScene().name;

        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.LoadScene(currentSceneName);
            return;
        }

        SceneManager.LoadScene(currentSceneName);
    }

    public void ReturnToMenu()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.LoadScene(menuSceneName);
            return;
        }

        SceneManager.LoadScene(menuSceneName);
    }

    public void SetPauseAvailability(bool state)
    {
        canPause = state;

        if (!canPause && isPaused)
            ResumeGameplay();
    }

    private void SetGameplayVehicleControl(bool state)
    {
        if (RCCP_SceneManager.Instance == null || RCCP_SceneManager.Instance.activePlayerVehicle == null)
            return;

        RCCP_CarController activeVehicle = RCCP_SceneManager.Instance.activePlayerVehicle;
        wasVehicleControllableBeforePause = activeVehicle.canControl;
        activeVehicle.SetCanControl(state);
    }

    private void RestoreGameplayVehicleControl()
    {
        if (RCCP_SceneManager.Instance == null || RCCP_SceneManager.Instance.activePlayerVehicle == null)
            return;

        RCCP_SceneManager.Instance.activePlayerVehicle.SetCanControl(wasVehicleControllableBeforePause);
    }
}
