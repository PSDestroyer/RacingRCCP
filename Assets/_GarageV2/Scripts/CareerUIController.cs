using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;
public class CareerUIController : MonoBehaviour
{
    [SerializeField] private GarageUIController garageUIController;
    [SerializeField] private Transform missionContent;
    [SerializeField] private TMP_Text TitleText;
    [SerializeField] private MissionButton missionButtonPrefab;
    private Coroutine selectMissionCoroutine;

    public void OpenTournament(TournamentSO tournament)
    {
        if (TitleText != null)
        {
            TitleText.text = tournament.tournamentName;
        }
        BuildMissionList(tournament);
        garageUIController.OpenPanel(UIPanelType.CareerMissions);

        if (selectMissionCoroutine != null)
            StopCoroutine(selectMissionCoroutine);

        selectMissionCoroutine = StartCoroutine(SelectFirstMissionButtonNextFrame());
    }

    private void BuildMissionList(TournamentSO tournament)
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

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

    private IEnumerator SelectFirstMissionButtonNextFrame()
    {
        yield return null;

        if (EventSystem.current == null || missionContent == null)
            yield break;

        if (missionContent.childCount == 0)
            yield break;

        MissionButton missionButton = missionContent.GetChild(0).GetComponent<MissionButton>();
        if (missionButton == null || missionButton.Button == null)
            yield break;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(missionButton.Button.gameObject);
        selectMissionCoroutine = null;
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
