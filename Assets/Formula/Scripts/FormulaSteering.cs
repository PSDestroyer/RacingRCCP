using UnityEngine;

namespace FormulaGame
{
    // RCCP owns input, assists and wheel physics. This only controls how quickly
    // its steering rack reaches the requested angle, relative to the available lock.
    [DefaultExecutionOrder(-5)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RCCP_CarController))]
    public sealed class FormulaSteering : MonoBehaviour
    {
        private RCCP_CarController car;
        private RCCP_Axle axle;
        private float authoredSpeed;
        private float appliedSpeed;
        private bool ownsSpeed;

        private void Awake() => car = GetComponent<RCCP_CarController>();

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

            float target = car.steerInput_P * axle.maxSteerAngle;
            bool reversing = target * axle.steerAngle < 0f;
            bool returning = Mathf.Abs(target) < Mathf.Abs(axle.steerAngle);
            float secondsToLock = reversing ? .16f : returning ? .12f : .24f;
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
