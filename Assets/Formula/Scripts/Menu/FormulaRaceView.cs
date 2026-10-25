using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FormulaGame
{
    // Presentation for the race; timing, rewards and pause state stay with their existing owners.
    public sealed class FormulaRaceView : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private GameObject hud, pause, pauseOverview, pauseConfirmation, options, error;
        [SerializeField] private TMP_Text eventName, position, lap, sessionTime, lapTime, bestLap, objective, validity, speed, gear, rpm, startMessage, confirmTitle, confirmDetails, errorDetails;
        [SerializeField] private UnityEngine.UI.Image revFill;
        [SerializeField] private UnityEngine.UI.Button pauseButton, resume, restart, leave, settings, confirm, cancel, optionsBack, retrySave, leaveError;
        [SerializeField] private UnityEngine.UI.Slider[] volumes;
        [SerializeField] private TMP_Text[] volumeValues;
        private PauseGameplay pauseController;
        private Action pending;
        private bool wasPaused, finished, loadingVolumes, audioDirty, confirmFromError;
        public GameObject Hud => hud;
        public void Bind(GamePlayManager game)
        {
            pauseController = PauseGameplay.Instance;
            if (pauseController == null) pauseController = new GameObject("Formula Pause").AddComponent<PauseGameplay>();
            if (pauseController.pausePanel != null) pauseController.pausePanel.SetActive(false);
            if (pauseController.settingsPanel != null) pauseController.settingsPanel.SetActive(false);
            pauseController.pausePanel = pause; pauseController.settingsPanel = options;
            pauseController.OptionsOverride = HandleOptions;
            pauseController.SetPauseAvailability(game.FormulaRaceStarted);
            pauseButton.onClick.AddListener(pauseController.PauseGame);
            resume.onClick.AddListener(pauseController.ResumeGameplay);
            restart.onClick.AddListener(() => Confirm("RESTART SESSION?", "Your current run will end. Earned career progress is retained.", () => { pauseController.ResumeGameplay(); FormulaSession.Launch(FormulaProgression.State.activeEventId, FormulaProgression.State.activeMode); }));
            leave.onClick.AddListener(() => Confirm("LEAVE SESSION?", "This unfinished run will not award credits or XP. Your saved progress is retained.", FormulaSession.ReturnToMenu));
            cancel.onClick.AddListener(CancelConfirmation); confirm.onClick.AddListener(() => pending?.Invoke());
            settings.onClick.AddListener(() => { pauseController.OpenSettingsPanel(); RefreshVolumes(); FormulaUI.Focus(options.transform); });
            optionsBack.onClick.AddListener(() => { SaveAudio(); pauseController.CloseSettingsPanel(); EventSystem.current?.SetSelectedGameObject(settings.gameObject); });
            retrySave.onClick.AddListener(() => { error.SetActive(false); FormulaSession.RetryResult(); });
            leaveError.onClick.AddListener(() => { error.SetActive(false); pause.SetActive(true); Confirm("LEAVE WITHOUT SAVING?", "This result has not been saved. Its credits and XP will be lost.", FormulaSession.ReturnToMenu, true); });
            for (int i = 0; i < volumes.Length; i++) { int index = i; volumes[i].onValueChanged.AddListener(value => ChangeVolume(index, value)); }
            pause.SetActive(false); options.SetActive(false); error.SetActive(false);
            eventName.text = FormulaSession.CurrentEvent.name.ToUpperInvariant();
            Wire(pauseOverview.transform); Wire(pauseConfirmation.transform); Wire(options.transform); Wire(error.transform);
        }
        public void Tick(GamePlayManager game)
        {
            FormulaInputHints.Poll();
            bool paused = pause.activeSelf || options.activeSelf;
            if (paused && !wasPaused) { pauseOverview.SetActive(true); pauseConfirmation.SetActive(false); FormulaUI.Focus(pauseOverview.transform); }
            if (!paused && wasPaused) SaveAudio();
            wasPaused = paused;
            if (game.CarController == null || finished) return;
            var car = game.CarController; var e = FormulaSession.CurrentEvent;
            startMessage.text = game.FormulaRaceStarted ? "" : game.raceStateText != null ? game.raceStateText.text : "";
            position.text = e.opponentCount > 0 ? "P" + game.FormulaPosition + " / " + game.FormulaParticipants : "SOLO";
            lap.text = "LAP " + Mathf.Min(game.FormulaCompletedLaps + 1, game.totalRaceLaps) + " / " + game.totalRaceLaps;
            sessionTime.text = FormulaRaceUI.TimeText(game.FormulaElapsed);
            lapTime.text = FormulaRaceUI.TimeText(FormulaSession.LapElapsed(game.FormulaElapsed));
            bestLap.text = "BEST  " + FormulaRaceUI.TimeText(FormulaSession.BestLap);
            validity.text = FormulaSession.LapInvalid ? "LAP INVALID · TRACK LIMITS\nReturn to the circuit. Your next lap can count." : "VALID LAP";
            validity.color = FormulaSession.LapInvalid ? FormulaUI.Red : Color.white;
            objective.text = e.mode == "race" ? "BRONZE TARGET  /  FINISH IN THE TOP 3" : e.mode == "familiarization" ? "OBJECTIVE  /  COMPLETE A VALID LAP" : "BRONZE TARGET  /  " + FormulaRaceUI.TimeText(FormulaAssets.Current.FindTrack(e.trackId).referenceLapSeconds * 1.2);
            if (FormulaProgression.State.activeMode != "career") objective.text = "QUICK SESSION  /  " + (e.mode == "race" ? "RACE FOR YOUR BEST POSITION" : "COMPLETE VALID LAPS");
            speed.text = Mathf.RoundToInt(car.absoluteSpeed).ToString();
            gear.text = car.NGearNow ? "N" : car.reversingNow || car.direction < 0 ? "R" : Mathf.Max(1, car.currentGear).ToString();
            float fraction = Mathf.InverseLerp(car.minEngineRPM, car.maxEngineRPM, car.engineRPM);
            revFill.fillAmount = fraction; revFill.color = fraction > .9f ? FormulaUI.Red : FormulaUI.Accent;
            rpm.text = Mathf.RoundToInt(car.engineRPM).ToString("N0") + " RPM";
        }
        private void Confirm(string title, string details, Action action, bool fromError = false)
        {
            confirmFromError = fromError; wasPaused = true; pending = action; confirmTitle.text = title; confirmDetails.text = details;
            confirm.GetComponentInChildren<TMP_Text>().text = fromError ? "LEAVE WITHOUT SAVING" : title.StartsWith("RESTART", StringComparison.Ordinal) ? "RESTART SESSION" : "LEAVE SESSION";
            cancel.GetComponentInChildren<TMP_Text>().text = fromError ? "BACK TO RESULT" : "KEEP RACING";
            pauseOverview.SetActive(false); pauseConfirmation.SetActive(true); EventSystem.current?.SetSelectedGameObject(cancel.gameObject);
        }
        private void CancelConfirmation()
        {
            pending = null; pauseConfirmation.SetActive(false);
            if (confirmFromError) { confirmFromError = false; pause.SetActive(false); error.SetActive(true); FormulaUI.Focus(error.transform); }
            else { pauseOverview.SetActive(true); FormulaUI.Focus(pauseOverview.transform); }
        }
        private bool HandleOptions()
        {
            if (pauseConfirmation.activeSelf) { CancelConfirmation(); return true; }
            if (options.activeSelf) { SaveAudio(); pauseController.CloseSettingsPanel(); EventSystem.current?.SetSelectedGameObject(settings.gameObject); return true; }
            return false;
        }
        public void SaveError(string message)
        {
            if (error.activeSelf) return;
            errorDetails.text = "Your result could not be saved. Retry saving to keep your credits and XP.\n" + message;
            hud.SetActive(false); pause.SetActive(false); options.SetActive(false); error.SetActive(true); FormulaUI.Focus(error.transform);
        }
        public void Finish()
        {
            finished = true; hud.SetActive(false); pause.SetActive(false); options.SetActive(false); error.SetActive(false);
            pauseController.SetPauseAvailability(false);
        }
        private void RefreshVolumes()
        {
            loadingVolumes = true; var d = FormulaProgression.Data;
            float[] values = { d.soundLevel, d.VehicleLevel, d.musicLevel };
            for (int i = 0; i < volumes.Length; i++) { volumes[i].SetValueWithoutNotify(values[i]); volumeValues[i].text = Mathf.RoundToInt(values[i] * 100) + "%"; }
            loadingVolumes = false;
        }
        private void ChangeVolume(int index, float value)
        {
            if (loadingVolumes) return;
            audioDirty = true; var d = FormulaProgression.Data;
            if (index == 0) { d.soundLevel = value; SoundManager.Instance?.SetSfxVolume(value); }
            else if (index == 1) { d.VehicleLevel = value; SoundManager.Instance?.SetVehicleVolume(value); }
            else { d.musicLevel = value; SoundManager.Instance?.SetMusicVolume(value); }
            volumeValues[index].text = Mathf.RoundToInt(value * 100) + "%";
        }
        private void SaveAudio() { if (audioDirty && HalvaStudio.Save.SaveManager.Instance != null) { if (HalvaStudio.Save.SaveManager.Instance.TrySave(true)) audioDirty = false; } }
        private void OnDisable() { if (Application.isPlaying) SaveAudio(); }
        public void OnCancel(BaseEventData data)
        {
            if (confirmFromError) CancelConfirmation();
            else if (pause.activeSelf || options.activeSelf) pauseController.TogglePause();
        }
        private void OnDestroy() { if (pauseController != null) pauseController.OptionsOverride = null; }
        private static void Wire(Transform root)
        {
            var controls = root.GetComponentsInChildren<UnityEngine.UI.Selectable>();
            for (int i = 0; i < controls.Length; i++)
            {
                var previous = controls[(i + controls.Length - 1) % controls.Length];
                var next = controls[(i + 1) % controls.Length];
                controls[i].navigation = new UnityEngine.UI.Navigation
                {
                    mode = UnityEngine.UI.Navigation.Mode.Explicit,
                    selectOnUp = previous, selectOnDown = next,
                    selectOnLeft = previous, selectOnRight = next
                };
            }
        }
    }
}
