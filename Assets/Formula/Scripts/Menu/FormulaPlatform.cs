using UnityEngine.InputSystem;

namespace FormulaGame
{
    // Presentation policy, independent of the last connected input device.
    // Switch builds always advertise Nintendo controls and use the authored
    // graphics profile; desktop builds retain their existing preferences.
    public static class FormulaPlatform
    {
        public static bool IsNintendoSwitch
        {
            get
            {
#if UNITY_SWITCH
                return true;
#elif UNITY_EDITOR
                return EditorSwitchPreview;
#else
                return false;
#endif
            }
        }
#if UNITY_EDITOR
        public static bool EditorSwitchPreview { get; set; }
#endif
        public static bool AllowsVideoPreferences => !IsNintendoSwitch;
        public const int SwitchFrameLimit = 60;
        public const string SwitchControllerName = "Joy-Con / Pro Controller";

        public static string BindingName(InputAction action, int index) => IsNintendoSwitch
            ? SwitchBindingName(action.bindings[index].effectivePath)
            : action.GetBindingDisplayString(index, InputBinding.DisplayStringOptions.DontIncludeInteractions);

        // Use physical Nintendo button positions even before an NPad is attached.
        // Generic Gamepad display strings otherwise default to Xbox labels.
        public static string SwitchBindingName(string path)
        {
            if (string.IsNullOrEmpty(path)) return "—";
            int slash = path.IndexOf('/');
            string control = (slash < 0 ? path : path.Substring(slash + 1)).ToLowerInvariant();
            switch (control)
            {
                case "buttonsouth": return "B";
                case "{cancel}": return "B";
                case "buttoneast": return "A";
                case "{submit}": return "A";
                case "buttonwest": return "Y";
                case "buttonnorth": return "X";
                case "leftshoulder": return "L";
                case "rightshoulder": return "R";
                case "lefttrigger": return "ZL";
                case "righttrigger": return "ZR";
                case "start": return "+";
                case "select": return "−";
                case "leftstick": return FormulaLocalization.T("Left stick");
                case "rightstick": return FormulaLocalization.T("Right stick");
                case "leftstickpress": return FormulaLocalization.T("Press left stick");
                case "rightstickpress": return FormulaLocalization.T("Press right stick");
                case "dpad": return FormulaLocalization.T("Directional buttons");
                case "dpad/up": return "↑";
                case "dpad/down": return "↓";
                case "dpad/left": return "←";
                case "dpad/right": return "→";
                case "leftstick/x": return FormulaLocalization.T("Left stick");
                case "leftstick/y": return FormulaLocalization.T("Left stick");
                case "leftstick/left": return FormulaLocalization.T("Left stick") + " ←";
                case "leftstick/right": return FormulaLocalization.T("Left stick") + " →";
                case "leftstick/up": return FormulaLocalization.T("Left stick") + " ↑";
                case "leftstick/down": return FormulaLocalization.T("Left stick") + " ↓";
                case "rightstick/x": return FormulaLocalization.T("Right stick");
                case "rightstick/y": return FormulaLocalization.T("Right stick");
                case "rightstick/left": return FormulaLocalization.T("Right stick") + " ←";
                case "rightstick/right": return FormulaLocalization.T("Right stick") + " →";
                case "rightstick/up": return FormulaLocalization.T("Right stick") + " ↑";
                case "rightstick/down": return FormulaLocalization.T("Right stick") + " ↓";
                default: return "—";
            }
        }
    }
}
