using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace FormulaGame
{
    // Last meaningful input, rather than a plugged-in controller, owns the prompts.
    public static class FormulaInputHints
    {
        private static int frame = -1;
        public static bool Gamepad { get; private set; }
        public static int Revision { get; private set; }
        public static void Poll()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
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
            Gamepad = pad; Revision++;
        }
        public static void ConfigureUI(UnityEngine.InputSystem.UI.InputSystemUIInputModule module)
        {
#if UNITY_SWITCH
            if (module == null) return;
            SwapFaceBinding(module.submit?.action, "<Gamepad>/buttonSouth", "<Gamepad>/buttonEast");
            SwapFaceBinding(module.cancel?.action, "<Gamepad>/buttonEast", "<Gamepad>/buttonSouth");
#endif
        }
#if UNITY_SWITCH
        private static void SwapFaceBinding(InputAction action, string original, string replacement)
        {
            if (action == null) return;
            for (int i = 0; i < action.bindings.Count; i++) if (action.bindings[i].path == original) action.ApplyBindingOverride(i, replacement);
        }
#endif
        public static string Confirm => Gamepad ? "A · Select" : "Enter · Select";
        public static string Back => Gamepad ? "B · Back" : "Esc · Back";
        public static string Sections => Gamepad ?
#if UNITY_SWITCH
            "L/R · Tabs"
#else
            "LB/RB · Tabs"
#endif
            : "Q/E · Tabs";
        public static string Menu => Confirm;
    }
}
