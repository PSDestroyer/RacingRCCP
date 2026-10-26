using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
namespace FormulaGame
{
    public sealed class FormulaCareerView : MonoBehaviour
    {
        [SerializeField] private FormulaEventCard[] events;
        [SerializeField] private GameObject[] standardChrome;
        [SerializeField] private TMP_Text driver, wallet;
        [SerializeField] private UnityEngine.UI.Button back;
        [SerializeField] private TMP_Text pageLabel;
        [SerializeField] private UnityEngine.UI.Button previousPage, nextPage;
        private int page;
        private bool opened;
        public int Page => page;
        public int PageCount => Mathf.Max(1, Mathf.CeilToInt((float)FormulaCatalog.Current.careerEvents.Count / events.Length));
        public void Bind(Action<string, string> inspect, Action onBack = null)
        {
            foreach (var card in events) card.Bind(inspect);
            if (back != null && onBack != null) back.onClick.AddListener(() => onBack());
            if (previousPage != null) previousPage.onClick.AddListener(() => SetPage(page - 1));
            if (nextPage != null) nextPage.onClick.AddListener(() => SetPage(page + 1));
        }
        public void Open()
        {
            if (opened) return;
            opened = true;
            var next = FormulaCatalog.Current.careerEvents.Find(e => FormulaProgression.State.Best(e.id) < Medal.Bronze);
            ShowEvent(next?.id ?? FormulaProgression.State.lastEventId);
        }
        public GameObject ShowEvent(string id)
        {
            int index = FormulaCatalog.Current.careerEvents.FindIndex(e => e.id == id);
            if (index < 0) return null;
            SetPage(index / events.Length);
            return events[index % events.Length].gameObject;
        }
        public void SetPage(int index)
        {
            page = (index % PageCount + PageCount) % PageCount;
            Refresh();
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (isActiveAndEnabled && selected != null && selected.transform.IsChildOf(transform) && !selected.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(events[0].gameObject);
        }
        public void SetPresentationActive(bool active)
        {
            if (standardChrome == null) return;
            foreach (var item in standardChrome) if (item != null) item.SetActive(!active);
        }
        public void Refresh()
        {
            var catalog = FormulaCatalog.Current.careerEvents;
            page = Mathf.Clamp(page, 0, PageCount - 1);
            for (int i = 0; i < events.Length; i++)
            {
                int index = page * events.Length + i;
                events[i].gameObject.SetActive(index < catalog.Count);
                if (index < catalog.Count) events[i].ShowCareer(catalog[index], index);
            }
            if (pageLabel != null) pageLabel.text = "EVENTS " + (page * events.Length + 1).ToString("00") + "–" + Mathf.Min((page + 1) * events.Length, catalog.Count).ToString("00") + "  /  " + catalog.Count.ToString("00");
            if (previousPage != null) previousPage.interactable = PageCount > 1;
            if (nextPage != null) nextPage.interactable = PageCount > 1;
            WireNavigation();
            if (driver != null) driver.text = FormulaMenuFormatting.DriverName;
            if (wallet != null) wallet.text = FormulaProgression.Data.money.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        }
        private void WireNavigation()
        {
            var buttons = events.Where(c => c.gameObject.activeSelf).Select(c => c.GetComponent<UnityEngine.UI.Button>()).ToArray();
            for (int i = 0; i < buttons.Length; i++)
                buttons[i].navigation = new UnityEngine.UI.Navigation
                {
                    mode = UnityEngine.UI.Navigation.Mode.Explicit,
                    selectOnUp = i >= 3 ? buttons[i - 3] : back,
                    selectOnDown = i + 3 < buttons.Length ? buttons[i + 3] : i % 3 == 0 ? previousPage : nextPage,
                    selectOnLeft = buttons[(i + buttons.Length - 1) % buttons.Length],
                    selectOnRight = buttons[(i + 1) % buttons.Length]
                };
            if (buttons.Length == 0) return;
            foreach (var pager in new[] { previousPage, nextPage })
                if (pager != null) pager.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnUp = buttons[buttons.Length - 1], selectOnDown = back, selectOnLeft = pager == previousPage ? back : previousPage, selectOnRight = pager == nextPage ? back : nextPage };
            if (back != null) back.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnUp = buttons[buttons.Length - 1], selectOnDown = buttons[0], selectOnLeft = nextPage, selectOnRight = previousPage };
        }
    }
}
