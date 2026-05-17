using UnityEngine;
using TMPro;
using UnityEngine.UI;
public class TournamentCard : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image icon;
    [SerializeField] private Image bgImage;
    
    private TournamentSO tournament;
    
    public TournamentSO Tournament => tournament;

    public void Setup(TournamentSO tournament)
    {
        this.tournament = tournament;

        if (nameText != null)
        {
            nameText.text = tournament.tournamentName;
        }

        if (descriptionText != null)
        {
            descriptionText.text = tournament.description;
        }

        if (icon != null)
        {
            icon.sprite = tournament.icon;
        }

        if (bgImage != null)
        {
            bgImage.sprite = tournament.backgroundImage;
        }
    }
}
