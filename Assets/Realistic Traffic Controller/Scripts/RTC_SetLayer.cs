//----------------------------------------------
//        Realistic Traffic Controller
//
// Copyright © 2014 - 2024 BoneCracker Games
// https://www.bonecrackergames.com
//
//----------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RTC_SetLayer : MonoBehaviour {

    public string layerName = "Default";

    private IEnumerator Start() {

        yield return new WaitForEndOfFrame();
        gameObject.layer = LayerMask.NameToLayer(layerName);

    }

}
