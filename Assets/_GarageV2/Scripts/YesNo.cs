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
    [Header("Buy Yes/No")] 
    [SerializeField]private GameObject BuyYesNoPanelObject;
    [SerializeField] private TMP_Text BuyInfoText;
    [SerializeField] private TMP_Text CarNameText;
    [SerializeField] private TMP_Text CarPriceText;
    [SerializeField] private TMP_Text CarPowerText;
    [SerializeField] private TMP_Text CarInfoText;
    [SerializeField] private Image carImage;
    [SerializeField] private Image carClass;

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
    public async Task<bool> ShowBuyYesNoPanelAsync(string Text,string Name,string Info,string Price,string power,Sprite Class, Sprite CarImg)
    {
        InputAction cancelAction = GetAction("Cancel");
        if (cancelAction != null)
            cancelAction.performed += ClosePanelCTX;

        InfoText.text = "";
        BuyInfoText.text = Text;
        CarNameText.text = Name;
        CarInfoText.text = Info;
        CarPriceText.text = Price;
        CarPowerText.text = power;
        carClass.sprite = Class;
        carImage.sprite = CarImg;
        tcs = new TaskCompletionSource<bool>();
        YesNoPanelObject.SetActive(true);        
        BuyYesNoPanelObject.SetActive(true);        
        YesButton.Select();

        bool result = await tcs.Task;
        BuyYesNoPanelObject.SetActive(false);
        
        ClosePanel();
        return result;
    }
    
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
