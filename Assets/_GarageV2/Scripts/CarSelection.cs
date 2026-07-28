using System;
using System.Collections;
using System.Collections.Generic;
using HalvaStudio.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class CarSelection : MonoBehaviour
{
  [SerializeField] public Transform spawnCarPoint;
  [SerializeField] private Transform spawninPanel;
  [SerializeField] private CarButton uiPrefab;
  [SerializeField] private Button selectbut;
    [SerializeField] private TextMeshProUGUI selecttext;
    [SerializeField] private TextMeshProUGUI selecttextJP;
  [SerializeField] private MoneyManager moneyManager;
  [SerializeField] public GameObject player;
  private static int indexcar;
  private GameObject PlayerCar1;
    private int previewCarIndex = -1;
    public GarageUIController CanvasManager;
    private bool litenered;
    public PlayerInput playerInput;
    public SoundManager SM;
    public YesNo yesNo;
    [Header("--Stats--")] public Slider PowerSlider;
    public TMP_Text PowerText;
    public Slider SpeedSlider;
    public TMP_Text SpeedText;
    public Slider SteerSlider;
    public TMP_Text SteerText;
    public Slider BrakeSlider;
    public TMP_Text BrakeText;
    public Image Turbo;
    public TMP_Text Traction;
    public List<string> TractionText = new List<string>();
    public TMP_Text Name;
    public TMP_Text NameShadow;
    public TMP_Text PriceText;
    [Header("Stats Scaling")]
    [SerializeField] private float powerSliderMax = 400f;
    [SerializeField] private float speedSliderMax = 420f;
    [SerializeField] private float steeringSliderMax = 100f;
    [SerializeField] private float brakeSliderMax = 4000f;

    
    
    [Header("Scrollbar")]
    public ScrollRect scrollRect;
    private RectTransform contentRectTransform;
    private float contentTop;
    private float contentBottom;
    private bool isScrolling = false;
    private float scrollDuration = 0.5f;
    private float elapsedTime = 0f;
    private float lastMoveTime = 0f;
    private float moveCooldown = 0.2f; // Adjust this value as needed
    private Vector2 targetNormPos;
    private bool navigationSubscribed;
    private int lastNavigationFrame = -1;
    private bool navigationInputHeld;
  private void Start()
  {
   GlobalCarData._buttonList.Clear();
   ConfigureVerticalScrollView();
   EnsureStarterCarBought();
   ConfigureStatsLabels();

   ClearSelectedObjectIfInside(spawninPanel);

   for (int i = spawninPanel.childCount - 1; i >= 0; i--)
       Destroy(spawninPanel.GetChild(i).gameObject);
   
    for (var i = 0; i < GlobalCarData._carlists.Count; i++)
    {
      CarButton uibutton = Instantiate(uiPrefab, spawninPanel);
      uibutton.SetUpButton(GlobalCarData._carlists[i],this);
      Button button = uibutton.GetComponent<Button>();
      ConfigureCarButtonLayout(button);
      GlobalCarData._buttonList.Add(button);
    }
    
    
        selectbut.onClick.AddListener(()=>SelectOrBuy());
        contentRectTransform = scrollRect.content.GetComponent<RectTransform>();
        SetupListeners();
    
        StartCoroutine(scroll());
        StartCoroutine(SelectInitialCarNextFrame());
  }

  IEnumerator scroll()
  {
      yield return new WaitForSeconds(1);
      if (GlobalCarData._buttonList.Count == 0)
          yield break;

      Canvas.ForceUpdateCanvases();
      int savedIndex = ResolveSelectedOwnedCarIndex();
      ScrollToSelectedButton(GlobalCarData._buttonList[savedIndex]);

  }

  private IEnumerator SelectInitialCarNextFrame()
  {
      yield return null;

      if (GlobalCarData._buttonList == null || GlobalCarData._buttonList.Count == 0)
          yield break;

      int savedIndex = ResolveSelectedOwnedCarIndex();
      OnPressedButton(savedIndex);
      FocusCarButton(savedIndex);
  }
  
  private void SetupListeners()
  {  
    if (GlobalCarData._buttonList.Count == 0)
        return;

    for (int i = 0; i < GlobalCarData._buttonList.Count ; i++)
    {
           
      int id = i;
      GlobalCarData._buttonList[id].onClick.AddListener(() => OnPressedButton(id));
    }

    litenered = true;
    int savedIndex = ResolveSelectedOwnedCarIndex();
    OnPressedButton(savedIndex);
    FocusCarButton(savedIndex);

  }

  public void OnPressedButton(int id)
  {
    RemoveDestroyedButtons();

    if (id < 0 || id >= GlobalCarData._buttonList.Count)
        return;

    indexcar = Mathf.Clamp(indexcar, 0, GlobalCarData._buttonList.Count - 1);
   // Debug.Log("Pressed");
    Button selectedButton = GlobalCarData._buttonList[id];
    if (selectedButton == null)
        return;

    CarButton selectedCarButton = selectedButton.GetComponent<CarButton>();
    if (selectedCarButton == null)
        return;

    selectedCarButton.isPressed();

    if (id != indexcar && indexcar >= 0 && indexcar < GlobalCarData._buttonList.Count)
    {
        Button previousButton = GlobalCarData._buttonList[indexcar];
        if (previousButton != null)
        {
            CarButton previousCarButton = previousButton.GetComponent<CarButton>();
            if (previousCarButton != null)
                previousCarButton.UnPressed();
        }
    }
       
    indexcar = id;
    ScrollToSelectedButton(selectedButton);
    UpdateCurrentCar();
        if (SaveManager.Instance.IsCarBought(GlobalCarData._carlists[indexcar].carName))
        {
            selecttext.text = UILocalization.Get("Select", "Select");
            selecttextJP.text = UILocalization.Get("Select", "Select");
        }
        else
        {
            selecttext.text = UILocalization.Get("BuyYes/No", "Buy");
            selecttextJP.text = UILocalization.Get("BuyYes/No", "Buy");

        }
  }

    public void SelectOrBuy()
    {
        if (SaveManager.Instance.IsCarBought(GlobalCarData._carlists[indexcar].carName))
        {
            LoadGame();
        }
        else
        {
            BuyCar();
        }
    }
    public async void BuyCar()
    {
        RemoveEvents();
        bool result = await yesNo.ShowYesNoPanelAsync(UILocalization.Get("shop.buy_select_confirm", "Buy / Select?"));

        if (result)
        {
            if (GlobalCarData._carlists[indexcar].price <= SaveManager.Instance.saveData.money)
                {
                    moneyManager.MoneyToTake( GlobalCarData._carlists[indexcar].price);
                    SaveManager.Instance.SaveCar(GlobalCarData._carlists[indexcar].carName,true, GlobalCarData._carlists[indexcar].power,GlobalCarData._carlists[indexcar].speed,GlobalCarData._carlists[indexcar].turbo,GlobalCarData._carlists[indexcar].color,GlobalCarData._carlists[indexcar].steerAngle,GlobalCarData._carlists[indexcar].traction,GlobalCarData._carlists[indexcar].brake);
                    selecttext.text = UILocalization.Get("Select", "Select");
                    SaveManager.Instance.saveData.currentCar = indexcar;
                    SaveManager.Instance.Save();
                    SM.PlayNewCarClip();
                    Debug.Log("bought");
                }
                else
                {
                    yesNo.NotifyNotEnoughMoney();
                    SM.PlayButtonError();
                    Debug.Log("dont have enought Money");
                }
                
            Debug.Log("YES");
        }
        else
        {
            SM.PlayButtonClick();
            Debug.Log("NO");
        }
        SetEvents();

    }

    public void ScrollToSelectedButton(Button selectedButton)
    {
        if (scrollRect == null || scrollRect.content == null || selectedButton == null) return;

        RectTransform content = scrollRect.content;
        RectTransform viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();
        RectTransform item = selectedButton.GetComponent<RectTransform>();

        // Bounds in viewport space
        Bounds contentBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, content);
        Bounds itemBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, item);

        Vector2 norm = scrollRect.normalizedPosition;

        // --- Horizontal ---
        if (scrollRect.horizontal)
        {
            float contentWidth = contentBounds.size.x;
            float viewWidth = viewport.rect.width;

            if (contentWidth > viewWidth + 0.001f)
            {
                // item center in content-bounds space
                float itemCenterX = itemBounds.center.x - contentBounds.min.x;
                float hiddenWidth = contentWidth - viewWidth;

                // normalized 0..1 (0=left, 1=right)
                float x = Mathf.Clamp01((itemCenterX - viewWidth * 0.5f) / hiddenWidth);
                norm.x = x;
            }
        }

        // --- Vertical ---
        if (scrollRect.vertical)
        {
            float contentHeight = contentBounds.size.y;
            float viewHeight = viewport.rect.height;

            if (contentHeight > viewHeight + 0.001f)
            {
                // item center in content-bounds space
                float itemCenterY = itemBounds.center.y - contentBounds.min.y;
                float hiddenHeight = contentHeight - viewHeight;

                // normalized 0..1 (0=bottom, 1=top) in ScrollRect.normalizedPosition
                float y = Mathf.Clamp01((itemCenterY - viewHeight * 0.5f) / hiddenHeight);
                norm.y = y;
            }
        }

        targetNormPos = norm;
        isScrolling = true;
        elapsedTime = 0f;
    }

    private void Update()
    {
        HandleNavigationInput();

        if (!isScrolling) return;

        elapsedTime += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsedTime / scrollDuration);

        scrollRect.normalizedPosition = Vector2.Lerp(scrollRect.normalizedPosition, targetNormPos, t);

        if (elapsedTime >= scrollDuration)
            isScrolling = false;
    }

    #region
  //  public void Next()
  //{
  //  indexcar += 1;
        
  //  if (indexcar > GlobalCarData._carlists.Count-1)
  //  {
  //    indexcar = 0;
  //    Debug.Log(indexcar);
  //  }
       
  //  UpdateCurrentCar();
  //}
    #endregion

  private void UpdateCurrentCar()
  {    
      SM.PlayButtonClick();
      ReplacePreviewCar(indexcar);

      // player.GetComponent<Rigidbody>().isKinematic = true;
      // foreach (var mirror in player.GetComponentsInChildren<RCC_Mirror>())
      {
          // mirror.gameObject.SetActive(false); 
      }
      UpdateStats();
  }

    public void UpdateStats()
    {
        Name.text = GlobalCarData._carlists[indexcar].displayName;
        NameShadow.text = GlobalCarData._carlists[indexcar].displayName;
        ConfigureStatsSliders();
        PowerSlider.value = GlobalCarData._carlists[indexcar].power;
        PowerText.text = GlobalCarData._carlists[indexcar].power.ToString();
        SpeedSlider.value = GlobalCarData._carlists[indexcar].speed;
        SpeedText.text = GlobalCarData._carlists[indexcar].speed.ToString();
        SteerSlider.value = GlobalCarData._carlists[indexcar].steerAngle;
        SteerText.text = GlobalCarData._carlists[indexcar].steerAngle.ToString();
        BrakeSlider.value = GlobalCarData._carlists[indexcar].brake;
        BrakeText.text = GlobalCarData._carlists[indexcar].brake.ToString();
        Traction.text = TractionText[GlobalCarData._carlists[indexcar].traction].ToString();
        if (PriceText != null)
            PriceText.text = GlobalCarData._carlists[indexcar].price <= 0
                ? UILocalization.Get("ui.free", "FREE")
                : GlobalCarData._carlists[indexcar].price.ToString();
        Turbo.gameObject.SetActive(GlobalCarData._carlists[indexcar].turbo);
    }

    private void EnsureStarterCarBought()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.saveData == null || GlobalCarData._carlists == null || GlobalCarData._carlists.Count == 0)
            return;

        CarSO starterCar = GlobalCarData._carlists[0];

        if (starterCar == null || SaveManager.Instance.IsCarBought(starterCar.carName))
            return;

        SaveManager.Instance.SaveCar(starterCar.carName, true, starterCar.power, starterCar.speed, starterCar.turbo, starterCar.color, starterCar.steerAngle, starterCar.traction, starterCar.brake);
        SaveManager.Instance.Save();
    }

    private void ConfigureStatsSliders()
    {
        ConfigureSlider(PowerSlider, powerSliderMax);
        ConfigureSlider(SpeedSlider, speedSliderMax);
        ConfigureSlider(SteerSlider, steeringSliderMax);
        ConfigureSlider(BrakeSlider, brakeSliderMax);
    }

    private void ConfigureSlider(Slider slider, float maxValue)
    {
        if (slider == null)
            return;

        slider.minValue = 0f;
        slider.maxValue = Mathf.Max(1f, maxValue);
    }

    private void ConfigureStatsLabels()
    {
        SetSliderLabel(PowerSlider, UILocalization.Get("Power", "Power"));
        SetSliderLabel(SpeedSlider, UILocalization.Get("ui.speed", "Speed"));
        SetSliderLabel(SteerSlider, UILocalization.Get("Steering", "Steering"));
        SetSliderLabel(BrakeSlider, UILocalization.Get("Brake", "Brake"));
    }

    private void SetSliderLabel(Slider slider, string label)
    {
        if (slider == null)
            return;

        TMP_Text labelText = null;
        Transform labelTransform = slider.transform.Find("Name");

        if (labelTransform != null)
            labelText = labelTransform.GetComponent<TMP_Text>();

        if (labelText == null)
        {
            TMP_Text[] labels = slider.GetComponentsInChildren<TMP_Text>(true);

            foreach (TMP_Text candidate in labels)
            {
                if (candidate != PowerText && candidate != SpeedText && candidate != SteerText && candidate != BrakeText)
                {
                    labelText = candidate;
                    break;
                }
            }
        }

        if (labelText == null)
            return;

        foreach (MonoBehaviour component in labelText.GetComponents<MonoBehaviour>())
        {
            if (component != null && component.GetType().Name == "LocalizeStringEvent")
                component.enabled = false;
        }

        labelText.text = label;
    }


  public void LoadGame()
  {
      SaveManager.Instance.saveData.currentCar = indexcar;
       SaveManager.Instance.Save();
      CanvasManager.Back();
  }
  public void loadmaincar()
  {
      if (GlobalCarData._carlists == null || GlobalCarData._carlists.Count == 0 || SaveManager.Instance == null || SaveManager.Instance.saveData == null)
          return;

      int savedCarIndex = ResolveSelectedOwnedCarIndex();
      indexcar = savedCarIndex;

      if (player == null || previewCarIndex != savedCarIndex)
          ReplacePreviewCar(savedCarIndex);

      UpdateStats();
      UpdateSelectionVisuals(savedCarIndex);
  }

  private int ResolveSelectedOwnedCarIndex()
  {
      if (GlobalCarData._carlists == null || GlobalCarData._carlists.Count == 0 || SaveManager.Instance == null || SaveManager.Instance.saveData == null)
          return 0;

      int savedIndex = Mathf.Clamp(SaveManager.Instance.saveData.currentCar, 0, GlobalCarData._carlists.Count - 1);
      CarSO savedCar = GlobalCarData._carlists[savedIndex];

      if (savedCar != null && SaveManager.Instance.IsCarBought(savedCar.carName))
          return savedIndex;

      for (int i = 0; i < GlobalCarData._carlists.Count; i++)
      {
          CarSO candidate = GlobalCarData._carlists[i];
          if (candidate != null && SaveManager.Instance.IsCarBought(candidate.carName))
          {
              SaveManager.Instance.saveData.currentCar = i;
              SaveManager.Instance.Save();
              return i;
          }
      }

      return 0;
  }

  private void UpdateSelectionVisuals(int selectedIndex)
  {
      if (GlobalCarData._buttonList == null)
          return;

      for (int i = 0; i < GlobalCarData._buttonList.Count; i++)
      {
          Button button = GlobalCarData._buttonList[i];
          if (button == null)
              continue;

          CarButton carButton = button.GetComponent<CarButton>();
          if (carButton == null)
              continue;

          if (i == selectedIndex)
              carButton.isPressed();
          else
              carButton.UnPressed();
      }
  }
  public void Navigations(InputAction.CallbackContext ctx)
  {
      if (!ctx.performed)
          return;

      TryMoveSelection(ctx.ReadValue<Vector2>().y);
  }

  public void NavigationCanceled(InputAction.CallbackContext ctx)
  {
      navigationInputHeld = false;
  }

  private void HandleNavigationInput()
  {
      if (playerInput == null || playerInput.actions == null)
          return;

      InputAction navigateAction = playerInput.actions.FindAction("Navigate", false);
      if (navigateAction == null)
          return;

      Vector2 inputValue = navigateAction.ReadValue<Vector2>();
      TryMoveSelection(inputValue.y);
  }

  private void TryMoveSelection(float verticalInput)
  {
      if (Mathf.Abs(verticalInput) <= 0.1f)
      {
          navigationInputHeld = false;
          return;
      }

      if (!CanHandleNavigation())
          return;

      RemoveDestroyedButtons();

      if (GlobalCarData._buttonList == null || GlobalCarData._buttonList.Count == 0)
          return;

      if (navigationInputHeld || Time.frameCount == lastNavigationFrame || Time.time - lastMoveTime <= moveCooldown)
          return;

      navigationInputHeld = true;
      lastNavigationFrame = Time.frameCount;
      lastMoveTime = Time.time;

      int newindex = indexcar;

      if (verticalInput <= -0.1f)
      {
          newindex++;
          if (newindex > GlobalCarData._buttonList.Count - 1)
              newindex = 0;
      }
      else
      {
          newindex--;
          if (newindex < 0)
              newindex = GlobalCarData._buttonList.Count - 1;
      }

      OnPressedButton(newindex);
      FocusCarButton(newindex);
  }

  private void OnEnable()
  {
      LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
      if (litenered && GlobalCarData._buttonList != null && GlobalCarData._buttonList.Count > 0)
      {
          int savedIndex = ResolveSelectedOwnedCarIndex();
          OnPressedButton(savedIndex);
          FocusCarButton(savedIndex);
      }
      // if (litenered)
      //     GlobalCarData._buttonList[SaveManager.Instance.saveData.currentCar].onClick.Invoke();
      SetEvents();
      // selectbut.Select();
  }

  private void SetEvents()
  {
      if (playerInput == null || playerInput.actions == null)
          return;

      InputAction navigateAction = playerInput.actions.FindAction("Navigate", false);
      if (navigateAction == null)
          return;

      for (int i = 0; i < 8; i++)
      {
          navigateAction.performed -= Navigations;
          navigateAction.canceled -= NavigationCanceled;
      }

      navigationSubscribed = true;
      // playerInput.actions["Submit"].performed += SelectOrBuyCtx;
  }

  private void OnDisable()
  {
      LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
      RemoveEvents();
  }

  private void OnDestroy()
  {
      LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
      RemoveEvents();
  }

  private void OnLocaleChanged(Locale _)
  {
      ConfigureStatsLabels();

      if (GlobalCarData._carlists == null || GlobalCarData._carlists.Count == 0)
          return;

      indexcar = Mathf.Clamp(indexcar, 0, GlobalCarData._carlists.Count - 1);
      UpdateStats();

      bool owned = SaveManager.Instance != null &&
                   SaveManager.Instance.IsCarBought(GlobalCarData._carlists[indexcar].carName);
      string action = owned
          ? UILocalization.Get("Select", "Select")
          : UILocalization.Get("BuyYes/No", "Buy");

      if (selecttext != null)
          selecttext.text = action;
      if (selecttextJP != null)
          selecttextJP.text = action;
  }

  private void RemoveEvents()
  {
      if (playerInput == null || playerInput.actions == null)
          return;

       InputAction navigateAction = playerInput.actions.FindAction("Navigate", false);
       if (navigateAction != null)
       {
           navigateAction.performed -= Navigations;
           navigateAction.canceled -= NavigationCanceled;
       }

       navigationSubscribed = false;
       navigationInputHeld = false;
       // playerInput.actions["Submit"].performed -= SelectOrBuyCtx;
  }

    private bool CanHandleNavigation()
    {
        if (CanvasManager == null)
            return true;

        UIPanelType currentPanel = CanvasManager.GetCurrentPanel();
        return currentPanel == UIPanelType.MainHub || currentPanel == UIPanelType.Shop;
    }

    private void ConfigureVerticalScrollView()
    {
        if (scrollRect == null || scrollRect.content == null)
            return;

        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        RectTransform content = scrollRect.content;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);

        HorizontalLayoutGroup horizontal = content.GetComponent<HorizontalLayoutGroup>();
        if (horizontal != null)
            horizontal.enabled = false;

        GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();

        if (grid != null)
        {
            grid.enabled = true;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Vertical;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 1;
        }
        else
        {
            VerticalLayoutGroup vertical = content.GetComponent<VerticalLayoutGroup>();
            if (vertical == null)
                vertical = content.gameObject.AddComponent<VerticalLayoutGroup>();

            vertical.enabled = true;
            vertical.childAlignment = TextAnchor.UpperCenter;
            vertical.spacing = 15f;
            vertical.childControlWidth = false;
            vertical.childControlHeight = false;
            vertical.childForceExpandWidth = false;
            vertical.childForceExpandHeight = false;
        }

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = content.gameObject.AddComponent<ContentSizeFitter>();

        if (fitter != null)
        {
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    private void ConfigureCarButtonLayout(Button button)
    {
        if (button == null)
            return;

        RectTransform rect = button.GetComponent<RectTransform>();
        Vector2 buttonSize = GetConfiguredButtonSize(button);

        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);

            if (rect.sizeDelta.x <= 0.01f || rect.sizeDelta.y <= 0.01f)
                rect.sizeDelta = buttonSize;
        }

        LayoutElement layout = button.GetComponent<LayoutElement>();
        if (layout == null)
            layout = button.gameObject.AddComponent<LayoutElement>();

        layout.ignoreLayout = false;
        layout.minWidth = buttonSize.x;
        layout.minHeight = buttonSize.y;
        layout.preferredWidth = buttonSize.x;
        layout.preferredHeight = buttonSize.y;
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        Graphic targetGraphic = button.GetComponent<Graphic>();
        if (targetGraphic != null)
            button.targetGraphic = targetGraphic;
    }

    private Vector2 GetConfiguredButtonSize(Button button)
    {
        if (scrollRect != null && scrollRect.content != null)
        {
            GridLayoutGroup grid = scrollRect.content.GetComponent<GridLayoutGroup>();
            if (grid != null && grid.cellSize.x > 0.01f && grid.cellSize.y > 0.01f)
                return grid.cellSize;
        }

        RectTransform rect = button != null ? button.GetComponent<RectTransform>() : null;
        if (rect != null && rect.sizeDelta.x > 0.01f && rect.sizeDelta.y > 0.01f)
            return rect.sizeDelta;

        return new Vector2(330f, 150f);
    }

    private void FocusCarButton(int carId)
    {
        RemoveDestroyedButtons();

        if (GlobalCarData._buttonList == null || carId < 0 || carId >= GlobalCarData._buttonList.Count)
            return;

        Button button = GlobalCarData._buttonList[carId];
        if (button == null)
            return;

        Graphic targetGraphic = button.GetComponent<Graphic>();
        if (targetGraphic != null)
            button.targetGraphic = targetGraphic;

        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == button.gameObject)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void RemoveDestroyedButtons()
    {
        if (GlobalCarData._buttonList == null)
            return;

        for (int i = GlobalCarData._buttonList.Count - 1; i >= 0; i--)
        {
            if (GlobalCarData._buttonList[i] == null)
                GlobalCarData._buttonList.RemoveAt(i);
        }

        if (GlobalCarData._buttonList.Count > 0)
            indexcar = Mathf.Clamp(indexcar, 0, GlobalCarData._buttonList.Count - 1);
        else
            indexcar = 0;
    }

    private void ClearSelectedObjectIfInside(Transform root)
    {
        if (root == null || EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)
            return;

        Transform selected = EventSystem.current.currentSelectedGameObject.transform;
        if (selected == root || selected.IsChildOf(root))
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void ReplacePreviewCar(int carId)
    {
        if (spawnCarPoint == null)
            return;

        for (int i = spawnCarPoint.childCount - 1; i >= 0; i--)
            Destroy(spawnCarPoint.GetChild(i).gameObject);

        GameObject carPrefab = Resources.Load<GameObject>(GlobalCarData._carlists[carId].carPrefabLocation);

        if (carPrefab == null)
        {
            Debug.LogError($"Car prefab not found at '{GlobalCarData._carlists[carId].carPrefabLocation}'.", this);
            player = null;
            return;
        }

        player = Instantiate(carPrefab, spawnCarPoint);
        previewCarIndex = carId;

        RCCP_CarController controller = player.GetComponent<RCCP_CarController>();

        if (controller != null)
        {
            controller.canControl = false;
            controller.engineRunning = false;
            PrepareMenuPreviewCar(controller);

            if (RCCP_SceneManager.Instance != null)
                RCCP_SceneManager.Instance.RegisterPlayer(controller, false, false);
        }
    }

    private void PrepareMenuPreviewCar(RCCP_CarController controller)
    {
        if (controller == null)
            return;

        controller.SetCanControl(false);
        controller.externalControl = false;

        if (controller.Inputs != null)
            controller.Inputs.OverrideInputs(new RCCP_Inputs());

        Rigidbody carRigidbody = controller.Rigid != null ? controller.Rigid : controller.GetComponent<Rigidbody>();

        if (carRigidbody != null)
        {
            carRigidbody.linearVelocity = Vector3.zero;
            carRigidbody.angularVelocity = Vector3.zero;
        }
    }
}
