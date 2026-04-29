//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// UI paint button. 
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/UI/Modification/RCCP UI Color Button")]
public class RCCP_UI_Color : RCCP_UIComponent {

    /// <summary>
    /// Picked color.
    /// </summary>
    public PickedColor _pickedColor = PickedColor.Orange;
    public enum PickedColor { Orange, Red, Green, Blue, Black, White, Cyan, Magenta, Pink }

    public void OnClick() {

        //  Finding the player vehicle.
        RCCP_CarController playerVehicle = RCCPSceneManager.activePlayerVehicle;

        //  If no player vehicle found, return.
        if (!playerVehicle)
            return;

        //  If player vehicle doesn't have the customizer component, return.
        if (!playerVehicle.Customizer)
            return;

        if (!playerVehicle.Customizer.PaintManager)
            return;

        //  Color.
        Color selectedColor = new Color();

        switch (_pickedColor)
        {
            case PickedColor.Orange:
                selectedColor = new Color(1f, 0.55f, 0.1f); // vivid orange
                break;

            case PickedColor.Red:
                selectedColor = new Color(0.9f, 0.2f, 0.2f); // bright red
                break;

            case PickedColor.Green:
                selectedColor = new Color(0.1f, 0.7f, 0.3f); // fresh green
                break;

            case PickedColor.Blue:
                selectedColor = new Color(0.1f, 0.4f, 0.9f); // vibrant blue
                break;

            case PickedColor.Black:
                selectedColor = new Color(0.15f, 0.15f, 0.15f); // richer dark
                break;

            case PickedColor.White:
                selectedColor = new Color(1f, 1f, 1f);
                break;

            case PickedColor.Cyan:
                selectedColor = new Color(0.0f, 0.7f, 0.8f); // punchy cyan
                break;

            case PickedColor.Magenta:
                selectedColor = new Color(0.7f, 0.2f, 0.8f); // saturated magenta
                break;

            case PickedColor.Pink:
                selectedColor = new Color(1f, 0.4f, 0.7f); // strong pink
                break;
        }

        playerVehicle.Customizer.PaintManager.Paint(selectedColor);

    }

}
