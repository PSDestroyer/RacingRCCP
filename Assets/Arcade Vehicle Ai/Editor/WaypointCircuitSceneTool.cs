using UnityEditor;
using UnityEngine;

namespace ArcadeVP {

    [CustomEditor(typeof(WaypointCircuit))]
    public class WaypointCircuitSceneTool : Editor {

        private const float DefaultHandleSize = .6f;

        public override void OnInspectorGUI() {

            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Scene tool: Hold Shift and Left Click in Scene view to place a waypoint under the mouse. The waypoint list is rebuilt automatically from the circuit children.", MessageType.Info);

            WaypointCircuit circuit = (WaypointCircuit)target;

            if (GUILayout.Button("Rebuild From Children")) {
                RebuildFromChildren(circuit);
            }

        }

        private void OnSceneGUI() {

            WaypointCircuit circuit = (WaypointCircuit)target;
            Event currentEvent = Event.current;

            if (circuit == null || currentEvent == null)
                return;

            DrawWaypointHandles(circuit);

            if (currentEvent.shift)
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            if (currentEvent.type != EventType.MouseDown || currentEvent.button != 0 || !currentEvent.shift || currentEvent.alt)
                return;

            Ray ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
            Vector3 spawnPosition = GetSpawnPosition(circuit, ray);
            CreateWaypoint(circuit, spawnPosition);
            currentEvent.Use();

        }

        private static Vector3 GetSpawnPosition(WaypointCircuit circuit, Ray ray) {

            if (Physics.Raycast(ray, out RaycastHit hit, 5000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return hit.point;

            Plane fallbackPlane = new Plane(Vector3.up, circuit.transform.position);

            if (fallbackPlane.Raycast(ray, out float enter))
                return ray.GetPoint(enter);

            return circuit.transform.position;

        }

        private static void CreateWaypoint(WaypointCircuit circuit, Vector3 position) {

            int waypointIndex = circuit.transform.childCount;
            GameObject waypointObject = new GameObject($"Waypoint {waypointIndex:000}");
            Undo.RegisterCreatedObjectUndo(waypointObject, "Create Waypoint");
            waypointObject.transform.SetParent(circuit.transform, true);
            waypointObject.transform.position = position;
            waypointObject.transform.rotation = Quaternion.identity;

            RebuildFromChildren(circuit);
            Selection.activeGameObject = circuit.gameObject;
            EditorUtility.SetDirty(circuit);

        }

        private static void RebuildFromChildren(WaypointCircuit circuit) {

            int childCount = circuit.transform.childCount;
            Transform[] items = new Transform[childCount];

            for (int i = 0; i < childCount; i++)
                items[i] = circuit.transform.GetChild(i);

            Undo.RecordObject(circuit, "Rebuild Waypoint Circuit");
            circuit.waypointList.items = items;
            circuit.RebuildRoute();
            EditorUtility.SetDirty(circuit);

        }

        private static void DrawWaypointHandles(WaypointCircuit circuit) {

            Handles.color = new Color(1f, .45f, .1f, .9f);

            for (int i = 0; i < circuit.transform.childCount; i++) {
                Transform waypoint = circuit.transform.GetChild(i);
                float size = HandleUtility.GetHandleSize(waypoint.position) * DefaultHandleSize;
                Handles.SphereHandleCap(0, waypoint.position, Quaternion.identity, size, EventType.Repaint);
                Handles.Label(waypoint.position + Vector3.up * size, waypoint.name);
            }

        }

    }

}
