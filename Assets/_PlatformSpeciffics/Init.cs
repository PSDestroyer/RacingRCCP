using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using _Assets._PlatformSpeciffics.Switch;
using HalvaStudio.Save;

public class Init : MonoBehaviour
{
    private void Awake()
    {
        Debug.Log("INIT SCENE AWAKE!");
#if UNITY_SWITCH && !UNITY_EDITOR
        NintendoSave.Initialize();
#else
        Debug.Log("Nintendo initialization is skipped outside a Switch build.");
#endif
    }

    private void Start()
    {
#if UNITY_SWITCH && !UNITY_EDITOR
        string nickname = NintendoManager.GetNickname();

        if (!string.IsNullOrWhiteSpace(nickname) &&
            SaveManager.Instance != null &&
            SaveManager.Instance.saveData != null)
        {
            SaveManager.Instance.saveData.PlayerName = nickname;
            SaveManager.Instance.Save(true);
        }
#endif

        UnityEngine.SceneManagement.SceneManager.LoadScene(1);
    }

}
