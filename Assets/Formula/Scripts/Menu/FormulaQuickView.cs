using System;
using TMPro;
using UnityEngine;

namespace FormulaGame
{
    public sealed class FormulaQuickView : MonoBehaviour
    {
        [SerializeField] private FormulaEventCard[] events;
        [SerializeField] private UnityEngine.UI.Button[] modes;
        [SerializeField] private GameObject[] modeMarkers;
        [SerializeField] private TMP_Text sessionDetails;
        private int selectedMode;
        private static readonly string[,] EventIds = { { "g03", "g04" }, { "g02", "g05" }, { "g01", "g05" } };
        public void Bind(Action<string, string> inspect)
        {
            for (int i = 0; i < modes.Length; i++) { int index = i; modes[i].onClick.AddListener(() => SelectMode(index)); }
            for (int i = 0; i < events.Length; i++) { int track = i; events[i].GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() => inspect(EventIds[selectedMode, track], selectedMode == 1 ? "time_trial" : "quick")); }
        }
        public void SelectMode(int index) { selectedMode = Mathf.Clamp(index, 0, 2); Refresh(); }
        public void Refresh()
        {
            for (int i = 0; i < modeMarkers.Length; i++) modeMarkers[i].SetActive(i == selectedMode);
            if (sessionDetails != null) sessionDetails.text = selectedMode == 0 ? "RACE  /  3 LAPS  /  3 AI DRIVERS\nYour equipped upgrades and setup are used." : selectedMode == 1 ? "TIME ATTACK  /  3 LAPS  /  SOLO\nStock car and Balanced setup for an equal challenge." : "PRACTICE  /  SOLO\nGrand Prix: 1 lap · Technical: 3 laps. Your equipped car is used.";
        }
    }
}
