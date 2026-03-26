using UnityEngine;
using UnityEditor;
using System.IO;

public class SaveManagerEditor : EditorWindow
{
    private string filePath; // Path to the save file

    [MenuItem("Window/Save File Deletion Window")]
    public static void ShowWindow()
    {
        GetWindow<SaveManagerEditor>("Save File Deletion");
    }

    private void OnEnable()
    {
        // Initialize filePath with default value
        filePath = Path.Combine(Application.persistentDataPath, "save.json");
    }

    private void OnGUI()
    {
        GUILayout.Label("Save File Deletion", EditorStyles.boldLabel);

        // Display and update the file path
        filePath = EditorGUILayout.TextField("File Path", filePath);

        if (GUILayout.Button("Delete Save File"))
        {
            DeleteSaveFile();
        }

        if (GUILayout.Button("Delete Player Prefs"))
        {
            PlayerPrefs.DeleteAll();
            Debug.Log("PlayerPrefs deleted.");
        }

        if (GUILayout.Button("Delete Everything"))
        {
            DeleteSaveFile();
            PlayerPrefs.DeleteAll();
            Debug.Log("All data deleted.");
        }
    }

    private void DeleteSaveFile()
    {
        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
                Debug.Log("Save file deleted at path: " + filePath);
            }
            catch (IOException ex)
            {
                Debug.LogError("Error deleting file: " + ex.Message);
            }
        }
        else
        {
            Debug.LogWarning("Save file does not exist at path: " + filePath);
        }
    }
}