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

[CustomEditor(typeof(RCCP_Records))]
public class RCCP_RecordsEditor : Editor {

    RCCP_Records prop;
    GUISkin skin;
    static RCCP_Recorder selectedRecorder;
    static float replayStartTime = 0f;

    Color originalGUIColor;

    private void OnEnable() {

        skin = Resources.Load<GUISkin>("RCCP_Gui");

    }

    public override void OnInspectorGUI() {

        originalGUIColor = GUI.color;
        prop = (RCCP_Records)target;
        serializedObject.Update();
        GUI.skin = skin;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("RCCP Records Editor Window", EditorStyles.boldLabel);
        GUI.color = new Color(.75f, 1f, .75f);
        EditorGUILayout.LabelField("This editor will keep update necessary .asset files in your project for RCCP. Don't change directory of the ''Resources/RCCP Assets''.", EditorStyles.helpBox);
        GUI.color = originalGUIColor;
        EditorGUILayout.Space();

        GUI.color = new Color(.75f, 1f, .75f);
        EditorGUILayout.LabelField("All recorded clips are stored here. Replaying any recorded clip is so easy. Just use ''RCCP.StartStopReplay(recordIndex or recordClip)'' in your script!", EditorStyles.helpBox);
        GUI.color = originalGUIColor;
        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical(GUI.skin.box);

        GUILayout.Label("Replay Target", EditorStyles.boldLabel);

        selectedRecorder = (RCCP_Recorder)EditorGUILayout.ObjectField(
            "Recorder",
            selectedRecorder,
            typeof(RCCP_Recorder),
            true
        );

        replayStartTime = EditorGUILayout.FloatField("Start Time", replayStartTime);
        replayStartTime = Mathf.Max(0f, replayStartTime);

        using (new EditorGUI.DisabledScope(!Application.isPlaying || selectedRecorder == null)) {

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Play Selected Record") && prop.records != null && prop.records.Count > 0)
                selectedRecorder.Play(prop.records[0], replayStartTime);

            if (GUILayout.Button("Stop Replay"))
                selectedRecorder.Stop();

            EditorGUILayout.EndHorizontal();

        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Record replay buttons are available in Play Mode.", MessageType.Info);
        else if (selectedRecorder == null)
            EditorGUILayout.HelpBox("Assign an RCCP_Recorder from the scene to replay a stored record.", MessageType.Info);

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical(GUI.skin.box);

        GUILayout.Label("Recorded Clips", EditorStyles.boldLabel);

        EditorGUILayout.Space();

        EditorGUI.indentLevel++;

        if (prop.records != null) {

            for (int i = 0; i < prop.records.Count; i++) {

                EditorGUILayout.BeginHorizontal(GUI.skin.box);

                EditorGUILayout.LabelField(prop.records[i].recordName);

                using (new EditorGUI.DisabledScope(!Application.isPlaying || selectedRecorder == null)) {

                    if (GUILayout.Button("Play", GUILayout.Width(55f)))
                        selectedRecorder.Play(prop.records[i], replayStartTime);

                }

                GUI.color = Color.red;

                using (new EditorGUI.DisabledScope(!Application.isPlaying || selectedRecorder == null)) {

                    GUI.color = new Color(1f, .8f, .2f);

                    if (GUILayout.Button("Stop", GUILayout.Width(55f)))
                        selectedRecorder.Stop();

                }

                GUI.color = Color.red;

                if (GUILayout.Button("X", GUILayout.Width(25f)))
                    DeleteRecord(prop.records[i]);

                GUI.color = originalGUIColor;

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();

            }

        }

        EditorGUI.indentLevel--;

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        GUI.color = Color.red;

        if (GUILayout.Button("Delete All Records"))
            DeleteAllRecords();

        GUI.color = originalGUIColor;

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Developed by Ekrem Bugra Ozdoganlar\nBoneCracker Games", EditorStyles.centeredGreyMiniLabel, GUILayout.MaxHeight(50f));

        serializedObject.ApplyModifiedProperties();

        if (GUI.changed)
            EditorUtility.SetDirty(prop);

    }

    private void DeleteRecord(RCCP_Recorder.RecordedClip record) {

        prop.records.Remove(record);

    }

    private void DeleteAllRecords() {

        prop.records.Clear();

    }

}
#endif
