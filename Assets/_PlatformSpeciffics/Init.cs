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
#if UNITY_EDITOR
        Debug.Log("Nintendo Initialize!");
#else
        NintendoSave.Initialize();
#endif
    }

    private void Start()
    {
        //LoadingScreen.LoadScene("Menu");
        if(SaveManager.Instance.saveData.PlayerName== null)
        UnityEngine.SceneManagement.SceneManager.LoadScene(1);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(2);

    }

}
