using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FormulaGame
{
    [DisallowMultipleComponent]
    public sealed class FormulaVehicle : MonoBehaviour
    {
        public Renderer body;
        private RCCP_CarController car;
        private float torque, frontBrake, rearBrake, downforce, drag, frontAntiroll, rearAntiroll;
        private readonly List<WheelCollider> wheels = new List<WheelCollider>();
        private readonly List<JointSpring> springs = new List<JointSpring>();
        private bool ready;
        public bool IsReady => ready;
        private IEnumerator Start()
        {
            yield return null; // RCCP resolves axles and creates visual pivots in Start.
            car = GetComponent<RCCP_CarController>();
            car.GetAllComponents();
            torque = car.Engine.maximumTorqueAsNM;
            frontBrake = car.FrontAxle.maxBrakeTorque; rearBrake = car.RearAxle.maxBrakeTorque;
            frontAntiroll = car.FrontAxle.antirollForce; rearAntiroll = car.RearAxle.antirollForce;
            downforce = car.AeroDynamics.downForce; drag = car.AeroDynamics.airResistance;
            foreach (var w in GetComponentsInChildren<WheelCollider>()) { wheels.Add(w); springs.Add(w.suspensionSpring); }
            ready = true;
            Apply();
        }
        private void OnEnable() => FormulaProgression.Changed += Apply;
        private void OnDisable() => FormulaProgression.Changed -= Apply;
        public void Apply()
        {
            if (!ready) return;
            var state = FormulaProgression.State;
            bool ai = GetComponent<RCCP_AI>() != null;
            bool stock = FormulaSession.StockVehicle;
            int fixedLevel = ai ? FormulaSession.OpponentLevel : 0;
            int engine = ai ? fixedLevel : stock ? 0 : state.Equipped("engine");
            int brakes = ai ? fixedLevel : stock ? 0 : state.Equipped("brakes");
            int suspension = ai ? fixedLevel : stock ? 0 : state.Equipped("suspension");
            int aero = ai ? fixedLevel : stock ? 0 : state.Equipped("aero");
            string setup = ai || stock ? "balanced" : state.setup;
            ApplyDevelopment(engine, brakes, suspension, aero, setup);
            car.Stability.ABS = ai || stock || state.abs;
            car.Stability.TCS = ai || stock || state.tcs;
            car.Stability.ESP = ai || stock || state.esp;
            if (!ai) PreviewLivery(state.equippedLivery);
        }
        // Always derive from the original RCCP values, including when equipping a lower level.
        public void ApplyDevelopment(int engine, int brakes, int suspension, int aero, string setup)
        {
            if (!ready) return;
            engine = Mathf.Clamp(engine, 0, 3); brakes = Mathf.Clamp(brakes, 0, 3);
            suspension = Mathf.Clamp(suspension, 0, 3); aero = Mathf.Clamp(aero, 0, 3);
            car.Engine.maximumTorqueAsNM = torque * (1f + 0.03f * engine);
            car.FrontAxle.maxBrakeTorque = frontBrake * (1f + 0.04f * brakes);
            car.RearAxle.maxBrakeTorque = rearBrake * (1f + 0.04f * brakes);
            float aeroSetup = setup == "speed" ? .92f : setup == "corners" ? 1.08f : 1f;
            float dragSetup = setup == "speed" ? .96f : setup == "corners" ? 1.04f : 1f;
            car.AeroDynamics.downForce = downforce * (1f + .04f * aero) * aeroSetup;
            car.AeroDynamics.airResistance = drag * (1f + .01f * aero) * dragSetup;
            car.FrontAxle.antirollForce = frontAntiroll * (1f + .025f * suspension);
            car.RearAxle.antirollForce = rearAntiroll * (1f + .025f * suspension);
            for (int i = 0; i < wheels.Count; i++)
            {
                var s = springs[i]; s.spring *= 1f + .015f * suspension; s.damper *= 1f + .035f * suspension;
                wheels[i].suspensionSpring = s;
            }
        }
        public void PreviewLivery(string id)
        {
            var skins = FormulaCatalog.Current.liveries;
            int index = skins.FindIndex(l => l.id == id);
            var assets = FormulaAssets.Current;
            if (body != null && assets != null && index >= 0 && index < assets.liveryMaterials.Length)
                body.sharedMaterial = assets.liveryMaterials[index];
        }
    }
}
