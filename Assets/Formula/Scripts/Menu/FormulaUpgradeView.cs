using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FormulaGame
{
    // A component page owns its level choices. The presenter owns purchases and saves.
    public sealed class FormulaUpgradeView : MonoBehaviour
    {
        [Serializable] public sealed class LevelRow
        {
            public UnityEngine.UI.Button button;
            public TMP_Text value, status;
            public UnityEngine.UI.RawImage surface, coin;
        }
        [SerializeField] private UnityEngine.UI.Button[] categories;
        [SerializeField] private GameObject[] categoryMarkers, standardChrome;
        [SerializeField] private LevelRow[] levels;
        [SerializeField] private TMP_Text driver, wallet, description;
        [SerializeField] private UnityEngine.UI.Button back;
        [SerializeField] private FormulaStatisticsView statistics;
        [SerializeField] private Texture2D normalSurface, installedSurface;
        private static readonly string[] Branches = { "engine", "suspension", "aero", "brakes" };
        private Action<string, int> apply;
        private int category, previewLevel = 1;
        private RCCP_CarController car;
        private WheelCollider wheel;
        private UnityEngine.UI.Selectable tabNavigation;
        public string BranchId => Branches[category];
        public bool IsPresented { get; private set; }
        public void Bind(Action<string, int> onApply, Action onBack)
        {
            apply = onApply;
            for (int i = 0; i < categories.Length; i++) { int index = i; categories[i].onClick.AddListener(() => SelectCategory(index)); }
            for (int i = 0; i < levels.Length; i++) { int level = i; levels[i].button.onClick.AddListener(() => { PreviewLevel(level); apply?.Invoke(BranchId, level); }); }
            back.onClick.AddListener(() => onBack());
        }
        public void SetPresentationActive(bool active)
        {
            if (active == IsPresented) return;
            IsPresented = active;
            foreach (var item in standardChrome) if (item != null) item.SetActive(!active);
            if (active) { Refresh(); WireNavigation(); }
        }
        public void SelectCategory(int index)
        {
            if (category != index) { category = Mathf.Clamp(index, 0, Branches.Length - 1); previewLevel = Mathf.Min(3, FormulaProgression.State.Owned(BranchId) + 1); }
            Render(); WireNavigation();
        }
        public void ChangeCategory(int delta)
        {
            bool categoryFocus = Array.Exists(categories, b => b.gameObject == EventSystem.current?.currentSelectedGameObject);
            SelectCategory((category + delta + Branches.Length) % Branches.Length);
            if (categoryFocus) EventSystem.current?.SetSelectedGameObject(categories[category].gameObject);
        }
        public void PreviewLevel(int level) { previewLevel = level; Render(); }
        public void FocusLevel(int level)
        {
            PreviewLevel(level); EventSystem.current?.SetSelectedGameObject(levels[level].button.gameObject);
        }
        public void Refresh()
        {
            driver.text = FormulaMenuFormatting.DriverName;
            wallet.text = FormulaProgression.Data.money.ToString("N0", CultureInfo.InvariantCulture);
            Render();
        }
        public void RefreshPhysics(RCCP_CarController controller, WheelCollider collider) { car = controller; wheel = collider; Render(); }
        private void Render()
        {
            if (levels == null || levels.Length == 0) return;
            var p = FormulaProgression.State; var branch = FormulaCatalog.Current.Upgrade(BranchId);
            int owned = p.Owned(BranchId), equipped = p.Equipped(BranchId);
            for (int i = 0; i < categories.Length; i++) categoryMarkers[i].SetActive(i == category);
            for (int i = 0; i < levels.Length; i++)
            {
                var row = levels[i]; bool installed = i == equipped, purchased = i <= owned;
                row.surface.texture = installed ? installedSurface : normalSurface;
                row.status.text = installed ? "INSTALLED" : purchased ? "OWNED · EQUIP" : branch.levels.Find(l => l.level == i).incrementalPriceCredits.ToString("N0", CultureInfo.InvariantCulture);
                row.status.fontSize = installed ? 28 : purchased ? 22 : 20;
                row.status.color = !purchased && FormulaProgression.Data.money < branch.levels.Find(l => l.level == i).incrementalPriceCredits ? FormulaUI.Red : Color.white;
                row.coin.gameObject.SetActive(!purchased);
                row.value.text = car != null ? ValueAtLevel(BranchId, i, car, wheel).ToString(BranchId == "aero" ? "0.##" : "0.#", CultureInfo.InvariantCulture) + " " + Unit(BranchId) : "—";
            }
            var selected = previewLevel == 0 ? null : branch.levels.Find(l => l.level == previewLevel);
            string state = previewLevel == equipped ? "Installed" : previewLevel <= owned ? "Owned · Select to equip" : previewLevel != owned + 1 ? "Purchase Level " + (owned + 1) + " first" : !FormulaProgression.Unlocked(selected.unlock, p) ? "Requires " + FormulaProgression.LockReason(selected.unlock, p).Replace(" · ", " in ") : "Select to review purchase";
            string effect = BranchId == "engine" ? "Torque +3% per level" : BranchId == "brakes" ? "Brake torque +4% per level" : BranchId == "suspension" ? "Firmer springs, damping and anti-roll" : "Downforce +4% / Drag +1% per level";
            description.text = branch.name.ToUpperInvariant() + " · " + (previewLevel == 0 ? "STOCK" : "LEVEL " + previewLevel) + "\n" + state + "\n" + effect;
            statistics.PreviewUpgrade(BranchId, previewLevel);
        }
        public static string Unit(string branch) => branch == "suspension" ? "N/m" : branch == "aero" ? "coef." : "Nm";
        public static float ValueAtLevel(string branch, int level, RCCP_CarController car, WheelCollider wheel)
        {
            float current, step;
            switch (branch)
            {
                case "engine": current = car.Engine.maximumTorqueAsNM; step = .03f; break;
                case "brakes": current = car.FrontAxle.maxBrakeTorque; step = .04f; break;
                case "suspension": current = wheel != null ? wheel.suspensionSpring.spring : 0; step = .015f; break;
                default: current = car.AeroDynamics.downForce; step = .04f; break;
            }
            return current / (1 + step * FormulaProgression.State.Equipped(branch)) * (1 + step * level);
        }
        public UnityEngine.UI.Selectable FirstControl => categories[category];
        public UnityEngine.UI.Selectable BackControl => back;
        public void WireNavigation(UnityEngine.UI.Selectable garageTab = null)
        {
            if (categories == null || categories.Length == 0 || levels == null || levels.Length == 0) return;
            if (garageTab != null) tabNavigation = garageTab;
            for (int i = 0; i < categories.Length; i++) categories[i].navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnLeft = categories[(i + categories.Length - 1) % categories.Length], selectOnRight = categories[(i + 1) % categories.Length], selectOnDown = levels[Mathf.Min(3, FormulaProgression.State.Owned(BranchId) + 1)].button, selectOnUp = tabNavigation != null ? tabNavigation : back };
            for (int i = 0; i < levels.Length; i++) levels[i].button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnUp = i == 0 ? categories[category] : levels[i - 1].button, selectOnDown = i == levels.Length - 1 ? back : levels[i + 1].button, selectOnRight = categories[category] };
            back.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnUp = levels[levels.Length - 1].button, selectOnLeft = levels[levels.Length - 1].button, selectOnDown = categories[category] };
        }
    }
}
