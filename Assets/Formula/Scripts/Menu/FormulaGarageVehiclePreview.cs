using System;
using System.Linq;
using UnityEngine;
namespace FormulaGame
{
    public sealed class FormulaGarageVehiclePreview : MonoBehaviour
    {
        [SerializeField] private Transform vehicleAnchor;
        [SerializeField] private GameObject editorVehicle;
        [SerializeField] private Camera presentationCamera;
        private Vector3 shotPosition;
        private Quaternion shotRotation;
        private bool moving;
        private FormulaVehicle vehicle;
        public FormulaVehicle Vehicle => vehicle;
        public RCCP_CarController Car { get; private set; }
        public WheelCollider Wheel { get; private set; }
        public void Initialize()
        {
            if (vehicle != null) return;
            if (editorVehicle != null) editorVehicle.SetActive(false);
            var root = Instantiate(FormulaAssets.Current.vehiclePrefab, vehicleAnchor.position, vehicleAnchor.rotation, transform);
            root.name = "Generic Vehicle";
            var hits = Physics.RaycastAll(vehicleAnchor.position + Vector3.up * 5, Vector3.down, 30, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                .Where(h => !h.collider.transform.IsChildOf(root.transform) && h.normal.y > .8f).OrderBy(h => h.distance).ToArray();
            if (hits.Length > 0)
            {
                float bottom = root.GetComponentsInChildren<MeshRenderer>().Min(r => r.bounds.min.y);
                root.transform.position += Vector3.up * (hits[0].point.y - bottom + .02f);
            }
            vehicle = root.GetComponent<FormulaVehicle>(); Car = root.GetComponent<RCCP_CarController>(); Wheel = root.GetComponentInChildren<WheelCollider>();
            Car.canControl = false; Car.Engine.engineRunning = false; Car.Rigid.isKinematic = true;
            // RCCP's visual alignment hides wheels when their physics collider is off.
            // A parked display preserves authored wheel poses instead of running that alignment.
            foreach (var wheel in root.GetComponentsInChildren<RCCP_WheelCollider>())
            {
                wheel.alignWheels = false;
                if (wheel.wheelModel != null) wheel.wheelModel.gameObject.SetActive(true);
            }
            foreach (var wheel in root.GetComponentsInChildren<WheelCollider>()) wheel.enabled = false;
            if (Car.Audio != null) Car.Audio.enabled = false;
        }
        public void ShowLivery(string id) { if (vehicle != null) vehicle.PreviewLivery(id); }
        public void Present(string page, string tab, bool instant = false)
        {
            if (presentationCamera == null) return;
            bool upgrade = page == "Garaj" && tab == "Dezvoltare";
            Vector3 offset = page != "Garaj" ? new Vector3(3.3f, 1.7f, 8.2f) : tab == "Setup" ? new Vector3(3.8f, 2f, 8.6f) : tab == "Livree" ? new Vector3(3.8f, 1.8f, 8.3f) : new Vector3(.65f, 1.1f, 6.7f);
            shotPosition = vehicleAnchor.position + offset;
            Vector3 right = Vector3.Cross(Vector3.up, vehicleAnchor.position - shotPosition).normalized;
            float aimHeight = upgrade ? -.18f : page == "Garaj" ? tab == "Livree" ? .2f : .12f : .42f;
            shotRotation = Quaternion.LookRotation(vehicleAnchor.position + Vector3.up * aimHeight - right * (upgrade ? .72f : 1.9f) - shotPosition);
            presentationCamera.fieldOfView = 34;
            if (instant || FormulaProgression.State.reducedMotion) { presentationCamera.transform.SetPositionAndRotation(shotPosition, shotRotation); moving = false; }
            else moving = true;
        }
        private void LateUpdate()
        {
            if (!moving || presentationCamera == null) return;
            float blend = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 10);
            var cameraTransform = presentationCamera.transform;
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, shotPosition, blend);
            cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, shotRotation, blend);
            if ((cameraTransform.position - shotPosition).sqrMagnitude < .00001f && Quaternion.Angle(cameraTransform.rotation, shotRotation) < .01f)
            { cameraTransform.SetPositionAndRotation(shotPosition, shotRotation); moving = false; }
        }
    }
}
