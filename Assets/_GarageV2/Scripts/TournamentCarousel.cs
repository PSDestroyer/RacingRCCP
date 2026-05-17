using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using DG.Tweening;

public class TournamentCarousel : MonoBehaviour
{
    [SerializeField] private CareerUIController careerUIController;
    
    [Header("Data")]
    [SerializeField] private List<TournamentSO> tournaments;
    
    [Header("UI")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content;
    [SerializeField] private TournamentCard cardPrefab;
    
    [Header("Animation")]
    [SerializeField] private float slideDuration = 0.45f;

    private readonly List<TournamentCard> cards = new();
    private int currentIndex;
    private bool isSliding;

    private void Start()
    {
        BuildCards();
        SetScrollPosition(0);
        PlayCardAnimations(0, 1);
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
        if (cards.Count == 0 || isSliding)
        {
            return;
        }

        if (currentIndex >= cards.Count - 1)
        {
            return;
        }
        
        currentIndex++;
        SnapToIndex(currentIndex, 1);
    }

    public void Previous()
    {
        if (cards.Count == 0 || isSliding)
        {
            return;
        }

        if (currentIndex <= 0)
        {
            return;
        }
        
        currentIndex--;
        SnapToIndex(currentIndex, -1);
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
    
    private void SnapToIndex(int index, int direction)
    {
        if (cards.Count == 0)
        {
            return;
        }

        if (cards.Count <= 1)
        {
            SetScrollPosition(0);
            PlayCardAnimations(0, direction);
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        float targetPosition = GetNormalizedPosition(index);

        if (scrollRect != null)
        {
            scrollRect.StopMovement();
        }

        scrollRect.DOKill();
        PlayCardAnimations(index, direction);
        isSliding = true;

        scrollRect.DOHorizontalNormalizedPos(targetPosition, slideDuration)
            .SetEase(Ease.OutCubic)
            .OnComplete(() => isSliding = false);
    }

    private void SetScrollPosition(int index)
    {
        if (cards.Count == 0)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        scrollRect.horizontalNormalizedPosition = GetNormalizedPosition(index);
    }

    private void PlayCardAnimations(int index, int direction)
    {
        UIJellySlide[] animations = cards[index].GetComponentsInChildren<UIJellySlide>();
        foreach (UIJellySlide animation in animations)
        {
            animation.PlayIn(direction);
        }
    }

    private float GetNormalizedPosition(int index)
    {
        if (cards.Count <= 1)
        {
            return 0f;
        }

        return (float)index / (cards.Count - 1);
    }
}
