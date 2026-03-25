//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright © 2014 - 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// All Mirror demo scenes.
/// </summary>
public class RCCP_DemoScenes_Mirror : ScriptableObject {

    public int instanceId = 0;

    #region singleton
    private static RCCP_DemoScenes_Mirror instance;
    public static RCCP_DemoScenes_Mirror Instance { get { if (instance == null) instance = Resources.Load("RCCP_DemoScenes_Mirror") as RCCP_DemoScenes_Mirror; return instance; } }
    #endregion

    public Object demo_Blank_Mirror;

    public string path_Demo_Blank_Mirror;

    public void Clean() {

        demo_Blank_Mirror = null;

        path_Demo_Blank_Mirror = "";

    }

    public void GetPaths() {

        if (demo_Blank_Mirror != null)
            path_Demo_Blank_Mirror = RCCP_GetAssetPath.GetAssetPath(demo_Blank_Mirror);

    }

}
