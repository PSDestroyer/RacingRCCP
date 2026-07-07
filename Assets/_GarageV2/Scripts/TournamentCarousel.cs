using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class TournamentCarousel : MonoBehaviour
{
    [SerializeField] private CareerUIController careerUIController;
    
    [Header("Data")]
    [SerializeField] private List<TournamentSO> tournaments;
    
    [Header("UI")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content;
    [SerializeField] private TournamentCard cardPrefab;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button selectButton;
    
    [Header("Animation")]
    [SerializeField] private float slideDuration = 0.45f;

    private readonly List<TournamentCard> cards = new();
    private int currentIndex;
    private bool isSliding;

    private void Awake()
    {
        AutoBindButtons();
    }

    private void OnEnable()
    {
        FocusPrimarySelection();
    }

    private void Start()
    {
        BuildCards();
        SetScrollPosition(0);
        PlayCardAnimations(0, 1);
        FocusPrimarySelection();
    }

    private void Update()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame)
                Previous();

            if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)
                Next();

            if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)
                SelectCurrentTournament();
        }

        if (Gamepad.current != null)
        {
            if (Gamepad.current.leftShoulder.wasPressedThisFrame)
                Previous();

            if (Gamepad.current.rightShoulder.wasPressedThisFrame)
                Next();

            if (Gamepad.current.dpad.left.wasPressedThisFrame)
                Previous();

            if (Gamepad.current.dpad.right.wasPressedThisFrame)
                Next();

            if (Gamepad.current.buttonSouth.wasPressedThisFrame)
                SelectCurrentTournament();
        }
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
        TryMove(1, false);
    }

    public void Previous()
    {
        TryMove(-1, false);
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

    public void FocusPrimarySelection()
    {
        if (EventSystem.current == null)
            return;

        GameObject target = null;

        if (selectButton != null && selectButton.gameObject.activeInHierarchy && selectButton.IsInteractable())
            target = selectButton.gameObject;
        else if (nextButton != null && nextButton.gameObject.activeInHierarchy && nextButton.IsInteractable())
            target = nextButton.gameObject;
        else if (previousButton != null && previousButton.gameObject.activeInHierarchy && previousButton.IsInteractable())
            target = previousButton.gameObject;

        if (target == null)
            return;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(target);
    }
    
    private bool TryMove(int direction, bool selectAfterMove)
    {
        if (cards.Count == 0 || isSliding)
        {
            return false;
        }

        int targetIndex = currentIndex + direction;

        if (targetIndex < 0 || targetIndex >= cards.Count)
        {
            return false;
        }

        currentIndex = targetIndex;
        SnapToIndex(currentIndex, direction, selectAfterMove);
        return true;
    }

    private void SnapToIndex(int index, int direction, bool selectAfterMove = false)
    {
        if (cards.Count == 0)
        {
            return;
        }

        if (cards.Count <= 1)
        {
            SetScrollPosition(0);
            PlayCardAnimations(0, direction);
            if (selectAfterMove)
                SelectCurrentTournament();
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
            .OnComplete(() =>
            {
                isSliding = false;
                if (selectAfterMove)
                    SelectCurrentTournament();
                else
                    FocusPrimarySelection();
            });
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

    private void AutoBindButtons()
    {
        if (previousButton == null)
            previousButton = FindButtonByName("left");

        if (nextButton == null)
            nextButton = FindButtonByName("right");

        if (selectButton == null)
            selectButton = FindButtonByName("select");
    }

    private Button FindButtonByName(string token)
    {
        Button[] buttons = GetComponentsInParent<Button>(true);
        foreach (Button button in buttons)
        {
            if (button == null)
                continue;

            string objectName = button.name.ToLowerInvariant();
            if (objectName.Contains(token))
                return button;
        }

        return null;
    }
}
