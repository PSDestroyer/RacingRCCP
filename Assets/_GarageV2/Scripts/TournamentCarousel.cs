using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
public class TournamentCarousel : MonoBehaviour
{
    [SerializeField] private CareerUIController careerUIController;
    
    [Header("Data")]
    [SerializeField] private List<TournamentSO> tournaments;
    
    [Header("UI")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content;
    [SerializeField] private TournamentCard cardPrefab;

    private readonly List<TournamentCard> cards = new();
    private int currentIndex;

    private void Start()
    {
        BuildCards();
        SnapToIndex(0);
    }

    private void BuildCards()
    {
        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }
        cards.Clear();

        foreach (TournamentSO tournament in tournaments)
        {
            TournamentCard card = Instantiate(cardPrefab, content);
            card.Setup(tournament);
            cards.Add(card);
        }
    }

    public void Next()
    {
        if (cards.Count == 0)
        {
            return;
        }
        
        currentIndex++;
        if (currentIndex >= cards.Count)
        {
            currentIndex = cards.Count - 1;
        }
        
        SnapToIndex(currentIndex);
    }

    public void Previous()
    {
        if (cards.Count == 0)
        {
            return;
        }
        
        currentIndex--;

        if (currentIndex < 0)
        {
            currentIndex = 0;
        }
        
        SnapToIndex(currentIndex);
    }

    public void SelectCurrentTournament()
    {
        if (cards.Count == 0)
        {
            return;
        }

        TournamentSO selectedTournament = cards[currentIndex].Tournament;
        careerUIController.OpenTournament(selectedTournament);
    }
    
    private void SnapToIndex(int index)
    {
        if (cards.Count <= 1)
        {
            scrollRect.horizontalNormalizedPosition = 0f;
            return;
        }
        
        float narmalized = (float)index / (cards.Count - 1);
        scrollRect.horizontalNormalizedPosition = narmalized;
    }
}
