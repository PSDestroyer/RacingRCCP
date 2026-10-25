using System.Collections;
using UnityEngine;

namespace FormulaGame
{
    [RequireComponent(typeof(GamePlayManager))]
    [DefaultExecutionOrder(100)]
    public sealed class FormulaRaceUI : MonoBehaviour
    {
        public float trackHalfWidth = 18f;
        private GamePlayManager game;
        private Canvas canvas;
        private FormulaRaceView raceView;
        private float nextRefresh, outsideSeconds;
        private bool showingResult;
        private void Awake() => game = GetComponent<GamePlayManager>();
        private void OnEnable() => FormulaSession.Finished += OnFinished;
        private void OnDisable() => FormulaSession.Finished -= OnFinished;
        private void Start()
        {
            canvas = FormulaUI.Canvas("Formula Race", 180);
            FormulaInputHints.ConfigureUI(UnityEngine.EventSystems.EventSystem.current?.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>());
            if (FormulaMenuAssets.Current.racePrefab == null) { Debug.LogError("Formula race presentation is not authored."); enabled = false; return; }
            raceView = Instantiate(FormulaMenuAssets.Current.racePrefab, canvas.transform, false).GetComponent<FormulaRaceView>();
            raceView.Bind(game);
            canvas.gameObject.AddComponent<FormulaPresentation>();
            HideLegacyHud();
        }
        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .1f;
            raceView?.Tick(game);
            if (game.CarController == null || showingResult || !FormulaSession.InRace || raceView == null) return;
            if (FormulaSession.LastError != null)
            {
                raceView.SaveError(FormulaSession.LastError); return;
            }
            FormulaSession.ObserveBraking(game.CarController.absoluteSpeed, game.CarController.Inputs.inputs.brakeInput);
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
            showingResult = true; raceView.Finish();
            if (FormulaMenuAssets.Current != null && FormulaMenuAssets.Current.resultsPrefab != null)
            {
                var result = Instantiate(FormulaMenuAssets.Current.resultsPrefab, canvas.transform, false);
                result.GetComponent<FormulaResultsView>().Present(receipt);
                canvas.GetComponent<FormulaPresentation>().Register((RectTransform)result.transform); yield break;
            }
            Debug.LogError("Formula results prefab is not authored.");
        }
        private void HideLegacyHud()
        {
            foreach (var legacyCanvas in game.transform.root.GetComponentsInChildren<Canvas>(true))
                if (legacyCanvas != canvas) legacyCanvas.enabled = false;
            foreach (var source in new[] { game.lapUIRoot, game.timerUIRoot })
                if (source != null)
                {
                    var oldCanvas = source.GetComponentInParent<Canvas>();
                    if (oldCanvas != null && oldCanvas != canvas) oldCanvas.enabled = false;
                }
            foreach (var legacy in FindObjectsByType<GameplayMangerCarHud>(FindObjectsSortMode.None))
            {
                legacy.enabled = false;
                foreach (var item in new UnityEngine.Object[] { legacy.speedKmhText, legacy.currentGearText, legacy.currentGearImage, legacy.tachometerNeedle, legacy.absImage, legacy.espImage, legacy.tcsImage })
                    if (item is Component component) component.gameObject.SetActive(false);
            }
            foreach (var text in new[] { game.currentLapText, game.totalLapsText, game.missionGoalText, game.racePositionText, game.racePositionTotalText, game.liveLeaderboardText, game.raceStateText, game.DriftModeText, game.DriftTimerText, game.PerfectDriftBestTimeText, game.TotalScoreText, game.scoreText, game.missionIntroText })
                if (text != null) text.enabled = false;
        }
        public static string TimeText(double seconds)
        {
            if (seconds <= 0 || double.IsInfinity(seconds)) return "—";
            int minutes = (int)(seconds / 60); return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:00}:{1:00.000}", minutes, seconds % 60);
        }
        private void OnDestroy() { if (canvas != null) Destroy(canvas.gameObject); }
    }
}
