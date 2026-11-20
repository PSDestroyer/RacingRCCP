using UnityEngine;
using UnityEngine.SceneManagement;
using _Assets._PlatformSpeciffics.Switch;
using HalvaStudio.Save;

public class Init : MonoBehaviour
{
    private void Awake()
    {
        Debug.Log("INIT SCENE AWAKE!");
#if UNITY_SWITCH && !UNITY_EDITOR
        NintendoSave.Initialize();
#endif
    }

    private void Start()
    {
#if UNITY_STANDALONE && !UNITY_SWITCH
        SceneManager.LoadScene("Menu");
#else
        SceneManager.LoadScene(SaveManager.Instance.saveData.PlayerName == null ? "Username" : "Menu");
#endif
    }

}
