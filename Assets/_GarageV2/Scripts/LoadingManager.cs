using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingManager : MonoBehaviour
{
    public static LoadingManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject loadingRoot;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private CanvasGroup loadingCanvasGroup;

    [Header("Settings")]
    [SerializeField] private float minimumLoadTime = 0.5f;
    [SerializeField] private float progressSmoothSpeed = 2f;
    [SerializeField] private bool pauseAudioDuringLoad = true;

    private bool isLoading;
    private float displayedProgress;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (loadingRoot != null)
            loadingRoot.SetActive(false);
    }

    public void LoadScene(string sceneName)
    {
        if (isLoading)
            return;

        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        isLoading = true;
        displayedProgress = 0f;

        if (pauseAudioDuringLoad)
            AudioListener.pause = true;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        ShowLoadingUI();

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        float timer = 0f;

        while (!op.isDone)
        {
            timer += Time.unscaledDeltaTime;

            float targetProgress = Mathf.Clamp01(op.progress / 0.9f);
            displayedProgress = Mathf.MoveTowards(displayedProgress, targetProgress, Time.unscaledDeltaTime * progressSmoothSpeed);

            UpdateUI(displayedProgress);

            if (op.progress >= 0.9f && timer >= minimumLoadTime)
            {
                displayedProgress = 1f;
                UpdateUI(displayedProgress);

                yield return new WaitForSecondsRealtime(0.05f);
                op.allowSceneActivation = true;
            }

            yield return null;
        }

        HideLoadingUI();

        AudioListener.pause = false;
        isLoading = false;
    }

    private void ShowLoadingUI()
    {
        if (loadingRoot != null)
            loadingRoot.SetActive(true);

        if (loadingCanvasGroup != null)
        {
            loadingCanvasGroup.alpha = 1f;
            loadingCanvasGroup.interactable = false;
            loadingCanvasGroup.blocksRaycasts = true;
        }

        UpdateUI(0f);
    }

    private void HideLoadingUI()
    {
        if (loadingRoot != null)
            loadingRoot.SetActive(false);

        if (loadingCanvasGroup != null)
        {
            loadingCanvasGroup.blocksRaycasts = false;
        }
    }

    private void UpdateUI(float progress01)
    {
        if (progressSlider != null)
            progressSlider.value = progress01;

        if (progressText != null)
            progressText.text = $"{progress01 * 100f:0}%";
    }
}