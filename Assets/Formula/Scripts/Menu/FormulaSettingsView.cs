using System;
using HalvaStudio.Save;
using TMPro;
using UnityEngine;

namespace FormulaGame
{
    public sealed class FormulaSettingsView : MonoBehaviour
    {
        [SerializeField] private GameObject overview, details, audioPage, audioMarker, controlsMarker;
        [SerializeField] private FormulaControlsView controls;
        [SerializeField] private UnityEngine.UI.Button[] assists;
        [SerializeField] private TMP_Text[] assistLabels;
        [SerializeField] private UnityEngine.UI.Button openDetails, audioTab, controlsTab, vibration;
        [SerializeField] private TMP_Text vibrationLabel;
        [SerializeField] private UnityEngine.UI.Slider[] volumes;
        [SerializeField] private TMP_Text[] volumeValues;
        [SerializeField] private UnityEngine.UI.Button[] preferenceTabs, preferenceButtons, preferenceShortcuts;
        [SerializeField] private GameObject[] preferencePages, preferenceMarkers;
        [SerializeField] private TMP_Text[] preferenceLabels;
        private bool audioDirty;
        public bool DetailsOpen => details.activeSelf;
        public bool IsRebinding => controls.IsRebinding;
        public FormulaControlsView Controls => controls;
        public void Bind(Action<string> toggleAssist)
        {
            string[] ids = { "abs", "tcs", "esp" };
            for (int i = 0; i < assists.Length; i++) { string id = ids[i]; assists[i].onClick.AddListener(() => toggleAssist(id)); }
            openDetails.onClick.AddListener(OpenDetails);
            audioTab.onClick.AddListener(() => ShowDetails(false)); controlsTab.onClick.AddListener(() => ShowDetails(true));
            vibration.onClick.AddListener(() => { FormulaProgression.Data.vibrationsState = !FormulaProgression.Data.vibrationsState; audioDirty = true; Refresh(); });
            for (int i = 0; i < volumes.Length; i++) { int index = i; volumes[i].onValueChanged.AddListener(value => SetVolume(index, value)); }
            controls.Initialize();
            for (int i = 0; i < preferenceTabs.Length; i++) { int index = i; preferenceTabs[i].onClick.AddListener(() => OpenPreferences(index)); }
            for (int i = 0; i < preferenceShortcuts.Length; i++) { int index = i; preferenceShortcuts[i].onClick.AddListener(() => OpenPreferences(index)); }
            for (int i = 0; i < preferenceButtons.Length; i++) { int index = i; preferenceButtons[i].onClick.AddListener(() => TogglePreference(index)); }
        }
        public void Refresh()
        {
            var p = FormulaProgression.State;
            assistLabels[0].text = "ABS · " + (p.abs ? "On" : "Off"); assistLabels[1].text = "TCS · " + (p.tcs ? "On" : "Off"); assistLabels[2].text = "ESP · " + (p.esp ? "On" : "Off");
            var d = FormulaProgression.Data; float[] values = { d.soundLevel, d.VehicleLevel, d.musicLevel };
            for (int i = 0; i < volumes.Length; i++) { volumes[i].SetValueWithoutNotify(values[i]); volumeValues[i].text = Mathf.RoundToInt(values[i] * 100) + "%"; }
            vibrationLabel.text = "Vibration · " + (d.vibrationsState ? "On" : "Off");
            if (preferenceLabels == null || preferenceLabels.Length < 8) return;
            preferenceLabels[0].text = "DISPLAY MODE  /  " + (Screen.fullScreen ? "Full screen" : "Windowed");
#if UNITY_EDITOR || UNITY_SWITCH
            preferenceLabels[0].text = "DISPLAY MODE  /  Platform managed"; preferenceButtons[0].interactable = false;
#endif
            preferenceLabels[1].text = "SAFE AREA  /  " + p.safeAreaPercent + "%";
            preferenceLabels[2].text = "FRAME LIMIT  /  " + (Application.targetFrameRate > 0 ? Application.targetFrameRate.ToString() + " FPS" : "Unlimited");
            preferenceLabels[3].text = "QUALITY  /  " + QualitySettings.names[QualitySettings.GetQualityLevel()];
            preferenceLabels[4].text = "V-SYNC  /  " + (QualitySettings.vSyncCount > 0 ? "On" : "Off");
            preferenceLabels[5].text = "TEXT SIZE  /  " + p.textScale + "%";
            preferenceLabels[6].text = "REDUCED MOTION  /  " + (p.reducedMotion ? "On" : "Off");
            preferenceLabels[7].text = "HIGH CONTRAST  /  " + (p.highContrast ? "On" : "Off");
        }
        private void SetVolume(int index, float value)
        {
            value = Mathf.Clamp01(value); var d = FormulaProgression.Data;
            if (index == 0) { SoundManager.Instance?.SetSfxVolume(value); d.soundLevel = value; }
            else if (index == 1) { SoundManager.Instance?.SetVehicleVolume(value); d.VehicleLevel = value; }
            else { SoundManager.Instance?.SetMusicVolume(value); d.musicLevel = value; }
            audioDirty = true; volumeValues[index].text = Mathf.RoundToInt(value * 100) + "%";
        }
        private void OpenDetails() { overview.SetActive(false); details.SetActive(true); ShowDetails(false); }
        private void OpenPreferences(int index)
        {
            controls.CancelRebind(); overview.SetActive(false); details.SetActive(true);
            audioPage.SetActive(false); controls.gameObject.SetActive(false); audioMarker.SetActive(false); controlsMarker.SetActive(false);
            for (int i = 0; i < preferencePages.Length; i++) { preferencePages[i].SetActive(i == index); preferenceMarkers[i].SetActive(i == index); }
            FormulaUI.Focus(preferencePages[index].transform);
        }
        private void TogglePreference(int index)
        {
            bool changed = FormulaProgression.Commit(d =>
            {
                var p = d.formula;
                switch (index)
                {
                    case 0: p.displayMode = Screen.fullScreen ? 0 : 1; break;
                    case 1: p.safeAreaPercent = p.safeAreaPercent == 100 ? 95 : 100; break;
                    case 2:
#if UNITY_SWITCH
                        p.frameLimit = Application.targetFrameRate == 30 ? 60 : 30;
#else
                        p.frameLimit = Application.targetFrameRate == 30 ? 60 : Application.targetFrameRate == 60 ? 120 : 30;
#endif
                        break;
                    case 3: p.qualityLevel = (QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length; break;
                    case 4: p.vSync = QualitySettings.vSyncCount > 0 ? 0 : 1; break;
                    case 5: p.textScale = p.textScale == 100 ? 115 : 100; break;
                    case 6: p.reducedMotion = !p.reducedMotion; break;
                    case 7: p.highContrast = !p.highContrast; break;
                }
                return null;
            }, out string error);
            if (changed) FormulaPresentation.ApplyVideoPreferences();
            Refresh();
            if (!changed && preferenceLabels != null) preferenceLabels[index].text = error;
        }
        private void ShowDetails(bool showControls)
        {
            controls.CancelRebind();
            foreach (var item in preferencePages) item.SetActive(false);
            foreach (var item in preferenceMarkers) item.SetActive(false);
            audioPage.SetActive(!showControls); controls.gameObject.SetActive(showControls);
            if (audioMarker != null) audioMarker.SetActive(!showControls); if (controlsMarker != null) controlsMarker.SetActive(showControls);
            if (showControls) controls.Refresh();
            FormulaUI.Focus(showControls ? controls.transform : audioPage.transform);
        }
        public bool BackWithinPage()
        {
            if (controls.IsRebinding) { controls.CancelRebind(); return true; }
            if (!DetailsOpen) return false;
            SaveAudio(); details.SetActive(false); overview.SetActive(true);
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(openDetails.gameObject); return true;
        }
        private void SaveAudio() { if (!audioDirty || SaveManager.Instance == null) return; SaveManager.Instance.Save(); audioDirty = false; }
        private void OnDisable() { if (Application.isPlaying) { controls.CancelRebind(); SaveAudio(); } }
    }
}
