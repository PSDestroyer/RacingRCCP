//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright © 2014 - 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public class RCCP_MirrorInitLoad : MonoBehaviour {

    [InitializeOnLoadMethod]
    static void InitOnLoad() {

        EditorApplication.delayCall += EditorUpdate;

    }

    public static void EditorUpdate() {

        bool hasKey = false;

#if RCCP_MIRROR
        hasKey = true;
#endif

        if (!hasKey) {

            RCCP_SetScriptingSymbol.SetEnabled("RCCP_MIRROR", true);
            EditorUtility.DisplayDialog("Realistic Car Controller Pro | Mirror For Realistic Car Controller Pro", "Be sure you have imported latest Mirror to your project. Run the RCCP_Scene_Blank_Mirror demo scene. You can find more detailed info in documentation.", "Close");

            RCCP_SceneUpdater.Check();

            RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;

            if (rp == null)   // Built-in - nothing to convert
                return;

            bool isURP = rp.GetType().ToString().Contains("Universal");
            bool isHDRP = rp.GetType().ToString().Contains("HD");

            if (!isURP && !isHDRP)
                return;

            string rpName = isURP ? "URP" : "HDRP";
            bool ok = EditorUtility.DisplayDialog(
                "Convert Materials",
                $"Your project is using {rpName}.\n\n" +
                $"You'll need to convert the imported assets to be working with {rpName}.\n\n" +
                $"You can open the RCCP Render Pipeline Converter Window and proceed.",
                "Yes, open converter",
                "No thanks"
            );

            if (!ok)
                return;

            RCCP_RenderPipelineConverterWindow.Init();

        }

    }

}
