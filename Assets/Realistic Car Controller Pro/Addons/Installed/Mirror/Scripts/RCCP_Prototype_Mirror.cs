//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright © 2014 - 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class RCCP_Prototype_Mirror : ScriptableObject {

    /// <summary>
    /// All spawnable Mirror prototype vehicles.
    /// </summary>
    public RCCP_CarController[] vehicles;

    #region singleton
    private static RCCP_Prototype_Mirror instance;
    public static RCCP_Prototype_Mirror Instance { get { if (instance == null) instance = Resources.Load("RCCP_Prototype_Mirror") as RCCP_Prototype_Mirror; return instance; } }
    #endregion

}
