//----------------------------------------------
//        RCCP AI Setup Assistant
//
// Copyright 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace BoneCrackerGames.RCCP.AIAssistant {

public partial class RCCP_AIAssistantWindow {

    #region Wheel Setup Methods

    private void OpenFrontWheelSetup() {
        if (!HasRCCPController) {
            SetStatus("Error: No RCCP vehicle selected", MessageType.Error);
            return;
        }

        GameObject vehicle = selectedController.gameObject;
        GameObject[] frontWheels = RCCP_DetectPossibleWheels.DetectPossibleFrontWheels(vehicle);

        if (frontWheels == null || frontWheels.Length < 2) {
            EditorUtility.DisplayDialog("No Front Wheels Detected",
                "Could not detect front wheel models. Make sure your vehicle has separated wheel meshes.", "OK");
            return;
        }

        RCCP_PopupWindow_PossibleWheels.ShowWindow(frontWheels, selectedWheels => {
            if (selectedWheels != null && selectedWheels.Length >= 2) {
                AssignFrontWheels(selectedWheels);
                SetStatus("Front wheels assigned!", MessageType.Info);
                Repaint();
            }
        });
    }

    private void OpenRearWheelSetup() {
        if (!HasRCCPController) {
            SetStatus("Error: No RCCP vehicle selected", MessageType.Error);
            return;
        }

        GameObject vehicle = selectedController.gameObject;
        GameObject[] rearWheels = RCCP_DetectPossibleWheels.DetectPossibleRearWheels(vehicle);

        if (rearWheels == null || rearWheels.Length < 2) {
            EditorUtility.DisplayDialog("No Rear Wheels Detected",
                "Could not detect rear wheel models. Make sure your vehicle has separated wheel meshes.", "OK");
            return;
        }

        RCCP_PopupWindow_PossibleWheels.ShowWindow(rearWheels, selectedWheels => {
            if (selectedWheels != null && selectedWheels.Length >= 2) {
                AssignRearWheels(selectedWheels);
                SetStatus("Rear wheels assigned!", MessageType.Info);
                Repaint();
            }
        });
    }

    private void AssignFrontWheels(GameObject[] wheels) {
        if (!HasRCCPController || wheels == null || wheels.Length < 2) return;

        RCCP_Axle[] axles = selectedController.GetComponentsInChildren<RCCP_Axle>(true);
        RCCP_Axle frontAxle = null;

        foreach (var axle in axles) {
            if (axle.gameObject.name.Contains("Front")) {
                frontAxle = axle;
                break;
            }
        }

        if (frontAxle != null) {
            GameObject leftWheel = IsOnRight(selectedController.gameObject, wheels[0]) ? wheels[1] : wheels[0];
            GameObject rightWheel = IsOnRight(selectedController.gameObject, wheels[0]) ? wheels[0] : wheels[1];

            RCCP_CreateNewVehicle.AssignWheelsToAxle(frontAxle, leftWheel, rightWheel);
            EditorUtility.SetDirty(frontAxle);
        }
    }

    private void AssignRearWheels(GameObject[] wheels) {
        if (!HasRCCPController || wheels == null || wheels.Length < 2) return;

        RCCP_Axle[] axles = selectedController.GetComponentsInChildren<RCCP_Axle>(true);
        RCCP_Axle rearAxle = null;

        foreach (var axle in axles) {
            if (axle.gameObject.name.Contains("Rear")) {
                rearAxle = axle;
                break;
            }
        }

        if (rearAxle != null) {
            GameObject leftWheel = IsOnRight(selectedController.gameObject, wheels[0]) ? wheels[1] : wheels[0];
            GameObject rightWheel = IsOnRight(selectedController.gameObject, wheels[0]) ? wheels[0] : wheels[1];

            RCCP_CreateNewVehicle.AssignWheelsToAxle(rearAxle, leftWheel, rightWheel);
            EditorUtility.SetDirty(rearAxle);
        }
    }

    private bool IsOnRight(GameObject vehicle, GameObject wheel) {
        Vector3 localPosition = vehicle.transform.InverseTransformPoint(wheel.transform.position);
        return localPosition.x > 0;
    }

    private void OpenBodyCollidersWizard() {
        if (!HasRCCPController) {
            SetStatus("Error: No RCCP vehicle selected", MessageType.Error);
            return;
        }

        List<Transform> excludedTransforms = new List<Transform>();
        RCCP_Axle[] axles = selectedController.GetComponentsInChildren<RCCP_Axle>(true);

        foreach (var axle in axles) {
            if (axle.leftWheelModel != null) excludedTransforms.Add(axle.leftWheelModel);
            if (axle.rightWheelModel != null) excludedTransforms.Add(axle.rightWheelModel);
        }

#if RCCP_V2_2_OR_NEWER
        RCCP_BodyCollidersWizard.ShowWindow(selectedController.gameObject, excludedTransforms, 3);
#else
        // V2.0: ShowWindow takes 2 parameters (no depth)
        RCCP_BodyCollidersWizard.ShowWindow(selectedController.gameObject, excludedTransforms);
#endif
        SetStatus("Body Colliders Wizard opened", MessageType.Info);
    }

    #endregion

}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
