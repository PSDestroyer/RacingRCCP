using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HalvaStudio.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace FormulaGame
{
    // Coordinates serialized views. Layout and visual assets are authored in Formula_Menu.prefab.
    [DefaultExecutionOrder(-10000)]
    public sealed class FormulaMainMenu : MonoBehaviour
    {
        [SerializeField] private FormulaMenuView view;
        [SerializeField] private FormulaGarageVehiclePreview vehiclePreview;
        [SerializeField] private bool startInGarage;
        // Migration references retained for existing serialized scenes and authoring tools.
        [HideInInspector] public Canvas legacyCanvas;
        [HideInInspector] public GarageUIController legacyNavigation;
        [HideInInspector] public Transform spawnPoint;
        [HideInInspector] public UnityEngine.UI.Button legacyBack;
        [HideInInspector] public GameObject[] legacyDisplayCars = Array.Empty<GameObject>();
        [SerializeField, HideInInspector] private GameObject editorGaragePreview;
        [SerializeField, HideInInspector] private float previewHorizontalOffset;
        private readonly Dictionary<string, GameObject> focusMemory = new Dictionary<string, GameObject>();
        private string page = "Acasă", garageTab = "Dezvoltare", renderedKey, selectedLivery;
        private Transform activeScreen;
        private GameObject modalOrigin;
        private Coroutine fadeRoutine;
        private Vector2 screenPosition;
        private InputAction cancelAction;
        private bool initialized, transactionBusy, statsDirty;
        private static readonly string[] GarageTabs = { "Dezvoltare", "Setup", "Livree" };
        public string CurrentPage => page;
        public string CurrentGarageTab => garageTab;
        public FormulaMenuView View => view;
        private void Awake()
        {
            FormulaSession.EnterMenu();
            // Old scenes cannot flash their previous Canvas while being migrated.
            if (legacyCanvas != null) legacyCanvas.gameObject.SetActive(false);
            if (legacyNavigation != null) legacyNavigation.enabled = false;
        }
        private void Start()
        {
            if (view == null || vehiclePreview == null)
            {
                Debug.LogError("Formula menu scene is not authored. Run Tools/Formula Career/Author Menu Hierarchy.", this); enabled = false; return;
            }
            FormulaProgression.Ensure(FormulaProgression.Data);
            vehiclePreview.Initialize();
            cancelAction = EventSystem.current?.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>()?.cancel?.action;
            view.Back.onClick.AddListener(Back);
            view.Home.Bind(() => EventDetails(FormulaCatalog.Current.careerEvents.Find(e => FormulaProgression.State.Best(e.id) < Medal.Bronze) ?? FormulaCatalog.Current.Event("g06"), "career"),
                () => Show("Garaj"), () => ChangeGarageTab(0), () => Show("Curse rapide"), () => Show("Profil"), () => Show("Setări"));
            view.Career.Bind(InspectEvent); view.Quick.Bind(InspectEvent);
            view.Home.BindCareerOverview(() => Show("Carieră"));
            view.Garage.Bind(ChangeGarageTab, UpgradeLevels, BuyUpgrade, id => Mutate(d => FormulaProgression.EquipSetup(d, id)), SelectLivery, ApplySelectedLivery);
            view.Dialogs.Bind(CloseModal); view.Dialogs.Close();
            view.Settings.Bind(id => Mutate(d => { if (id == "abs") d.formula.abs = !d.formula.abs; else if (id == "tcs") d.formula.tcs = !d.formula.tcs; else d.formula.esp = !d.formula.esp; return null; }));
            screenPosition = ((RectTransform)view.Screens).anchoredPosition;
            initialized = true;
            if (startInGarage) garageTab = "Livree";
            selectedLivery = FormulaProgression.State.equippedLivery;
            Refresh(); Show(startInGarage ? "Garaj" : "Acasă");
        }
        private void OnEnable() => FormulaProgression.Changed += Refresh;
        private void OnDisable() => FormulaProgression.Changed -= Refresh;
        private void Update()
        {
            if (!initialized) return;
            if (!view.Dialogs.IsOpen && page == "Garaj")
            {
                bool previous = Gamepad.current?.leftShoulder.wasPressedThisFrame == true || Keyboard.current?.qKey.wasPressedThisFrame == true;
                bool next = Gamepad.current?.rightShoulder.wasPressedThisFrame == true || Keyboard.current?.eKey.wasPressedThisFrame == true;
                if (previous || next) ChangeGarageTab((Array.IndexOf(GarageTabs, garageTab) + (next ? 1 : 2)) % 3);
            }
            bool cancel = cancelAction?.WasPerformedThisFrame() == true || Keyboard.current?.escapeKey.wasPressedThisFrame == true;
            if (cancel) Back();
        }
        public void Back()
        {
            if (!initialized) return;
            if (view.Dialogs.IsOpen) CloseModal();
            else if (page == "Setări" && view.Settings.BackWithinPage()) return;
            else if (page != "Acasă") Show("Acasă");
        }
        private void ChangeGarageTab(int index) { garageTab = GarageTabs[index]; Show("Garaj"); }
        private void RememberFocus()
        {
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (selected != null && activeScreen != null && selected.transform.IsChildOf(activeScreen) && renderedKey != null) focusMemory[renderedKey] = selected;
        }
        private void RestoreFocus(GameObject preferred = null)
        {
            if (view.Dialogs.IsOpen || activeScreen == null || EventSystem.current == null) return;
            var candidate = preferred;
            if (candidate == null) focusMemory.TryGetValue(renderedKey, out candidate);
            var selectable = candidate != null ? candidate.GetComponent<UnityEngine.UI.Selectable>() : null;
            if (selectable == null || !selectable.IsInteractable() || !selectable.IsActive())
            {
                selectable = page == "Garaj" && garageTab == "Livree" ? activeScreen.GetComponentsInChildren<FormulaLiveryCard>().FirstOrDefault(c => c.LiveryId == selectedLivery)?.GetComponent<UnityEngine.UI.Selectable>() : null;
            }
            if (selectable != null) EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            else FormulaUI.Focus(activeScreen);
        }
        private IEnumerator RevealContent()
        {
            var screen = (RectTransform)view.Screens;
            view.Transition.alpha = 0;
            for (float time = 0; time < .18f; time += Time.unscaledDeltaTime)
            {
                float blend = Mathf.SmoothStep(0, 1, time / .18f); view.Transition.alpha = blend;
                screen.anchoredPosition = screenPosition + Vector2.right * (14 * (1 - blend)); yield return null;
            }
            screen.anchoredPosition = screenPosition; view.Transition.alpha = 1; fadeRoutine = null;
        }
        private void Refresh()
        {
            if (!initialized) return;
            view.RefreshWallet(); view.Home.Refresh(); view.Career.Refresh(); view.Quick.Refresh(); view.Profile.Refresh(); view.Settings.Refresh(); view.Garage.Refresh();
            selectedLivery = FormulaProgression.State.equippedLivery; view.Garage.InspectLivery(selectedLivery);
            vehiclePreview.ShowLivery(selectedLivery); statsDirty = true;
        }
        public void Show(string target)
        {
            if (!initialized) return;
            RememberFocus(); string previousKey = renderedKey; page = target;
            renderedKey = page + "|" + (page == "Garaj" ? garageTab : "");
            activeScreen = view.Activate(page); view.Garage.ShowSection(Array.IndexOf(GarageTabs, garageTab));
            vehiclePreview.Present(page, garageTab, previousKey == null);
            string hints = Gamepad.current != null ? (page == "Garaj" ? "L/R · Sections\nConfirm · Back" : "Confirm · Select\nBack · Menu") : (page == "Garaj" ? "Q/E · Sections\nEnter · Confirm" : "Enter · Confirm\nEsc · Back");
            view.SetChrome(page == "Acasă" ? "MAIN MENU" : page == "Garaj" ? "GARAGE / " + FormulaMenuFormatting.PageName(garageTab).ToUpperInvariant() : FormulaMenuFormatting.PageName(page).ToUpperInvariant(), hints, page != "Acasă");
            selectedLivery = FormulaProgression.State.equippedLivery;
            vehiclePreview.ShowLivery(selectedLivery); view.Garage.InspectLivery(selectedLivery); statsDirty = true;
            RestoreFocus();
            if (previousKey != null && previousKey != renderedKey)
            {
                if (fadeRoutine != null) StopCoroutine(fadeRoutine); fadeRoutine = StartCoroutine(RevealContent());
            }
        }
        private void LateUpdate()
        {
            if (!statsDirty || !initialized || vehiclePreview.Vehicle == null || !vehiclePreview.Vehicle.IsReady) return;
            statsDirty = false; view.Garage.RefreshStatistics(vehiclePreview.Car, vehiclePreview.Wheel);
        }
        private void InspectEvent(string id, string mode) => EventDetails(FormulaCatalog.Current.Event(id), mode);
        private void EventDetails(FormulaCatalog.CareerEvent e, string mode)
        {
            bool unlocked = mode != "career" || FormulaProgression.Unlocked(e.unlock, FormulaProgression.State);
            OpenDialog(); view.Dialogs.EventBriefing(e, mode, () => { if (unlocked) Launch(e.id, mode); }, unlocked);
        }
        private static string DevelopmentDetails(string branch, int previous, int next)
        {
            switch (branch)
            {
                case "engine": return $"Torque: +{previous * 3}% → +{next * 3}% over stock.\nAcceleration also depends on grip. No additional tradeoff.";
                case "brakes": return $"Brake torque: +{previous * 4}% → +{next * 4}%.\nAxle balance is retained. ABS and grip limit braking.";
                case "suspension": return $"Springs +{next * 1.5f:0.#}%, damping +{next * 3.5f:0.#}%, anti-roll +{next * 2.5f:0.#}%.\nFirmer response; less compliance over bumps.";
                default: return $"Downforce: +{previous * 4}% → +{next * 4}%.\nTradeoff: aerodynamic drag +{next}% over stock.";
            }
        }
        private void BuyUpgrade(string branchId)
        {
            var p = FormulaProgression.State; var branch = FormulaCatalog.Current.Upgrade(branchId); int next = p.Owned(branchId) + 1;
            var level = branch.levels.Find(l => l.level == next); if (level == null) return;
            bool unlocked = FormulaProgression.Unlocked(level.unlock, p), affordable = FormulaProgression.Data.money >= level.incrementalPriceCredits;
            string reason = !unlocked ? FormulaProgression.LockReason(level.unlock, p) : !affordable ? "You need another " + (level.incrementalPriceCredits - FormulaProgression.Data.money).ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " CR." : "Available to purchase.";
            Confirm(branch.name + " / Level " + next, DevelopmentDetails(branchId, p.Equipped(branchId), next) + "\n\n" + reason + "\nCost: " + level.incrementalPriceCredits.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " CR", unlocked && affordable ? "Buy and equip" : "Unavailable", () => Mutate(d => FormulaProgression.BuyUpgrade(d, branchId, next)), unlocked && affordable);
        }
        private void UpgradeLevels(string branchId)
        {
            OpenDialog(); view.Dialogs.SelectLevel(branchId, level => Mutate(d => FormulaProgression.EquipUpgrade(d, branchId, level)));
        }
        private void SelectLivery(string id) { selectedLivery = id; vehiclePreview.ShowLivery(id); view.Garage.InspectLivery(id); }
        private void ApplySelectedLivery()
        {
            var livery = FormulaCatalog.Current.Skin(selectedLivery); var p = FormulaProgression.State;
            if (p.ownedLiveries.Contains(livery.id)) Mutate(d => FormulaProgression.BuyOrEquipLivery(d, livery.id));
            else if (FormulaProgression.Unlocked(livery.unlock, p) && livery.acquisition == "credit_purchase")
                Confirm(livery.displayName, "Cosmetic livery. Your upgrades and setup are retained.\nCost: " + livery.priceCredits.Value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " CR", "Buy and equip", () => Mutate(d => FormulaProgression.BuyOrEquipLivery(d, livery.id)));
        }
        private void Launch(string id, string mode) { string error = FormulaSession.Launch(id, mode); if (error != null) { CloseModal(); view.SetStatus(error); } }
        private void Mutate(Func<SaveManager.SaveData, string> action)
        {
            if (transactionBusy) return;
            var origin = view.Dialogs.IsOpen ? modalOrigin : EventSystem.current?.currentSelectedGameObject;
            transactionBusy = true; bool ok = FormulaProgression.Commit(action, out string error);
            CloseModal(); transactionBusy = false;
            if (ok) RestoreFocus(origin); else view.SetStatus(error);
        }
        private void OpenDialog()
        {
            modalOrigin = EventSystem.current?.currentSelectedGameObject; view.SetBaseInteraction(false);
        }
        private void Confirm(string title, string details, string accept, Action action, bool canConfirm = true)
        {
            OpenDialog(); view.Dialogs.Confirm(title, details, accept, () => { if (canConfirm) action(); }, canConfirm);
        }
        private void CloseModal()
        {
            bool wasOpen = view.Dialogs.IsOpen; view.Dialogs.Close(); view.SetBaseInteraction(true);
            if (wasOpen) RestoreFocus(modalOrigin);
        }
    }
}
