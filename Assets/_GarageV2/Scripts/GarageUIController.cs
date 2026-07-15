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
    Settings,
    Controls
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
    [Header("Runtime Panels")]
    [SerializeField] private string controlsPanelResourcePath = "UI/Controls";
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

        RegisterRuntimePanel(UIPanelType.Controls, controlsPanelResourcePath);
        
        if (back != null)
            back.gameObject.SetActive(false);
    }

    private void RegisterRuntimePanel(UIPanelType type, string resourcePath)
    {
        if (panels.ContainsKey(type) || string.IsNullOrWhiteSpace(resourcePath))
            return;

        GameObject instance = FindExistingRuntimePanel(type.ToString());

        if (instance == null)
        {
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"Runtime panel prefab not found in Resources: {resourcePath}", this);
                return;
            }

            instance = Instantiate(prefab, transform);
            instance.name = prefab.name;
        }

        UIPanel panel = instance.GetComponent<UIPanel>();
        if (panel == null)
            panel = instance.AddComponent<UIPanel>();

        panel.SetRoot(instance);
        panel.Hide();
        panels.Add(type, panel);
    }

    private GameObject FindExistingRuntimePanel(string panelName)
    {
        foreach (Transform child in transform)
        {
            if (child == null)
                continue;

            if (child.name == panelName)
                return child.gameObject;
        }

        return null;
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

    public void FocusCurrentPanelSelection()
    {
        if (!hasCurrentPanel)
            return;

        if (!panels.TryGetValue(currentPanel, out UIPanel panel))
            return;

        GameObject selected = panel.DefaultSelected;
        if (selected != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(selected);
        }
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
        ActivatePanelCamera(nextPanel.PanelCamera);
        UpdateBackButton();
        FocusCurrentPanelSelection();
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

        back.gameObject.SetActive(false);
    }

    private void ActivatePanelCamera(GameObject targetCamera)
    {
        foreach (UIPanel panel in panels.Values)
        {
            if (panel == null || panel.PanelCamera == null)
                continue;

            panel.PanelCamera.SetActive(panel.PanelCamera == targetCamera);
        }
    }

}
