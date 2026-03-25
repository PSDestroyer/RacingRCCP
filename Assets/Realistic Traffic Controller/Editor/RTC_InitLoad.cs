//----------------------------------------------
//        Realistic Traffic Controller
//
// Copyright © 2014 - 2024 BoneCracker Games
// https://www.bonecrackergames.com
//
//----------------------------------------------

using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;

public class RTC_InitLoad {

    [InitializeOnLoadMethod]
    static void InitOnLoad() {

        EditorApplication.delayCall += EditorUpdate;

    }

    public static void EditorUpdate() {

        bool hasKey = false;

#if BCG_RTRC
        hasKey = true;
#endif

        if (!hasKey) {

            EditorUtility.DisplayDialog("Regards from BoneCracker Games", "Thank you for purchasing and using Realistic Traffic Controller. Please read the documentation before use. Also check out the online documentation for updated info. Have fun :)", "Let's get started!");

            RTC_Installation.Check();
            RTC_Installation.CheckPrefabs();

            RTC_SetScriptingSymbol.SetEnabled("BCG_RTRC", true);

        }

        CheckRP();

    }

    public static void CheckRP() {

        RenderPipelineAsset activePipeline;

        activePipeline = GraphicsSettings.currentRenderPipeline;

        if (activePipeline == null) {

            RTC_SetScriptingSymbol.SetEnabled("BCG_URP", false);
            RTC_SetScriptingSymbol.SetEnabled("BCG_HDRP", false);

        } else if (activePipeline.GetType().ToString().Contains("Universal")) {

#if !BCG_URP
            RTC_RenderPipelineConverterWindow.Init();
            RTC_SetScriptingSymbol.SetEnabled("BCG_URP", true);
            RTC_SetScriptingSymbol.SetEnabled("BCG_HDRP", false);
#endif

        } else if (activePipeline.GetType().ToString().Contains("HD")) {

#if !BCG_HDRP
            RTC_RenderPipelineConverterWindow.Init();
            RTC_SetScriptingSymbol.SetEnabled("BCG_HDRP", true);
            RTC_SetScriptingSymbol.SetEnabled("BCG_URP", false);
#endif

        } else {

            RTC_SetScriptingSymbol.SetEnabled("BCG_URP", false);
            RTC_SetScriptingSymbol.SetEnabled("BCG_HDRP", false);

        }

    }

}
