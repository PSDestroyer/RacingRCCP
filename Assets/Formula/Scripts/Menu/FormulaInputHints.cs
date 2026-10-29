using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace FormulaGame
{
    // Last meaningful input, rather than a plugged-in controller, owns the prompts.
    public static class FormulaInputHints
    {
        private static int frame = -1;
        private static bool lastGamepad;
        public static bool Gamepad => FormulaPlatform.IsNintendoSwitch || lastGamepad;
        public static int Revision { get; private set; }
        public static void Poll()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            if (FormulaPlatform.IsNintendoSwitch)
            {
                if (!lastGamepad) { lastGamepad = true; Revision++; }
                return;
            }
            var device = UnityEngine.InputSystem.Gamepad.current;
            bool pad = Gamepad && device != null;
            var keyboard = Keyboard.current; var mouse = Mouse.current;
            if (keyboard?.anyKey.wasPressedThisFrame == true || mouse?.leftButton.wasPressedThisFrame == true || mouse?.rightButton.wasPressedThisFrame == true || (mouse != null && mouse.delta.ReadValue().sqrMagnitude > 4)) pad = false;
            if (device != null)
            {
                foreach (var control in device.allControls)
                    if (control is ButtonControl button && button.wasPressedThisFrame) { pad = true; break; }
                if (device.leftStick.ReadValue().sqrMagnitude > .36f || device.rightStick.ReadValue().sqrMagnitude > .36f) pad = true;
            }
            if (pad == Gamepad) return;
            lastGamepad = pad; Revision++;
        }
        public static void ConfigureUI(UnityEngine.InputSystem.UI.InputSystemUIInputModule module)
        {
            if (module == null || !FormulaPlatform.IsNintendoSwitch) return;
            SwapFaceBinding(module.submit?.action, "<Gamepad>/buttonSouth", "*/{Submit}", "<Gamepad>/buttonEast");
            SwapFaceBinding(module.cancel?.action, "<Gamepad>/buttonEast", "*/{Cancel}", "<Gamepad>/buttonSouth");
        }
        private static void SwapFaceBinding(InputAction action, string original, string usage, string replacement)
        {
            if (action == null) return;
            for (int i = 0; i < action.bindings.Count; i++)
                if (action.bindings[i].path == original || action.bindings[i].path == usage) action.ApplyBindingOverride(i, replacement);
        }
        public static string Confirm => Gamepad ? FormulaLocalization.T("A · Select") : FormulaLocalization.T("Enter · Select");
        public static string Back => Gamepad ? FormulaLocalization.T("B · Back") : FormulaLocalization.T("Esc · Back");
        public static string Sections => FormulaPlatform.IsNintendoSwitch ? FormulaLocalization.T("L/R · Tabs")
            : Gamepad ? FormulaLocalization.T("LB/RB · Tabs") : FormulaLocalization.T("Q/E · Tabs");
        public static string CancelRebind => FormulaPlatform.IsNintendoSwitch ? FormulaLocalization.T("\nB · Cancel") : FormulaLocalization.T("\nEsc · Cancel");
        public static string Menu => Confirm;
    }
}
