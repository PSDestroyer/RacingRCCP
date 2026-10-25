using UnityEngine;

namespace FormulaGame
{
    // Runs before RCCP_Engine consumes and resets its per-step torque multiplier.
    // The normal RCCP TCS remains responsible for straight-line wheelspin.
    [DefaultExecutionOrder(-5)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RCCP_CarController))]
    public sealed class FormulaTractionControl : MonoBehaviour
    {
        private RCCP_CarController car;
        private float rearSlipScale = 1f;
        private float bodySlipScale = 1f;
        public float TorqueScale { get; private set; } = 1f;

        private void Awake() => car = GetComponent<RCCP_CarController>();

        private void FixedUpdate()
        {
            var engine = car.Engine;
            var stability = car.Stability;
            var rear = car.RearAxle;
            if (engine == null || !engine.isActiveAndEnabled || !engine.engineRunning ||
                stability == null || !stability.isActiveAndEnabled || !stability.TCS ||
                rear == null || !car.canControl || car.externalControl || car.Inputs.overrideExternalInputs ||
                car.Gearbox == null || car.Gearbox.currentGearState.gearState != RCCP_Gearbox.CurrentGearState.GearState.InForwardGear ||
                car.throttleInput_P <= .05f || car.handbrakeInput_P > .1f)
            {
                ResetControl();
                return;
            }

            Vector3 velocity = transform.InverseTransformDirection(car.Rigid.linearVelocity);
            if (velocity.z < 20f / 3.6f)
            {
                ResetControl();
                return;
            }

            // RCCP's slip properties advance their filters on every read. Sample
            // the physics contacts directly so this assist has one fixed-step clock.
            bool leftGrounded = rear.leftWheelCollider.WheelCollider.GetGroundHit(out WheelHit left);
            bool rightGrounded = rear.rightWheelCollider.WheelCollider.GetGroundHit(out WheelHit right);
            if (!leftGrounded && !rightGrounded)
            {
                ResetControl();
                return;
            }

            float bodySlip = Mathf.Abs(Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg);
            bool cornering = Mathf.Abs(car.FrontAxle.steerAngle) > .35f || bodySlip > 2f;
            float dt = Time.fixedDeltaTime;
            if (cornering)
            {
                float slip = Mathf.Max(0f, Mathf.Max(leftGrounded ? left.forwardSlip : 0f,
                    rightGrounded ? right.forwardSlip : 0f));
                if (slip > .15f)
                    rearSlipScale -= 4f * dt * Mathf.Clamp01((slip - .15f) / .15f);
                else if (slip < .10f)
                    rearSlipScale += .4f * dt;
            }
            else
            {
                // Do not trap power in the slip deadband after the car straightens.
                rearSlipScale = Mathf.MoveTowards(rearSlipScale, 1f, 2f * dt);
            }
            rearSlipScale = Mathf.Clamp01(rearSlipScale);

            // Reserve rear tyre grip for turning before the car develops a spin.
            float targetBodyScale = Mathf.Clamp01((7f - bodySlip) / 4f);
            bodySlipScale = Mathf.MoveTowards(bodySlipScale, targetBodyScale,
                (targetBodyScale < bodySlipScale ? 10f : 2f) * dt);
            TorqueScale = Mathf.Min(rearSlipScale, bodySlipScale);
            engine.Multiply(TorqueScale);
        }

        private void ResetControl()
        {
            rearSlipScale = bodySlipScale = TorqueScale = 1f;
        }

        private void OnDisable() => ResetControl();
    }
}
