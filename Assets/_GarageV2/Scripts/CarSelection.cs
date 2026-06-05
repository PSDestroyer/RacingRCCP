using System;
using System.Collections;
using System.Collections.Generic;
using HalvaStudio.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Settings;

public class CarSelection : MonoBehaviour
{
  public struct CarDisplayStats
  {
      public int power;
      public int speed;
      public int steer;
      public int brake;
      public int traction;
      public bool turbo;
  }

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
    public Image CarClassImage;
    public TMP_Text Traction;
    public TMP_Text CarInfoText;
    public List<string> TractionText = new List<string>();
    public TMP_Text Name;
    public TMP_Text NameShadow;

    
    
    [Header("Scrollbar")]
    public ScrollRect scrollRect;
    private RectTransform contentRectTransform;
    private float contentTop;
    private float contentBottom;
    private bool isScrolling = false;
    private float targetNormalizedPosition;
    private float scrollDuration = 0.5f;
    private float elapsedTime = 0f;
    private float lastMoveTime = 0f;
    private float moveCooldown = 0.2f; // Adjust this value as needed
    private Vector2 targetNormPos;
    private void Start()
  {
   if (SM == null)
       SM = SoundManager.Instance;
   GlobalCarData._buttonList.Clear();
   
    for (var i = 0; i < GlobalCarData._carlists.Count; i++)
    {
      CarButton uibutton = Instantiate(uiPrefab, spawninPanel);
      uibutton.SetUpButton(GlobalCarData._carlists[i],this);
      GlobalCarData._buttonList.Add( uibutton.GetComponent<Button>());
    }
    
    
        selectbut.onClick.AddListener(()=>SelectOrBuy());
        contentRectTransform = scrollRect.content.GetComponent<RectTransform>();
        contentTop = contentRectTransform.localPosition.x - 1100 + (contentRectTransform.rect.width / 2f);
        contentBottom = contentRectTransform.localPosition.x + 50- (contentRectTransform.rect.width / 2f);
        SetupListeners();
    
        StartCoroutine(scroll());
  }

  IEnumerator scroll()
  {
      yield return new WaitForSeconds(1);
      Canvas.ForceUpdateCanvases();
      ScrollToSelectedButton(GlobalCarData._buttonList[0]);

  }
  
    private void SetupListeners()
  {  
    for (int i = 0; i < GlobalCarData._buttonList.Count ; i++)
    {
           
      int id = i;
      GlobalCarData._buttonList[id].onClick.AddListener(() => OnPressedButton(id));
    }

    litenered = true;
    RefreshCarButtonsUI();
    GlobalCarData._buttonList[SaveManager.Instance.saveData.currentCar].onClick.Invoke();
    GlobalCarData._buttonList[SaveManager.Instance.saveData.currentCar].Select();
    ScrollToSelectedButton(GlobalCarData._buttonList[SaveManager.Instance.saveData.currentCar]);

  }

  public void OnPressedButton(int id)
  {
   // Debug.Log("Pressed");
    GlobalCarData._buttonList[id].GetComponent<CarButton>().isPressed();
    if(id!=indexcar)
    GlobalCarData._buttonList[indexcar].GetComponent<CarButton>().UnPressed();
       
    indexcar = id;
    ScrollToSelectedButton(GlobalCarData._buttonList[id]);
    UpdateCurrentCar();
        RefreshCarButtonsUI();
        if (SaveManager.Instance.IsCarBought(GlobalCarData._carlists[indexcar].carName))
        {
            var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI","Select");

            selecttext.text = operation.Result;
            selecttextJP.text = operation.Result;
            
            SaveManager.Instance.saveData.currentCar = indexcar;
        }
        else
        {
            var buystring = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI","BuyYes/No");

            selecttext.text = "<color=#DFA93B> "+buystring.Result + " "+GlobalCarData._carlists[id].price+" <sprite index=0>";
            selecttextJP.text = "<color=#DFA93B> "+buystring.Result +GlobalCarData._carlists[id].price+" <sprite index=0>";

        }
        selectbut.Select();

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
        var buystring = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI","BuyYes/No");

        bool result = await yesNo.ShowYesNoPanelAsync(buystring.Result+"?");

        if (result)
        {
            if (GlobalCarData._carlists[indexcar].price <= SaveManager.Instance.saveData.money)
                {
                    CarDisplayStats carStats = GetDisplayCarStats(GlobalCarData._carlists[indexcar]);
                    moneyManager.MoneyToTake( GlobalCarData._carlists[indexcar].price);
                    SaveManager.Instance.SaveCar(GlobalCarData._carlists[indexcar].carName,true, carStats.power, carStats.speed, carStats.turbo, GlobalCarData._carlists[indexcar].color, carStats.steer, carStats.traction, carStats.brake);
                    var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI","Select");

                    selecttext.text = operation.Result;
                    selecttextJP.text = operation.Result;
                    SaveManager.Instance.saveData.currentCar = indexcar;
                    SaveManager.Instance.Save();
                    RefreshCarButtonsUI();
                    PlayNewCarClipSafe();
                    Debug.Log("bought");
                }
                else
                {
                    var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync("UI","No money");
                    yesNo.Notify(operation.Result);
                    PlayButtonErrorSafe();
                    Debug.Log("dont have enought Money");
                }
                
            Debug.Log("YES");
        }
        else
        {
            PlayButtonClickSafe();
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
      PlayButtonClickSafe();
      if(player!=null) Destroy(player.gameObject);
      player = Instantiate(Resources.Load<GameObject>(GlobalCarData._carlists[indexcar].carPrefabLocation), spawnCarPoint);
      player.GetComponent<RCCP_CarController>().canControl = false;
      player.GetComponent<RCCP_CarController>().engineRunning = false;
      player.GetComponent<RCCP_CarController>().Lights.lowBeamHeadlights = true;

      // player.GetComponent<Rigidbody>().isKinematic = true;
      // foreach (var mirror in player.GetComponentsInChildren<RCC_Mirror>())
      {
          // mirror.gameObject.SetActive(false); 
      }
      UpdateStats();
  }

    public void UpdateStats()
    {
        CarSO currentCar = GlobalCarData._carlists[indexcar];
        CarDisplayStats displayStats = GetDisplayCarStats(currentCar);

        Name.text = currentCar.carName;
        NameShadow.text = currentCar.carName;
        if (CarInfoText != null)
            CarInfoText.text = currentCar.carInfo;
        if (CarClassImage != null)
            CarClassImage.sprite = currentCar.CarClass;
        PowerSlider.value = displayStats.power;
        PowerText.text = displayStats.power.ToString();
        SpeedSlider.value = displayStats.speed;
        SpeedText.text = displayStats.speed.ToString();
        SteerSlider.value = displayStats.steer;
        SteerText.text = displayStats.steer.ToString();
        BrakeSlider.value = displayStats.brake;
        BrakeText.text = displayStats.brake.ToString();
        Traction.text = displayStats.traction >= 0 && displayStats.traction < TractionText.Count ? TractionText[displayStats.traction].ToString() : displayStats.traction.ToString();
        Turbo.gameObject.SetActive(displayStats.turbo);
    }


  public void LoadGame()
  {
      SaveManager.Instance.saveData.currentCar = indexcar;
       SaveManager.Instance.Save();
      CanvasManager.Back();
  }
  public void loadmaincar()
  {
      int savedCarIndex = SaveManager.Instance.saveData.currentCar;

      if (player == null || indexcar != savedCarIndex)
      {
          if (player != null) Destroy(player.gameObject);

          player = Instantiate(Resources.Load<GameObject>(GlobalCarData._carlists[savedCarIndex].carPrefabLocation), spawnCarPoint);

           player.GetComponent<RCCP_CarController>().canControl = false;
           player.GetComponent<RCCP_CarController>().engineRunning = false;
           player.GetComponent<RCCP_CarController>().Lights.lowBeamHeadlights = true;

           
          // var rb = player.GetComponent<Rigidbody>();
          // if (rb != null) rb.isKinematic = true;
      }
  }
  public void Navigations(InputAction.CallbackContext ctx)
  {
      if (ctx.performed && Time.time - lastMoveTime > moveCooldown)
      {
            
          lastMoveTime = Time.time; // Update last move time
            
          Vector2 inputValue = ctx.ReadValue<Vector2>();
            
          int newindex = indexcar;
            
          if (inputValue.x >= 0.1f)
          {
              newindex++;
              if (newindex > GlobalCarData._carlists.Count - 1)
              {
                  newindex = 0;
              }
          }
          else if (inputValue.x <= -0.1f)
          {
              newindex--;
              if (newindex < 0)
              {
                  newindex = GlobalCarData._carlists.Count - 1;
              }
          }

          // Debug.Log(newindex);
          OnPressedButton(newindex);
      }
    
      // selectbut.Select();
  }
  private void OnEnable()
  {
      if (SM == null)
          SM = SoundManager.Instance;

      if (litenered)
      {
          GlobalCarData._buttonList[SaveManager.Instance.saveData.currentCar].onClick.Invoke();
          GlobalCarData._buttonList[SaveManager.Instance.saveData.currentCar].Select();
          ScrollToSelectedButton(GlobalCarData._buttonList[SaveManager.Instance.saveData.currentCar]);
      }
      // if (litenered)
      //     GlobalCarData._buttonList[SaveManager.Instance.saveData.currentCar].onClick.Invoke();
      SetEvents();
      // selectbut.Select();
  }

  private void SetEvents()
  {
      InputAction navigateAction = GetAction("Navigate");
      if (navigateAction != null)
          navigateAction.performed += Navigations;
      selectbut.Select();
      // playerInput.actions["Submit"].performed += SelectOrBuyCtx;
  }

  private void RefreshCarButtonsUI()
  {
      int buttonCount = Mathf.Min(GlobalCarData._buttonList.Count, GlobalCarData._carlists.Count);

      for (int i = 0; i < buttonCount; i++)
      {
          if (GlobalCarData._buttonList[i] == null)
              continue;

          CarButton carButton = GlobalCarData._buttonList[i].GetComponent<CarButton>();

          if (carButton != null)
              carButton.SetUpButton(GlobalCarData._carlists[i], this);
      }
  }

  public CarDisplayStats GetDisplayCarStats(CarSO carData)
  {
      CarDisplayStats stats = new CarDisplayStats();

      if (carData == null)
          return stats;

      SaveManager.SaveData.CarSpecs savedCarSpecs = SaveManager.Instance != null ? SaveManager.Instance.GetCarSpecs(carData.carName) : null;

      if (savedCarSpecs != null && savedCarSpecs.isBought)
      {
          stats.power = savedCarSpecs.power;
          stats.speed = savedCarSpecs.topSpeed;
          stats.steer = savedCarSpecs.steerAngle;
          stats.brake = savedCarSpecs.brake;
          stats.traction = savedCarSpecs.traction;
          stats.turbo = savedCarSpecs.turbo;
          return stats;
      }

      return BuildStatsFromCarPrefab(carData);
  }

  private CarDisplayStats BuildStatsFromCarPrefab(CarSO carData)
  {
      CarDisplayStats stats = new CarDisplayStats();

      RCCP_CarController sourceController = TryGetLiveCarController(carData);

      if (sourceController == null)
      {
          GameObject carPrefab = Resources.Load<GameObject>(carData.carPrefabLocation);

          if (carPrefab != null)
              sourceController = carPrefab.GetComponent<RCCP_CarController>() ?? carPrefab.GetComponentInChildren<RCCP_CarController>(true);
      }

      if (sourceController == null)
          return stats;

      RCCP_Engine engine = sourceController.Engine != null ? sourceController.Engine : sourceController.GetComponentInChildren<RCCP_Engine>(true);
      RCCP_Gearbox gearbox = sourceController.Gearbox != null ? sourceController.Gearbox : sourceController.GetComponentInChildren<RCCP_Gearbox>(true);
      RCCP_Axle frontAxle = sourceController.FrontAxle != null ? sourceController.FrontAxle : sourceController.GetComponentInChildren<RCCP_Axle>(true);

      stats.power = engine != null ? Mathf.RoundToInt(engine.maximumTorqueAsNM) : 0;
      stats.speed = Mathf.RoundToInt(CalculateEstimatedTopSpeed(sourceController, engine, gearbox));
      stats.steer = frontAxle != null ? Mathf.RoundToInt(frontAxle.maxSteerAngle) : 0;
      stats.brake = Mathf.RoundToInt(GetRepresentativeBrakeValue(sourceController));
      stats.traction = EstimateTractionIndex(sourceController);
      stats.turbo = engine != null && (engine.turboCharged || engine.maxTurboChargePsi > 0f);

      return stats;
  }

  private RCCP_CarController TryGetLiveCarController(CarSO carData)
  {
      if (carData == null || player == null || indexcar < 0 || indexcar >= GlobalCarData._carlists.Count)
          return null;

      if (GlobalCarData._carlists[indexcar] != carData)
          return null;

      return player.GetComponent<RCCP_CarController>();
  }

  private float CalculateEstimatedTopSpeed(RCCP_CarController carController, RCCP_Engine engine, RCCP_Gearbox gearbox)
  {
      if (carController == null || engine == null || gearbox == null || gearbox.gearRatios == null || gearbox.gearRatios.Length == 0)
          return 0f;

      float lastGearRatio = gearbox.gearRatios[gearbox.gearRatios.Length - 1];
      float differentialRatio = GetAverageFinalDriveRatio(carController);
      float radius = GetAverageDrivenWheelRadius(carController);

      if (lastGearRatio <= 0f || differentialRatio <= 0f || radius <= 0f)
          return Mathf.Max(0f, carController.maximumSpeed);

      return (engine.maxEngineRPM / lastGearRatio / differentialRatio) * (2f * Mathf.PI * radius) * 60f / 1000f;
  }

  private float GetAverageFinalDriveRatio(RCCP_CarController carController)
  {
      if (carController == null || carController.Differentials == null || carController.Differentials.Length == 0)
          return Mathf.Max(0.01f, carController != null ? carController.differentialRatio : 1f);

      float ratio = 0f;
      int validCount = 0;

      for (int i = 0; i < carController.Differentials.Length; i++)
      {
          RCCP_Differential differential = carController.Differentials[i];

          if (differential == null)
              continue;

          ratio += differential.finalDriveRatio;
          validCount++;
      }

      return validCount > 0 ? Mathf.Max(0.01f, ratio / validCount) : Mathf.Max(0.01f, carController.differentialRatio);
  }

  private float GetAverageDrivenWheelRadius(RCCP_CarController carController)
  {
      if (carController == null)
          return 0f;

      float radius = 0f;
      int wheelCount = 0;

      List<RCCP_Axle> drivenAxles = GetCandidateDrivenAxles(carController);

      for (int i = 0; i < drivenAxles.Count; i++)
      {
          RCCP_Axle axle = drivenAxles[i];

          if (axle == null)
              continue;

          if (axle.leftWheelCollider != null && axle.leftWheelCollider.WheelCollider != null)
          {
              radius += axle.leftWheelCollider.WheelCollider.radius;
              wheelCount++;
          }

          if (axle.rightWheelCollider != null && axle.rightWheelCollider.WheelCollider != null)
          {
              radius += axle.rightWheelCollider.WheelCollider.radius;
              wheelCount++;
          }
      }

      return wheelCount > 0 ? radius / wheelCount : 0f;
  }

  private List<RCCP_Axle> GetCandidateDrivenAxles(RCCP_CarController carController)
  {
      List<RCCP_Axle> drivenAxles = new List<RCCP_Axle>();

      if (carController == null)
          return drivenAxles;

      if (carController.PoweredAxles != null && carController.PoweredAxles.Count > 0)
      {
          for (int i = 0; i < carController.PoweredAxles.Count; i++)
          {
              RCCP_Axle axle = carController.PoweredAxles[i];

              if (axle != null && !drivenAxles.Contains(axle))
                  drivenAxles.Add(axle);
          }
      }

      if (drivenAxles.Count > 0)
          return drivenAxles;

      if (carController.AxleManager != null && carController.AxleManager.Axles != null && carController.AxleManager.Axles.Count > 0)
      {
          for (int i = 0; i < carController.AxleManager.Axles.Count; i++)
          {
              RCCP_Axle axle = carController.AxleManager.Axles[i];

              if (axle == null)
                  continue;

              if (axle.isPower)
                  drivenAxles.Add(axle);
          }

          if (drivenAxles.Count > 0)
              return drivenAxles;

          for (int i = 0; i < carController.AxleManager.Axles.Count; i++)
          {
              RCCP_Axle axle = carController.AxleManager.Axles[i];

              if (axle != null)
                  drivenAxles.Add(axle);
          }
      }

      if (drivenAxles.Count == 0 && carController.FrontAxle != null)
          drivenAxles.Add(carController.FrontAxle);

      if (drivenAxles.Count == 0 && carController.RearAxle != null)
          drivenAxles.Add(carController.RearAxle);

      return drivenAxles;
  }

  private float GetRepresentativeBrakeValue(RCCP_CarController carController)
  {
      if (carController == null || carController.AxleManager == null || carController.AxleManager.Axles == null || carController.AxleManager.Axles.Count == 0)
          return 0f;

      float maxBrake = 0f;

      for (int i = 0; i < carController.AxleManager.Axles.Count; i++)
      {
          RCCP_Axle axle = carController.AxleManager.Axles[i];

          if (axle == null || !axle.isBrake)
              continue;

          maxBrake = Mathf.Max(maxBrake, axle.maxBrakeTorque);
      }

      return maxBrake;
  }

  private int EstimateTractionIndex(RCCP_CarController carController)
  {
      if (carController == null || carController.Stability == null)
          return 0;

      float tractionStrength = carController.Stability.tractionHelperStrength;

      if (tractionStrength < 0.15f)
          return 0;

      if (tractionStrength < 0.35f)
          return 1;

      return 2;
  }

  private void OnDisable()
  {
      RemoveEvents();
  }
  private void RemoveEvents()
  {
      InputAction navigateAction = GetAction("Navigate");
      if (navigateAction != null)
          navigateAction.performed -= Navigations;
      // playerInput.actions["Submit"].performed -= SelectOrBuyCtx;
  }

  private SoundManager GetSoundManager()
  {
      if (SM == null)
          SM = SoundManager.Instance;

      return SM;
  }

  private void PlayButtonClickSafe()
  {
      SoundManager soundManager = GetSoundManager();

      if (soundManager != null)
          soundManager.PlayButtonClick();
  }

  private void PlayButtonErrorSafe()
  {
      SoundManager soundManager = GetSoundManager();

      if (soundManager != null)
          soundManager.PlayButtonError();
  }

  private void PlayNewCarClipSafe()
  {
      SoundManager soundManager = GetSoundManager();

      if (soundManager != null)
          soundManager.PlayNewCarClip();
  }

  private InputAction GetAction(string actionName)
  {
      if (playerInput == null && InputManager.Instance != null)
          playerInput = InputManager.Instance.GetPlayerInput();

      if (playerInput == null || playerInput.actions == null)
          return null;

      return playerInput.actions[actionName];
  }
}
