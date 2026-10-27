using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace FormulaGame
{
    // Edits the existing RCCP action asset and existing save field, never a parallel input map.
    public sealed class FormulaControlsView : MonoBehaviour
    {
        [Serializable] public sealed class Row { public UnityEngine.UI.Button button; public TMP_Text action, binding; }
        private struct BindingEntry { public InputAction action; public int index; }
        [SerializeField] private UnityEngine.UI.Button keyboard, gamepad, wheel, previous, next, reset, cancelRebind;
        [SerializeField] private Row[] rows;
        [SerializeField] private TMP_Text pageLabel, prompt;
        [SerializeField] private GameObject waiting;
        [SerializeField] private CanvasGroup listInteraction;
        private readonly List<BindingEntry> entries = new List<BindingEntry>();
        private InputActionAsset actions;
        private InputActionRebindingExtensions.RebindingOperation operation;
        private InputAction reboundAction;
        private bool wasEnabled;
        private string conflictMessage;
        private TMP_Text cancelLabel;
        private string device = "<Keyboard>";
        private int pageIndex;
        private static readonly string[] DrivingActions = { "Throttle", "Brake", "Steering", "Gear Shift Up", "Gear Shift Down", "Clutch", "Change Camera", "Look Back", "Options", "Pause" };
        private GameObject returnFocus;
        public bool IsRebinding => operation != null || conflictMessage != null;
        public void Initialize()
        {
            cancelLabel = cancelRebind.GetComponentInChildren<TMP_Text>(true);
            keyboard.onClick.AddListener(() => SelectDevice("<Keyboard>")); gamepad.onClick.AddListener(() => SelectDevice("<Gamepad>")); wheel.onClick.AddListener(() => SelectDevice("Wheel"));
            previous.onClick.AddListener(() => { pageIndex--; RefreshRows(); }); next.onClick.AddListener(() => { pageIndex++; RefreshRows(); });
            reset.onClick.AddListener(ResetDevice); cancelRebind.onClick.AddListener(CancelRebind);
            for (int i = 0; i < rows.Length; i++) { int row = i; rows[i].button.onClick.AddListener(() => Rebind(row)); }
        }
        private void SelectDevice(string path) { CancelRebind(); device = path; pageIndex = 0; Refresh(); }
        public void Refresh()
        {
            actions = RCCP_InputActions.Instance.inputActions; entries.Clear();
            foreach (string actionName in DrivingActions) foreach (var map in actions.actionMaps) foreach (var action in map.actions)
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    if (action.name != actionName) continue;
                    var b = action.bindings[i]; if (b.isComposite || string.IsNullOrEmpty(b.path)) continue;
                    bool matches = device == "Wheel" ? (b.groups ?? "").Contains("G920") : b.path.StartsWith(device, StringComparison.OrdinalIgnoreCase);
                    if (matches && !(device == "<Keyboard>" && IsSecondaryComposite(action, i))) entries.Add(new BindingEntry { action = action, index = i });
                }
            RefreshRows();
        }
        private void RefreshRows()
        {
            int pages = Mathf.Max(1, Mathf.CeilToInt(entries.Count / (float)rows.Length)); pageIndex = Mathf.Clamp(pageIndex, 0, pages - 1);
            previous.interactable = pageIndex > 0; next.interactable = pageIndex < pages - 1;
            pageLabel.text = FormulaLocalization.F("{0} / PAGE {1} OF {2}", device == "<Keyboard>" ? FormulaLocalization.T("KEYBOARD") : device == "<Gamepad>" ? FormulaLocalization.T("GAMEPAD") : FormulaLocalization.T("STEERING WHEEL"), pageIndex + 1, pages);
            for (int i = 0; i < rows.Length; i++)
            {
                int index = pageIndex * rows.Length + i; var row = rows[i]; row.button.gameObject.SetActive(index < entries.Count);
                if (index >= entries.Count) continue;
                var entry = entries[index]; var b = entry.action.bindings[entry.index];
                row.action.text = ActionName(entry.action.name) + BindingPart(entry.action, entry.index);
                row.binding.text = entry.action.GetBindingDisplayString(entry.index, InputBinding.DisplayStringOptions.DontIncludeInteractions);
            }
        }
        private static bool IsSecondaryComposite(InputAction action, int index)
        {
            if (!action.bindings[index].isPartOfComposite) return false;
            for (int i = index - 1; i >= 0; i--) if (action.bindings[i].isComposite) return action.bindings[i].name == "arrows";
            return false;
        }
        private static string BindingPart(InputAction action, int index)
        {
            var binding = action.bindings[index];
            if (!binding.isPartOfComposite) return "";
            if (action.name == "Steering") return binding.name.Equals("negative", StringComparison.OrdinalIgnoreCase) ? FormulaLocalization.T(" / Left") : FormulaLocalization.T(" / Right");
            for (int i = index - 1; i >= 0; i--)
                if (action.bindings[i].isComposite)
                {
                    string name = action.bindings[i].name;
                    return name == "wasd" ? FormulaLocalization.T(" / WASD") : name == "arrows" ? FormulaLocalization.T(" / Arrow keys") : "";
                }
            return "";
        }
        private static string ActionName(string name)
        {
            switch (name)
            {
                case "Throttle": return FormulaLocalization.T("Throttle");
                case "Brake": return FormulaLocalization.T("Brake");
                case "Steering": return FormulaLocalization.T("Steering");
                case "Handbrake": return FormulaLocalization.T("Handbrake");
                case "Start/Stop Engine": return FormulaLocalization.T("Start / stop engine");
                case "Low Beam Lights": return FormulaLocalization.T("Low beam");
                case "High Beam Lights": return FormulaLocalization.T("High beam");
                case "Indicator Right": return FormulaLocalization.T("Right indicator");
                case "Indicator Left": return FormulaLocalization.T("Left indicator");
                case "Indicator Hazard": return FormulaLocalization.T("Hazard lights");
                case "Gear Shift Up": return FormulaLocalization.T("Shift up");
                case "Gear Shift Down": return FormulaLocalization.T("Shift down");
                case "Change Camera": return FormulaLocalization.T("Change camera");
                case "Look Back": return FormulaLocalization.T("Look back");
                case "MouseInput": return FormulaLocalization.T("Camera movement");
                case "Clutch": return FormulaLocalization.T("Clutch");
                case "Pause": case "Options": return FormulaLocalization.T("Pause / menu");
                case "Record": return FormulaLocalization.T("Record");
                case "Replay": return FormulaLocalization.T("Replay");
                default: return name;
            }
        }
        private void Rebind(int row)
        {
            if (IsRebinding) return;
            var entry = entries[pageIndex * rows.Length + row]; reboundAction = entry.action; wasEnabled = reboundAction.enabled;
            returnFocus = rows[row].button.gameObject; reboundAction.Disable();
            waiting.SetActive(true); listInteraction.interactable = false; listInteraction.blocksRaycasts = false;
            prompt.text = FormulaLocalization.T("Press a new input for\n") + rows[row].action.text + FormulaLocalization.T("\nEsc · Cancel");
            operation = reboundAction.PerformInteractiveRebinding(entry.index).WithCancelingThrough("<Keyboard>/escape").WithTimeout(15)
                .OnApplyBinding((candidate, path) =>
                {
                    string conflict = BindingConflict(actions, entry.action, entry.index, path, candidate.selectedControl);
                    if (conflict == null) entry.action.ApplyBindingOverride(entry.index, path);
                    else conflictMessage = FormulaLocalization.T("INPUT ALREADY IN USE\n") + FormulaLocalization.F("{0} is already assigned to {1}.\nYour previous binding is unchanged.", InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice), conflict);
                })
                .OnCancel(_ => Finish(false)).OnComplete(_ => Finish(true));
            if (device != "Wheel") operation.WithControlsHavingToMatchPath(device);
            else operation.WithControlsExcluding("<Mouse>").WithControlsExcluding("<Keyboard>").WithControlsExcluding("<Gamepad>");
            EventSystem.current?.SetSelectedGameObject(cancelRebind.gameObject); operation.Start();
        }
        private void Finish(bool save)
        {
            var completed = operation; operation = null; completed?.Dispose();
            if (wasEnabled) reboundAction?.Enable(); reboundAction = null;
            if (conflictMessage != null)
            {
                prompt.text = conflictMessage; cancelLabel.text = FormulaLocalization.T("Back");
                EventSystem.current?.SetSelectedGameObject(cancelRebind.gameObject); return;
            }
            if (save) RCCP_RebindSaveLoad.Save();
            CloseRebind();
        }
        // Compare effective bindings, including hidden alternate keys. A rejected candidate
        // never reaches ApplyBindingOverride or the RCCP save owner.
        internal static string BindingConflict(InputActionAsset asset, InputAction target, int index, string path, InputControl control = null)
        {
            foreach (var map in asset.actionMaps) foreach (var action in map.actions)
            {
                if (Array.IndexOf(DrivingActions, action.name) < 0) continue;
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    if (action == target && i == index) continue;
                    var binding = action.bindings[i];
                    if (binding.isComposite || string.IsNullOrEmpty(binding.effectivePath)) continue;
                    if (string.Equals(binding.effectivePath, path, StringComparison.OrdinalIgnoreCase)
                        || control != null && InputControlPath.Matches(binding.effectivePath, control))
                        return ActionName(action.name) + BindingPart(action, i);
                }
            }
            return null;
        }
        private void CloseRebind()
        {
            conflictMessage = null; waiting.SetActive(false); listInteraction.interactable = true; listInteraction.blocksRaycasts = true;
            if (cancelLabel != null) cancelLabel.text = FormulaLocalization.T("Cancel");
            RefreshRows();
            if (isActiveAndEnabled && returnFocus != null) EventSystem.current?.SetSelectedGameObject(returnFocus);
        }
        public void CancelRebind() { if (operation != null) operation.Cancel(); else if (conflictMessage != null) CloseRebind(); }
        private void ResetDevice()
        {
            foreach (var map in actions.actionMaps) foreach (var action in map.actions)
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    bool matches = device == "Wheel" ? (binding.groups ?? "").Contains("G920") : (binding.path ?? "").StartsWith(device, StringComparison.OrdinalIgnoreCase);
                    if (!binding.isComposite && matches) action.RemoveBindingOverride(i);
                }
            RCCP_RebindSaveLoad.Save(); RefreshRows();
        }
        private void OnDisable() => CancelRebind();
    }
}
