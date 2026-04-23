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

        switch (_pickedColor) {

            case PickedColor.Orange:
                selectedColor = Color.red + (Color.green / 2f);
                break;

            case PickedColor.Red:
                selectedColor = new Color(0.75f, 0.05f, 0.05f); // sport red

                break;

            case PickedColor.Green:
                selectedColor = new Color(0.0f, 0.3f, 0.1f); // british racing green;
                break;

            case PickedColor.Blue:
                selectedColor = new Color(0.0f, 0.2f, 0.6f); // deep metallic blue (BMW style);
                break;

            case PickedColor.Black:
                selectedColor = Color.black;
                break;

            case PickedColor.White:
                selectedColor = Color.white;
                break;

            case PickedColor.Cyan:
                selectedColor = new Color(0.0f, 0.5f, 0.6f);
                break;

            case PickedColor.Magenta:
                selectedColor = new Color(0.4f, 0.1f, 0.5f);
                break;

            case PickedColor.Pink:
                selectedColor = new Color(0.8f, 0.4f, 0.6f); // soft pink (mai realist decât neon)
                break;

        }

        playerVehicle.Customizer.PaintManager.Paint(selectedColor);

    }

}
