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
        [SerializeField] private TMP_Text pageLabel;
        [SerializeField] private UnityEngine.UI.Button previousPage, nextPage;
        private int selectedMode, page;
        private Action<string, string> inspect;
        public int Page => page;
        public int PageCount => Mathf.Max(1, Mathf.CeilToInt((float)FormulaAssets.Current.tracks.Length / events.Length));
        public int SelectedMode => selectedMode;
        private string Session => selectedMode == 0 ? "race" : selectedMode == 1 ? "trial" : "practice";
        public void Bind(Action<string, string> inspect)
        {
            this.inspect = inspect;
            for (int i = 0; i < modes.Length; i++) { int index = i; modes[i].onClick.AddListener(() => SelectMode(index)); }
            for (int i = 0; i < events.Length; i++) { int slot = i; events[i].GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() => OpenCircuit(slot)); }
            if (previousPage != null) previousPage.onClick.AddListener(() => SetPage(page - 1));
            if (nextPage != null) nextPage.onClick.AddListener(() => SetPage(page + 1));
        }
        private void OpenCircuit(int slot)
        {
            int index = page * events.Length + slot;
            var tracks = FormulaAssets.Current.tracks;
            if (index < tracks.Length) inspect?.Invoke(Session + ":" + tracks[index].id, selectedMode == 1 ? "time_trial" : "quick");
        }
        public void SetPage(int index) { page = (index % PageCount + PageCount) % PageCount; Refresh(); }
        public void SelectMode(int index) { selectedMode = Mathf.Clamp(index, 0, 2); Refresh(); }
        public void Refresh()
        {
            var tracks = FormulaAssets.Current.tracks;
            page = Mathf.Clamp(page, 0, PageCount - 1);
            for (int i = 0; i < modeMarkers.Length; i++) modeMarkers[i].SetActive(i == selectedMode);
            for (int i = 0; i < events.Length; i++)
            {
                int index = page * events.Length + i; events[i].gameObject.SetActive(index < tracks.Length);
                if (index < tracks.Length) events[i].ShowCircuit(tracks[index], index, Session);
            }
            if (pageLabel != null) pageLabel.text = "CIRCUITS " + (page * events.Length + 1).ToString("00") + "–" + Mathf.Min((page + 1) * events.Length, tracks.Length).ToString("00") + "  /  " + tracks.Length.ToString("00");
            // Keep both pager controls focusable so changing page never strands controller focus.
            if (previousPage != null) previousPage.interactable = PageCount > 1;
            if (nextPage != null) nextPage.interactable = PageCount > 1;
            if (sessionDetails != null) sessionDetails.text = selectedMode == 0 ? "RACE  /  3 LAPS  /  3 AI DRIVERS  ·  Equipped car" : selectedMode == 1 ? "TIME ATTACK  /  3 LAPS  /  SOLO  ·  Stock car · Balanced setup" : "PRACTICE  /  1 LAP  /  SOLO  ·  Equipped car";
        }
    }
}
