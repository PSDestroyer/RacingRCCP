using System;
using System.Collections.Generic;
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
        [SerializeField] private TMP_Text participantCount, timingCaption, deltaCaption, delta, lapStatus, pauseHint;
        [SerializeField] private GameObject standingsRoot;
        [SerializeField] private TMP_Text[] standings;
        [SerializeField] private UnityEngine.UI.Image[] standingHighlights, shiftLights;
        [SerializeField] private CanvasGroup notification;
        [SerializeField] private FormulaRaceMinimap minimap;
        [SerializeField] private TMP_Text goalCaption, goalValue, goalProgress, pauseObjective;
        [SerializeField] private GameObject carLeft, carRight;
        [SerializeField] private GameObject careerGoal;
        [SerializeField] private RectTransform timingRoot;
        private PauseGameplay pauseController;
        private GamePlayManager race;
        private Action pending;
        private bool wasPaused, finished, loadingVolumes, audioDirty, confirmFromError;
        private bool raceStarted, wasInvalid;
        private float messageUntil, nextStandings;
        private double previousBest;
        private double[] currentSplits, bestSplits;
        private bool[] recordedSplits, referenceSplits;
        private readonly List<GamePlayManager.RaceRacer> rankedRacers = new List<GamePlayManager.RaceRacer>(8);
        private string splitText = "—";
        private double splitDifference;
        private int displayedGear = int.MinValue;
        private double passTime;
        private bool career;
        private float leftUntil, rightUntil;
        public GameObject Hud => hud;
        public void Bind(GamePlayManager game)
        {
            race = game;
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
            career = FormulaProgression.State.activeMode == "career";
            var activeEvent = FormulaSession.CurrentEvent;
            passTime = career && activeEvent.mode == "time_attack" ? FormulaAssets.Current.FindTrack(activeEvent.trackId).referenceLapSeconds * 1.2 : 0;
            careerGoal.SetActive(career);
            goalCaption.text = passTime > 0 ? "TIME TO BEAT" : career ? "PASS TARGET" : "SESSION GOAL";
            goalValue.text = activeEvent.opponentCount > 0 ? career ? "TOP 3 + VALID LAP" : "RACE FOR POSITION"
                : passTime > 0 ? FormulaRaceUI.TimeText(passTime) : career ? "ONE VALID LAP" : "FASTEST VALID LAP";
            ((RectTransform)standingsRoot.transform).anchoredPosition = new Vector2(32, career ? -224 : -144);
            int count = Mathf.Max(1, game.raceWaypoints != null ? game.raceWaypoints.waypoints.Count : 1);
            currentSplits = new double[count]; bestSplits = new double[count];
            recordedSplits = new bool[count]; referenceSplits = new bool[count];
            FormulaSession.CheckpointPassed += OnCheckpoint;
            FormulaSession.LapCompleted += OnLapCompleted;
            notification.alpha = 0; notification.interactable = false; notification.blocksRaycasts = false;
            standingsRoot.SetActive(FormulaSession.CurrentEvent.opponentCount > 0);
            minimap.Bind(game);
            Wire(pauseOverview.transform); Wire(pauseConfirmation.transform); Wire(options.transform); Wire(error.transform);
        }
        public void Tick(GamePlayManager game)
        {
            FormulaInputHints.Poll();
            bool paused = pause.activeSelf || options.activeSelf;
            if (paused && !wasPaused) { pauseOverview.SetActive(true); pauseConfirmation.SetActive(false); FormulaUI.Focus(pauseOverview.transform); }
            if (!paused && wasPaused) SaveAudio();
            wasPaused = paused;
            hud.SetActive(!paused && !finished && !error.activeSelf);
            if (game.CarController == null || finished) return;
            var e = FormulaSession.CurrentEvent;
            startMessage.text = game.FormulaRaceStarted ? "" : game.raceStateText != null ? game.raceStateText.text : "";
            bool racing = e.opponentCount > 0;
            position.text = racing ? "P" + game.FormulaPosition : "SOLO";
            position.fontSize = racing ? 52 : 32;
            participantCount.text = racing ? "/ " + game.FormulaParticipants : "";
            lap.text = Mathf.Min(game.FormulaCompletedLaps + 1, game.totalRaceLaps) + " / " + game.totalRaceLaps;
            bool invalid = FormulaSession.LapInvalid;
            lapStatus.text = invalid ? "LAP INVALID" : game.FormulaCompletedLaps == game.totalRaceLaps - 1 ? "FINAL LAP" : "";
            lapStatus.color = invalid ? FormulaUI.Red : FormulaUI.Accent;
            timingCaption.text = "CURRENT LAP";
            sessionTime.gameObject.SetActive(racing);
            bestLap.text = FormulaSession.BestLap > 0 ? "BEST LAP  " + FormulaRaceUI.TimeText(FormulaSession.BestLap) : "FIRST TIMED LAP";
            deltaCaption.gameObject.SetActive(!racing && (FormulaSession.BestLap > 0 || invalid));
            deltaCaption.text = invalid ? "LAP NOT COUNTED" : splitText == "—" ? "AWAITING SPLIT" : "SPLIT TO BEST";
            deltaCaption.color = Color.white;
            delta.text = invalid ? "—" : splitText;
            delta.gameObject.SetActive(!racing && !invalid && splitText != "—");
            delta.color = splitDifference > 0 ? FormulaUI.Red : splitDifference < 0 ? FormulaUI.Accent : Color.white;
            float timingHeight = racing || FormulaSession.BestLap > 0 || invalid ? 128 : 102;
            if (timingRoot.sizeDelta.y != timingHeight) timingRoot.sizeDelta = new Vector2(timingRoot.sizeDelta.x, timingHeight);
            UpdateGoal(game, invalid);
            UpdateProximity(game, racing && game.FormulaRaceStarted && !paused);
            pauseHint.text = FormulaInputHints.Gamepad ?
#if UNITY_SWITCH
                "+"
#else
                "MENU"
#endif
                : "ESC";
            if (game.FormulaRaceStarted && !raceStarted)
            {
                raceStarted = true;
                // Career already has its exact target on screen; no duplicate objective toast.
                if (!career) ShowMessage(e.name.ToUpperInvariant(), racing ? game.totalRaceLaps + " laps · " + e.opponentCount + " rivals" : "Set your fastest valid lap", 3);
            }
            if (invalid && !wasInvalid) ShowMessage("LAP INVALID · TRACK LIMITS", InvalidRecovery(game), 5, true);
            wasInvalid = invalid;
            UpdateTelemetry(game);
            if (racing && Time.unscaledTime >= nextStandings)
            {
                nextStandings = Time.unscaledTime + .35f;
                game.CopyFormulaStandings(rankedRacers);
                int playerIndex = Mathf.Clamp(game.FormulaPosition - 1, 0, rankedRacers.Count - 1);
                int first = Mathf.Clamp(playerIndex - 1, 0, Mathf.Max(0, rankedRacers.Count - standings.Length));
                for (int i = 0; i < standings.Length; i++)
                {
                    int index = first + i; bool visible = index < rankedRacers.Count;
                    standings[i].transform.parent.gameObject.SetActive(visible);
                    if (!visible) continue;
                    var racer = rankedRacers[index]; bool player = game.IsPlayerRacer(racer.racerTransform);
                    // Rich text is reserved for our formatting, not driver names.
                    string name = player ? "YOU" : (racer.displayName ?? "DRIVER").Replace("<", "").Replace(">", "").ToUpperInvariant();
                    standings[i].text = (index + 1).ToString("00") + "   " + name;
                    standings[i].color = player ? FormulaUI.Accent : Color.white;
                    standingHighlights[i].color = player ? new Color(.24f, .025f, 0, 1) : new Color(0, 0, 0, 1);
                }
            }
        }
        // Vehicle instruments and clocks follow rendered frames; race summaries can refresh less often.
        public void UpdateTelemetry(GamePlayManager game)
        {
            if (finished || game.CarController == null || !hud.activeSelf) return;
            var car = game.CarController;
            startMessage.text = game.FormulaRaceStarted ? "" : game.raceStateText != null ? game.raceStateText.text : "";
            double currentLap = FormulaSession.LapElapsed(game.FormulaElapsed);
            SetClock(lapTime, currentLap, false);
            if (sessionTime.gameObject.activeSelf) SetClock(sessionTime, game.FormulaElapsed, true);
            float targetAlpha = Time.time < messageUntil ? 1 : 0;
            notification.alpha = FormulaProgression.State.reducedMotion ? targetAlpha : Mathf.MoveTowards(notification.alpha, targetAlpha, Time.unscaledDeltaTime * 5);
            speed.SetText("{0:0}", car.absoluteSpeed);
            int currentGear = car.reversingNow || car.direction < 0 ? -1 : car.NGearNow ? 0 : Mathf.Max(1, car.currentGear);
            if (displayedGear != currentGear)
            {
                displayedGear = currentGear; gear.text = currentGear < 0 ? "R" : currentGear == 0 ? "N" : currentGear.ToString();
            }
            float fraction = Mathf.InverseLerp(car.minEngineRPM, car.maxEngineRPM, car.engineRPM);
            revFill.fillAmount = fraction; revFill.color = fraction > .9f ? FormulaUI.Red : FormulaUI.Accent;
            rpm.SetText("{0:0} RPM", Mathf.Round(car.engineRPM / 100) * 100);
            for (int i = 0; i < shiftLights.Length; i++)
            {
                bool lit = fraction >= .45f + i * .045f;
                Color tint = i >= 8 ? FormulaUI.Red : i >= 4 ? FormulaUI.Accent : Color.white;
                tint.a = lit ? 1 : .16f;
                if (shiftLights[i].color != tint) shiftLights[i].color = tint;
            }
            gear.color = fraction >= .95f && currentGear > 0 ? FormulaUI.Accent : Color.white;
        }
        private static void SetClock(TMP_Text text, double seconds, bool lapPrefix)
        {
            long ms = (long)(Math.Max(0, seconds) * 1000);
            text.SetText(lapPrefix ? "RACE  {0:00}:{1:00}.{2:000}" : "{0:00}:{1:00}.{2:000}", ms / 60000, ms / 1000 % 60, ms % 1000);
        }
        private string InvalidRecovery(GamePlayManager game)
        {
            if (game.FormulaCompletedLaps + 1 < game.totalRaceLaps) return "Next lap starts a fresh attempt";
            return FormulaSession.ValidLaps > 0 ? "Finish session · valid lap retained" : "Finish session, then select Retry";
        }
        private void UpdateGoal(GamePlayManager game, bool invalid)
        {
            bool hasValid = FormulaSession.ValidLaps > 0;
            string progress = !game.FormulaRaceStarted ? "STARTS AT LIGHTS OUT" : invalid ?
                hasValid ? "VALID LAP RETAINED" : game.FormulaCompletedLaps + 1 < game.totalRaceLaps ? "NEXT LAP: FRESH ATTEMPT" : "RETRY AFTER FINISH"
                : passTime > 0 && hasValid ? FormulaSession.BestLap <= passTime ? "PASS TARGET ACHIEVED" : "TO FIND  " + FormulaRaceUI.TimeText(FormulaSession.BestLap - passTime)
                : hasValid ? "VALID LAP RECORDED" : "STAY WITHIN TRACK LIMITS";
            goalProgress.text = progress;
            goalProgress.color = invalid && !hasValid ? FormulaUI.Red : hasValid && (passTime <= 0 || FormulaSession.BestLap <= passTime) ? FormulaUI.Accent : Color.white;
            goalValue.color = passTime > 0 && hasValid && FormulaSession.BestLap <= passTime ? FormulaUI.Accent : Color.white;
            string requirement = FormulaSession.CurrentEvent.opponentCount > 0 ? career ? "Finish in the top 3 and record a valid lap." : "Race for your best finishing position."
                : passTime > 0 ? "Record a valid lap in " + FormulaRaceUI.TimeText(passTime) + " or faster."
                : "Pass every checkpoint and stay within track limits.";
            string pauseTarget = passTime > 0 ? "PASS TIME  /  " + goalValue.text + " OR FASTER" : goalCaption.text + "  /  " + goalValue.text;
            pauseObjective.text = pauseTarget + "\n" + requirement + "\n" + progress;
        }
        private void UpdateProximity(GamePlayManager game, bool enabled)
        {
            if (enabled && game.aiRacers != null)
            {
                var player = game.CarController.transform;
                Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                foreach (var rival in game.aiRacers)
                {
                    if (rival == null || rival.eliminated || rival.racerTransform == null || !rival.racerTransform.gameObject.activeInHierarchy) continue;
                    Vector3 offset = rival.racerTransform.position - player.position;
                    float side = Vector3.Dot(offset, right), along = Vector3.Dot(offset, forward);
                    // Physical overlap warning only; this is not an inferred race-time gap.
                    if (Mathf.Abs(offset.y) > 2.5f || Mathf.Abs(along) > 6f || Mathf.Abs(side) > 7f || Mathf.Abs(side) < 1.25f) continue;
                    if (side < 0) leftUntil = Time.time + .4f; else rightUntil = Time.time + .4f;
                }
            }
            carLeft.SetActive(enabled && Time.time < leftUntil);
            carRight.SetActive(enabled && Time.time < rightUntil);
        }
        private void ShowMessage(string title, string body, float seconds, bool warning = false)
        {
            validity.text = title; validity.color = warning ? FormulaUI.Red : FormulaUI.Accent;
            objective.text = body; messageUntil = Time.time + seconds;
        }
        private void OnCheckpoint(int index, double elapsed)
        {
            if (index < 0 || index >= currentSplits.Length) return;
            currentSplits[index] = elapsed; recordedSplits[index] = true;
            if (referenceSplits[index] && !FormulaSession.LapInvalid)
            {
                splitDifference = elapsed - bestSplits[index];
                splitText = splitDifference.ToString("+0.00;-0.00;0.00", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        private void OnLapCompleted()
        {
            bool best = FormulaSession.LastLapValid && (previousBest <= 0 || FormulaSession.LastLap < previousBest);
            if (best && race != null && race.FormulaCompletedLaps < race.totalRaceLaps && race.totalRaceLaps > 1) FormulaAudio.Play(FormulaAudio.Cue.Record);
            if (best)
            {
                Array.Copy(currentSplits, bestSplits, currentSplits.Length);
                Array.Copy(recordedSplits, referenceSplits, recordedSplits.Length);
            }
            ShowMessage(!FormulaSession.LastLapValid ? "LAP NOT COUNTED" : best ? "NEW BEST LAP" : "LAP COMPLETE",
                FormulaSession.LastLapValid ? FormulaRaceUI.TimeText(FormulaSession.LastLap) : "Track limits · lap excluded from timing", 4, !FormulaSession.LastLapValid);
            previousBest = FormulaSession.BestLap;
            Array.Clear(recordedSplits, 0, recordedSplits.Length);
            splitText = "—"; splitDifference = 0;
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
        private void OnDestroy()
        {
            FormulaSession.CheckpointPassed -= OnCheckpoint; FormulaSession.LapCompleted -= OnLapCompleted;
            if (pauseController != null) pauseController.OptionsOverride = null;
        }
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
