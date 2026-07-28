using UnityEngine;
using TMPro;
using UnityEngine.UI;
public class TournamentButton : MonoBehaviour
{
    [SerializeField] private TournamentSO tournament;
    [SerializeField] private CareerUIController careerUIController;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image icon;

    private void Start()
    {
        if (tournament != null && nameText != null)
        {
            nameText.text = UILocalization.GetKnownText(tournament.tournamentName);
        }

        if (tournament != null && icon != null)
        {
            icon.sprite = tournament.icon;
        }
        
        Button button = GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OpenTournament);
    }

    public void OpenTournament()
    {
        careerUIController.OpenTournament(tournament);
    }
}
