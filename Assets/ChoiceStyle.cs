using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
public class ChoiceStyle : MonoBehaviour
{
    [SerializeField]private string SceneName = "GameplayTestScene";
    
    public void PlayRacing()
    {
        SelectedCareerMission.Mission = null;
        SelectedGameMode.RaceType = RaceType.Racing;
        Debug.Log("MENU selected: " + SelectedGameMode.RaceType);
        LoadGameplay();
    }

    public void PlayDrift()
    {
        SelectedCareerMission.Mission = null;
        SelectedGameMode.RaceType = RaceType.FreeDrift;
        Debug.Log("MENU selected: " + SelectedGameMode.RaceType);
        LoadGameplay();
    }

    private void LoadGameplay()
    {
        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.LoadScene(SceneName);
        }
        else
        {
            SceneManager.LoadScene(SceneName);
        }
    }
}
