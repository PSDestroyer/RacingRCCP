//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright 2014 - 2025 BoneCracker Games
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
using System.Linq;
using UnityEditor.Events;
using UnityEngine.Events;
using BoneCrackerGames.RCCP.CoreProtection;

[CustomEditor(typeof(RCCP_MainComponent), true)]
public class RCCP_CarControllerEditor : Editor {

    RCCP_CarController prop;
    GUISkin skin;
    Color guiColor;
    List<string> errorMessages = new List<string>();

    static bool statsEnabled;

    // Validation Panel State
    private List<RCCP_VehicleValidator.ValidationResult> cachedValidationResults;
    private Vector2 validationScrollPosition;
    private static bool showErrors = true;
    private static bool showWarnings = true;
    private static bool showInfo = false;
    private static RCCP_VehicleValidator.Category? filterCategory = null;

    RCCP_Engine engine;
    RCCP_Clutch clutch;
    RCCP_Gearbox gearbox;
    RCCP_Differential[] differentials;
    RCCP_Axles axles;
    RCCP_Input inputs;
    RCCP_AeroDynamics aero;
    RCCP_Audio audio;
    RCCP_Lights lights;
    RCCP_Stability stability;
    RCCP_Damage damage;
    RCCP_Particles particles;
    RCCP_Lod lod;
    RCCP_OtherAddons otherAddons;
    RCCP_Customizer customizer;

    private void OnEnable() {

        skin = Resources.Load<GUISkin>("RCCP_Gui");

        if (!EditorApplication.isPlaying)
            ReOrderComponents();

    }

    private void AddEngineToClutchListener() {

        engine.outputEvent = new RCCP_Event_Output();

        var targetinfo = UnityEvent.GetValidMethodInfo(clutch,
"ReceiveOutput", new Type[] { typeof(RCCP_Output) });

        var methodDelegate = Delegate.CreateDelegate(typeof(UnityAction<RCCP_Output>), clutch, targetinfo) as UnityAction<RCCP_Output>;
        UnityEventTools.AddPersistentListener(engine.outputEvent, methodDelegate);

    }

    private void AddClutchToGearboxListener() {

        clutch.outputEvent = new RCCP_Event_Output();

        var targetinfo = UnityEvent.GetValidMethodInfo(gearbox,
"ReceiveOutput", new Type[] { typeof(RCCP_Output) });

        var methodDelegate = Delegate.CreateDelegate(typeof(UnityAction<RCCP_Output>), gearbox, targetinfo) as UnityAction<RCCP_Output>;
        UnityEventTools.AddPersistentListener(clutch.outputEvent, methodDelegate);

    }

    private void AddGearboxToDifferentialListener() {

        gearbox.outputEvent = new RCCP_Event_Output();

        // Connect gearbox to the first differential if any exist
        if (differentials != null && differentials.Length > 0 && differentials[0] != null) {

            var targetinfo = UnityEvent.GetValidMethodInfo(differentials[0],
"ReceiveOutput", new Type[] { typeof(RCCP_Output) });

            var methodDelegate = Delegate.CreateDelegate(typeof(UnityAction<RCCP_Output>), differentials[0], targetinfo) as UnityAction<RCCP_Output>;
            UnityEventTools.AddPersistentListener(gearbox.outputEvent, methodDelegate);

        }

    }

    private void AddDifferentialToAxle() {

        if (!axles)
            return;

        if (differentials == null || differentials.Length == 0)
            return;

        float[] indexes = new float[axles.GetComponentsInChildren<RCCP_Axle>(true).Length];

        if (indexes.Length < 1)
            return;

        for (int i = 0; i < indexes.Length; i++)
            indexes[i] = axles.GetComponentsInChildren<RCCP_Axle>(true)[i].leftWheelCollider.transform.localPosition.z;

        int biggestIndex = 0;
        int lowestIndex = 0;

        for (int i = 0; i < indexes.Length; i++) {

            if (indexes[i] >= biggestIndex)
                biggestIndex = i;

            if (indexes[i] <= lowestIndex)
                lowestIndex = i;

        }

        RCCP_Axle rearAxle = axles.GetComponentsInChildren<RCCP_Axle>(true)[lowestIndex];

        // Connect the first differential to rear axle if available
        if (rearAxle && differentials.Length > 0 && differentials[0] != null)
            differentials[0].connectedAxle = rearAxle;

    }

    private void ReOrderComponents() {

        int index = 0;

        if (engine) {

            engine.transform.SetSiblingIndex(index);
            index++;

        }

        if (clutch) {

            clutch.transform.SetSiblingIndex(index);
            index++;

        }

        if (gearbox) {

            gearbox.transform.SetSiblingIndex(index);
            index++;

        }

        if (differentials != null) {

            for (int i = 0; i < differentials.Length; i++) {

                if (differentials[i]) {

                    differentials[i].transform.SetSiblingIndex(index);
                    index++;

                }

            }

        }

        if (axles) {

            axles.transform.SetSiblingIndex(index);
            index++;

        }

        if (inputs) {

            inputs.transform.SetSiblingIndex(index);
            index++;

        }

        if (aero) {

            aero.transform.SetSiblingIndex(index);
            index++;

        }

        if (stability) {

            stability.transform.SetSiblingIndex(index);
            index++;

        }

        if (audio) {

            audio.transform.SetSiblingIndex(index);
            index++;

        }

        if (lights) {

            lights.transform.SetSiblingIndex(index);
            index++;

        }

        if (damage) {

            damage.transform.SetSiblingIndex(index);
            index++;

        }

        if (particles) {

            particles.transform.SetSiblingIndex(index);
            index++;

        }

        if (lod) {

            lod.transform.SetSiblingIndex(index);
            index++;

        }

        if (otherAddons) {

            otherAddons.transform.SetSiblingIndex(index);
            index++;

        }

    }

    private void GetAllComponents() {

        engine = prop.GetComponentInChildren<RCCP_Engine>(true);
        clutch = prop.GetComponentInChildren<RCCP_Clutch>(true);
        gearbox = prop.GetComponentInChildren<RCCP_Gearbox>(true);
        differentials = prop.GetComponentsInChildren<RCCP_Differential>(true);
        axles = prop.GetComponentInChildren<RCCP_Axles>(true);
        inputs = prop.GetComponentInChildren<RCCP_Input>(true);
        aero = prop.GetComponentInChildren<RCCP_AeroDynamics>(true);
        audio = prop.GetComponentInChildren<RCCP_Audio>(true);
        stability = prop.GetComponentInChildren<RCCP_Stability>(true);
        lights = prop.GetComponentInChildren<RCCP_Lights>(true);
        damage = prop.GetComponentInChildren<RCCP_Damage>(true);
        particles = prop.GetComponentInChildren<RCCP_Particles>(true);
        lod = prop.GetComponentInChildren<RCCP_Lod>(true);
        otherAddons = prop.GetComponentInChildren<RCCP_OtherAddons>(true);
        customizer = prop.GetComponentInChildren<RCCP_Customizer>(true);

    }

    public override void OnInspectorGUI() {

        prop = (RCCP_CarController)target;
        serializedObject.Update();
        GUI.skin = skin;
        guiColor = GUI.color;

        GetAllComponents();

        RCCP_SceneManager sm = RCCP_SceneManager.Instance;

        if (!EditorApplication.isPlaying)
            prop.checkComponents = false;

        CheckMissingAxleManager();

        if (EditorUtility.IsPersistent(prop))
            EditorGUILayout.HelpBox("Double click the prefab to edit settings. Some editor features are disabled in this mode.", MessageType.Warning);

        if (Screen.width < 500)
            EditorGUILayout.HelpBox("Increase width of your inspector panel to see all content.", MessageType.Warning);

        // Verification banner (non-blocking)
        if (!RCCP_CoreServerProxy.IsVerified) {
            DrawVerificationBanner();
        }

        // AI Assistant banner (drawn before disabling GUI so it remains clickable)
#if BCG_RCCP_AI
        DrawAIAssistantBanner();
#else
        DrawAIAssistantPromoBanner();
#endif

        if (EditorUtility.IsPersistent(prop))
            GUI.enabled = false;

        GUILayout.Label("<color=#FF9500>Drivetrain</color>");

        EditorGUILayout.BeginHorizontal();

        DrivetrainButtons();

        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10f);

        GUILayout.Label("<color=#FF9500>Addons</color>");

        EditorGUILayout.BeginHorizontal();

        AddonButtons();

        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10f);

        EditorGUILayout.BeginHorizontal();

        AddonButtons2();

        EditorGUILayout.EndHorizontal();

        GUI.enabled = true;

        GUILayout.Space(10f);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("ineffectiveBehavior"), new GUIContent("Ineffective Behavior", "Selected behavior in RCCP_Settings won't affect this vehicle if this option is enabled."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("canControl"), new GUIContent("Can Control", "Is this vehicle controllable now? RCCP_Inputs attached to the vehicle will receive inputs when enabled."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("externalControl"), new GUIContent("External Control", "Is this vehicle controlled by an external controller?"));

        // Per-Vehicle Behavior Section
        EditorGUILayout.Space();
        DrawPerVehicleBehaviorSection();

        EditorGUILayout.Space();

        statsEnabled = EditorGUILayout.BeginToggleGroup(new GUIContent("Statistics", "Will be updated at runtime."), statsEnabled);

        if (statsEnabled) {

            if (!EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Statistics will be updated at runtime", MessageType.Info);

            GUI.enabled = false;

            EditorGUILayout.PropertyField(serializedObject.FindProperty("engineRPM"), new GUIContent("Engine RPM"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("minEngineRPM"), new GUIContent("Minimum Engine RPM"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxEngineRPM"), new GUIContent("Maximum Engine RPM"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("currentGear"), new GUIContent("Current Gear"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("currentGearRatio"), new GUIContent("Current Gear Ratio"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lastGearRatio"), new GUIContent("Last Gear Ratio"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("differentialRatio"), new GUIContent("Differential Ratio"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("speed"), new GUIContent("Physically Speed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("wheelRPM2Speed"), new GUIContent("Wheel RPM 2 Speed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tractionWheelRPM2EngineRPM"), new GUIContent("Wheel RPM 2 Engine RPM"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("targetWheelSpeedForCurrentGear"), new GUIContent("Target Wheel Speed For Current Gear"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maximumSpeed"), new GUIContent("Maximum Speed"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("producedEngineTorque"), new GUIContent("Produced Engine Torque as NM"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("producedGearboxTorque"), new GUIContent("Produced Gearbox Torque as NM"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("producedDifferentialTorque"), new GUIContent("Produced Differential Torque as NM"));
            EditorGUILayout.Space();

            if (prop.PoweredAxles != null)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_poweredAxles"), new GUIContent("Power Axles"), true);

            if (prop.BrakedAxles != null)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_brakedAxles"), new GUIContent("Brake Axles"), true);

            if (prop.SteeredAxles != null)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_steeredAxles"), new GUIContent("Steer Axles"), true);

            if (prop.HandbrakedAxles != null)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_handbrakedAxles"), new GUIContent("Handbrake Axles"), true);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("direction"), new GUIContent("Direction"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("shiftingNow"), new GUIContent("Shifting Now"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("reversingNow"), new GUIContent("Reversing Now"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("steerAngle"), new GUIContent("Steer Angle"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("throttleInput_V"), new GUIContent("Vehicle Throttle Input"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("brakeInput_V"), new GUIContent("Vehicle Brake Input"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("steerInput_V"), new GUIContent("Vehicle Steer Input"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("handbrakeInput_V"), new GUIContent("Vehicle Handbrake Input"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("clutchInput_V"), new GUIContent("Vehicle clutch Input"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("nosInput_V"), new GUIContent("Nos Input"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lowBeamLights"), new GUIContent("Low Beam Lights"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("highBeamLights"), new GUIContent("High Beam Lights"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("indicatorsLeftLights"), new GUIContent("Indicator Lights L"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("indicatorsRightLights"), new GUIContent("Indicator Lights R"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("indicatorsAllLights"), new GUIContent("Indicator Lights All"));

            GUI.enabled = true;

        }

        EditorGUILayout.EndToggleGroup();

        if (!EditorApplication.isPlaying && !EditorUtility.IsPersistent(prop)) {

            if (RCCP_Settings.Instance.setLayers)
                SetLayers();

        }

        serializedObject.ApplyModifiedProperties();

        if (GUI.changed)
            EditorUtility.SetDirty(prop);

        if (!EditorApplication.isPlaying)
            DrawValidationPanel();

        if (!EditorApplication.isPlaying)
            Repaint();

    }

    private void SetLayers() {

        prop.transform.gameObject.layer = LayerMask.NameToLayer(RCCP_Settings.Instance.RCCPLayer);

        var children = prop.transform.GetComponentsInChildren<Transform>(true);

        if (RCCP_Settings.Instance.RCCPLayer != "") {

            foreach (var child in children)
                child.gameObject.layer = LayerMask.NameToLayer(RCCP_Settings.Instance.RCCPLayer);

        }

        if (RCCP_Settings.Instance.RCCPWheelColliderLayer != "") {

            foreach (RCCP_WheelCollider item in prop.gameObject.GetComponentsInChildren<RCCP_WheelCollider>(true))
                item.gameObject.layer = LayerMask.NameToLayer(RCCP_Settings.Instance.RCCPWheelColliderLayer);

        }

        if (RCCP_Settings.Instance.RCCPDetachablePartLayer != "") {

            foreach (RCCP_DetachablePart item in prop.gameObject.GetComponentsInChildren<RCCP_DetachablePart>(true))
                item.gameObject.layer = LayerMask.NameToLayer(RCCP_Settings.Instance.RCCPDetachablePartLayer);

        }

    }

    private void DrawVerificationBanner() {

        Color bannerColor = new Color(1f, 0.58f, 0f, 0.2f);
        Color textColor = new Color(1f, 0.85f, 0.5f);

        // Draw banner background
        Rect bannerRect = EditorGUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(32));
        EditorGUI.DrawRect(bannerRect, bannerColor);

        // Text style
        GUIStyle textStyle = new GUIStyle(EditorStyles.boldLabel) {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 11,
            normal = { textColor = textColor },
            padding = new RectOffset(8, 0, 0, 0)
        };

        // Reserve space for the button
        float buttonWidth = 100;
        Rect textRect = new Rect(bannerRect.x, bannerRect.y, bannerRect.width - buttonWidth - 8, bannerRect.height);
        GUI.Label(textRect, "Please verify your Asset Store purchase.", textStyle);

        // Verify button on the right
        Rect buttonRect = new Rect(bannerRect.xMax - buttonWidth - 4, bannerRect.y + 5, buttonWidth, bannerRect.height - 10);
        if (GUI.Button(buttonRect, "Verify Now")) {
            RCCP_WelcomeWindow.OpenWindowWithVerification();
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(4);

    }

#if BCG_RCCP_AI
    private static Texture2D aiAssistantBannerTex;
    private static GUIStyle aiAssistantLabelStyle;
    private static GUIStyle aiAssistantStatusStyle;

    private void DrawAIAssistantBanner() {

        GUILayout.Space(10f);

        // Create gradient texture if needed
        if (aiAssistantBannerTex == null) {
            aiAssistantBannerTex = CreateHorizontalGradient(512, 1,
                new Color(0.15f, 0.12f, 0.25f, 1f),  // Dark purple-blue
                new Color(0.35f, 0.20f, 0.55f, 1f)); // Lighter purple
        }

        // Banner rect - entire area is clickable
        Rect bannerRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(36f));

        // Check for hover
        bool isHovered = bannerRect.Contains(Event.current.mousePosition);
        if (isHovered) {
            EditorGUIUtility.AddCursorRect(bannerRect, MouseCursor.Link);
        }

        // Handle click - use MouseUp for more reliable detection
        if ((Event.current.type == EventType.MouseUp || Event.current.type == EventType.MouseDown)
            && Event.current.button == 0
            && bannerRect.Contains(Event.current.mousePosition)) {
            if (Event.current.type == EventType.MouseUp) {
                OpenAIAssistantWindow();
            }
            Event.current.Use();
            GUIUtility.ExitGUI();
        }

        // Draw gradient background
        if (Event.current.type == EventType.Repaint && aiAssistantBannerTex != null) {
            GUI.DrawTexture(bannerRect, aiAssistantBannerTex, ScaleMode.StretchToFill);

            // Hover highlight
            if (isHovered) {
                EditorGUI.DrawRect(bannerRect, new Color(1f, 1f, 1f, 0.05f));
            }

            // Draw subtle top highlight
            Rect highlightRect = new Rect(bannerRect.x, bannerRect.y, bannerRect.width, 1f);
            EditorGUI.DrawRect(highlightRect, new Color(1f, 1f, 1f, 0.08f));

            // Draw bottom shadow
            Rect shadowRect = new Rect(bannerRect.x, bannerRect.yMax - 1f, bannerRect.width, 1f);
            EditorGUI.DrawRect(shadowRect, new Color(0f, 0f, 0f, 0.3f));
        }

        GUILayout.Space(12f);

        // Sparkle icon
        GUIStyle iconStyle = new GUIStyle(EditorStyles.label);
        iconStyle.fontSize = 16;
        iconStyle.normal.textColor = new Color(1f, 0.85f, 0.4f, 1f); // Golden yellow
        iconStyle.alignment = TextAnchor.MiddleCenter;
        GUILayout.Label("✦", iconStyle, GUILayout.Width(20f), GUILayout.Height(36f));

        GUILayout.Space(4f);

        // Label - use cached style from RCCP GUISkin
        if (aiAssistantLabelStyle == null && skin != null) {
            aiAssistantLabelStyle = new GUIStyle(skin.label);
            aiAssistantLabelStyle.fontSize = 11;
            aiAssistantLabelStyle.fontStyle = FontStyle.Bold;
            aiAssistantLabelStyle.normal.textColor = Color.white;
            aiAssistantLabelStyle.alignment = TextAnchor.MiddleLeft;
        }
        if (aiAssistantLabelStyle != null)
            GUILayout.Label("AI ASSISTANT", aiAssistantLabelStyle, GUILayout.Height(30f));
        else
            GUILayout.Label("AI ASSISTANT", EditorStyles.boldLabel, GUILayout.Height(30f));

        GUILayout.FlexibleSpace();

        // API Status indicator (right side)
        DrawAPIStatusIndicator();

        GUILayout.Space(12f);

        EditorGUILayout.EndHorizontal();
        GUILayout.Space(8f);

    }

    private void DrawAPIStatusIndicator() {
        // Get API status from AI Assistant settings
        bool hasApiKey = false;
        bool isServerProxy = false;

        // Check RCCP_AISettings for server proxy mode
        var settingsType = System.Type.GetType("BoneCrackerGames.RCCP.AIAssistant.RCCP_AISettings, Assembly-CSharp-Editor");
        if (settingsType != null) {
            var instanceProp = settingsType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (instanceProp != null) {
                var settings = instanceProp.GetValue(null);
                if (settings != null) {
                    var useServerProxyField = settingsType.GetField("useServerProxy");
                    if (useServerProxyField != null)
                        isServerProxy = (bool)useServerProxyField.GetValue(settings);
                }
            }
        }

        // Check for valid auth: either server proxy enabled OR own API key set
        if (isServerProxy) {
            hasApiKey = true;
        } else {
            // Check RCCP_AIEditorPrefs for the API key (stored in EditorPrefs)
            var editorPrefsType = System.Type.GetType("BoneCrackerGames.RCCP.AIAssistant.RCCP_AIEditorPrefs, Assembly-CSharp-Editor");
            if (editorPrefsType != null) {
                var apiKeyProp = editorPrefsType.GetProperty("ApiKey", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (apiKeyProp != null) {
                    string apiKey = (string)apiKeyProp.GetValue(null);
                    hasApiKey = !string.IsNullOrEmpty(apiKey);
                }
            }
        }

        // Draw RCCP version indicator (V2.0 warning or V2.2+ badge)
        DrawRCCPVersionBadge();
        GUILayout.Space(8f);

        // Status dot and text - use cached style from RCCP GUISkin
        if (aiAssistantStatusStyle == null && skin != null) {
            aiAssistantStatusStyle = new GUIStyle(skin.label);
            aiAssistantStatusStyle.fontSize = 10;
            aiAssistantStatusStyle.alignment = TextAnchor.MiddleRight;
            aiAssistantStatusStyle.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
        }

        Color statusColor;
        string statusText;

        if (hasApiKey) {
            statusColor = new Color(0.4f, 0.9f, 0.5f, 1f); // Green
            statusText = isServerProxy ? "Ready" : "Ready";
        } else {
            statusColor = new Color(0.9f, 0.6f, 0.3f, 1f); // Orange
            statusText = "Setup Required";
        }

        if (aiAssistantStatusStyle != null)
            GUILayout.Label(statusText, aiAssistantStatusStyle, GUILayout.Height(36f));
        else
            GUILayout.Label(statusText, EditorStyles.miniLabel, GUILayout.Height(36f));

        GUILayout.Space(6f);

        // Status dot
        Rect dotRect = GUILayoutUtility.GetRect(8f, 8f, GUILayout.Width(8f), GUILayout.Height(36f));
        if (Event.current.type == EventType.Repaint) {
            float dotY = dotRect.y + (dotRect.height - 8f) / 2f;
            Rect actualDot = new Rect(dotRect.x, dotY, 8f, 8f);

            // Glow effect
            EditorGUI.DrawRect(new Rect(actualDot.x - 1f, actualDot.y - 1f, 10f, 10f), new Color(statusColor.r, statusColor.g, statusColor.b, 0.3f));
            EditorGUI.DrawRect(actualDot, statusColor);
        }
    }

    /// <summary>
    /// Draws a small RCCP version badge indicating V2.0 (warning) or V2.2+ (OK).
    /// </summary>
    private void DrawRCCPVersionBadge() {
        // Get version info from RCCP_AIVersionDetector via reflection
        string icon = "✓";
        string text = "V2.2+";
        bool isWarning = false;

        var versionDetectorType = System.Type.GetType("BoneCrackerGames.RCCP.AIAssistant.RCCP_AIVersionDetector, Assembly-CSharp-Editor");
        if (versionDetectorType != null) {
            var getBadgeMethod = versionDetectorType.GetMethod("GetVersionBadge", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (getBadgeMethod != null) {
                var result = getBadgeMethod.Invoke(null, null);
                if (result != null) {
                    // Result is a ValueTuple<string, string, bool>
                    var tupleType = result.GetType();
                    var item1Field = tupleType.GetField("Item1");
                    var item2Field = tupleType.GetField("Item2");
                    var item3Field = tupleType.GetField("Item3");
                    if (item1Field != null && item2Field != null && item3Field != null) {
                        icon = (string)item1Field.GetValue(result);
                        text = (string)item2Field.GetValue(result);
                        isWarning = (bool)item3Field.GetValue(result);
                    }
                }
            }
        }

        // Badge colors
        Color badgeColor = isWarning ? new Color(0.95f, 0.65f, 0.2f, 1f) : new Color(0.4f, 0.85f, 0.5f, 1f);
        Color bgColor = new Color(badgeColor.r, badgeColor.g, badgeColor.b, 0.2f);

        string tooltip = isWarning
            ? "RCCP V2.0 detected - some AI features limited. Upgrade to V2.2+ recommended."
            : "RCCP V2.2+ - all AI features available.";

        // Badge style
        GUIStyle badgeStyle = new GUIStyle(EditorStyles.miniLabel);
        badgeStyle.fontSize = 9;
        badgeStyle.fontStyle = FontStyle.Bold;
        badgeStyle.alignment = TextAnchor.MiddleCenter;
        badgeStyle.normal.textColor = badgeColor;
        badgeStyle.padding = new RectOffset(4, 4, 1, 1);

        GUIContent content = new GUIContent($"{icon} {text}", tooltip);
        Vector2 size = badgeStyle.CalcSize(content);

        // Draw badge
        Rect rect = GUILayoutUtility.GetRect(size.x + 4, 16f, GUILayout.Width(size.x + 4), GUILayout.Height(36f));
        rect.y += (36f - 16f) / 2f; // Center vertically
        rect.height = 16f;

        if (Event.current.type == EventType.Repaint) {
            // Background
            EditorGUI.DrawRect(rect, bgColor);

            // Border
            Color borderColor = new Color(badgeColor.r, badgeColor.g, badgeColor.b, 0.4f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), borderColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), borderColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), borderColor);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), borderColor);
        }

        // Text
        GUI.Label(rect, content, badgeStyle);
    }

#if BCG_RCCP_AI
    private void OpenAIAssistantWindow() {

        BoneCrackerGames.RCCP.AIAssistant.RCCP_AIAssistantWindow.ShowWindow();

    }
#endif

#endif

    private static Texture2D CreateHorizontalGradient(int width, int height, Color left, Color right) {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.HideAndDontSave;

        Color[] colors = new Color[width * height];
        for (int x = 0; x < width; x++) {
            float t = (float)x / (width - 1);
            Color c = Color.Lerp(left, right, t);
            for (int y = 0; y < height; y++) {
                colors[y * width + x] = c;
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        return tex;
    }

#if !BCG_RCCP_AI
    private static Texture2D aiPromobannerTex;
    private static GUIStyle aiPromoLabelStyle;
    private static GUIStyle aiPromoStatusStyle;

    private void DrawAIAssistantPromoBanner() {

        GUILayout.Space(10f);

        // Create gradient texture if needed (blue-teal for promo)
        if (aiPromobannerTex == null) {
            aiPromobannerTex = CreateHorizontalGradient(512, 1,
                new Color(0.10f, 0.18f, 0.28f, 1f),  // Dark blue
                new Color(0.15f, 0.35f, 0.45f, 1f));  // Teal
        }

        // Banner rect - entire area is clickable
        Rect bannerRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(36f));

        // Check for hover
        bool isHovered = bannerRect.Contains(Event.current.mousePosition);
        if (isHovered) {
            EditorGUIUtility.AddCursorRect(bannerRect, MouseCursor.Link);
        }

        // Handle click - opens Asset Store page
        if ((Event.current.type == EventType.MouseUp || Event.current.type == EventType.MouseDown)
            && Event.current.button == 0
            && bannerRect.Contains(Event.current.mousePosition)) {
            if (Event.current.type == EventType.MouseUp) {
                Application.OpenURL(RCCP_AssetPaths.AIAssistant);
            }
            Event.current.Use();
            GUIUtility.ExitGUI();
        }

        // Draw gradient background
        if (Event.current.type == EventType.Repaint && aiPromobannerTex != null) {
            GUI.DrawTexture(bannerRect, aiPromobannerTex, ScaleMode.StretchToFill);

            // Hover highlight
            if (isHovered) {
                EditorGUI.DrawRect(bannerRect, new Color(1f, 1f, 1f, 0.05f));
            }

            // Draw subtle top highlight
            Rect highlightRect = new Rect(bannerRect.x, bannerRect.y, bannerRect.width, 1f);
            EditorGUI.DrawRect(highlightRect, new Color(1f, 1f, 1f, 0.08f));

            // Draw bottom shadow
            Rect shadowRect = new Rect(bannerRect.x, bannerRect.yMax - 1f, bannerRect.width, 1f);
            EditorGUI.DrawRect(shadowRect, new Color(0f, 0f, 0f, 0.3f));
        }

        GUILayout.Space(12f);

        // Sparkle icon
        GUIStyle iconStyle = new GUIStyle(EditorStyles.label);
        iconStyle.fontSize = 16;
        iconStyle.normal.textColor = new Color(0.4f, 0.85f, 1f, 1f); // Light blue
        iconStyle.alignment = TextAnchor.MiddleCenter;
        GUILayout.Label("\u2728", iconStyle, GUILayout.Width(20f), GUILayout.Height(36f));

        GUILayout.Space(4f);

        // Label
        if (aiPromoLabelStyle == null && skin != null) {
            aiPromoLabelStyle = new GUIStyle(skin.label);
            aiPromoLabelStyle.fontSize = 11;
            aiPromoLabelStyle.fontStyle = FontStyle.Bold;
            aiPromoLabelStyle.normal.textColor = Color.white;
            aiPromoLabelStyle.alignment = TextAnchor.MiddleLeft;
        }
        if (aiPromoLabelStyle != null)
            GUILayout.Label("AI ASSISTANT", aiPromoLabelStyle, GUILayout.Height(30f));
        else
            GUILayout.Label("AI ASSISTANT", EditorStyles.boldLabel, GUILayout.Height(30f));

        GUILayout.FlexibleSpace();

        // Right side - "Get on Asset Store >" text
        if (aiPromoStatusStyle == null && skin != null) {
            aiPromoStatusStyle = new GUIStyle(skin.label);
            aiPromoStatusStyle.fontSize = 10;
            aiPromoStatusStyle.alignment = TextAnchor.MiddleRight;
            aiPromoStatusStyle.normal.textColor = new Color(0.5f, 0.85f, 1f, 0.9f);
        }
        if (aiPromoStatusStyle != null)
            GUILayout.Label("Get on Asset Store \u203a", aiPromoStatusStyle, GUILayout.Height(36f));
        else
            GUILayout.Label("Get on Asset Store \u203a", EditorStyles.miniLabel, GUILayout.Height(36f));

        GUILayout.Space(12f);

        EditorGUILayout.EndHorizontal();
        GUILayout.Space(8f);

    }
#endif

    private void DrivetrainButtons() {

        EditorGUILayout.BeginHorizontal();

        EngineButton();
        GUILayout.Space(1f);
        ClutchButton();
        GUILayout.Space(1f);
        GearboxButton();
        GUILayout.Space(1f);
        DifferentialsButton();
        GUILayout.Space(1f);
        AxlesButton();

        EditorGUILayout.EndHorizontal();

    }

    private void AddonButtons() {

        EditorGUILayout.BeginHorizontal();

        InputsButton();
        GUILayout.Space(1f);
        AeroButton();
        GUILayout.Space(1f);
        StabilityButton();
        GUILayout.Space(1f);
        AudioButton();
        GUILayout.Space(1f);
        CustomizerButton();

        EditorGUILayout.EndHorizontal();

    }

    private void AddonButtons2() {

        EditorGUILayout.BeginHorizontal();

        LightsButton();
        GUILayout.Space(1f);
        DamageButton();
        GUILayout.Space(1f);
        ParticlesButton();
        GUILayout.Space(1f);
        LODButton();
        GUILayout.Space(1f);
        OtherAddonsButton();

        EditorGUILayout.EndHorizontal();

    }

    private void EngineButton() {

        EditorGUILayout.BeginVertical(GUI.skin.window);

        if (engine)
            engine.enabled = EditorGUILayout.ToggleLeft("", engine.enabled, GUILayout.Width(15f));

        GUILayout.Label(("<color=#FF9500>Engine</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

        if (GUILayout.Button(Resources.Load("Editor Icons/Icon_Engine") as Texture, GUILayout.Width(70f), GUILayout.Height(50f), GUILayout.ExpandWidth(true))) {

            if (engine)
                Selection.activeGameObject = engine.gameObject;
            else
                AddEngine();

        }

        if (!EditorUtility.IsPersistent(prop)) {

            if (engine)
                GUILayout.Label(("<color=#FF9500>Equipped</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));
            else
                GUILayout.Label(("<color=#FF9500>Create</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

            GUI.color = Color.red;

            if (engine) {

                if (GUILayout.Button("Remove", GUILayout.Width(50f), GUILayout.ExpandWidth(true)))
                    RemoveEngine();

            }

            GUI.color = guiColor;

        }

        EditorGUILayout.EndVertical();

    }

    private void ClutchButton() {

        EditorGUILayout.BeginVertical(GUI.skin.window);

        if (clutch)
            clutch.enabled = EditorGUILayout.ToggleLeft("", clutch.enabled, GUILayout.Width(15f));

        GUILayout.Label(("<color=#FF9500>Clutch</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

        if (GUILayout.Button(Resources.Load("Editor Icons/Icon_Clutch") as Texture, GUILayout.Width(70f), GUILayout.Height(50f), GUILayout.ExpandWidth(true))) {

            if (clutch)
                Selection.activeGameObject = clutch.gameObject;
            else
                AddClutch();

        }

        if (!EditorUtility.IsPersistent(prop)) {

            if (clutch)
                GUILayout.Label(("<color=#FF9500>Equipped</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));
            else
                GUILayout.Label(("<color=#FF9500>Create</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

            GUI.color = Color.red;

            if (clutch) {

                if (GUILayout.Button("Remove", GUILayout.Width(50f), GUILayout.ExpandWidth(true)))
                    RemoveClutch();

            }

            GUI.color = guiColor;

        }

        EditorGUILayout.EndVertical();

    }

    private void GearboxButton() {

        EditorGUILayout.BeginVertical(GUI.skin.window);

        if (gearbox)
            gearbox.enabled = EditorGUILayout.ToggleLeft("", gearbox.enabled, GUILayout.Width(15f));

        GUILayout.Label(("<color=#FF9500>Gearbox</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

        if (GUILayout.Button(Resources.Load("Editor Icons/Icon_Gearbox") as Texture, GUILayout.Width(70f), GUILayout.Height(50f), GUILayout.ExpandWidth(true))) {

            if (gearbox)
                Selection.activeGameObject = gearbox.gameObject;
            else
                AddGearbox();

        }

        if (!EditorUtility.IsPersistent(prop)) {

            if (gearbox)
                GUILayout.Label(("<color=#FF9500>Equipped</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));
            else
                GUILayout.Label(("<color=#FF9500>Create</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

            GUI.color = Color.red;

            if (gearbox) {

                if (GUILayout.Button("Remove", GUILayout.Width(50f), GUILayout.ExpandWidth(true)))
                    RemoveGearbox();

            }

            GUI.color = guiColor;

        }

        EditorGUILayout.EndVertical();

    }

    // at the top of RCCP_CarControllerEditor, alongside your other fields:
    private int selectedDifferentialIndex = 0;

    private void DifferentialsButton() {

        EditorGUILayout.BeginVertical(GUI.skin.window);

        // Toggle for all differentials
        if (differentials != null && differentials.Length > 0) {

            bool allEnabled = true;

            for (int i = 0; i < differentials.Length; i++) {

                if (differentials[i] && !differentials[i].enabled) {
                    allEnabled = false;
                    break;
                }

            }

            bool newToggleState = EditorGUILayout.ToggleLeft("", allEnabled, GUILayout.Width(15f));

            if (newToggleState != allEnabled) {
                for (int i = 0; i < differentials.Length; i++) {
                    if (differentials[i])
                        differentials[i].enabled = newToggleState;
                }
            }
        }

        GUILayout.Label("<color=#FF9500>Differentials</color>", GUILayout.Width(50f), GUILayout.ExpandWidth(true));

        // First row: icon + dropdown + select button
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(
            Resources.Load("Editor Icons/Icon_Differential") as Texture,
            GUILayout.Width(70f), GUILayout.Height(50f), GUILayout.ExpandWidth(true)
        )) {
            if (differentials != null && differentials.Length > 0)
                Selection.activeGameObject = differentials[selectedDifferentialIndex].gameObject;
            else
                AddDifferential();
        }

        EditorGUILayout.EndHorizontal();

        if (differentials != null && differentials.Length > 1) {

            // build name list
            string[] diffNames = new string[differentials.Length];
            for (int i = 0; i < differentials.Length; i++)
                diffNames[i] = differentials[i] != null ? differentials[i].name : $"Differential {i + 1}";

            // clamp index
            selectedDifferentialIndex = Mathf.Clamp(
                selectedDifferentialIndex, 0, differentials.Length - 1
            );

            // show popup
            selectedDifferentialIndex = EditorGUILayout.Popup(
                selectedDifferentialIndex, diffNames
            );

            if (GUILayout.Button("Select")) {
                var d = differentials[selectedDifferentialIndex];
                if (d != null)
                    Selection.activeGameObject = d.gameObject;
            }
        }


        if (!EditorUtility.IsPersistent(prop)) {

            // Equipped / Create label
            if (differentials != null && differentials.Length > 0)
                GUILayout.Label(
                    $"<color=#FF9500>Equipped ({differentials.Length})</color>",
                    GUILayout.Width(50f), GUILayout.ExpandWidth(true)
                );
            else
                GUILayout.Label(
                    "<color=#FF9500>Create</color>",
                    GUILayout.Width(50f), GUILayout.ExpandWidth(true)
                );

            // Add / Remove buttons
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Add", GUILayout.Width(25f), GUILayout.ExpandWidth(true)))
                AddDifferential();

            GUI.color = Color.red;

            if (differentials != null && differentials.Length > 0)
                if (GUILayout.Button("Remove", GUILayout.Width(25f), GUILayout.ExpandWidth(true)))
                    RemoveDifferential();

            GUI.color = guiColor;

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }


    private void AxlesButton() {

        EditorGUILayout.BeginVertical(GUI.skin.window);

        if (axles)
            axles.enabled = EditorGUILayout.ToggleLeft("", axles.enabled, GUILayout.Width(15f));

        GUILayout.Label(("<color=#FF9500>Axles</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

        if (GUILayout.Button(Resources.Load("Editor Icons/Icon_Axle") as Texture, GUILayout.Width(70f), GUILayout.Height(50f), GUILayout.ExpandWidth(true))) {

            if (axles)
                Selection.activeGameObject = axles.gameObject;
            else
                AddAxles();

        }

        if (!EditorUtility.IsPersistent(prop)) {

            if (axles)
                GUILayout.Label(("<color=#FF9500>Equipped</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));
            else
                GUILayout.Label(("<color=#FF9500>Create</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

            GUI.color = Color.red;

            if (axles) {

                if (GUILayout.Button("Remove", GUILayout.Width(50f), GUILayout.ExpandWidth(true)))
                    RemoveAxle();

            }

            GUI.color = guiColor;

        }

        EditorGUILayout.EndVertical();

    }

    private void InputsButton() {

        EditorGUILayout.BeginVertical(GUI.skin.window);

        if (inputs)
            inputs.enabled = EditorGUILayout.ToggleLeft("", inputs.enabled, GUILayout.Width(15f));

        GUILayout.Label(("<color=#FF9500>Inputs</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

        if (GUILayout.Button(Resources.Load("Editor Icons/Icon_Inputs") as Texture, GUILayout.Width(70f), GUILayout.Height(35f), GUILayout.ExpandWidth(true))) {

            if (inputs)
                Selection.activeGameObject = inputs.gameObject;
            else
                AddInputs();

        }

        if (!EditorUtility.IsPersistent(prop)) {

            if (inputs)
                GUILayout.Label(("<color=#FF9500>Equipped</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));
            else
                GUILayout.Label(("<color=#FF9500>Create</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

            if (inputs) {

                GUI.color = Color.red;

                if (GUILayout.Button("Remove", GUILayout.Width(50f), GUILayout.ExpandWidth(true)))
                    RemoveInputs();

                GUI.color = guiColor;

            }

        }

        EditorGUILayout.EndVertical();

    }

    private void AudioButton() {

        EditorGUILayout.BeginVertical(GUI.skin.window);

        if (audio)
            audio.enabled = EditorGUILayout.ToggleLeft("", audio.enabled, GUILayout.Width(15f));

        GUILayout.Label(("<color=#FF9500>Audio</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

        if (GUILayout.Button(Resources.Load("Editor Icons/Icon_Audio") as Texture, GUILayout.Width(70f), GUILayout.Height(35f), GUILayout.ExpandWidth(true))) {

            if (audio)
                Selection.activeGameObject = audio.gameObject;
            else
                AddAudio();

        }

        if (!EditorUtility.IsPersistent(prop)) {

            if (audio)
                GUILayout.Label(("<color=#FF9500>Equipped</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));
            else
                GUILayout.Label(("<color=#FF9500>Create</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

            if (audio) {

                GUI.color = Color.red;

                if (GUILayout.Button("Remove", GUILayout.Width(50f), GUILayout.ExpandWidth(true)))
                    RemoveAudio();

                GUI.color = guiColor;

            }

        }

        EditorGUILayout.EndVertical();

    }

    private void CustomizerButton() {

        EditorGUILayout.BeginVertical(GUI.skin.window);

        if (customizer)
            customizer.enabled = EditorGUILayout.ToggleLeft("", customizer.enabled, GUILayout.Width(15f));

        GUILayout.Label(("<color=#FF9500>Customizer</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

        if (GUILayout.Button(Resources.Load("Editor Icons/Icon_Customizer") as Texture, GUILayout.Width(70f), GUILayout.Height(35f), GUILayout.ExpandWidth(true))) {

            if (customizer)
                Selection.activeGameObject = customizer.gameObject;
            else
                AddCustomizer();

        }

        if (!EditorUtility.IsPersistent(prop)) {

            if (customizer)
                GUILayout.Label(("<color=#FF9500>Equipped</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));
            else
                GUILayout.Label(("<color=#FF9500>Create</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

            if (customizer) {

                GUI.color = Color.red;

                if (GUILayout.Button("Remove", GUILayout.Width(50f), GUILayout.ExpandWidth(true)))
                    RemoveCustomizer();

                GUI.color = guiColor;

            }

        }

        EditorGUILayout.EndVertical();

    }

    private void AeroButton() {

        EditorGUILayout.BeginVertical(GUI.skin.window);

        if (aero)
            aero.enabled = EditorGUILayout.ToggleLeft("", aero.enabled, GUILayout.Width(15f));

        GUILayout.Label(("<color=#FF9500>Dynamics</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

        if (GUILayout.Button(Resources.Load("Editor Icons/Icon_Aero") as Texture, GUILayout.Width(70f), GUILayout.Height(35f), GUILayout.ExpandWidth(true))) {

            if (aero)
                Selection.activeGameObject = aero.gameObject;
            else
                AddAero();

        }

        if (!EditorUtility.IsPersistent(prop)) {

            if (aero)
                GUILayout.Label(("<color=#FF9500>Equipped</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));
            else
                GUILayout.Label(("<color=#FF9500>Create</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

            if (aero) {

                GUI.color = Color.red;

                if (GUILayout.Button("Remove", GUILayout.Width(50f), GUILayout.ExpandWidth(true)))
                    RemoveAero();

                GUI.color = guiColor;

            }

        }

        EditorGUILayout.EndVertical();

    }

    private void StabilityButton() {

        EditorGUILayout.BeginVertical(GUI.skin.window);

        if (stability)
            stability.enabled = EditorGUILayout.ToggleLeft("", stability.enabled, GUILayout.Width(15f));

        GUILayout.Label(("<color=#FF9500>Stability</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

        if (GUILayout.Button(Resources.Load("Editor Icons/Icon_Stability") as Texture, GUILayout.Width(70f), GUILayout.Height(35f), GUILayout.ExpandWidth(true))) {

            if (stability)
                Selection.activeGameObject = stability.gameObject;
            else
                AddStability();

        }

        if (!EditorUtility.IsPersistent(prop)) {

            if (stability)
                GUILayout.Label(("<color=#FF9500>Equipped</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));
            else
                GUILayout.Label(("<color=#FF9500>Create</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

            if (stability) {

                GUI.color = Color.red;

                if (GUILayout.Button("Remove", GUILayout.Width(50f), GUILayout.ExpandWidth(true)))
                    RemoveStability();

                GUI.color = guiColor;

            }

        }

        EditorGUILayout.EndVertical();

    }

    private void LightsButton() {

        EditorGUILayout.BeginVertical(GUI.skin.window);

        if (lights)
            lights.enabled = EditorGUILayout.ToggleLeft("", lights.enabled, GUILayout.Width(15f));

        GUILayout.Label(("<color=#FF9500>Lights</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

        if (GUILayout.Button(Resources.Load("Editor Icons/Icon_Light") as Texture, GUILayout.Width(70f), GUILayout.Height(35f), GUILayout.ExpandWidth(true))) {

            if (lights)
                Selection.activeGameObject = lights.gameObject;
            else
                AddLights();

        }

        if (!EditorUtility.IsPersistent(prop)) {

            if (lights)
                GUILayout.Label(("<color=#FF9500>Equipped</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));
            else
                GUILayout.Label(("<color=#FF9500>Create</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

            if (lights) {

                GUI.color = Color.red;

                if (GUILayout.Button("Remove", GUILayout.Width(50f), GUILayout.ExpandWidth(true)))
                    RemoveLights();

                GUI.color = guiColor;

            }

        }

        EditorGUILayout.EndVertical();

    }

    private void DamageButton() {

        EditorGUILayout.BeginVertical(GUI.skin.window);

        if (damage)
            damage.enabled = EditorGUILayout.ToggleLeft("", damage.enabled, GUILayout.Width(15f));

        GUILayout.Label(("<color=#FF9500>Damage</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

        if (GUILayout.Button(Resources.Load("Editor Icons/Icon_Damage") as Texture, GUILayout.Width(70f), GUILayout.Height(35f), GUILayout.ExpandWidth(true))) {

            if (damage)
                Selection.activeGameObject = damage.gameObject;
            else
                AddDamage();

        }

        if (!EditorUtility.IsPersistent(prop)) {

            if (damage)
                GUILayout.Label(("<color=#FF9500>Equipped</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));
            else
                GUILayout.Label(("<color=#FF9500>Create</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

            if (damage) {

                GUI.color = Color.red;

                if (GUILayout.Button("Remove", GUILayout.Width(50f), GUILayout.ExpandWidth(true)))
                    RemoveDamage();

                GUI.color = guiColor;

            }

        }

        EditorGUILayout.EndVertical();

    }

    private void ParticlesButton() {

        EditorGUILayout.BeginVertical(GUI.skin.window);

        if (particles)
            particles.enabled = EditorGUILayout.ToggleLeft("", particles.enabled, GUILayout.Width(15f));

        GUILayout.Label(("<color=#FF9500>Particles</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

        if (GUILayout.Button(Resources.Load("Editor Icons/Icon_Particles") as Texture, GUILayout.Width(70f), GUILayout.Height(35f), GUILayout.ExpandWidth(true))) {

            if (particles)
                Selection.activeGameObject = particles.gameObject;
            else
                AddParticles();

        }

        if (!EditorUtility.IsPersistent(prop)) {

            if (particles)
                GUILayout.Label(("<color=#FF9500>Equipped</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));
            else
                GUILayout.Label(("<color=#FF9500>Create</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

            if (particles) {

                GUI.color = Color.red;

                if (GUILayout.Button("Remove", GUILayout.Width(50f), GUILayout.ExpandWidth(true)))
                    RemoveParticles();

                GUI.color = guiColor;

            }

        }

        EditorGUILayout.EndVertical();

    }

    private void LODButton() {

        EditorGUILayout.BeginVertical(GUI.skin.window);

        if (lod)
            lod.enabled = EditorGUILayout.ToggleLeft("", lod.enabled, GUILayout.Width(15f));

        GUILayout.Label(("<color=#FF9500>LOD</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

        if (GUILayout.Button(Resources.Load("Editor Icons/Icon_LOD") as Texture, GUILayout.Width(70f), GUILayout.Height(35f), GUILayout.ExpandWidth(true))) {

            if (lod)
                Selection.activeGameObject = lod.gameObject;
            else
                AddLOD();

        }

        if (!EditorUtility.IsPersistent(prop)) {

            if (lod)
                GUILayout.Label(("<color=#FF9500>Equipped</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));
            else
                GUILayout.Label(("<color=#FF9500>Create</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

            if (lod) {

                GUI.color = Color.red;

                if (GUILayout.Button("Remove", GUILayout.Width(50f), GUILayout.ExpandWidth(true)))
                    RemoveLOD();

                GUI.color = guiColor;

            }

        }

        EditorGUILayout.EndVertical();

    }

    private void OtherAddonsButton() {

        EditorGUILayout.BeginVertical(GUI.skin.window);

        GUILayout.Label(("<color=#FF9500>Other Addons</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

        if (GUILayout.Button(Resources.Load("Editor Icons/Icon_OtherAddons") as Texture, GUILayout.Width(70f), GUILayout.Height(35f), GUILayout.ExpandWidth(true))) {

            if (otherAddons)
                Selection.activeGameObject = otherAddons.gameObject;
            else
                AddOtherAddons();

        }

        if (!EditorUtility.IsPersistent(prop)) {

            if (otherAddons)
                GUILayout.Label(("<color=#FF9500>Equipped</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));
            else
                GUILayout.Label(("<color=#FF9500>Create</color>"), GUILayout.Width(50f), GUILayout.ExpandWidth(true));

            if (otherAddons) {

                GUI.color = Color.red;

                if (GUILayout.Button("Remove", GUILayout.Width(50f), GUILayout.ExpandWidth(true)))
                    RemoveOtherAddons();

                GUI.color = guiColor;

            }

        }

        EditorGUILayout.EndVertical();

    }

    private void AddEngine() {

        if (EditorUtility.IsPersistent(prop))
            return;

        GameObject subject = new GameObject("RCCP_Engine");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(0);
        engine = subject.AddComponent<RCCP_Engine>();

        EditorUtility.SetDirty(prop);

    }

    private void AddClutch() {

        if (EditorUtility.IsPersistent(prop))
            return;

        GameObject subject = new GameObject("RCCP_Clutch");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(1);
        clutch = subject.gameObject.AddComponent<RCCP_Clutch>();

        EditorUtility.SetDirty(prop);

    }

    private void AddGearbox() {

        if (EditorUtility.IsPersistent(prop))
            return;

        GameObject subject = new GameObject("RCCP_Gearbox");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(2);
        gearbox = subject.gameObject.AddComponent<RCCP_Gearbox>();

        EditorUtility.SetDirty(prop);

    }

    private void AddDifferential() {

        if (EditorUtility.IsPersistent(prop))
            return;

        int differentialIndex = (differentials != null) ? differentials.Length : 0;
        int siblingIndex = 3 + differentialIndex;

        GameObject subject = new GameObject("RCCP_Differential_" + (differentialIndex + 1));
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(siblingIndex);
        RCCP_Differential newDifferential = subject.gameObject.AddComponent<RCCP_Differential>();

        // Auto-connect to an available axle if this is the first differential
        if (differentialIndex == 0 && axles != null) {

            RCCP_Axle[] allAxles = axles.GetComponentsInChildren<RCCP_Axle>(true);

            if (allAxles.Length > 0) {

                // Find the rear axle (lowest Z position)
                float lowestZ = float.MaxValue;
                RCCP_Axle rearAxle = null;

                for (int i = 0; i < allAxles.Length; i++) {

                    if (allAxles[i].leftWheelCollider != null) {

                        float zPos = allAxles[i].leftWheelCollider.transform.localPosition.z;

                        if (zPos < lowestZ) {

                            lowestZ = zPos;
                            rearAxle = allAxles[i];

                        }

                    }

                }

                if (rearAxle != null)
                    newDifferential.connectedAxle = rearAxle;

            }

        }

        // Refresh the differentials array
        GetAllComponents();

        EditorUtility.SetDirty(prop);

    }

    private void AddAxles() {

        if (EditorUtility.IsPersistent(prop))
            return;

        GameObject subject = new GameObject("RCCP_Axles");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(4);
        axles = subject.gameObject.AddComponent<RCCP_Axles>();

        EditorUtility.SetDirty(prop);

    }

    private void AddAxle() {

        if (EditorUtility.IsPersistent(prop))
            return;

        GameObject subject = new GameObject("RCCP_Axle_New");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(4);
        RCCP_Axle axle = subject.gameObject.AddComponent<RCCP_Axle>();
        axle.gameObject.name = "RCCP_Axle_New";
        axle.isBrake = true;
        axle.isHandbrake = true;

        EditorUtility.SetDirty(prop);

    }

    private void AddInputs() {

        if (EditorUtility.IsPersistent(prop))
            return;

        GameObject subject = new GameObject("RCCP_Inputs");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(5);
        inputs = subject.gameObject.AddComponent<RCCP_Input>();

        EditorUtility.SetDirty(prop);

    }

    private void AddAero() {

        if (EditorUtility.IsPersistent(prop))
            return;

        GameObject subject = new GameObject("RCCP_Aero");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(6);
        aero = subject.gameObject.AddComponent<RCCP_AeroDynamics>();

        EditorUtility.SetDirty(prop);

    }

    private void AddAudio() {

        if (EditorUtility.IsPersistent(prop))
            return;

        GameObject subject = new GameObject("RCCP_Audio");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(7);
        audio = subject.gameObject.AddComponent<RCCP_Audio>();

        EditorUtility.SetDirty(prop);

    }

    private void AddCustomizer() {

        if (EditorUtility.IsPersistent(prop))
            return;

        GameObject subject = new GameObject("RCCP_Customizer");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(8);
        customizer = subject.gameObject.AddComponent<RCCP_Customizer>();

        EditorUtility.SetDirty(prop);

    }

    private void AddStability() {

        if (EditorUtility.IsPersistent(prop))
            return;

        GameObject subject = new GameObject("RCCP_Stability");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(9);
        stability = subject.gameObject.AddComponent<RCCP_Stability>();

        EditorUtility.SetDirty(prop);

    }

    private void AddLights() {

        if (EditorUtility.IsPersistent(prop))
            return;

        GameObject subject = new GameObject("RCCP_Lights");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(10);
        lights = subject.gameObject.AddComponent<RCCP_Lights>();

        EditorUtility.SetDirty(prop);

    }

    private void AddDamage() {

        if (EditorUtility.IsPersistent(prop))
            return;

        GameObject subject = new GameObject("RCCP_Damage");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(11);
        damage = subject.gameObject.AddComponent<RCCP_Damage>();

        EditorUtility.SetDirty(prop);

    }

    private void AddParticles() {

        if (EditorUtility.IsPersistent(prop))
            return;

        GameObject subject = new GameObject("RCCP_Particles");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(12);
        particles = subject.gameObject.AddComponent<RCCP_Particles>();

        EditorUtility.SetDirty(prop);

    }

    private void AddLOD() {

        if (EditorUtility.IsPersistent(prop))
            return;

        GameObject subject = new GameObject("RCCP_LOD");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(13);
        lod = subject.gameObject.AddComponent<RCCP_Lod>();

        EditorUtility.SetDirty(prop);

    }

    private void AddOtherAddons() {

        if (EditorUtility.IsPersistent(prop))
            return;

        GameObject subject = new GameObject("RCCP_OtherAddons");
        subject.transform.SetParent(prop.transform, false);
        subject.transform.SetSiblingIndex(13);
        otherAddons = subject.gameObject.AddComponent<RCCP_OtherAddons>();

        EditorUtility.SetDirty(prop);

    }

    private void RemoveEngine() {

        bool isPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(Selection.activeGameObject);

        if (isPrefab) {

            bool disconnectPrefabConnection = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Unpacking Prefab", "This gameobject is connected to a prefab. In order to do remove this component, you'll need to unpack the prefab connection first. After removing the component, you can override your existing prefab with this gameobject.", "Disconnect", "Cancel"));

            if (!disconnectPrefabConnection)
                return;

            PrefabUtility.UnpackPrefabInstance(Selection.activeGameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        }

        EditorApplication.delayCall += () => {

            bool answer = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Removing Component", "Are you sure want to remove this component? You can't undo this operation.", "Remove", "Cancel"));

            if (answer) {

                DestroyImmediate(engine.gameObject);
                engine = null;

                EditorUtility.SetDirty(prop);

            }

        };

    }

    private void RemoveSpecificDifferential(int index) {

        if (differentials == null || index < 0 || index >= differentials.Length || differentials[index] == null)
            return;

        bool isPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(Selection.activeGameObject);

        if (isPrefab) {

            bool disconnectPrefabConnection = EditorUtility.DisplayDialog("Realistic Car Controller Pro | Unpacking Prefab", "This gameobject is connected to a prefab. In order to remove this component, you'll need to unpack the prefab connection first. After removing the component, you can override your existing prefab with this gameobject.", "Disconnect", "Cancel");

            if (!disconnectPrefabConnection)
                return;

            PrefabUtility.UnpackPrefabInstance(Selection.activeGameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        }

        bool answer = EditorUtility.DisplayDialog("Realistic Car Controller Pro | Removing Component", "Are you sure want to remove this differential? You can't undo this operation.", "Remove", "Cancel");

        if (!answer)
            return;

        DestroyImmediate(differentials[index].gameObject);

        // Refresh the differentials array
        GetAllComponents();

        EditorUtility.SetDirty(prop);

    }

    private void RemoveClutch() {

        bool isPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(Selection.activeGameObject);

        if (isPrefab) {

            bool disconnectPrefabConnection = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Unpacking Prefab", "This gameobject is connected to a prefab. In order to do remove this component, you'll need to unpack the prefab connection first. After removing the component, you can override your existing prefab with this gameobject.", "Disconnect", "Cancel"));

            if (!disconnectPrefabConnection)
                return;

            PrefabUtility.UnpackPrefabInstance(Selection.activeGameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        }

        EditorApplication.delayCall += () => {

            bool answer = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Removing Component", "Are you sure want to remove this component? You can't undo this operation.", "Remove", "Cancel"));

            if (answer) {

                DestroyImmediate(clutch.gameObject);
                clutch = null;

                EditorUtility.SetDirty(prop);

            }

        };

    }

    private void RemoveGearbox() {

        bool isPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(Selection.activeGameObject);

        if (isPrefab) {

            bool disconnectPrefabConnection = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Unpacking Prefab", "This gameobject is connected to a prefab. In order to do remove this component, you'll need to unpack the prefab connection first. After removing the component, you can override your existing prefab with this gameobject.", "Disconnect", "Cancel"));

            if (!disconnectPrefabConnection)
                return;

            PrefabUtility.UnpackPrefabInstance(Selection.activeGameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        }

        EditorApplication.delayCall += () => {

            bool answer = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Removing Component", "Are you sure want to remove this component? You can't undo this operation.", "Remove", "Cancel"));

            if (answer) {

                DestroyImmediate(gearbox.gameObject);
                gearbox = null;

                EditorUtility.SetDirty(prop);

            }

        };

    }

    private void RemoveDifferential() {

        bool isPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(Selection.activeGameObject);

        if (isPrefab) {

            bool disconnectPrefabConnection = EditorUtility.DisplayDialog(
                "Realistic Car Controller Pro | Unpacking Prefab",
                "This GameObject is connected to a prefab. To remove this component, you need to unpack the prefab first.",
                "Disconnect", "Cancel"
            );

            if (!disconnectPrefabConnection)
                return;

            PrefabUtility.UnpackPrefabInstance(
                Selection.activeGameObject,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction
            );

        }

        // Delay the actual removal so the dialog can close cleanly
        EditorApplication.delayCall += () => {

            bool answer = EditorUtility.DisplayDialog(
                "Realistic Car Controller Pro | Removing Differential",
                "Are you sure you want to remove this differential? You can't undo this operation.",
                "Remove", "Cancel"
            );

            if (!answer || differentials == null || differentials.Length == 0)
                return;

            // Figure out which differential to remove:
            // 1) If the user has selected one of the differential GameObjects, remove that one
            // 2) Otherwise, remove the last differential in the list
            int indexToRemove = -1;
            GameObject selectedGO = Selection.activeGameObject;

            for (int i = 0; i < differentials.Length; i++) {
                if (differentials[i] != null && differentials[i].gameObject == selectedGO) {
                    indexToRemove = i;
                    break;
                }
            }

            if (indexToRemove < 0)
                indexToRemove = differentials.Length - 1;

            RemoveSpecificDifferential(indexToRemove);

        };

    }


    private void RemoveAxle() {

        bool isPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(Selection.activeGameObject);

        if (isPrefab) {

            bool disconnectPrefabConnection = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Unpacking Prefab", "This gameobject is connected to a prefab. In order to do remove this component, you'll need to unpack the prefab connection first. After removing the component, you can override your existing prefab with this gameobject.", "Disconnect", "Cancel"));

            if (!disconnectPrefabConnection)
                return;

            PrefabUtility.UnpackPrefabInstance(Selection.activeGameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        }

        EditorApplication.delayCall += () => {

            bool answer = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Removing Component", "Are you sure want to remove this component? You can't undo this operation.", "Remove", "Cancel"));

            if (answer) {

                DestroyImmediate(axles.gameObject);
                axles = null;

                EditorUtility.SetDirty(prop);

            }

        };

    }

    private void RemoveInputs() {

        bool isPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(Selection.activeGameObject);

        if (isPrefab) {

            bool disconnectPrefabConnection = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Unpacking Prefab", "This gameobject is connected to a prefab. In order to do remove this component, you'll need to unpack the prefab connection first. After removing the component, you can override your existing prefab with this gameobject.", "Disconnect", "Cancel"));

            if (!disconnectPrefabConnection)
                return;

            PrefabUtility.UnpackPrefabInstance(Selection.activeGameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        }

        EditorApplication.delayCall += () => {

            bool answer = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Removing Component", "Are you sure want to remove this component? You can't undo this operation.", "Remove", "Cancel"));

            if (answer) {

                DestroyImmediate(inputs.gameObject);
                inputs = null;

                EditorUtility.SetDirty(prop);

            }

        };

    }

    private void RemoveAero() {

        bool isPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(Selection.activeGameObject);

        if (isPrefab) {

            bool disconnectPrefabConnection = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Unpacking Prefab", "This gameobject is connected to a prefab. In order to do remove this component, you'll need to unpack the prefab connection first. After removing the component, you can override your existing prefab with this gameobject.", "Disconnect", "Cancel"));

            if (!disconnectPrefabConnection)
                return;

            PrefabUtility.UnpackPrefabInstance(Selection.activeGameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        }

        EditorApplication.delayCall += () => {

            bool answer = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Removing Component", "Are you sure want to remove this component? You can't undo this operation.", "Remove", "Cancel"));

            if (answer) {

                DestroyImmediate(aero.gameObject);
                aero = null;

                EditorUtility.SetDirty(prop);

            }

        };

    }

    private void RemoveAudio() {

        bool isPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(Selection.activeGameObject);

        if (isPrefab) {

            bool disconnectPrefabConnection = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Unpacking Prefab", "This gameobject is connected to a prefab. In order to do remove this component, you'll need to unpack the prefab connection first. After removing the component, you can override your existing prefab with this gameobject.", "Disconnect", "Cancel"));

            if (!disconnectPrefabConnection)
                return;

            PrefabUtility.UnpackPrefabInstance(Selection.activeGameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        }

        EditorApplication.delayCall += () => {

            bool answer = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Removing Component", "Are you sure want to remove this component? You can't undo this operation.", "Remove", "Cancel"));

            if (answer) {

                DestroyImmediate(audio.gameObject);
                audio = null;

                EditorUtility.SetDirty(prop);

            }

        };

    }

    private void RemoveCustomizer() {

        bool isPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(Selection.activeGameObject);

        if (isPrefab) {

            bool disconnectPrefabConnection = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Unpacking Prefab", "This gameobject is connected to a prefab. In order to do remove this component, you'll need to unpack the prefab connection first. After removing the component, you can override your existing prefab with this gameobject.", "Disconnect", "Cancel"));

            if (!disconnectPrefabConnection)
                return;

            PrefabUtility.UnpackPrefabInstance(Selection.activeGameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        }

        EditorApplication.delayCall += () => {

            bool answer = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Removing Component", "Are you sure want to remove this component? You can't undo this operation.", "Remove", "Cancel"));

            if (answer) {

                DestroyImmediate(customizer.gameObject);
                customizer = null;

                EditorUtility.SetDirty(prop);

            }

        };

    }

    private void RemoveStability() {

        bool isPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(Selection.activeGameObject);

        if (isPrefab) {

            bool disconnectPrefabConnection = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Unpacking Prefab", "This gameobject is connected to a prefab. In order to do remove this component, you'll need to unpack the prefab connection first. After removing the component, you can override your existing prefab with this gameobject.", "Disconnect", "Cancel"));

            if (!disconnectPrefabConnection)
                return;

            PrefabUtility.UnpackPrefabInstance(Selection.activeGameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        }

        EditorApplication.delayCall += () => {

            bool answer = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Removing Component", "Are you sure want to remove this component? You can't undo this operation.", "Remove", "Cancel"));

            if (answer) {

                DestroyImmediate(stability.gameObject);
                stability = null;

                EditorUtility.SetDirty(prop);

            }

        };

    }

    private void RemoveLights() {

        bool isPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(Selection.activeGameObject);

        if (isPrefab) {

            bool disconnectPrefabConnection = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Unpacking Prefab", "This gameobject is connected to a prefab. In order to do remove this component, you'll need to unpack the prefab connection first. After removing the component, you can override your existing prefab with this gameobject.", "Disconnect", "Cancel"));

            if (!disconnectPrefabConnection)
                return;

            PrefabUtility.UnpackPrefabInstance(Selection.activeGameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        }

        EditorApplication.delayCall += () => {

            bool answer = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Removing Component", "Are you sure want to remove this component? You can't undo this operation.", "Remove", "Cancel"));

            if (answer) {

                DestroyImmediate(lights.gameObject);
                lights = null;

                EditorUtility.SetDirty(prop);

            }

        };

    }

    private void RemoveDamage() {

        bool isPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(Selection.activeGameObject);

        if (isPrefab) {

            bool disconnectPrefabConnection = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Unpacking Prefab", "This gameobject is connected to a prefab. In order to do remove this component, you'll need to unpack the prefab connection first. After removing the component, you can override your existing prefab with this gameobject.", "Disconnect", "Cancel"));

            if (!disconnectPrefabConnection)
                return;

            PrefabUtility.UnpackPrefabInstance(Selection.activeGameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        }

        EditorApplication.delayCall += () => {

            bool answer = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Removing Component", "Are you sure want to remove this component? You can't undo this operation.", "Remove", "Cancel"));

            if (answer) {

                DestroyImmediate(damage.gameObject);
                damage = null;

                EditorUtility.SetDirty(prop);

            }

        };

    }

    private void RemoveParticles() {

        bool isPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(Selection.activeGameObject);

        if (isPrefab) {

            bool disconnectPrefabConnection = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Unpacking Prefab", "This gameobject is connected to a prefab. In order to do remove this component, you'll need to unpack the prefab connection first. After removing the component, you can override your existing prefab with this gameobject.", "Disconnect", "Cancel"));

            if (!disconnectPrefabConnection)
                return;

            PrefabUtility.UnpackPrefabInstance(Selection.activeGameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        }

        EditorApplication.delayCall += () => {

            bool answer = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Removing Component", "Are you sure want to remove this component? You can't undo this operation.", "Remove", "Cancel"));

            if (answer) {

                DestroyImmediate(particles.gameObject);
                particles = null;

                EditorUtility.SetDirty(prop);

            }

        };

    }

    private void RemoveLOD() {

        bool isPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(Selection.activeGameObject);

        if (isPrefab) {

            bool disconnectPrefabConnection = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Unpacking Prefab", "This gameobject is connected to a prefab. In order to do remove this component, you'll need to unpack the prefab connection first. After removing the component, you can override your existing prefab with this gameobject.", "Disconnect", "Cancel"));

            if (!disconnectPrefabConnection)
                return;

            PrefabUtility.UnpackPrefabInstance(Selection.activeGameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        }

        EditorApplication.delayCall += () => {

            bool answer = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Removing Component", "Are you sure want to remove this component? You can't undo this operation.", "Remove", "Cancel"));

            if (answer) {

                DestroyImmediate(lod.gameObject);
                lod = null;

                EditorUtility.SetDirty(prop);

            }

        };

    }

    private void RemoveOtherAddons() {

        bool isPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(Selection.activeGameObject);

        if (isPrefab) {

            bool disconnectPrefabConnection = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Unpacking Prefab", "This gameobject is connected to a prefab. In order to do remove this component, you'll need to unpack the prefab connection first. After removing the component, you can override your existing prefab with this gameobject.", "Disconnect", "Cancel"));

            if (!disconnectPrefabConnection)
                return;

            PrefabUtility.UnpackPrefabInstance(Selection.activeGameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        }

        EditorApplication.delayCall += () => {

            bool answer = (EditorUtility.DisplayDialog("Realistic Car Controller Pro | Removing Component", "Are you sure want to remove this component? You can't undo this operation.", "Remove", "Cancel"));

            if (answer) {

                DestroyImmediate(otherAddons.gameObject);
                otherAddons = null;

                EditorUtility.SetDirty(prop);

            }

        };

    }

    private void DrawValidationPanel() {

        GUILayout.Space(10f);

        // Toolbar Header
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label("Validation", EditorStyles.boldLabel, GUILayout.Width(70f));

        // Validate Button
        if (GUILayout.Button(EditorGUIUtility.IconContent("d_Refresh"), EditorStyles.toolbarButton, GUILayout.Width(30f))) {
            cachedValidationResults = RCCP_VehicleValidator.ValidateVehicle(prop);
        }

        // Auto-fix Button
        int fixableCount = cachedValidationResults?.Count(r => r.CanAutoFix) ?? 0;
        using (new EditorGUI.DisabledScope(fixableCount == 0)) {
            GUIContent fixContent = new GUIContent(fixableCount > 0 ? $" Fix ({fixableCount})" : " Fix", EditorGUIUtility.IconContent("d_PlayButton").image, "Auto-fix all issues");
            if (GUILayout.Button(fixContent, EditorStyles.toolbarButton, GUILayout.Width(60f))) {
                int fixedCount = RCCP_VehicleValidator.AutoFixAll(cachedValidationResults);
                if (fixedCount > 0) {
                    cachedValidationResults = RCCP_VehicleValidator.ValidateVehicle(prop);
                }
            }
        }

        GUILayout.FlexibleSpace();

        // Summary inline
        if (cachedValidationResults != null && cachedValidationResults.Count > 0) {
            var summary = RCCP_VehicleValidator.GetSummary(cachedValidationResults);

            if (summary.errorCount > 0) {
                GUILayout.Label(EditorGUIUtility.IconContent("console.erroricon.sml"), EditorStyles.miniLabel, GUILayout.Width(20f));
                GUILayout.Label(summary.errorCount.ToString(), EditorStyles.miniLabel, GUILayout.Width(20f));
            }
            if (summary.warningCount > 0) {
                GUILayout.Label(EditorGUIUtility.IconContent("console.warnicon.sml"), EditorStyles.miniLabel, GUILayout.Width(20f));
                GUILayout.Label(summary.warningCount.ToString(), EditorStyles.miniLabel, GUILayout.Width(20f));
            }
            if (summary.infoCount > 0) {
                GUILayout.Label(EditorGUIUtility.IconContent("console.infoicon.sml"), EditorStyles.miniLabel, GUILayout.Width(20f));
                GUILayout.Label(summary.infoCount.ToString(), EditorStyles.miniLabel, GUILayout.Width(20f));
            }
        }

        // Clear/Close
        if (GUILayout.Button(EditorGUIUtility.IconContent("d_clear"), EditorStyles.toolbarButton, GUILayout.Width(30f))) {
            cachedValidationResults = null;
        }

        EditorGUILayout.EndHorizontal();

        if (cachedValidationResults == null) return;

        // Filter Bar
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        GUILayout.Label("Filter:", EditorStyles.miniLabel, GUILayout.Width(40f));

        showErrors = GUILayout.Toggle(showErrors, EditorGUIUtility.IconContent("console.erroricon.sml"), EditorStyles.miniButtonLeft, GUILayout.Width(30f));
        showWarnings = GUILayout.Toggle(showWarnings, EditorGUIUtility.IconContent("console.warnicon.sml"), EditorStyles.miniButtonMid, GUILayout.Width(30f));
        showInfo = GUILayout.Toggle(showInfo, EditorGUIUtility.IconContent("console.infoicon.sml"), EditorStyles.miniButtonRight, GUILayout.Width(30f));

        GUILayout.FlexibleSpace();

        // Category dropdown
        string[] categoryNames = Enum.GetNames(typeof(RCCP_VehicleValidator.Category));
        string[] displayNames = new string[categoryNames.Length + 1];
        displayNames[0] = "All Categories";
        Array.Copy(categoryNames, 0, displayNames, 1, categoryNames.Length);

        int selectedIndex = filterCategory.HasValue ? (int)filterCategory.Value + 1 : 0;
        int newIndex = EditorGUILayout.Popup(selectedIndex, displayNames, EditorStyles.toolbarPopup, GUILayout.Width(120f));

        if (newIndex == 0)
            filterCategory = null;
        else
            filterCategory = (RCCP_VehicleValidator.Category)(newIndex - 1);

        EditorGUILayout.EndHorizontal();

        // Results
        if (cachedValidationResults.Count == 0) {
            EditorGUILayout.HelpBox("No issues found.", MessageType.Info);
            return;
        }

        DrawValidationResults();

    }

    private void DrawValidationResults() {

        // Filter results based on severity and category
        var filteredResults = cachedValidationResults.Where(r =>
            ((r.severity == RCCP_VehicleValidator.Severity.Error && showErrors) ||
             (r.severity == RCCP_VehicleValidator.Severity.Warning && showWarnings) ||
             (r.severity == RCCP_VehicleValidator.Severity.Info && showInfo)) &&
            (!filterCategory.HasValue || r.category == filterCategory.Value)
        ).ToList();

        if (filteredResults.Count == 0) {
            EditorGUILayout.LabelField("No matching issues.", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        // Group by category
        var groupedResults = filteredResults.GroupBy(r => r.category).OrderBy(g => g.Key);

        validationScrollPosition = EditorGUILayout.BeginScrollView(validationScrollPosition, GUILayout.MaxHeight(250f));

        foreach (var group in groupedResults) {

            // Category Header
            EditorGUILayout.LabelField(group.Key.ToString(), EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            foreach (var result in group) {
                DrawValidationResultItem(result);
            }
            EditorGUI.indentLevel--;

            GUILayout.Space(5f);

        }

        EditorGUILayout.EndScrollView();

    }

    private void DrawValidationResultItem(RCCP_VehicleValidator.ValidationResult result) {

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        // Icon
        string iconName = result.severity switch {
            RCCP_VehicleValidator.Severity.Error => "console.erroricon.sml",
            RCCP_VehicleValidator.Severity.Warning => "console.warnicon.sml",
            _ => "console.infoicon.sml"
        };
        GUILayout.Label(EditorGUIUtility.IconContent(iconName), GUILayout.Width(20f), GUILayout.Height(20f));

        // Message & Suggestion
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(result.message, EditorStyles.wordWrappedMiniLabel);
        if (!string.IsNullOrEmpty(result.suggestion)) {
            // Slightly dimmer color for suggestion
            Color originalColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            EditorGUILayout.LabelField(result.suggestion, EditorStyles.miniLabel);
            GUI.color = originalColor;
        }
        EditorGUILayout.EndVertical();

        // Action Buttons
        if (result.targetObject != null) {
            // Select button
            if (GUILayout.Button(EditorGUIUtility.IconContent("d_Search Icon"), EditorStyles.iconButton, GUILayout.Width(28f), GUILayout.Height(24f))) {
                Selection.activeObject = result.targetObject;
                EditorGUIUtility.PingObject(result.targetObject);
            }
        }

        if (result.CanAutoFix) {
            // Fix button
            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_Toggle Icon").image, "Auto-Fix"), EditorStyles.iconButton, GUILayout.Width(28f), GUILayout.Height(24f))) {
                result.autoFix?.Invoke();
                cachedValidationResults = RCCP_VehicleValidator.ValidateVehicle(prop);
            }
        }

        EditorGUILayout.EndHorizontal();

    }

    private void CheckMissingAxleManager() {

        bool axleFound = false;

        RCCP_Axle[] foundAxles = prop.GetComponentsInChildren<RCCP_Axle>(true);

        if (foundAxles.Length >= 1)
            axleFound = true;

        if (axleFound) {

            bool axleManagerFound = false;

            RCCP_Axles foundAxleManager = prop.GetComponentInChildren<RCCP_Axles>(true);

            if (foundAxleManager != null)
                axleManagerFound = true;

            if (!axleManagerFound) {

                GameObject newAxleManager = new GameObject("RCCP_Axles");
                newAxleManager.transform.SetParent(prop.transform, false);
                axles = newAxleManager.AddComponent<RCCP_Axles>();
                Debug.Log("Found missing axle manager on " + prop.transform.name + ". Adding it...");

            } else {

                for (int i = 0; i < foundAxles.Length; i++) {

                    if (foundAxles[i].transform.parent != foundAxleManager.transform)
                        foundAxles[i].transform.SetParent(foundAxleManager.transform, false);

                }

            }

        }

    }

    /// <summary>
    /// Draws the per-vehicle behavior preset section in the Inspector.
    /// </summary>
    private void DrawPerVehicleBehaviorSection() {

        EditorGUILayout.LabelField("Per-Vehicle Behavior", EditorStyles.boldLabel);

        SerializedProperty useCustomBehaviorProp = serializedObject.FindProperty("useCustomBehavior");
        SerializedProperty customBehaviorIndexProp = serializedObject.FindProperty("customBehaviorIndex");

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.PropertyField(useCustomBehaviorProp, new GUIContent("Use Custom Behavior", "When enabled, this vehicle uses its own behavior preset instead of the global preset from RCCP_Settings."));

        if (useCustomBehaviorProp.boolValue) {

            EditorGUI.indentLevel++;

            // Get behavior names for dropdown
            string[] behaviorNames = GetBehaviorNames();
            int currentIndex = customBehaviorIndexProp.intValue;

            // Clamp index to valid range
            if (currentIndex < 0 || currentIndex >= behaviorNames.Length)
                currentIndex = 0;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Custom Behavior Preset", GUILayout.Width(EditorGUIUtility.labelWidth));

            // Show colored indicator to make custom behavior visually distinct
            Color originalBgColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.4f, 0.8f, 1f); // Cyan tint

            int newIndex = EditorGUILayout.Popup(currentIndex, behaviorNames);

            GUI.backgroundColor = originalBgColor;

            if (newIndex != currentIndex)
                customBehaviorIndexProp.intValue = newIndex;

            EditorGUILayout.EndHorizontal();

            // Show info box explaining the custom behavior
            string behaviorName = (newIndex >= 0 && newIndex < behaviorNames.Length) ? behaviorNames[newIndex] : "Unknown";
            EditorGUILayout.HelpBox(
                $"This vehicle uses the '{behaviorName}' behavior preset and ignores the global behavior from RCCP_Settings.",
                MessageType.Info);

            EditorGUI.indentLevel--;

        }

        if (EditorGUI.EndChangeCheck())
            serializedObject.ApplyModifiedProperties();

    }

    /// <summary>
    /// Gets the names of all behavior presets from RCCP_Settings.
    /// </summary>
    private string[] GetBehaviorNames() {

        RCCP_Settings settings = RCCP_Settings.Instance;

        if (settings == null || settings.behaviorTypes == null || settings.behaviorTypes.Length == 0)
            return new string[] { "None" };

        string[] names = new string[settings.behaviorTypes.Length];

        for (int i = 0; i < settings.behaviorTypes.Length; i++) {

            if (settings.behaviorTypes[i] != null && !string.IsNullOrEmpty(settings.behaviorTypes[i].behaviorName))
                names[i] = settings.behaviorTypes[i].behaviorName;
            else
                names[i] = $"Behavior {i}";

        }

        return names;

    }

}
#endif
