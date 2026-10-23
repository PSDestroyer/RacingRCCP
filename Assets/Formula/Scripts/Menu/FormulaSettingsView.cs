using System;
using HalvaStudio.Save;
using TMPro;
using UnityEngine;

namespace FormulaGame
{
    public sealed class FormulaSettingsView : MonoBehaviour
    {
        [SerializeField] private GameObject overview, details, audioPage;
        [SerializeField] private FormulaControlsView controls;
        [SerializeField] private UnityEngine.UI.Button[] assists;
        [SerializeField] private TMP_Text[] assistLabels;
        [SerializeField] private UnityEngine.UI.Button openDetails, audioTab, controlsTab, vibration;
        [SerializeField] private TMP_Text vibrationLabel;
        [SerializeField] private UnityEngine.UI.Slider[] volumes;
        [SerializeField] private TMP_Text[] volumeValues;
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
        }
        public void Refresh()
        {
            var p = FormulaProgression.State;
            assistLabels[0].text = "ABS · " + (p.abs ? "On" : "Off"); assistLabels[1].text = "TCS · " + (p.tcs ? "On" : "Off"); assistLabels[2].text = "ESP · " + (p.esp ? "On" : "Off");
            var d = FormulaProgression.Data; float[] values = { d.soundLevel, d.VehicleLevel, d.musicLevel };
            for (int i = 0; i < volumes.Length; i++) { volumes[i].SetValueWithoutNotify(values[i]); volumeValues[i].text = Mathf.RoundToInt(values[i] * 100) + "%"; }
            vibrationLabel.text = "Vibration · " + (d.vibrationsState ? "On" : "Off");
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
        private void ShowDetails(bool showControls)
        {
            controls.CancelRebind(); audioPage.SetActive(!showControls); controls.gameObject.SetActive(showControls);
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
