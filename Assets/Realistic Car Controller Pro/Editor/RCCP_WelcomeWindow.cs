//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright 2014 - 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using BoneCrackerGames.RCCP.CoreProtection;

public class RCCP_WelcomeWindow : EditorWindow {

    public class ToolBar {

        public string title;
        public UnityEngine.Events.UnityAction Draw;

        /// <summary>
        /// Create New Toolbar
        /// </summary>
        /// <param name="title">Title</param>
        /// <param name="onDraw">Method to draw when toolbar is selected</param>
        public ToolBar(string title, UnityEngine.Events.UnityAction onDraw) {

            this.title = title;
            this.Draw = onDraw;

        }

        public static implicit operator string(ToolBar tool) {

            return tool.title;

        }

    }

    /// <summary>
    /// Index of selected toolbar.
    /// </summary>
    public int toolBarIndex = 0;

    /// <summary>
    /// List of Toolbars
    /// </summary>
    public ToolBar[] toolBars = new ToolBar[]{

        new ToolBar("Welcome", WelcomePageContent),
        new ToolBar("Demos", DemosPageContent),
        new ToolBar("Addons", Addons),
        new ToolBar("Shaders", ShadersContent),
        new ToolBar("Keys", Keys),
        new ToolBar("Updates", UpdatePageContent),
        new ToolBar("DOC", Documentation)

    };

    public static Texture2D bannerTexture = null;

    private GUISkin skin;

    private const int windowWidth = 640;
    private const int windowHeight = 720;

    Vector2 scrollView;

    // Verification state
    private static string invoiceInput = "";
    private static bool isVerifying = false;
    private static string verificationMessage = "";
    private static bool isErrorMessage = false;
    private static DateTime verificationRetryTime = DateTime.MinValue;
    private bool forceShowVerification = false;

    [MenuItem("Tools/BoneCracker Games/Realistic Car Controller Pro/Welcome Window", false, 100)]
    [MenuItem("GameObject/BoneCracker Games/Realistic Car Controller Pro/Welcome Window", false, 100)]
    public static void OpenWindow() {

        GetWindow<RCCP_WelcomeWindow>(true);

    }

    public static void OpenWindowWithVerification() {

        var window = GetWindow<RCCP_WelcomeWindow>(true);
        window.forceShowVerification = true;

    }

    private void OnEnable() {

        titleContent = new GUIContent("Realistic Car Controller Pro");
        minSize = new Vector2(windowWidth, windowHeight);

        InitStyle();

    }

    private void InitStyle() {

        if (!skin)
            skin = Resources.Load("RCCP_Gui") as GUISkin;

        bannerTexture = (Texture2D)Resources.Load("Editor Icons/RCCP_Banner", typeof(Texture2D));

    }

    private void OnGUI() {

        GUI.skin = skin;

        // Verification panel: only shown when user explicitly clicks "Verify Now"
        if (forceShowVerification && !RCCP_CoreServerProxy.IsVerified) {
            DrawVerificationPanel();
            if (!EditorApplication.isPlaying)
                Repaint();
            return;
        }

        DrawHeader();

        // Grace period banner (non-blocking)
        if (!RCCP_CoreServerProxy.IsVerified) {
            DrawGracePeriodBanner();
        }

        DrawMenuButtons();
        DrawToolBar();
        DrawFooter();

        if (!EditorApplication.isPlaying)
            Repaint();

    }

    #region Verification UI

    private void DrawVerificationPanel() {

        // Background
        Rect bgRect = new Rect(0, 0, position.width, position.height);
        EditorGUI.DrawRect(bgRect, new Color(0.18f, 0.18f, 0.18f));

        float panelWidth = Mathf.Min(480, position.width - 40);
        float panelHeight = 480;
        float xOffset = (position.width - panelWidth) / 2;
        float yOffset = (position.height - panelHeight) / 2;

        GUILayout.BeginArea(new Rect(xOffset, yOffset, panelWidth, panelHeight));
        EditorGUILayout.BeginVertical(GUI.skin.box);

        GUILayout.Space(20);

        // Title
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) {
            fontSize = 20,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.58f, 0f) }
        };
        GUILayout.Label("Purchase Verification Required", titleStyle);
        GUILayout.Space(10);

        // Subtitle
        GUIStyle subtitleStyle = new GUIStyle(EditorStyles.wordWrappedLabel) {
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
        };
        GUILayout.Label("Please verify your Unity Asset Store purchase to continue using Realistic Car Controller Pro.", subtitleStyle);
        GUILayout.Space(5);
        GUILayout.Label("You can use your invoice from any BoneCracker Games product that includes RCCP.", subtitleStyle);
        GUILayout.Space(15);

        // Invoice input
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Space(5);
        GUILayout.Label("Invoice / Order Number", EditorStyles.boldLabel);
        GUILayout.Space(3);

        GUI.SetNextControlName("VerificationInvoiceInput");
        invoiceInput = EditorGUILayout.TextField(invoiceInput, GUILayout.Height(30));

        GUILayout.Space(3);
        GUIStyle hintStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { wordWrap = true };
        GUILayout.Label("Enter your Unity Asset Store invoice or order number", hintStyle);
        GUILayout.Space(5);
        EditorGUILayout.EndVertical();

        GUILayout.Space(8);

        // Messages
        if (!string.IsNullOrEmpty(verificationMessage)) {

            Color msgColor = isErrorMessage ? new Color(1f, 0.4f, 0.4f) : new Color(0.4f, 1f, 0.4f);
            GUIStyle msgStyle = new GUIStyle(EditorStyles.wordWrappedLabel) {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = msgColor }
            };
            GUILayout.Label(verificationMessage, msgStyle);
            GUILayout.Space(5);

        }

        // Rate limit warning
        if (verificationRetryTime > DateTime.Now) {

            int secondsRemaining = (int)(verificationRetryTime - DateTime.Now).TotalSeconds;
            int minutesRemaining = Mathf.CeilToInt(secondsRemaining / 60f);
            EditorGUILayout.HelpBox(
                $"Too many attempts. Please wait {minutesRemaining} minute(s) before trying again.",
                MessageType.Warning);
            GUILayout.Space(5);

        }

        // Verify button
        bool canVerify = !string.IsNullOrWhiteSpace(invoiceInput) &&
                        !isVerifying &&
                        verificationRetryTime <= DateTime.Now;

        EditorGUI.BeginDisabledGroup(!canVerify);
        Color oldBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(1f, 0.58f, 0f);

        string buttonText = isVerifying ? "Verifying..." : "Verify Purchase";
        if (GUILayout.Button(buttonText, GUILayout.Height(40))) {
            StartCoreVerification();
        }

        GUI.backgroundColor = oldBg;
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(10);

        // Help links
        GUIStyle linkStyle = new GUIStyle(EditorStyles.miniLabel) {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.5f, 0.7f, 1f) }
        };

        if (GUILayout.Button("How do I find my invoice or order number?", linkStyle)) {
            Application.OpenURL("https://assetstore.unity.com/orders");
        }

        GUILayout.Space(3);

        if (GUILayout.Button("Connection issues? Reset session", linkStyle)) {
            RCCP_CoreServerProxy.ClearRegistration();
            verificationMessage = "Session reset. Please try again.";
            isErrorMessage = false;
            Repaint();
        }

        // Back button (always available — verification is non-blocking)
        GUILayout.Space(5);
        if (GUILayout.Button("Back", GUILayout.Height(25))) {
            forceShowVerification = false;
            Repaint();
        }

        GUILayout.Space(10);
        EditorGUILayout.EndVertical();
        GUILayout.EndArea();

        // Handle Enter key
        Event e = Event.current;
        if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)) {
            if (canVerify && GUI.GetNameOfFocusedControl() == "VerificationInvoiceInput") {
                StartCoreVerification();
                e.Use();
            }
        }

    }

    private void DrawGracePeriodBanner() {

        Color bannerColor = new Color(1f, 0.58f, 0f, 0.15f);

        EditorGUILayout.BeginHorizontal(GUI.skin.box);
        Rect bannerRect = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(bannerRect, bannerColor);

        // Draw text centered in the rect
        GUIStyle bannerStyle = new GUIStyle(EditorStyles.boldLabel) {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11,
            normal = { textColor = new Color(1f, 0.85f, 0.5f) }
        };

        GUI.Label(bannerRect, "  Please verify your Asset Store purchase.  ", bannerStyle);

        // Verify Now button overlaid on the right
        Rect buttonRect = new Rect(bannerRect.xMax - 103, bannerRect.y + 3, 98, 22);
        if (GUI.Button(buttonRect, "Verify Now")) {
            forceShowVerification = true;
            Repaint();
        }

        EditorGUILayout.EndHorizontal();

    }

    private void StartCoreVerification() {

        if (string.IsNullOrWhiteSpace(invoiceInput)) return;
        if (isVerifying) return;
        if (verificationRetryTime > DateTime.Now) return;

        isVerifying = true;
        verificationMessage = "";
        Repaint();

        // Ensure registered first
        if (!RCCP_CoreServerProxy.IsRegistered) {
            RCCP_CoreServerProxy.RegisterDevice(this, (regSuccess, regMessage) => {
                if (regSuccess) {
                    // Registration may restore verified status from server DB
                    // (device was previously verified). Skip invoice check and show proper message.
                    if (RCCP_CoreServerProxy.IsVerified) {
                        isVerifying = false;
                        verificationMessage = "Your purchase has been previously verified on this device.";
                        isErrorMessage = false;
                        invoiceInput = "";
                        forceShowVerification = false;
                        Repaint();
                        return;
                    }
                    PerformCoreVerification();
                } else {
                    isVerifying = false;
                    verificationMessage = $"Failed to connect to server: {regMessage}";
                    isErrorMessage = true;
                    Repaint();
                }
            });
        } else {
            PerformCoreVerification();
        }

    }

    private void PerformCoreVerification() {

        RCCP_CoreServerProxy.VerifyInvoice(this, invoiceInput.Trim(), (result) => {

            isVerifying = false;

            // Handle invalid device token
            if (!result.Success && result.Error != null &&
                (result.Error.Contains("Invalid device token") || result.Error.Contains("invalid token"))) {
                RCCP_CoreServerProxy.ClearRegistration();
                verificationMessage = "Session expired. Please try again.";
                isErrorMessage = true;
                Repaint();
                return;
            }

            if (result.Success && result.Verified) {
                verificationMessage = result.Message ?? "Purchase verified successfully!";
                isErrorMessage = false;
                invoiceInput = "";
                forceShowVerification = false;

                EditorApplication.delayCall += () => {
                    Repaint();
                };
            } else {
                verificationMessage = result.Error ?? "Verification failed. Please check your invoice or order number.";
                isErrorMessage = true;

                if (result.RetryAfter > 0) {
                    verificationRetryTime = DateTime.Now.AddSeconds(result.RetryAfter);
                }
            }

            Repaint();

        });

    }

    #endregion

    private void DrawHeader() {

        GUILayout.Label(bannerTexture, GUILayout.Height(120));

    }

    private void DrawMenuButtons() {

        GUILayout.Space(-6);
        toolBarIndex = GUILayout.Toolbar(toolBarIndex, ToolbarNames());

    }

    #region ToolBars

    public static void WelcomePageContent() {

        GUILayout.Label("<size=18><color=#FF9500>Welcome!</color></size>");
        EditorGUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label("Thank you for purchasing and using Realistic Car Controller Pro. Please read the documentation before use. Also check out the online documentation for updated info. Have fun :)");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Separator();

#if !RCCP_DEMO

        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.HelpBox("Demo assets are not included. If you want to use demo assets (vehicles, scenes, examples, etc...), you must import demo assets to your project. You can import them from ''Addons'' tab.", MessageType.Warning, true);

        if (GUILayout.Button("Open Prototype Demo Scene")) {

            RCCP_DemoScenes.Instance.GetPaths();
            EditorSceneManager.OpenScene(RCCP_DemoScenes.Instance.path_demo_protototype, OpenSceneMode.Single);

        }

        EditorGUILayout.EndVertical();

        GUI.enabled = false;

#endif

        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.Space();

        if (GUILayout.Button("Add Demo Scenes To Build Settings"))
            AddDemoScenesToBuildSettings();

        EditorGUILayout.Separator();
        EditorGUILayout.HelpBox("If you want to add Photon PUN2 scenes, import and install Photon PUN2 & integration first. Then click again to add those scenes to your Build Settings.", MessageType.Info, true);
        EditorGUILayout.HelpBox("If you want to add Enter / Exit scenes, import BCG Shared Assets to your project first. Then click again to add those scenes to your Build Settings.", MessageType.Info, true);
        EditorGUILayout.Separator();

        EditorGUILayout.EndVertical();

        GUI.color = Color.red;

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Delete demo content from the project")) {

            if (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Warning", "You are about to delete demo content such as vehicle models, vehicle prefabs, vehicle textures, all scenes, scene models, scene prefabs, scene textures!", "Delete", "Cancel"))
                DeleteDemoContent();

        }

        GUI.color = Color.white;
        GUI.enabled = true;

    }

    public static void UpdatePageContent() {

        GUILayout.Label("<size=18><color=#FF9500>Updates</color></size>");

        EditorGUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label("<b>Installed Version: </b>" + RCCP_Version.version.ToString());
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(6);

        EditorGUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label("<b>1</b>- Always backup your project before updating RCCP or any asset in your project!");
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(6);

        EditorGUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label("<b>2</b>- If you have own assets such as prefabs, audioclips, models, scripts in Realistic Car Controller Pro folder, keep your own asset outside from Realistic Car Controller Pro folder.");
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(6);

        EditorGUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label("<b>3</b>- Delete Realistic Car Controller Pro folder, and import latest version to your project.");
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(6);

        if (GUILayout.Button("Check Updates"))
            Application.OpenURL(RCCP_AssetPaths.assetStorePath);

        GUILayout.Space(6);

        GUILayout.FlexibleSpace();

    }

    public static void ShadersContent() {

        GUILayout.Label("<size=18><color=#FF9500>Shaders [Builtin - URP - HDRP]</color></size>");

        EditorGUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label("RCCP comes with builtin render pipeline shaders. If your project is running on URP or HDRP, you'll need to convert demo materials of the RCCP and import URP shaders. There are four simple steps to do this process. \n\n1. Importing URP shaders\n\n2. Selecting all demo materials of the RCCP in the project, and converting them through Edit --> Rendering --> Convert menu." +
            "\n\n3. Converting car body shader. All demo vehicles are using a custom shader for their bodies. If you're going to use this shader, you'll need to convert.\n\n4. Remove the builtin shaders. You won't need them anymore.");
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(6);

        EditorGUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label("<b><color=#FF9500>Important</color></b> - Always backup your project before doing this!");
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(6);

        EditorGUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label("All these steps can be done through the Tools --> BCG --> RCCP --> URP --> To URP / To Builtin menu, or welcome window.");
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(6);

        GUILayout.FlexibleSpace();

    }

    public static void DemosPageContent() {

        GUILayout.Label("<size=18><color=#FF9500>Demo Scenes</color></size>");

        EditorGUILayout.BeginVertical(GUI.skin.box);

        GUILayout.Label("<b>Demo Content</b>");

#if !RCCP_DEMO

        bool decisionToImportDemo = false;

        if (GUILayout.Button("Import Demo Content"))
            decisionToImportDemo = EditorUtility.DisplayDialog("Realistic Car Controller Pro | Import Demo Content", "Do you want to import demo assets to your project? Be adviced, this will increase your build size even if you don't use them. You can always remove the demo assets from your project by using 'Delete Demo Content From Project' button in welcome window.", "Yes, import demo assets", "No");

        if (decisionToImportDemo)
            AssetDatabase.ImportPackage(RCCP_AddonPackages.Instance.GetAssetPath(RCCP_AddonPackages.Instance.demoPackage), true);

#else

        EditorGUILayout.HelpBox("Installed RCCP Demo Assets, You can open demo scenes and use demo content now.", MessageType.Info);
        GUILayout.Space(6);

#endif

        EditorGUILayout.EndVertical();

#if !RCCP_DEMO
        EditorGUILayout.HelpBox("Demo assets are not included. You can import them from ''Addons'' tab.", MessageType.Info, true);
        GUI.enabled = false;
#endif

        EditorGUILayout.Separator();
        EditorGUILayout.HelpBox("All scenes must be in your Build Settings to run AIO demo.", MessageType.Warning, true);
        EditorGUILayout.Separator();

        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.BeginHorizontal(GUI.skin.box);

        if (GUILayout.Button("RCCP City AIO")) {

            RCCP_DemoScenes.Instance.GetPaths();
            EditorSceneManager.OpenScene(RCCP_DemoScenes.Instance.path_city_AIO, OpenSceneMode.Single);
            EditorUtility.SetDirty(RCCP_DemoScenes.Instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

        }

        if (GUILayout.Button("RCCP City")) {

            RCCP_DemoScenes.Instance.GetPaths();
            EditorSceneManager.OpenScene(RCCP_DemoScenes.Instance.path_demo_City, OpenSceneMode.Single);
            EditorUtility.SetDirty(RCCP_DemoScenes.Instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

        }

        if (GUILayout.Button("RCCP City Car Selection")) {

            RCCP_DemoScenes.Instance.GetPaths();
            EditorSceneManager.OpenScene(RCCP_DemoScenes.Instance.path_demo_CarSelection, OpenSceneMode.Single);
            EditorUtility.SetDirty(RCCP_DemoScenes.Instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal(GUI.skin.box);

        if (GUILayout.Button("RCCP Blank API")) {

            RCCP_DemoScenes.Instance.GetPaths();
            EditorSceneManager.OpenScene(RCCP_DemoScenes.Instance.path_demo_APIBlank, OpenSceneMode.Single);
            EditorUtility.SetDirty(RCCP_DemoScenes.Instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

        }

        if (GUILayout.Button("RCCP Blank")) {

            RCCP_DemoScenes.Instance.GetPaths();
            EditorSceneManager.OpenScene(RCCP_DemoScenes.Instance.path_demo_BlankMobile, OpenSceneMode.Single);
            EditorUtility.SetDirty(RCCP_DemoScenes.Instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

        }

        if (GUILayout.Button("RCCP Damage")) {

            RCCP_DemoScenes.Instance.GetPaths();
            EditorSceneManager.OpenScene(RCCP_DemoScenes.Instance.path_demo_Damage, OpenSceneMode.Single);
            EditorUtility.SetDirty(RCCP_DemoScenes.Instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal(GUI.skin.box);

        if (GUILayout.Button("RCCP Customization")) {

            RCCP_DemoScenes.Instance.GetPaths();
            EditorSceneManager.OpenScene(RCCP_DemoScenes.Instance.path_demo_Customization, OpenSceneMode.Single);
            EditorUtility.SetDirty(RCCP_DemoScenes.Instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

        }

        if (GUILayout.Button("RCCP Override Inputs")) {

            RCCP_DemoScenes.Instance.GetPaths();
            EditorSceneManager.OpenScene(RCCP_DemoScenes.Instance.path_demo_OverrideInputs, OpenSceneMode.Single);
            EditorUtility.SetDirty(RCCP_DemoScenes.Instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

        }

        if (GUILayout.Button("RCCP Transport")) {

            RCCP_DemoScenes.Instance.GetPaths();
            EditorSceneManager.OpenScene(RCCP_DemoScenes.Instance.path_demo_Transport, OpenSceneMode.Single);
            EditorUtility.SetDirty(RCCP_DemoScenes.Instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        EditorGUILayout.Separator();

        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUI.enabled = true;

#if BCG_ENTEREXIT

        EditorGUILayout.BeginHorizontal(GUI.skin.box);

        if (BCG_DemoScenes.Instance.demo_CityFPS && GUILayout.Button("RCCP Blank Enter-Exit FPS")) {

            BCG_DemoScenes.Instance.GetPaths();
            EditorSceneManager.OpenScene(BCG_DemoScenes.Instance.path_demo_BlankFPS, OpenSceneMode.Single);
            EditorUtility.SetDirty(BCG_DemoScenes.Instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

        }

        if (BCG_DemoScenes.Instance.demo_CityTPS && GUILayout.Button("RCCP Blank Enter-Exit TPS")) {

            BCG_DemoScenes.Instance.GetPaths();
            EditorSceneManager.OpenScene(BCG_DemoScenes.Instance.path_demo_BlankTPS, OpenSceneMode.Single);
            EditorUtility.SetDirty(BCG_DemoScenes.Instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

        }

        EditorGUILayout.EndHorizontal();

#if RCCP_DEMO

        EditorGUILayout.BeginHorizontal(GUI.skin.box);

        if (BCG_DemoScenes.Instance.demo_CityFPS && GUILayout.Button("RCCP City Enter-Exit FPS")) {

            BCG_DemoScenes.Instance.GetPaths();
            EditorSceneManager.OpenScene(BCG_DemoScenes.Instance.path_demo_CityFPS, OpenSceneMode.Single);
            EditorUtility.SetDirty(BCG_DemoScenes.Instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

        }

        if (BCG_DemoScenes.Instance.demo_CityTPS && GUILayout.Button("RCCP City Enter-Exit TPS")) {

            BCG_DemoScenes.Instance.GetPaths();
            EditorSceneManager.OpenScene(BCG_DemoScenes.Instance.path_demo_CityTPS, OpenSceneMode.Single);
            EditorUtility.SetDirty(BCG_DemoScenes.Instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

        }

        EditorGUILayout.EndHorizontal();

#endif

#else

        EditorGUILayout.HelpBox("If you want to add enter exit scenes, you have to import latest BCG Shared Assets to your project first.", MessageType.Warning);

        if (GUILayout.Button("Download and import BCG Shared Assets"))
            AssetDatabase.ImportPackage(RCCP_AddonPackages.Instance.GetAssetPath(RCCP_AddonPackages.Instance.BCGSharedAssets), true);

#endif

        EditorGUILayout.EndVertical();

        EditorGUILayout.Separator();

        EditorGUILayout.BeginVertical(GUI.skin.box);

        bool photonInstalled = false;

#if PHOTON_UNITY_NETWORKING
        photonInstalled = true;
#endif

        bool photonAndRCCInstalled = false;

#if RCCP_PHOTON && PHOTON_UNITY_NETWORKING
        photonAndRCCInstalled = true;
#endif

        if (!photonAndRCCInstalled) {

            if (!photonInstalled) {

                EditorGUILayout.HelpBox("If you want to add Photon PUN2 scenes, you have to import latest Photon PUN2 to your project first.", MessageType.Warning);
                EditorGUILayout.HelpBox("You have to import latest Photon PUN2 to your project first.", MessageType.Warning);

                if (GUILayout.Button("Download and import Photon PUN2"))
                    Application.OpenURL(RCCP_AssetPaths.photonPUN2);

            } else {

                EditorGUILayout.HelpBox("Found Photon PUN2, You can import integration package and open Photon demo scenes now.", MessageType.Info);

                if (GUILayout.Button("Import Photon PUN2 Integration"))
                    AssetDatabase.ImportPackage(RCCP_AddonPackages.Instance.GetAssetPath(RCCP_AddonPackages.Instance.PhotonPUN2), true);

            }

        } else if (photonInstalled) {

#if RCCP_PHOTON && PHOTON_UNITY_NETWORKING

            EditorGUILayout.BeginHorizontal(GUI.skin.box);

            if (GUILayout.Button("RCCP Lobby Photon")) {

                RCCP_DemoScenes_Photon.Instance.GetPaths();
                EditorSceneManager.OpenScene(RCCP_DemoScenes_Photon.Instance.path_demo_PUN2Lobby, OpenSceneMode.Single);

            }

            if (GUILayout.Button("RCCP Blank Photon")) {

                RCCP_DemoScenes_Photon.Instance.GetPaths();
                EditorSceneManager.OpenScene(RCCP_DemoScenes_Photon.Instance.path_demo_PUN2City, OpenSceneMode.Single);

            }

            EditorGUILayout.EndHorizontal();

#endif

        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(GUI.skin.box);

        bool mirrorInstalled = false;

#if MIRROR
        mirrorInstalled = true;
#endif

        bool mirrorAndRCCPInstalled = false;

#if RCCP_MIRROR && MIRROR
        mirrorAndRCCPInstalled = true;
#endif

        if (!mirrorAndRCCPInstalled) {

            if (!mirrorInstalled) {

                EditorGUILayout.HelpBox("If you want to add mirror demo scenes, you have to import latest Mirror to your project first.", MessageType.Warning);
                EditorGUILayout.HelpBox("You have to import latest Mirror to your project first.", MessageType.Warning);

                if (GUILayout.Button("Download and import Mirror"))
                    Application.OpenURL(RCCP_AssetPaths.mirror);

            } else {

                EditorGUILayout.HelpBox("Found Mirror, You can import integration package and open Mirror demo scenes now.", MessageType.Info);

                if (GUILayout.Button("Import Mirror Integration"))
                    AssetDatabase.ImportPackage(RCCP_AddonPackages.Instance.GetAssetPath(RCCP_AddonPackages.Instance.mirror), true);

            }

        } else if (mirrorInstalled) {

#if RCCP_MIRROR && MIRROR

            EditorGUILayout.BeginHorizontal(GUI.skin.box);

            if (GUILayout.Button("RCCP Blank Mirror")) {

                RCCP_DemoScenes_Mirror.Instance.GetPaths();
                EditorSceneManager.OpenScene(RCCP_DemoScenes_Mirror.Instance.path_Demo_Blank_Mirror, OpenSceneMode.Single);

            }

            EditorGUILayout.EndHorizontal();

#endif

        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.EndVertical();
        GUILayout.FlexibleSpace();

    }

    public static void Addons() {

        GUILayout.Label("<size=18><color=#FF9500>Addons</color></size>");

        EditorGUILayout.BeginVertical(GUI.skin.box);

        GUILayout.Label("<b>RCCP AI Assistant</b> <color=#FF9500>(NEW)</color>");

#if !BCG_RCCP_AI

        EditorGUILayout.HelpBox("Configure vehicles with AI-powered assistance. Automatically set up drivetrain, audio, lights, damage and more using natural language descriptions.", MessageType.Info);

        if (GUILayout.Button("Get RCCP AI Assistant on Asset Store"))
            Application.OpenURL(RCCP_AssetPaths.AIAssistant);

#else

        EditorGUILayout.HelpBox("RCCP AI Assistant is installed.", MessageType.Info);

#endif

        EditorGUILayout.EndVertical();

        EditorGUILayout.Separator();

        EditorGUILayout.BeginVertical(GUI.skin.box);

        GUILayout.Label("<b>Demo Content</b>");

#if !RCCP_DEMO

        bool decisionToImportDemo = false;

        if (GUILayout.Button("Import Demo Content"))
            decisionToImportDemo = EditorUtility.DisplayDialog("Realistic Car Controller Pro | Import Demo Content", "Do you want to import demo assets to your project? Be adviced, this will increase your build size even if you don't use them. You can always remove the demo assets from your project by using 'Delete Demo Content From Project' button in welcome window.", "Yes, import demo assets", "No");

        if (decisionToImportDemo)
            AssetDatabase.ImportPackage(RCCP_AddonPackages.Instance.GetAssetPath(RCCP_AddonPackages.Instance.demoPackage), true);

#else

        EditorGUILayout.HelpBox("Installed RCCP Demo Assets, You can open demo scenes and use demo content now.", MessageType.Info);
        GUILayout.Space(6);

#endif

        EditorGUILayout.EndVertical();

        EditorGUILayout.Separator();

        EditorGUILayout.BeginVertical(GUI.skin.box);

        GUILayout.Label("<b>Photon PUN2</b>");

        bool photonInstalled = false;

#if PHOTON_UNITY_NETWORKING
        photonInstalled = true;
#endif

        bool photonAndRCCInstalled = false;

#if RCCP_PHOTON && PHOTON_UNITY_NETWORKING
        photonAndRCCInstalled = true;
#endif

        if (!photonAndRCCInstalled) {

            if (!photonInstalled) {

                EditorGUILayout.HelpBox("You have to import latest Photon PUN2 to your project first.", MessageType.Warning);

                if (GUILayout.Button("Download and import Photon PUN2"))
                    Application.OpenURL(RCCP_AssetPaths.photonPUN2);

            } else {

                EditorGUILayout.HelpBox("Found Photon PUN2, You can import integration package and open Photon demo scenes now.", MessageType.Info);

                if (GUILayout.Button("Import Photon PUN2 Integration"))
                    AssetDatabase.ImportPackage(RCCP_AddonPackages.Instance.GetAssetPath(RCCP_AddonPackages.Instance.PhotonPUN2), true);

            }

        } else if (photonInstalled) {

            EditorGUILayout.HelpBox("Installed Photon PUN2 with RCCP, You can open Photon demo scenes now.", MessageType.Info);
            EditorGUILayout.HelpBox("If you want to remove Photon PUN2 integration from the project, delete the ''Photon PUN2'' folder inside the Addons/Installed folder. After that, you need to remove ''RCCP_PHOTON'' scripting define symbol in your player settings. In order to do that, go to Edit --> Project Settings --> Player Settings --> Other Settings, and remove the scripting symbol from the list.", MessageType.Warning, true);

        }

#if RCCP_PHOTON && PHOTON_UNITY_NETWORKING
        if (photonInstalled) {

            EditorGUILayout.LabelField("Photon PUN2 Version: " + System.Reflection.Assembly.GetAssembly(typeof(ExitGames.Client.Photon.PhotonPeer)).GetName().Version.ToString(), EditorStyles.centeredGreyMiniLabel);
            GUILayout.Space(6);

        }
#endif

        EditorGUILayout.EndVertical();

        bool BCGInstalled = false;

#if BCG_ENTEREXIT
        BCGInstalled = true;
#endif

        EditorGUILayout.Separator();

        EditorGUILayout.BeginVertical(GUI.skin.box);

        GUILayout.Label("<b>BCG Shared Assets (Enter / Exit)</b>");

        if (!BCGInstalled) {

            EditorGUILayout.HelpBox("You have to import latest BCG Shared Assets to your project first.", MessageType.Warning);

            if (GUILayout.Button("Download and import BCG Shared Assets"))
                AssetDatabase.ImportPackage(RCCP_AddonPackages.Instance.GetAssetPath(RCCP_AddonPackages.Instance.BCGSharedAssets), true);

        } else {

            EditorGUILayout.HelpBox("Found BCG Shared Assets, You can open Enter / Exit demo scenes now.", MessageType.Info);
            EditorGUILayout.HelpBox("If you want to remove BCG Shared Assets integration from the project, delete the ''BoneCracker Games Shared Assets'' folder. After that, you need to remove ''BCG_ENTEREXIT'' scripting define symbol in your player settings. In order to do that, go to Edit --> Project Settings --> Player Settings --> Other Settings, and remove the scripting symbol from the list.", MessageType.Warning, true);

#if BCG_ENTEREXIT
            EditorGUILayout.LabelField("BCG Shared Assets Version: " + BCG_Version.version, EditorStyles.centeredGreyMiniLabel);
#endif
            GUILayout.Space(6);

        }

        EditorGUILayout.EndVertical();




        EditorGUILayout.Separator();

        EditorGUILayout.BeginVertical(GUI.skin.box);

        GUILayout.Label("<b>Mirror</b>");

        bool mirrorInstalled = false;

#if MIRROR
        mirrorInstalled = true;
#endif

        bool mirrorAndRCCPInstalled = false;

#if RCCP_MIRROR && MIRROR
        mirrorAndRCCPInstalled = true;
#endif

        if (!mirrorAndRCCPInstalled) {

            if (!mirrorInstalled) {

                EditorGUILayout.HelpBox("You have to import latest Mirror to your project first.", MessageType.Warning);

                if (GUILayout.Button("Download and import Mirror"))
                    Application.OpenURL(RCCP_AssetPaths.mirror);

            } else {

                EditorGUILayout.HelpBox("Found Mirror, You can import integration package and open Mirror demo scenes now.", MessageType.Info);

                if (GUILayout.Button("Import Mirror Integration"))
                    AssetDatabase.ImportPackage(RCCP_AddonPackages.Instance.GetAssetPath(RCCP_AddonPackages.Instance.mirror), true);

            }

        } else if (mirrorInstalled) {

            EditorGUILayout.HelpBox("Installed Mirror with RCCP, You can open Mirror demo scenes now.", MessageType.Info);
            EditorGUILayout.HelpBox("If you want to remove Mirror integration from the project, delete the ''Mirror'' folder inside the Addons/Installed folder. After that, you need to remove ''RCCP_MIRROR'' scripting define symbol in your player settings. In order to do that, go to Edit --> Project Settings --> Player Settings --> Other Settings, and remove the scripting symbol from the list.", MessageType.Warning, true);

        }

        EditorGUILayout.EndVertical();





        //EditorGUILayout.Separator();

        //EditorGUILayout.BeginVertical(GUI.skin.box);

        //GUILayout.Label("<b>Logitech</b>");

        //EditorGUILayout.BeginHorizontal();

        //if (GUILayout.Button("Download and import Logitech SDK"))
        //    Application.OpenURL(RCCP_AssetPaths.logitech);

        //if (GUILayout.Button("Import Logitech Integration"))
        //    AssetDatabase.ImportPackage(RCCP_AssetPaths.LogiAssetsPath, true);

        //EditorGUILayout.EndHorizontal();

        //EditorGUILayout.EndVertical();

        EditorGUILayout.Separator();

        EditorGUILayout.BeginVertical(GUI.skin.box);

        GUILayout.Label("<b>ProFlares</b>");

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Download and import ProFlares"))
            Application.OpenURL(RCCP_AssetPaths.proFlares);

        if (GUILayout.Button("Import ProFlares Integration"))
            AssetDatabase.ImportPackage(RCCP_AddonPackages.Instance.GetAssetPath(RCCP_AddonPackages.Instance.ProFlare), true);

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        EditorGUILayout.Separator();

        EditorGUILayout.BeginVertical(GUI.skin.box);

        GUILayout.Label("<b>Realistic Traffic Controller</b>");

        bool RTCInstalled = false;

#if BCG_RTRC
        RTCInstalled = true;
#endif

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Download and import\nRealistic Traffic Controller"))
            Application.OpenURL(RCCP_AssetPaths.RTC);

        if (GUILayout.Button("Import Integration For\nRealistic Traffic Controller"))
            AssetDatabase.ImportPackage(RCCP_AddonPackages.Instance.GetAssetPath(RCCP_AddonPackages.Instance.RTC), true);

        EditorGUILayout.EndHorizontal();

        if (RTCInstalled) {

            EditorGUILayout.HelpBox("Installed RTC with RCCP, You can open RTC demo scenes now.", MessageType.Info);

        }

        EditorGUILayout.EndVertical();

        GUILayout.FlexibleSpace();

    }

    public static void Keys() {

        GUILayout.Label("<size=18><color=#FF9500>Scripting Define Symbols</color></size>");

        EditorGUILayout.Separator();

        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("RCCP uses scripting define symbols to work with other addon packages. These packages are; \n\nDemo Content\nBoneCracker Shared Assets (Enter / Exit)\nPhoton Integration\nMirror Integration\n\nIf you attempt to import these addon packages, corresponding scripting symbol will be added to your build settings. But if you remove these addon packages, scripting symbol will still exists in the build settings and throw errors.");
        EditorGUILayout.EndVertical();
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("After removing any addon packages, please remove the corresponding scripting symbol in your build settings.\n\nPlease don't attempt to remove the key if package is still existing in the project. Remove the package first, after that you can remove the key.");
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(GUI.skin.box);

        GUILayout.Label("<b>Installed Scripting Symbols</b>");

        EditorGUILayout.BeginHorizontal();

        GUI.color = Color.red;

        if (EditorApplication.isCompiling)
            GUI.enabled = false;

#if !BCG_ENTEREXIT
        GUI.enabled = false;
#endif

        if (GUILayout.Button("Remove BCG_ENTEREXIT"))
            RCCP_SetScriptingSymbol.SetEnabled("BCG_ENTEREXIT", false);

        if (!EditorApplication.isCompiling)
            GUI.enabled = true;

#if !RCCP_DEMO
        GUI.enabled = false;
#endif

        if (GUILayout.Button("Remove RCCP_DEMO"))
            RCCP_SetScriptingSymbol.SetEnabled("RCCP_DEMO", false);

        if (!EditorApplication.isCompiling)
            GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();

#if !RCCP_PHOTON
        GUI.enabled = false;
#endif

        if (GUILayout.Button("Remove RCCP_PHOTON"))
            RCCP_SetScriptingSymbol.SetEnabled("RCCP_PHOTON", false);

        GUI.enabled = true;

        if (EditorApplication.isCompiling)
            GUI.enabled = false;

#if !RCCP_MIRROR
        GUI.enabled = false;
#endif

        if (GUILayout.Button("Remove RCCP_MIRROR"))
            RCCP_SetScriptingSymbol.SetEnabled("RCCP_MIRROR", false);

        GUI.color = Color.white;
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        EditorGUILayout.Separator();

        GUILayout.FlexibleSpace();

    }

    public static void Documentation() {

        GUILayout.Label("<size=18><color=#FF9500>Documentation</color></size>");

        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.HelpBox("Offline documentation can be found in the documentation folder.", MessageType.Info);

        if (GUILayout.Button("Open Documentation")) {
            UnityEngine.Object docAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(RCCP_AssetPaths.documentationPath);
            if (docAsset != null)
                AssetDatabase.OpenAsset(docAsset);
            else
                EditorUtility.RevealInFinder("Assets/Realistic Car Controller Pro/Documentation");
        }

        if (GUILayout.Button("Youtube Tutorial Videos"))
            Application.OpenURL(RCCP_AssetPaths.YTVideos);

        if (GUILayout.Button("Other Assets"))
            Application.OpenURL(RCCP_AssetPaths.otherAssets);

        EditorGUILayout.EndVertical();

        GUILayout.FlexibleSpace();

    }

    #endregion

    private string[] ToolbarNames() {

        string[] names = new string[toolBars.Length];

        for (int i = 0; i < toolBars.Length; i++)
            names[i] = toolBars[i];

        return names;

    }

    private void DrawToolBar() {

        // Offset toolbar content area when verification banner is visible
        float toolBarY = 150;
        if (!RCCP_CoreServerProxy.IsVerified) {
            toolBarY += 36;
        }

        GUILayout.BeginArea(new Rect(4, toolBarY, position.width - 8, position.height - (toolBarY + 40)));

        scrollView = EditorGUILayout.BeginScrollView(scrollView, false, false);

        toolBars[toolBarIndex].Draw();

        EditorGUILayout.EndScrollView();

        GUILayout.EndArea();
        GUILayout.FlexibleSpace();

    }

    private void DrawFooter() {

        EditorGUILayout.BeginHorizontal(GUI.skin.box);

        EditorGUILayout.LabelField("BoneCracker Games", EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.LabelField("Realistic Car Controller Pro " + RCCP_Version.version, EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.LabelField("Ekrem Bugra Ozdoganlar", EditorStyles.centeredGreyMiniLabel);

        EditorGUILayout.EndHorizontal();

    }

    private static void ImportPackage(string package) {

        try {
            AssetDatabase.ImportPackage(package, true);
        } catch (Exception) {
            Debug.LogError("Failed to import package: " + package);
            throw;
        }

    }

    private static void DeleteDemoContent() {

        Debug.LogWarning("Deleting demo content...");

        foreach (var item in RCCP_DemoContent.Instance.content) {

            if (item != null)
                FileUtil.DeleteFileOrDirectory(RCCP_GetAssetPath.GetAssetPath(item));

        }

        RCCP_DemoVehicles.Instance.vehicles = new RCCP_CarController[1];
        RCCP_DemoVehicles.Instance.vehicles[0] = RCCP_PrototypeContent.Instance.vehicles[0];
        RCCP_DemoScenes.Instance.Clean();

        EditorUtility.SetDirty(RCCP_DemoVehicles.Instance);
        EditorUtility.SetDirty(RCCP_DemoScenes.Instance);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        RCCP_SetScriptingSymbol.SetEnabled("RCCP_DEMO", false);

        Debug.LogWarning("Deleted demo content!");
        EditorUtility.DisplayDialog("Realistic Car Controller Pro | Deleted Demo Content", "All demo content have been deleted!", "Ok");

    }

    private static void AddDemoScenesToBuildSettings() {

        RCCP_DemoScenes.Instance.GetPaths();
        EditorUtility.SetDirty(RCCP_DemoScenes.Instance);

#if BCG_ENTEREXIT
        BCG_DemoScenes.Instance.GetPaths();
        EditorUtility.SetDirty(BCG_DemoScenes.Instance);
#endif

#if RCCP_PHOTON
        RCCP_DemoScenes_Photon.Instance.GetPaths();
        EditorUtility.SetDirty(RCCP_DemoScenes_Photon.Instance);
#endif

#if RCCP_MIRROR
        RCCP_DemoScenes_Mirror.Instance.GetPaths();
        EditorUtility.SetDirty(RCCP_DemoScenes_Mirror.Instance);
#endif

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        List<string> demoScenePaths = new List<string>();

        demoScenePaths.Add(RCCP_DemoScenes.Instance.path_city_AIO);
        demoScenePaths.Add(RCCP_DemoScenes.Instance.path_demo_City);
        demoScenePaths.Add(RCCP_DemoScenes.Instance.path_demo_CarSelection);
        demoScenePaths.Add(RCCP_DemoScenes.Instance.path_demo_APIBlank);
        demoScenePaths.Add(RCCP_DemoScenes.Instance.path_demo_BlankMobile);
        demoScenePaths.Add(RCCP_DemoScenes.Instance.path_demo_Damage);
        demoScenePaths.Add(RCCP_DemoScenes.Instance.path_demo_Customization);
        demoScenePaths.Add(RCCP_DemoScenes.Instance.path_demo_OverrideInputs);
        demoScenePaths.Add(RCCP_DemoScenes.Instance.path_demo_Transport);

#if BCG_ENTEREXIT

        demoScenePaths.Add(BCG_DemoScenes.Instance.path_demo_BlankFPS);
        demoScenePaths.Add(BCG_DemoScenes.Instance.path_demo_BlankTPS);

#if RCCP_DEMO

        demoScenePaths.Add(BCG_DemoScenes.Instance.path_demo_CityFPS);
        demoScenePaths.Add(BCG_DemoScenes.Instance.path_demo_CityTPS);

#endif

#endif

#if RCCP_PHOTON && PHOTON_UNITY_NETWORKING

        demoScenePaths.Add(RCCP_DemoScenes_Photon.Instance.path_demo_PUN2Lobby);
        demoScenePaths.Add(RCCP_DemoScenes_Photon.Instance.path_demo_PUN2City);

#endif

#if RCCP_MIRROR && MIRROR

        demoScenePaths.Add(RCCP_DemoScenes_Mirror.Instance.path_Demo_Blank_Mirror);

#endif

#if BCG_RTRC

        demoScenePaths.Add(RCCP_DemoScenes.Instance.path_demo_CityWithTraffic);

#endif

        demoScenePaths.Add(RCCP_DemoScenes.Instance.path_demo_CityWithAI);

        // Find valid Scene paths and make a list of EditorBuildSettingsScene
        List<EditorBuildSettingsScene> editorBuildSettingsScenes = new List<EditorBuildSettingsScene>();

        foreach (string path in demoScenePaths) {

            if (!string.IsNullOrEmpty(path))
                editorBuildSettingsScenes.Add(new EditorBuildSettingsScene(path, true));

        }

        // Set the Build Settings window Scene list
        EditorBuildSettings.scenes = editorBuildSettingsScenes.ToArray();

        EditorUtility.DisplayDialog("Realistic Car Controller Pro | Demo Scenes", "All demo scenes have been added to the Build Settings. For Photon and Enter / Exit scenes, you have to import and intregrate them first (Addons).", "Ok");

    }

}
#endif
