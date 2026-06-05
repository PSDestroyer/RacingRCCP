using HalvaStudio.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class GameplaySettingsPanelView : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider vehicleSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Button backButton;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private SettingsManager settingsManager;

    private bool isInitialized;

    public Button BackButton => backButton;
    public GameObject DefaultSelected => sfxSlider != null ? sfxSlider.gameObject : backButton != null ? backButton.gameObject : null;

    public void AutoBindFromExistingRoot(GameObject existingRoot)
    {
        root = existingRoot;

        if (settingsManager == null)
            settingsManager = existingRoot.GetComponentInChildren<SettingsManager>(true);

        if (settingsManager != null)
        {
            if (sfxSlider == null)
                sfxSlider = settingsManager.sfx;

            if (vehicleSlider == null)
                vehicleSlider = settingsManager.vehicle;

            if (musicSlider == null)
                musicSlider = settingsManager.music;
        }

        if (backButton == null)
        {
            Button[] buttons = existingRoot.GetComponentsInChildren<Button>(true);
            backButton = buttons.FirstOrDefault(button => button.name.Contains("Back"));
        }

        if (titleText == null)
            titleText = existingRoot.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.name.Contains("Settings"));
    }

    public void Initialize()
    {
        if (isInitialized)
            return;

        if (sfxSlider != null)
            sfxSlider.onValueChanged.AddListener(HandleSfxChanged);

        if (vehicleSlider != null)
            vehicleSlider.onValueChanged.AddListener(HandleVehicleChanged);

        if (musicSlider != null)
            musicSlider.onValueChanged.AddListener(HandleMusicChanged);

        isInitialized = true;
    }

    public void Show()
    {
        if (settingsManager != null)
            settingsManager.RefreshUIFromSave();
        else
            SyncFromSave();

        if (root != null)
            root.SetActive(true);
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);

        if (settingsManager != null)
            settingsManager.SaveSettings();
        else if (SaveManager.Instance != null)
            SaveManager.Instance.Save();
    }

    public void SetTitle(string value)
    {
        if (titleText != null)
            titleText.text = value;
    }

    public void SyncFromSave()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.saveData == null)
            return;

        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(SaveManager.Instance.saveData.soundLevel);

        if (vehicleSlider != null)
            vehicleSlider.SetValueWithoutNotify(SaveManager.Instance.saveData.VehicleLevel);

        if (musicSlider != null)
            musicSlider.SetValueWithoutNotify(SaveManager.Instance.saveData.musicLevel);
    }

    private void HandleSfxChanged(float value)
    {
        if (settingsManager != null)
        {
            settingsManager.OnSetSfxVolume(value);
            return;
        }

        value = Mathf.Clamp01(value);

        if (SaveManager.Instance != null && SaveManager.Instance.saveData != null)
            SaveManager.Instance.saveData.soundLevel = value;

        if (SoundManager.Instance != null)
            SoundManager.Instance.SetSfxVolume(value);
    }

    private void HandleVehicleChanged(float value)
    {
        if (settingsManager != null)
        {
            settingsManager.OnSetVehicleVolume(value);
            return;
        }

        value = Mathf.Clamp01(value);

        if (SaveManager.Instance != null && SaveManager.Instance.saveData != null)
            SaveManager.Instance.saveData.VehicleLevel = value;

        if (SoundManager.Instance != null)
            SoundManager.Instance.SetVehicleVolume(value);
    }

    private void HandleMusicChanged(float value)
    {
        if (settingsManager != null)
        {
            settingsManager.OnSetMusicVolume(value);
            return;
        }

        value = Mathf.Clamp01(value);

        if (SaveManager.Instance != null && SaveManager.Instance.saveData != null)
            SaveManager.Instance.saveData.musicLevel = value;

        if (SoundManager.Instance != null)
            SoundManager.Instance.SetMusicVolume(value);
    }
}
