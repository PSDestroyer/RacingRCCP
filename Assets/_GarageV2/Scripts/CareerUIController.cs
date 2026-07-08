using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;
using DG.Tweening.Core;

public class CareerUIController : MonoBehaviour
{
    [SerializeField] private GarageUIController garageUIController;
    [SerializeField] private Transform missionContent;
    [SerializeField] private TMP_Text TitleText;
    [SerializeField] private MissionButton missionButtonPrefab;

    [Header("Embedded Play Layout")]
    [SerializeField] private TournamentCarousel tournamentCarousel;
    [SerializeField] private Vector2 missionCardSize = new(350f, 160f);
    [SerializeField] private float missionColumnSpacing = 30f;
    [SerializeField] private float missionRowSpacing = 32f;
    [SerializeField] private float contentPaddingLeft = 24f;
    [SerializeField] private float contentPaddingRight = 24f;
    [SerializeField] private float contentPaddingTop = 12f;
    [SerializeField] private float contentPaddingBottom = 12f;

    private Coroutine selectMissionCoroutine;
    private readonly List<MissionButton> missionButtons = new();
    private ScrollRect missionScrollRect;
    private RectTransform missionContentRect;
    private RectTransform missionViewportRect;
    private RectTransform missionScrollRootRect;
    private CanvasGroup missionScrollCanvasGroup;
    private bool embeddedLayoutConfigured;
    private int lastTrackedMissionIndex = -1;
    private Tween missionScrollTween;
    private Sequence missionRefreshSequence;
    private TournamentSO currentTournament;

    public void OpenTournament(TournamentSO tournament, bool openDedicatedPanel = false)
    {
        ConfigureEmbeddedMissionLayout();
        currentTournament = tournament;

        if (TitleText != null)
        {
            TitleText.text = tournament.tournamentName;
        }

        BuildMissionList(tournament);
        AnimateMissionPanelRefresh();

        if (openDedicatedPanel && garageUIController != null)
            garageUIController.OpenPanel(UIPanelType.CareerMissions);

        RequestMissionFocus();
    }

    public void PreviewTournament(TournamentSO tournament)
    {
        OpenTournament(tournament, false);
    }

    private void Update()
    {
        HandleMissionNavigationInput();
        MaintainMissionFocus();
        TrackSelectedMission();
    }

    private void BuildMissionList(TournamentSO tournament)
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        missionButtons.Clear();

        foreach (Transform child in missionContent)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < tournament.missions.Count; i++)
        {
            MissionSO mission = tournament.missions[i];
            bool isCompleted = CareerMissionProgress.IsMissionCompleted(tournament, mission);
            bool isLocked = !CareerMissionProgress.IsMissionUnlocked(tournament, i);
            MissionButton button = Instantiate(missionButtonPrefab, missionContent);
            button.Setup(mission, this, isLocked, isCompleted);
            missionButtons.Add(button);
        }

        lastTrackedMissionIndex = -1;
        LayoutMissionButtons();
        ConfigureMissionNavigation();

        int firstUnlockedIndex = GetFirstUnlockedMissionIndex();
        EnsureMissionVisible(firstUnlockedIndex);
    }

    public void RequestMissionFocus()
    {
        if (selectMissionCoroutine != null)
            StopCoroutine(selectMissionCoroutine);

        selectMissionCoroutine = StartCoroutine(SelectFirstMissionButtonNextFrame());
    }

    private IEnumerator SelectFirstMissionButtonNextFrame()
    {
        int firstUnlockedIndex = GetFirstUnlockedMissionIndex();

        for (int i = 0; i < 3; i++)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            TryFocusMissionAtIndex(firstUnlockedIndex);
        }

        selectMissionCoroutine = null;
    }

    public void StartMission(MissionSO mission)
    {
        if (mission == null || currentTournament == null)
            return;

        int missionIndex = currentTournament.missions != null ? currentTournament.missions.IndexOf(mission) : -1;
        if (missionIndex < 0 || !CareerMissionProgress.IsMissionUnlocked(currentTournament, missionIndex))
            return;

        SelectedCareerMission.Tournament = currentTournament;
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

    public void OnMissionButtonSelected(MissionButton missionButton)
    {
        if (missionButton == null)
            return;

        int index = missionButtons.IndexOf(missionButton);
        if (index < 0)
            return;

        lastTrackedMissionIndex = index;
        if (EventSystem.current != null && missionButton.Button != null &&
            EventSystem.current.currentSelectedGameObject != missionButton.Button.gameObject)
        {
            EventSystem.current.SetSelectedGameObject(missionButton.Button.gameObject);
        }

        EnsureMissionVisible(index);
        UpdateTournamentBackground(index);
    }

    private void ConfigureEmbeddedMissionLayout()
    {
        if (embeddedLayoutConfigured)
            return;

        if (missionContentRect == null)
            missionContentRect = missionContent as RectTransform;

        if (missionScrollRect == null && missionContentRect != null)
            missionScrollRect = missionContentRect.GetComponentInParent<ScrollRect>(true);

        if (missionScrollRect != null)
            missionViewportRect = missionScrollRect.viewport;

        if (missionScrollRect != null)
            missionScrollRootRect = missionScrollRect.GetComponent<RectTransform>();

        if (tournamentCarousel == null)
            tournamentCarousel = GetComponentInParent<RectTransform>(true)?.GetComponentInChildren<TournamentCarousel>(true);

        if (missionContentRect == null || missionScrollRect == null)
            return;

        missionScrollRect.horizontal = true;
        missionScrollRect.vertical = false;
        missionScrollRect.movementType = ScrollRect.MovementType.Clamped;
        missionScrollRect.verticalScrollbar = null;

        if (missionViewportRect != null)
        {
            missionViewportRect.anchorMin = Vector2.zero;
            missionViewportRect.anchorMax = Vector2.one;
            missionViewportRect.offsetMin = Vector2.zero;
            missionViewportRect.offsetMax = Vector2.zero;
        }

        missionScrollRect.content = missionContentRect;

        foreach (LayoutGroup layoutGroup in missionContentRect.GetComponents<LayoutGroup>())
        {
            if (layoutGroup != null)
                layoutGroup.enabled = false;
        }

        ContentSizeFitter contentSizeFitter = missionContentRect.GetComponent<ContentSizeFitter>();
        if (contentSizeFitter != null)
            contentSizeFitter.enabled = false;

        if (missionScrollRootRect != null)
        {
            missionScrollCanvasGroup = missionScrollRootRect.GetComponent<CanvasGroup>();
            if (missionScrollCanvasGroup == null)
                missionScrollCanvasGroup = missionScrollRootRect.gameObject.AddComponent<CanvasGroup>();
        }

        embeddedLayoutConfigured = true;
    }

    private void LayoutMissionButtons()
    {
        if (missionContentRect == null)
            return;

        Canvas.ForceUpdateCanvases();
        float stepX = missionCardSize.x + missionColumnSpacing;
        float upperRowY = -contentPaddingTop;
        float lowerRowY = -(contentPaddingTop + missionCardSize.y + missionRowSpacing);

        for (int i = 0; i < missionButtons.Count; i++)
        {
            RectTransform itemRect = missionButtons[i].RectTransform;
            if (itemRect == null)
                continue;

            bool isTopRow = i % 2 == 0;
            float y = isTopRow ? upperRowY : lowerRowY;

            itemRect.anchorMin = new Vector2(0f, 1f);
            itemRect.anchorMax = new Vector2(0f, 1f);
            itemRect.pivot = new Vector2(0f, 1f);
            itemRect.sizeDelta = missionCardSize;
            itemRect.anchoredPosition = new Vector2(contentPaddingLeft + (i * stepX), y);
            itemRect.localScale = Vector3.one;
        }

        float cardsWidth = missionButtons.Count <= 0 ? 0f : missionCardSize.x + ((missionButtons.Count - 1) * stepX);
        float width = contentPaddingLeft + cardsWidth + contentPaddingRight;
        float height = contentPaddingTop + missionCardSize.y + missionRowSpacing + missionCardSize.y + contentPaddingBottom;
        missionContentRect.sizeDelta = new Vector2(width, height);
        missionContentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        missionContentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        LayoutRebuilder.ForceRebuildLayoutImmediate(missionContentRect);
    }

    private void ConfigureMissionNavigation()
    {
        for (int i = 0; i < missionButtons.Count; i++)
        {
            Button button = missionButtons[i].Button;
            if (button == null)
                continue;

            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            navigation.selectOnLeft = null;
            navigation.selectOnRight = null;
            navigation.selectOnUp = null;
            navigation.selectOnDown = null;
            button.navigation = navigation;
        }
    }

    private Button GetMissionButton(int index)
    {
        if (index < 0 || index >= missionButtons.Count)
            return null;

        return missionButtons[index] != null ? missionButtons[index].Button : null;
    }

    private void ScrollToMissionIndex(int index)
    {
        if (missionScrollRect == null || missionViewportRect == null || missionContentRect == null)
            return;

        if (index < 0 || index >= missionButtons.Count)
            return;

        float stepX = missionCardSize.x + missionColumnSpacing;
        float contentWidth = Mathf.Max(0f, missionContentRect.sizeDelta.x);
        float viewportWidth = Mathf.Max(0f, missionViewportRect.rect.width);
        float maxOffset = Mathf.Max(0f, contentWidth - viewportWidth);

        float targetOffset = (index * stepX) - ((viewportWidth - missionCardSize.x) * .5f);
        targetOffset = Mathf.Clamp(targetOffset, 0f, maxOffset);

        Vector2 anchoredPosition = missionContentRect.anchoredPosition;
        anchoredPosition.x = -targetOffset;
        missionContentRect.anchoredPosition = anchoredPosition;

        if (maxOffset <= 0f)
        {
            missionScrollRect.horizontalNormalizedPosition = 0f;
            return;
        }

        missionScrollRect.horizontalNormalizedPosition = Mathf.Clamp01(targetOffset / maxOffset);
    }

    private void EnsureMissionVisible(int index)
    {
        if (missionScrollRect == null || missionViewportRect == null || missionContentRect == null)
            return;

        if (index < 0 || index >= missionButtons.Count)
            return;

        RectTransform itemRect = missionButtons[index] != null ? missionButtons[index].RectTransform : null;
        if (itemRect == null)
            return;

        Canvas.ForceUpdateCanvases();

        Vector3[] viewportCorners = new Vector3[4];
        Vector3[] itemCorners = new Vector3[4];
        missionViewportRect.GetWorldCorners(viewportCorners);
        itemRect.GetWorldCorners(itemCorners);

        float viewportLeft = viewportCorners[0].x;
        float viewportRight = viewportCorners[3].x;
        float itemLeft = itemCorners[0].x;
        float itemRight = itemCorners[3].x;
        float delta = 0f;

        if (itemRight > viewportRight)
            delta = itemRight - viewportRight;
        else if (itemLeft < viewportLeft)
            delta = itemLeft - viewportLeft;

        if (Mathf.Abs(delta) <= 0.01f)
            return;

        Vector2 anchoredPosition = missionContentRect.anchoredPosition;
        float targetX = anchoredPosition.x - delta;

        float maxOffset = Mathf.Max(0f, missionContentRect.rect.width - missionViewportRect.rect.width);
        targetX = Mathf.Clamp(targetX, -maxOffset, 0f);

        missionScrollTween?.Kill();
        missionScrollTween = missionContentRect.DOAnchorPosX(targetX, 0.22f)
            .SetEase(Ease.OutCubic)
            .OnUpdate(UpdateScrollNormalizedPosition)
            .OnComplete(() =>
            {
                UpdateScrollNormalizedPosition();
                missionScrollTween = null;
            });

        if (maxOffset <= 0f)
        {
            missionScrollRect.horizontalNormalizedPosition = 0f;
            return;
        }
    }

    private void UpdateTournamentBackground(int missionIndex)
    {
        if (tournamentCarousel == null)
            return;

        float normalized = missionButtons.Count <= 1
            ? 0f
            : Mathf.Clamp01((float)missionIndex / (missionButtons.Count - 1));

        tournamentCarousel.SetMissionBackgroundPosition(normalized);
    }

    private void TryFocusMissionAtIndex(int index)
    {
        if (EventSystem.current == null || missionContent == null)
            return;

        if (missionContent.childCount == 0)
            return;

        if (index < 0 || index >= missionContent.childCount)
            return;

        MissionButton missionButton = missionContent.GetChild(index).GetComponent<MissionButton>();
        if (missionButton == null || missionButton.Button == null)
            return;

        lastTrackedMissionIndex = index;
        Canvas.ForceUpdateCanvases();
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(missionButton.Button.gameObject);
        EnsureMissionVisible(index);
        UpdateTournamentBackground(index);
    }

    private void TrackSelectedMission()
    {
        if (EventSystem.current == null || missionButtons.Count == 0)
            return;

        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
        if (currentSelected == null)
            return;

        for (int i = 0; i < missionButtons.Count; i++)
        {
            MissionButton missionButton = missionButtons[i];
            if (missionButton == null || missionButton.Button == null)
                continue;

            if (missionButton.Button.gameObject != currentSelected)
                continue;

            if (lastTrackedMissionIndex == i)
                return;

            lastTrackedMissionIndex = i;
            EnsureMissionVisible(i);
            UpdateTournamentBackground(i);
            return;
        }
    }

    private void MaintainMissionFocus()
    {
        if (garageUIController == null)
            return;

        if (garageUIController.GetCurrentPanel() != UIPanelType.Play)
            return;

        if (EventSystem.current == null || missionButtons.Count == 0)
            return;

        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
        if (currentSelected != null && IsMissionButtonObject(currentSelected))
            return;

        int fallbackIndex = lastTrackedMissionIndex >= 0 ? lastTrackedMissionIndex : GetFirstUnlockedMissionIndex();
        TryFocusMissionAtIndex(Mathf.Clamp(fallbackIndex, 0, missionButtons.Count - 1));
    }

    private void HandleMissionNavigationInput()
    {
        if (garageUIController == null || garageUIController.GetCurrentPanel() != UIPanelType.Play)
            return;

        if (missionButtons.Count == 0)
            return;

        int currentIndex = GetCurrentMissionIndex();
        if (currentIndex < 0)
            currentIndex = Mathf.Clamp(lastTrackedMissionIndex, 0, missionButtons.Count - 1);

        int targetIndex = -1;

        if (Gamepad.current != null)
        {
            if (Gamepad.current.dpad.right.wasPressedThisFrame)
                targetIndex = currentIndex + 1;
            else if (Gamepad.current.dpad.left.wasPressedThisFrame)
                targetIndex = currentIndex - 1;
        }

        if (targetIndex < 0 && Keyboard.current != null)
        {
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
                targetIndex = currentIndex + 1;
            else if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
                targetIndex = currentIndex - 1;
        }

        if (targetIndex < 0)
            return;

        targetIndex = Mathf.Clamp(targetIndex, 0, missionButtons.Count - 1);
        if (targetIndex == currentIndex)
            return;

        if (missionButtons[targetIndex] == null)
            return;

        TryFocusMissionAtIndex(targetIndex);
    }

    private bool IsMissionButtonObject(GameObject selectedObject)
    {
        for (int i = 0; i < missionButtons.Count; i++)
        {
            MissionButton missionButton = missionButtons[i];
            if (missionButton == null || missionButton.Button == null)
                continue;

            if (missionButton.Button.gameObject == selectedObject)
                return true;
        }

        return false;
    }

    private void UpdateScrollNormalizedPosition()
    {
        if (missionScrollRect == null || missionViewportRect == null || missionContentRect == null)
            return;

        float maxOffset = Mathf.Max(0f, missionContentRect.rect.width - missionViewportRect.rect.width);
        if (maxOffset <= 0f)
        {
            missionScrollRect.horizontalNormalizedPosition = 0f;
            return;
        }

        float currentOffset = -missionContentRect.anchoredPosition.x;
        missionScrollRect.horizontalNormalizedPosition = Mathf.Clamp01(currentOffset / maxOffset);
    }

    private void AnimateMissionPanelRefresh()
    {
        if (missionScrollRootRect == null || missionScrollCanvasGroup == null)
            return;

        missionRefreshSequence?.Kill();
        missionRefreshSequence = DOTween.Sequence();

        Vector2 basePosition = missionScrollRootRect.anchoredPosition;
        RectTransform parentRect = missionScrollRootRect.parent as RectTransform;
        float revealDistance = missionScrollRootRect.rect.width + 120f;
        if (parentRect != null)
            revealDistance = Mathf.Max(revealDistance, parentRect.rect.width * 0.7f);

        missionScrollRootRect.anchoredPosition = new Vector2(basePosition.x + revealDistance, basePosition.y);
        missionScrollCanvasGroup.alpha = 0f;

        missionRefreshSequence
            .AppendInterval(0.08f)
            .Append(missionScrollRootRect.DOAnchorPosX(basePosition.x, 0.48f).SetEase(Ease.OutCubic))
            .Join(missionScrollCanvasGroup.DOFade(1f, 0.36f).SetEase(Ease.OutSine))
            .OnKill(() =>
            {
                if (missionScrollRootRect != null)
                    missionScrollRootRect.anchoredPosition = basePosition;
                if (missionScrollCanvasGroup != null)
                    missionScrollCanvasGroup.alpha = 1f;
            })
            .OnComplete(() =>
            {
                missionRefreshSequence = null;
            });
    }

    private int GetCurrentMissionIndex()
    {
        if (EventSystem.current == null)
            return -1;

        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
        if (currentSelected == null)
            return -1;

        for (int i = 0; i < missionButtons.Count; i++)
        {
            MissionButton missionButton = missionButtons[i];
            if (missionButton == null || missionButton.Button == null)
                continue;

            if (missionButton.Button.gameObject == currentSelected)
                return i;
        }

        return -1;
    }

    private int GetFirstUnlockedMissionIndex()
    {
        for (int i = 0; i < missionButtons.Count; i++)
        {
            if (missionButtons[i] != null && !missionButtons[i].IsLocked)
                return i;
        }

        return 0;
    }
}
