using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
public class CareerUIController : MonoBehaviour
{
    [SerializeField] private GarageUIController garageUIController;
    [SerializeField] private Transform missionContent;
    [SerializeField] private TMP_Text TitleText;
    [SerializeField] private MissionButton missionButtonPrefab;

    public void OpenTournament(TournamentSO tournament)
    {
        if (TitleText != null)
        {
            TitleText.text = tournament.tournamentName;
        }
        BuildMissionList(tournament);
        garageUIController.OpenPanel(UIPanelType.CareerMissions);
    }

    private void BuildMissionList(TournamentSO tournament)
    {
        foreach (Transform child in missionContent)
        {
            Destroy(child.gameObject);
        }

        foreach (MissionSO mission in tournament.missions)
        {
            MissionButton button = Instantiate(missionButtonPrefab, missionContent);
            button.Setup(mission, this);
        }
    }

    public void StartMission(MissionSO mission)
    {
        SelectedCareerMission.Mission = mission;

        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.LoadScene(mission.sceneName);
        }
        else
        {
            SceneManager.LoadScene(mission.sceneName);
        }
    }
}
