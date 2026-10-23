using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FormulaGame
{
    [RequireComponent(typeof(GamePlayManager))]
    public sealed class FormulaRaceUI : MonoBehaviour
    {
        public float trackHalfWidth = 18f;
        private GamePlayManager game;
        private Canvas canvas;
        private TMP_Text hud;
        private float nextRefresh, outsideSeconds;
        private bool showingResult;
        private void Awake() => game = GetComponent<GamePlayManager>();
        private void OnEnable() => FormulaSession.Finished += OnFinished;
        private void OnDisable() => FormulaSession.Finished -= OnFinished;
        private void Start()
        {
            canvas = FormulaUI.Canvas("Formula Race", 180);
            var bar = FormulaUI.Box(canvas.transform, "RaceHeader", 0, 0, 1280, 80, FormulaUI.Background);
            hud = FormulaUI.Text(bar, "", 24, 8, 1000, 64, 26);
            FormulaUI.Button(bar, "race-menu", "Menu", 1090, 8, 166, 64, FormulaSession.ReturnToMenu);
        }
        private void Update()
        {
            if (game.CarController == null || showingResult || !FormulaSession.InRace) return;
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .1f;
            if (FormulaSession.LastError != null)
            {
                showingResult = true;
                var errorPanel = FormulaUI.Box(canvas.transform, "SaveError", 0, 0, 1280, 720, FormulaUI.Background);
                FormulaUI.Text(errorPanel, "The result could not be saved\n" + FormulaSession.LastError, 80, 140, 1100, 200, 28);
                FormulaUI.Button(errorPanel, "retry-save", "Retry saving", 80, 400, 500, 90, () => { showingResult = false; errorPanel.gameObject.SetActive(false); Destroy(errorPanel.gameObject); FormulaSession.RetryResult(); });
                FormulaUI.Button(errorPanel, "leave", "Menu without rewards", 620, 400, 540, 90, FormulaSession.ReturnToMenu);
                FormulaUI.Focus(errorPanel); return;
            }
            var e = FormulaSession.CurrentEvent;
            FormulaSession.ObserveBraking(game.CarController.absoluteSpeed, game.CarController.Inputs.inputs.brakeInput);
            hud.text = $"{e.name}  ·  Lap {Mathf.Min(game.FormulaCompletedLaps + 1, game.totalRaceLaps)}/{game.totalRaceLaps}  ·  {TimeText(game.FormulaElapsed)}\nBest lap: {TimeText(FormulaSession.BestLap)}";
            if (e.mode == "familiarization") hud.text += " · Complete a valid lap";
            if (!game.FormulaRaceStarted) return;
            float distance = DistanceToRoute(game.CarController.transform.position);
            outsideSeconds = distance > trackHalfWidth ? outsideSeconds + .1f * Time.timeScale : 0;
            if (outsideSeconds >= 1f) FormulaSession.InvalidateLap();
        }
        public float DistanceToRoute(Vector3 position)
        {
            var route = game.raceWaypoints;
            if (route == null || route.waypoints.Count < 3) return float.PositiveInfinity;
            float best = float.PositiveInfinity; position.y = 0;
            for (int i = 0; i < route.waypoints.Count; i++)
            {
                Vector3 a = route.waypoints[i].transform.position, b = route.waypoints[(i + 1) % route.waypoints.Count].transform.position;
                a.y = b.y = 0; Vector3 d = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(position - a, d) / Mathf.Max(.01f, d.sqrMagnitude));
                best = Mathf.Min(best, Vector3.Distance(position, a + d * t));
            }
            return best;
        }
        private void OnFinished(FormulaReceipt receipt) => StartCoroutine(ShowResults(receipt));
        private IEnumerator ShowResults(FormulaReceipt receipt)
        {
            yield return null; // Restore focus after the existing finish screen updates.
            showingResult = true;
            if (FormulaMenuAssets.Current != null && FormulaMenuAssets.Current.resultsPrefab != null)
            {
                var result = Instantiate(FormulaMenuAssets.Current.resultsPrefab, canvas.transform, false);
                result.GetComponent<FormulaResultsView>().Present(receipt); yield break;
            }
            var panel = FormulaUI.Box(canvas.transform, "Formula Results", 0, 0, 1280, 720, FormulaUI.Background);
            FormulaUI.Text(panel, FormulaSession.CurrentEvent.name, 60, 50, 1160, 60, 36);
            FormulaUI.Text(panel, FormulaUI.MedalName(receipt.medal), 60, 125, 1160, 65, 40, FormulaUI.Accent);
            string text = $"Time: {TimeText(receipt.elapsedSeconds)}  ·  Best lap: {TimeText(receipt.bestLapSeconds)}\n\n+{receipt.credits:N0} CR  ·  +{receipt.xp:N0} XP\nLevel bonus: +{receipt.levelCredits:N0} CR\nBalance: {FormulaProgression.Data.money:N0} CR  ·  Level {FormulaProgression.Data.currentLevel}";
            foreach (string id in receipt.newLiveries) text += "\nLivery unlocked: " + FormulaCatalog.Current.Skin(id).displayName;
            FormulaUI.Text(panel, text, 60, 215, 1160, 300, 28);
            FormulaUI.Button(panel, "retry", "Retry event", 60, 580, 340, 80, () => FormulaSession.Launch(receipt.eventId, FormulaProgression.State.activeMode));
            var next = FormulaCatalog.Current.careerEvents.Find(e => FormulaProgression.State.Best(e.id) < Medal.Bronze);
            if (next != null) FormulaUI.Button(panel, "next", "Continue career", 420, 580, 390, 80, () => FormulaSession.Launch(next.id, "career"));
            FormulaUI.Button(panel, "menu", "Menu", 830, 580, 390, 80, FormulaSession.ReturnToMenu);
            FormulaUI.Focus(panel);
        }
        public static string TimeText(double seconds)
        {
            if (seconds <= 0 || double.IsInfinity(seconds)) return "—";
            int minutes = (int)(seconds / 60); return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:00}:{1:00.000}", minutes, seconds % 60);
        }
        private void OnDestroy() { if (canvas != null) Destroy(canvas.gameObject); }
    }
}
