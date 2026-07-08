using UnityEngine;
using TMPro;
using UnityEngine.UI;
public class TournamentCard : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image icon;
    [SerializeField] private Image bgImage;
    [SerializeField] private float backgroundPanDistance = 240f;
    
    private TournamentSO tournament;
    private RectTransform backgroundRect;
    
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
            backgroundRect = bgImage.rectTransform;
            SetBackgroundOffsetNormalized(0f);
        }
    }

    public void SetBackgroundOffsetNormalized(float normalizedOffset)
    {
        if (backgroundRect == null)
            backgroundRect = bgImage != null ? bgImage.rectTransform : null;

        if (backgroundRect == null)
            return;

        float clamped = Mathf.Clamp01(normalizedOffset);
        Vector2 anchoredPosition = backgroundRect.anchoredPosition;
        anchoredPosition.x = Mathf.Lerp(0f, -backgroundPanDistance, clamped);
        backgroundRect.anchoredPosition = anchoredPosition;
    }
}
