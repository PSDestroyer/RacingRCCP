using UnityEngine;
using UnityEngine.InputSystem;

namespace FormulaGame
{
    // RCCP owns assists, available steering lock and wheel physics. Shape the
    // local gamepad near center, then tune rack travel relative to available lock.
    // RCCP's device reader runs at -50; shape its single local-player value
    // before RCCP_Input applies deadzones, assists and the speed/lock curve.
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RCCP_CarController))]
    public sealed class FormulaSteering : MonoBehaviour
    {
        private RCCP_CarController car;
        private RCCP_Axle axle;
        private float authoredSpeed;
        private float appliedSpeed;
        private bool ownsSpeed;
        private InputActionAsset actionAsset;
        private InputAction steeringAction;
        private bool gamepad;
        private float filteredStick;
        public bool GamepadResponseActive => gamepad;

        private void Awake() => car = GetComponent<RCCP_CarController>();

        public static float ShapeStick(float raw, float deadzone, float speed)
        {
            deadzone = Mathf.Clamp(deadzone, 0, .95f);
            float magnitude = Mathf.Abs(Mathf.Clamp(raw, -1, 1));
            if (magnitude <= deadzone) return 0;
            float normalized = (magnitude - deadzone) / (1 - deadzone);
            float lowSpeed = 1 - Mathf.InverseLerp(35, 100, Mathf.Abs(speed));
            float curved = Mathf.Lerp(normalized, Mathf.Pow(normalized, 1.65f), lowSpeed);
            // Encode back into raw units: RCCP removes its deadzone exactly once.
            return Mathf.Sign(raw) * (deadzone + curved * (1 - deadzone));
        }

        private void Update()
        {
            if (car == null || car.Inputs == null || !car.canControl || car.externalControl ||
                car.Inputs.overrideExternalInputs || car.Inputs.overridePlayerInputs)
            { gamepad = false; filteredStick = 0; return; }
            var manager = car.Inputs.RCCPInputManager;
            if (manager == null || manager.overrideInputs || manager.inputs == null)
            { gamepad = false; filteredStick = 0; return; }
            if (actionAsset != manager.inputActionsInstance)
            {
                actionAsset = manager.inputActionsInstance;
                steeringAction = actionAsset == null ? null : actionAsset.FindAction("Vehicle/Steering", false);
                gamepad = false; filteredStick = 0;
            }
            if (steeringAction == null || !steeringAction.enabled) { gamepad = false; filteredStick = 0; return; }
            if (steeringAction.activeControl != null) gamepad = steeringAction.activeControl.device is Gamepad;
            if (!gamepad) { filteredStick = 0; return; }
            float target = ShapeStick(manager.inputs.steerInput, car.Inputs.steeringDeadzone, car.absoluteSpeed);
            float rate = Mathf.Lerp(8, 1000, Mathf.InverseLerp(35, 100, car.absoluteSpeed));
            filteredStick = Mathf.MoveTowards(filteredStick, target, Time.deltaTime * rate);
            manager.inputs.steerInput = filteredStick;
        }

        private void FixedUpdate()
        {
            if (axle == null)
            {
                axle = car.FrontAxle;
                if (axle == null) return;
                authoredSpeed = axle.steerSpeed;
            }

            // AI and replay keep their own calibration, including post-finish AI.
            if (!car.canControl || car.externalControl || car.Inputs.overrideExternalInputs)
            {
                RestoreSpeed();
                return;
            }

            float target = car.Inputs.steerInput * axle.maxSteerAngle;
            bool reversing = target * axle.steerAngle < 0f;
            bool returning = Mathf.Abs(target) < Mathf.Abs(axle.steerAngle);
            float secondsToLock = reversing ? .16f : returning ? .12f : .24f;
            if (gamepad)
            {
                float lowSpeed = 1 - Mathf.InverseLerp(35, 100, car.absoluteSpeed);
                secondsToLock = Mathf.Lerp(secondsToLock, reversing ? .24f : returning ? .20f : .30f, lowSpeed);
            }
            float lockFraction = car.Inputs.steeringCurve == null ? 1f :
                Mathf.Clamp(car.Inputs.steeringCurve.Evaluate(car.absoluteSpeed), .01f, 1f);
            // RCCP_Axle uses steerSpeed * 150 degrees/second in FixedUpdate.
            // Scaling by available lock avoids a single-tick snap at racing speeds.
            axle.steerSpeed = axle.maxSteerAngle * lockFraction / (150f * secondsToLock);
            appliedSpeed = axle.steerSpeed;
            ownsSpeed = true;
        }

        private void RestoreSpeed()
        {
            // RCCP_AI explicitly sets its own rack speed during takeover. Restore
            // only our value, never a value subsequently written by another owner.
            if (axle != null && ownsSpeed && Mathf.Approximately(axle.steerSpeed, appliedSpeed))
                axle.steerSpeed = authoredSpeed;
            ownsSpeed = false;
        }

        private void OnDisable() => RestoreSpeed();
    }
}
