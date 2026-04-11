using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class YesNo : MonoBehaviour
{
    [Header("Yes/No")] 
    [SerializeField]private GameObject YesNoPanelObject;
    [SerializeField] private Button YesButton;
    [SerializeField] private Button NoButton;
    [SerializeField] private TMP_Text InfoText;
    private TaskCompletionSource<bool> tcs;
    public PlayerInput playerInput;

    [Header("Notification")] 
    [SerializeField]private GameObject NotifiPanelObject;
    [SerializeField] private TMP_Text Notification;

    void Start()
    {
        YesButton.onClick.AddListener(OnYesClicked);
        NoButton.onClick.AddListener(OnNoClicked);
        YesNoPanelObject.SetActive(false);
    }
    
    
    #region Yes/No

    
    public async Task<bool> ShowYesNoPanelAsync(string Text)
    {
        InputAction cancelAction = GetAction("Cancel");
        if (cancelAction != null)
            cancelAction.performed += ClosePanelCTX;

        InfoText.text = Text;
        tcs = new TaskCompletionSource<bool>();
        YesNoPanelObject.SetActive(true);        
        YesButton.Select();

        bool result = await tcs.Task;

        ClosePanel();
        return result;
    }
    

    public void ClosePanel()
    {
        InputAction cancelAction = GetAction("Cancel");
        if (cancelAction != null)
            cancelAction.performed -= ClosePanelCTX;
        YesNoPanelObject.SetActive(false);
        tcs = null;
    }
    public void ClosePanelCTX(InputAction.CallbackContext ctx)
    {
        ClosePanel();
    }
    private void OnYesClicked()
    {
        tcs.TrySetResult(true);

    }

    private void OnNoClicked()
    {
        tcs.TrySetResult(false);
    }
    
    #endregion

    #region Notification

    public void Notify(string info)
    {
        StartCoroutine(Notificationtime(info));
    }

    IEnumerator Notificationtime(string info)
    {
        Notification.text = info;
        NotifiPanelObject.SetActive(true);
        yield return new WaitForSeconds(2);
        NotifiPanelObject.SetActive(false);
    }
    #endregion

    private InputAction GetAction(string actionName)
    {
        if (playerInput == null && InputManager.Instance != null)
            playerInput = InputManager.Instance.GetPlayerInput();

        if (playerInput == null || playerInput.actions == null)
            return null;

        return playerInput.actions[actionName];
    }
}
