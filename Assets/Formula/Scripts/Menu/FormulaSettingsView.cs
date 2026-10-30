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
        [SerializeField] private UnityEngine.UI.Slider vibrationIntensity;
        [SerializeField] private TMP_Text vibrationIntensityValue;
        [SerializeField] private UnityEngine.UI.Slider[] volumes;
        [SerializeField] private TMP_Text[] volumeValues;
        [SerializeField] private UnityEngine.UI.Button[] preferenceTabs, preferenceButtons, preferenceShortcuts;
        [SerializeField] private GameObject[] preferencePages, preferenceMarkers;
        [SerializeField] private TMP_Text[] preferenceLabels;
        private bool audioDirty;
        private GameObject motionPage, motionMarker;
        [SerializeField] private UnityEngine.UI.Button language;
        [SerializeField] private TMP_Text languageLabel;
        public bool DetailsOpen => details.activeSelf;
        public bool IsRebinding => controls.IsRebinding;
        public FormulaControlsView Controls => controls;
        public void Bind(Action<string> toggleAssist)
        {
            ConfigurePlatformPresentation();
            foreach (var slider in volumes) FormulaMenuFocus.StyleSlider(slider);
            FormulaMenuFocus.StyleSlider(vibrationIntensity);
            if (language != null) language.onClick.AddListener(ChangeLanguage);
            string[] ids = { "abs", "tcs", "esp" };
            for (int i = 0; i < assists.Length; i++) { string id = ids[i]; assists[i].onClick.AddListener(() => toggleAssist(id)); }
            openDetails.onClick.AddListener(OpenDetails);
            audioTab.onClick.AddListener(() => ShowDetails(false)); controlsTab.onClick.AddListener(() => ShowDetails(true));
            vibration.onClick.AddListener(() => { FormulaProgression.Data.vibrationsState = !FormulaProgression.Data.vibrationsState; audioDirty = true; Refresh(); });
            if (vibrationIntensity != null) vibrationIntensity.onValueChanged.AddListener(SetVibrationIntensity);
            for (int i = 0; i < volumes.Length; i++) { int index = i; volumes[i].onValueChanged.AddListener(value => SetVolume(index, value)); }
            controls.Initialize();
            for (int i = 0; i < preferenceTabs.Length; i++) { int index = i; preferenceTabs[i].onClick.AddListener(() => OpenPreferences(index)); }
            for (int i = 0; i < preferenceShortcuts.Length; i++) { int index = i; preferenceShortcuts[i].onClick.AddListener(() => OpenPreferences(index)); }
            for (int i = 0; i < preferenceButtons.Length; i++) { int index = i; preferenceButtons[i].onClick.AddListener(() => TogglePreference(index)); }
        }
        private void ConfigurePlatformPresentation()
        {
            if (!FormulaPlatform.IsNintendoSwitch) return;
            // Video controls are absent, including their overview shortcuts and
            // navigation targets. Safe-area adjustment remains an accessibility aid.
            for (int i = 0; i < 2; i++)
            {
                preferenceTabs[i].gameObject.SetActive(false); preferenceShortcuts[i].gameObject.SetActive(false);
                preferencePages[i].SetActive(false); preferenceMarkers[i].SetActive(false);
            }
            foreach (int i in new[] { 0, 2, 3, 4 }) preferenceButtons[i].gameObject.SetActive(false);
            preferenceButtons[1].transform.SetParent(preferencePages[2].transform, false);
            int[] accessible = { 5, 6, 7, 1 };
            for (int i = 0; i < accessible.Length; i++)
                FormulaMenuTheme.Place((RectTransform)preferenceButtons[accessible[i]].transform, 30, 176 + i * 108, 1220, 106);
            FormulaMenuTheme.Place((RectTransform)preferenceTabs[2].transform, 494, 0, 232, 42);
            FormulaMenuTheme.Place((RectTransform)preferenceShortcuts[2].transform, 600, 290, 650, 78);
            foreach (var binding in GetComponentsInChildren<UnityEngine.Localization.Components.LocalizeStringEvent>(true))
                if (binding.StringReference.TableEntryReference.Key == "Volume and vibration\nKeyboard, gamepad and steering wheel")
                    binding.StringReference.TableEntryReference = "Volume and vibration\nJoy-Con / Pro Controller";
        }
        public void Refresh()
        {
            if (controls.isActiveAndEnabled) controls.Refresh();
            if (languageLabel != null) languageLabel.text = FormulaLocalization.F("LANGUAGE  /  {0}", FormulaLocalization.LanguageName);
            var p = FormulaProgression.State;
            assistLabels[0].text = FormulaLocalization.T("ABS · ") + (p.abs ? FormulaLocalization.T("On") : FormulaLocalization.T("Off")); assistLabels[1].text = FormulaLocalization.T("TCS · ") + (p.tcs ? FormulaLocalization.T("On") : FormulaLocalization.T("Off")); assistLabels[2].text = FormulaLocalization.T("ESP · ") + (p.esp ? FormulaLocalization.T("On") : FormulaLocalization.T("Off"));
            var d = FormulaProgression.Data; float[] values = { d.soundLevel, d.VehicleLevel, d.musicLevel };
            for (int i = 0; i < volumes.Length; i++) { volumes[i].SetValueWithoutNotify(values[i]); volumeValues[i].text = Mathf.RoundToInt(values[i] * 100) + "%"; }
            vibrationLabel.text = d.vibrationsState ? FormulaLocalization.T("Vibration · On") : FormulaLocalization.T("Vibration · Off");
            if (vibrationIntensity != null) vibrationIntensity.SetValueWithoutNotify(Mathf.RoundToInt(d.vibrationIntensity * 100));
            if (vibrationIntensityValue != null) vibrationIntensityValue.text = Mathf.RoundToInt(d.vibrationIntensity * 100) + "%";
            if (preferenceLabels == null || preferenceLabels.Length < 8) return;
            preferenceLabels[0].text = FormulaLocalization.T("DISPLAY MODE  /  ") + (Screen.fullScreen ? FormulaLocalization.T("Full screen") : FormulaLocalization.T("Windowed"));
#if UNITY_EDITOR || UNITY_SWITCH
            preferenceLabels[0].text = FormulaLocalization.T("DISPLAY MODE  /  Platform managed"); preferenceButtons[0].interactable = false;
#endif
            preferenceLabels[1].text = FormulaLocalization.T("SAFE AREA  /  ") + p.safeAreaPercent + "%";
            preferenceLabels[2].text = FormulaLocalization.T("FRAME LIMIT  /  ") + (Application.targetFrameRate > 0 ? Application.targetFrameRate.ToString() + FormulaLocalization.T(" FPS") : FormulaLocalization.T("Unlimited"));
            preferenceLabels[3].text = FormulaLocalization.T("QUALITY  /  ") + QualitySettings.names[QualitySettings.GetQualityLevel()];
            preferenceLabels[4].text = FormulaLocalization.T("V-SYNC  /  ") + (QualitySettings.vSyncCount > 0 ? FormulaLocalization.T("On") : FormulaLocalization.T("Off"));
            preferenceLabels[5].text = FormulaLocalization.T("TEXT SIZE  /  ") + p.textScale + "%";
            preferenceLabels[6].text = FormulaLocalization.T("REDUCED MOTION  /  ") + (p.reducedMotion ? FormulaLocalization.T("On") : FormulaLocalization.T("Off"));
            preferenceLabels[7].text = FormulaLocalization.T("HIGH CONTRAST  /  ") + (p.highContrast ? FormulaLocalization.T("On") : FormulaLocalization.T("Off"));
        }
        private void SetVolume(int index, float value)
        {
            value = Mathf.Clamp01(value); var d = FormulaProgression.Data;
            if (index == 0) { SoundManager.Instance?.SetSfxVolume(value); d.soundLevel = value; }
            else if (index == 1) { SoundManager.Instance?.SetVehicleVolume(value); d.VehicleLevel = value; }
            else { SoundManager.Instance?.SetMusicVolume(value); d.musicLevel = value; }
            audioDirty = true; volumeValues[index].text = Mathf.RoundToInt(value * 100) + "%";
            FormulaUIMotion.Pulse(volumeValues[index].gameObject, .045f, .16f);
        }
        private void ChangeLanguage()
        {
            string code = FormulaLocalization.NextLanguage;
            if (!FormulaLocalization.ChangeLanguage(code, out string error)) languageLabel.text = error;
        }
        private void SetVibrationIntensity(float percentage)
        {
            float value = Mathf.Clamp01(percentage / 100f);
            FormulaProgression.Data.vibrationIntensity = value;
            audioDirty = true;
            vibrationIntensityValue.text = Mathf.RoundToInt(value * 100) + "%";
            FormulaUIMotion.Pulse(vibrationIntensityValue.gameObject, .045f, .16f);
        }
        private void OpenDetails() { overview.SetActive(false); details.SetActive(true); ShowDetails(false); }
        private void OpenPreferences(int index)
        {
            if (!FormulaPlatform.AllowsVideoPreferences && index != 2) return;
            controls.CancelRebind(); overview.SetActive(false); details.SetActive(true);
            audioPage.SetActive(false); controls.gameObject.SetActive(false); audioMarker.SetActive(false); controlsMarker.SetActive(false);
            for (int i = 0; i < preferencePages.Length; i++) { preferencePages[i].SetActive(i == index); preferenceMarkers[i].SetActive(i == index); }
            AnimatePage(preferencePages[index], preferenceMarkers[index]);
            FormulaUI.Focus(preferencePages[index].transform);
        }
        private void TogglePreference(int index)
        {
            if (!FormulaPlatform.AllowsVideoPreferences && index != 1 && index != 5 && index != 6 && index != 7) return;
            bool changed = FormulaProgression.Commit(d =>
            {
                var p = d.formula;
                switch (index)
                {
                    case 0: p.displayMode = Screen.fullScreen ? 0 : 1; break;
                    case 1: p.safeAreaPercent = p.safeAreaPercent == 100 ? 95 : 100; break;
                    case 2:
                        p.frameLimit = Application.targetFrameRate == 30 ? 60 : Application.targetFrameRate == 60 ? 120 : 30;
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
            AnimatePage(showControls ? controls.gameObject : audioPage, showControls ? controlsMarker : audioMarker);
            FormulaUI.Focus(showControls ? controls.transform : audioPage.transform);
        }
        private void AnimatePage(GameObject page, GameObject marker)
        {
            if (motionPage != page) FormulaUIMotion.Enter(page);
            if (motionMarker != null && motionMarker != marker && marker != null) FormulaUIMotion.Glide(marker, motionMarker.transform.position);
            motionPage = page; motionMarker = marker;
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
