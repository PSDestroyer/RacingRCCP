using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum UIPanelType
{
    MainHub,
    Shop,
    Upgrade,
    Play,
    CareerMissions,
    Settings
}

public class GarageUIController : MonoBehaviour
{
    [System.Serializable]
    public class PanelEntry
    {
        public UIPanelType type;
        public UIPanel panel;
    }

    public PlayerInput playerInput;
    [Header("Panels")]
    [SerializeField] private CarSelection carSelection;
    [SerializeField] private List<PanelEntry> panelEntries;
    [SerializeField] private UIPanelType startPanel = UIPanelType.MainHub;

    [Header("Navigation")]
    [SerializeField] private Button back;
    // [SerializeField] private PlayerInput playerInput;

    private readonly Dictionary<UIPanelType, UIPanel> panels = new();
    private readonly Stack<UIPanelType> history = new();

    private UIPanelType currentPanel;
    private bool hasCurrentPanel;
    private bool isTransitioning;

    private void Awake()
    {
        
        panels.Clear();

        foreach (var entry in panelEntries)
        {
            if (entry.panel == null)
            {
                Debug.LogWarning($"Panel entry for {entry.type} has no panel assigned.", this);
                continue;
            }

            if (panels.ContainsKey(entry.type))
            {
                Debug.LogWarning($"Duplicate panel entry found for {entry.type}.", this);
                continue;
            }

            panels.Add(entry.type, entry.panel);
            entry.panel.Hide();
        }
        
        if (back != null)
            back.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (playerInput == null || playerInput.actions == null)
            return;

        playerInput.actions["Cancel"].performed += BackCtx;
        
    }

    private void OnDisable()
    {
        if (playerInput == null || playerInput.actions == null)
            return;

        playerInput.actions["Cancel"].performed -= BackCtx;
    }

    private void Start()
    {
        OpenPanel(startPanel, false);
        carSelection.loadmaincar();
    }

    public void OpenPanel(UIPanelType newPanel, bool addToHistory = true)
    {
        if (isTransitioning)
            return;

        if (!panels.TryGetValue(newPanel, out UIPanel nextPanel))
        {
            Debug.LogWarning($"Panel {newPanel} is not registered.", this);
            return;
        }

        if (hasCurrentPanel && currentPanel == newPanel)
            return;

        isTransitioning = true;

        if (hasCurrentPanel && panels.TryGetValue(currentPanel, out UIPanel currentPanelRef))
        {
            currentPanelRef.Hide();

            if (addToHistory)
                history.Push(currentPanel);
        }
    
        currentPanel = newPanel;
        hasCurrentPanel = true;

        nextPanel.Show();
        UpdateBackButton();

        GameObject selected = nextPanel.DefaultSelected;
        if (selected != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(selected);
        }
        SoundManager.Instance.PlayButtonClick();
        isTransitioning = false;
    }

    public void BackCtx(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed)
            return;

        Back();
    }

    public void Back()
    {
        if (isTransitioning)
            return;

        if (!hasCurrentPanel)
            return;

        if (currentPanel == UIPanelType.MainHub)
            return;

        if (history.Count == 0)
        {
            OpenPanel(UIPanelType.MainHub, false);
            return;
        }

        UIPanelType previous = history.Pop();
        OpenPanel(previous, false);
    }

    public UIPanelType GetCurrentPanel()
    {
        return currentPanel;
    }

    private void UpdateBackButton()
    {
        if (back == null)
            return;
        carSelection.loadmaincar();
        bool showBackButton = hasCurrentPanel && currentPanel != UIPanelType.MainHub;
        back.gameObject.SetActive(showBackButton);
    }
}
