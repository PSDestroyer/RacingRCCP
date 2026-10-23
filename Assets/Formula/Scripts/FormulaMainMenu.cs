using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
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
        private string briefingEvent, briefingMode, garageReturnEvent, garageReturnMode, garageReturnPage;
        private GameObject garageReturnFocus;
        private bool garageBriefing;
        private string previousBriefingEvent, previousBriefingMode;
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
            view.Home.Bind(() => Show("Carieră"),
                OpenGarage, () => Show("Curse rapide"), () => Show("Profil"), () => Show("Setări"));
            view.Career.Bind(InspectEvent, Back); view.Quick.Bind(InspectEvent);
            view.Garage.Bind(ChangeGarageTab, UpgradeLevels, BuyUpgrade, id => Mutate(d => FormulaProgression.EquipSetup(d, id)), SelectLivery, ApplySelectedLivery);
            if (view.Garage.Upgrade != null) view.Garage.Upgrade.Bind(SelectUpgradeLevel, Back);
            view.Dialogs.Bind(CloseModal, OpenGarageFromBriefing); view.Dialogs.Close();
            view.Settings.Bind(id => Mutate(d => { if (id == "abs") d.formula.abs = !d.formula.abs; else if (id == "tcs") d.formula.tcs = !d.formula.tcs; else d.formula.esp = !d.formula.esp; return null; }));
            screenPosition = ((RectTransform)view.Screens).anchoredPosition;
            initialized = true;
            garageTab = GarageTabs[FormulaProgression.State.lastGarageTab];
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
                if (previous || next)
                {
                    ChangeGarageTab((Array.IndexOf(GarageTabs, garageTab) + (next ? 1 : 2)) % 3);
                }
            }
            bool cancel = cancelAction?.WasPerformedThisFrame() == true || Keyboard.current?.escapeKey.wasPressedThisFrame == true;
            if (cancel) Back();
        }
        public void Back()
        {
            if (!initialized) return;
            if (view.Dialogs.IsOpen) CloseModal();
            else if (page == "Garaj" && garageReturnEvent != null)
            {
                string id = garageReturnEvent, mode = garageReturnMode, target = garageReturnPage; var origin = garageReturnFocus;
                ClearGarageReturn(); Show(target); RestoreFocus(origin); EventDetails(FormulaCatalog.Current.Event(id), mode);
            }
            else if (page == "Setări" && view.Settings.BackWithinPage()) return;
            else if (page != "Acasă") Show("Acasă");
        }
        public void OpenGarage() { ClearGarageReturn(); Show("Garaj"); }
        private void ClearGarageReturn() { garageReturnEvent = garageReturnMode = garageReturnPage = null; garageReturnFocus = null; view.Garage.SetEventContext(null, null); }
        private void OpenGarageFromBriefing()
        {
            if (garageBriefing) { CloseModal(); return; }
            garageReturnEvent = briefingEvent; garageReturnMode = briefingMode; garageReturnPage = page; garageReturnFocus = modalOrigin;
            CloseModal(); Show("Garaj"); view.Garage.SetEventContext(FormulaCatalog.Current.Event(briefingEvent), briefingMode);
        }
        private void ChangeGarageTab(int index)
        {
            index = Mathf.Clamp(index, 0, GarageTabs.Length - 1); garageTab = GarageTabs[index];
            string error = null;
            if (FormulaProgression.State.lastGarageTab != index)
                FormulaProgression.Commit(d => { d.formula.lastGarageTab = index; return null; }, out error);
            Show("Garaj"); if (error != null) view.SetStatus(error);
        }
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
            view.RefreshWallet(); view.Career.Refresh(); view.Quick.Refresh(); view.Profile.Refresh(); view.Settings.Refresh(); view.Garage.Refresh();
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
            view.SetChrome(page == "Acasă" ? "MAIN MENU" : page == "Garaj" ? FormulaMenuFormatting.PageName(garageTab).ToUpperInvariant() : FormulaMenuFormatting.PageName(page).ToUpperInvariant(), hints, page != "Acasă");
            selectedLivery = FormulaProgression.State.equippedLivery;
            vehiclePreview.ShowLivery(selectedLivery); view.Garage.InspectLivery(selectedLivery); statsDirty = true;
            view.Garage.WireNavigation(); RestoreFocus();
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
            briefingEvent = e.id; briefingMode = mode;
            bool unlocked = mode != "career" || FormulaProgression.Unlocked(e.unlock, FormulaProgression.State);
            OpenDialog(); view.Dialogs.EventBriefing(e, mode, () => { if (unlocked) Launch(e.id, mode); }, unlocked);
        }
        private static void UpgradeEffects(string branch, int previous, int next, out string effect, out string tradeoff, out string benefit)
        {
            switch (branch)
            {
                case "engine":
                    effect = $"Torque  +{previous * 3}% → +{next * 3}%"; tradeoff = "No additional tradeoff";
                    benefit = "Higher torque improves acceleration when grip is available."; break;
                case "brakes":
                    effect = $"Brake torque  +{previous * 4}% → +{next * 4}%"; tradeoff = "Front-to-rear brake balance is retained";
                    benefit = "Higher brake torque adds braking force. Grip and ABS still limit stopping distance."; break;
                case "suspension":
                    effect = $"Springs  +{previous * 1.5f:0.#}% → +{next * 1.5f:0.#}%";
                    tradeoff = $"Damping +{next * 3.5f:0.#}% · Anti-roll +{next * 2.5f:0.#}%";
                    benefit = "Firmer chassis response, with less compliance over bumps."; break;
                default:
                    effect = $"Downforce  +{previous * 4}% → +{next * 4}%"; tradeoff = $"Drag  +{previous}% → +{next}%";
                    benefit = "More grip in fast corners, with increased air resistance."; break;
            }
        }
        private void BuyUpgrade(string branchId)
        {
            SelectUpgradeLevel(branchId, FormulaProgression.State.Owned(branchId) + 1);
        }
        private void UpgradeLevels(string branchId)
        {
            OpenDialog(); view.Dialogs.SelectLevel(branchId, level => Mutate(d => FormulaProgression.EquipUpgrade(d, branchId, level)));
        }
        private void SelectUpgradeLevel(string branchId, int level)
        {
            var p = FormulaProgression.State; var branch = FormulaCatalog.Current.Upgrade(branchId);
            if (level == p.Equipped(branchId)) return;
            var target = branch.levels.Find(l => l.level == level);
            if (level != 0 && target == null) return;
            UpgradeEffects(branchId, p.Equipped(branchId), level, out string effect, out string tradeoff, out string benefit);
            string requirement, cost, label; Action action; bool actionable = true, focusAction = false;
            int next = p.Owned(branchId) + 1;
            if (level <= p.Owned(branchId))
            {
                requirement = "OWNED\nSwitch between purchased levels for free."; cost = "Free to equip"; label = "Equip · Free";
                action = () => Mutate(d => FormulaProgression.EquipUpgrade(d, branchId, level)); focusAction = true;
            }
            else if (level != next)
            {
                requirement = "PREVIOUS LEVEL REQUIRED\nPurchase Level " + next + " first."; cost = UpgradePrice(target); label = "View Level " + next;
                action = () => { CloseModal(); view.Garage.Upgrade.FocusLevel(next); SelectUpgradeLevel(branchId, next); }; focusAction = true;
            }
            else
            {
                bool unlocked = FormulaProgression.Unlocked(target.unlock, p), affordable = FormulaProgression.Data.money >= target.incrementalPriceCredits;
                var eventRule = UpgradeEventRule(target.unlock, p, !unlocked);
                var careerEvent = FormulaCatalog.Current.Event((string)eventRule?["eventId"]);
                cost = UpgradePrice(target);
                if (!unlocked)
                {
                    requirement = "LOCKED\n" + (careerEvent != null ? "Earn " + FormulaCatalog.MedalName(FormulaCatalog.ParseMedal((string)eventRule["minimumMedal"])) + " in " + careerEvent.name + " to unlock this upgrade." : FormulaProgression.LockReason(target.unlock, p));
                    label = careerEvent != null ? "View " + careerEvent.name : "Locked"; actionable = careerEvent != null;
                    action = () => ViewGarageEvent(careerEvent); focusAction = true;
                }
                else if (!affordable)
                {
                    requirement = "INSUFFICIENT CREDITS\nComplete a career event to earn credits.";
                    label = careerEvent != null ? "Earn credits · " + careerEvent.name : "Not enough credits"; actionable = careerEvent != null;
                    action = () => ViewGarageEvent(careerEvent); focusAction = true;
                }
                else
                {
                    requirement = "AVAILABLE\nPurchase once. Switch between owned levels for free.";
                    label = "Buy and equip · " + target.incrementalPriceCredits.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " CR";
                    action = () => Mutate(d => FormulaProgression.BuyUpgrade(d, branchId, level));
                }
            }
            // Credits and progression are independent requirements: show both when both are missing.
            bool unowned = level > p.Owned(branchId);
            int shortfall = unowned ? Math.Max(0, target.incrementalPriceCredits - FormulaProgression.Data.money) : 0;
            string funds = unowned ? "Balance: " + FormulaProgression.Data.money.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " CR · Upgrade cost: " + target.incrementalPriceCredits.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " CR" : null;
            if (shortfall > 0) cost = "MISSING " + shortfall.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " CR";
            OpenDialog(); view.Dialogs.Upgrade(branch.name + " / " + (level == 0 ? "Stock" : "Level " + level), effect, tradeoff, benefit, requirement, cost, label, action, actionable, focusAction, funds, shortfall > 0);
        }
        private static string UpgradePrice(FormulaCatalog.UpgradeLevel level) => "Upgrade cost · " + level.incrementalPriceCredits.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " CR";
        private static JObject UpgradeEventRule(JObject rule, FormulaProgress progress, bool unmetOnly)
        {
            if (rule == null || (unmetOnly && FormulaProgression.Unlocked(rule, progress))) return null;
            if ((string)rule["type"] == "event_medal") return rule;
            if (rule["all"] is JArray all)
                foreach (var child in all.OfType<JObject>()) { var found = UpgradeEventRule(child, progress, unmetOnly); if (found != null) return found; }
            return null;
        }
        private void ViewGarageEvent(FormulaCatalog.CareerEvent careerEvent)
        {
            if (careerEvent == null) return;
            // Keep the Garage origin while the briefing replaces the upgrade dialog.
            previousBriefingEvent = briefingEvent; previousBriefingMode = briefingMode; garageBriefing = true;
            briefingEvent = careerEvent.id; briefingMode = "career";
            bool unlocked = FormulaProgression.Unlocked(careerEvent.unlock, FormulaProgression.State);
            view.Dialogs.EventBriefing(careerEvent, "career", () => { if (unlocked) Launch(careerEvent.id, "career"); }, unlocked);
        }
        private void SelectLivery(string id) { selectedLivery = id; vehiclePreview.ShowLivery(id); view.Garage.InspectLivery(id); }
        private void ApplySelectedLivery()
        {
            var livery = FormulaCatalog.Current.Skin(selectedLivery);
            var offer = new FormulaLiveryOffer(livery, FormulaProgression.State, FormulaProgression.Data.money);
            if (offer.Equipped) return;
            if (offer.Owned) { Mutate(d => FormulaProgression.BuyOrEquipLivery(d, livery.id)); return; }
            bool ready = offer.Unlocked && offer.Shortfall == 0;
            string actionName = ready ? offer.Purchase ? offer.Action : "Claim and equip · Free" : offer.Unlocked ? "Earn credits · " + offer.Event.name : "View " + offer.Event.name;
            Action action = ready ? () => Mutate(d => FormulaProgression.BuyOrEquipLivery(d, livery.id)) : () => ViewGarageEvent(offer.Event);
            OpenDialog(); view.Dialogs.Livery(livery, offer, actionName, action, !ready || !offer.Purchase);
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
            if (garageBriefing) { briefingEvent = previousBriefingEvent; briefingMode = previousBriefingMode; garageBriefing = false; }
            bool wasOpen = view.Dialogs.IsOpen; view.Dialogs.Close(); view.SetBaseInteraction(true);
            if (wasOpen) RestoreFocus(modalOrigin);
        }
    }
}
