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
        private string device = "<Keyboard>";
        private int pageIndex;
        private static readonly string[] DrivingActions = { "Throttle", "Brake", "Steering", "Gear Shift Up", "Gear Shift Down", "Clutch", "Change Camera", "Look Back", "Options", "Pause" };
        private GameObject returnFocus;
        public bool IsRebinding => operation != null;
        public void Initialize()
        {
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
            pageLabel.text = (device == "<Keyboard>" ? "KEYBOARD" : device == "<Gamepad>" ? "GAMEPAD" : "STEERING WHEEL") + "  /  PAGE " + (pageIndex + 1) + " OF " + pages;
            for (int i = 0; i < rows.Length; i++)
            {
                int index = pageIndex * rows.Length + i; var row = rows[i]; row.button.gameObject.SetActive(index < entries.Count);
                if (index >= entries.Count) continue;
                var entry = entries[index]; var b = entry.action.bindings[entry.index];
                row.action.text = ActionName(entry.action.name) + BindingPart(entry.action, entry.index);
                row.binding.text = entry.action.GetBindingDisplayString(entry.index);
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
            if (action.name == "Steering") return binding.name.Equals("negative", StringComparison.OrdinalIgnoreCase) ? " / Left" : " / Right";
            for (int i = index - 1; i >= 0; i--)
                if (action.bindings[i].isComposite)
                {
                    string name = action.bindings[i].name;
                    return name == "wasd" ? " / WASD" : name == "arrows" ? " / Arrow keys" : "";
                }
            return "";
        }
        private static string ActionName(string name)
        {
            switch (name)
            {
                case "Throttle": return "Throttle";
                case "Brake": return "Brake";
                case "Steering": return "Steering";
                case "Handbrake": return "Handbrake";
                case "Start/Stop Engine": return "Start / stop engine";
                case "Low Beam Lights": return "Low beam";
                case "High Beam Lights": return "High beam";
                case "Indicator Right": return "Right indicator";
                case "Indicator Left": return "Left indicator";
                case "Indicator Hazard": return "Hazard lights";
                case "Gear Shift Up": return "Shift up";
                case "Gear Shift Down": return "Shift down";
                case "Change Camera": return "Change camera";
                case "Look Back": return "Look back";
                case "MouseInput": return "Camera movement";
                case "Clutch": return "Clutch";
                case "Pause": case "Options": return "Pause / menu";
                case "Record": return "Record";
                case "Replay": return "Replay";
                default: return name;
            }
        }
        private void Rebind(int row)
        {
            if (IsRebinding) return;
            var entry = entries[pageIndex * rows.Length + row]; reboundAction = entry.action; wasEnabled = reboundAction.enabled;
            returnFocus = rows[row].button.gameObject; reboundAction.Disable();
            waiting.SetActive(true); listInteraction.interactable = false; listInteraction.blocksRaycasts = false;
            prompt.text = "Press a new input for\n" + rows[row].action.text + "\nEsc · Cancel";
            operation = reboundAction.PerformInteractiveRebinding(entry.index).WithCancelingThrough("<Keyboard>/escape").WithTimeout(15)
                .OnCancel(_ => Finish(false)).OnComplete(_ => Finish(true));
            if (device != "Wheel") operation.WithControlsHavingToMatchPath(device);
            else operation.WithControlsExcluding("<Mouse>").WithControlsExcluding("<Keyboard>").WithControlsExcluding("<Gamepad>");
            EventSystem.current?.SetSelectedGameObject(cancelRebind.gameObject); operation.Start();
        }
        private void Finish(bool save)
        {
            var completed = operation; operation = null; completed?.Dispose();
            if (wasEnabled) reboundAction?.Enable(); reboundAction = null;
            waiting.SetActive(false); listInteraction.interactable = true; listInteraction.blocksRaycasts = true;
            if (save) RCCP_RebindSaveLoad.Save(); RefreshRows();
            if (isActiveAndEnabled && returnFocus != null) EventSystem.current?.SetSelectedGameObject(returnFocus);
        }
        public void CancelRebind() { if (operation != null) operation.Cancel(); }
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
