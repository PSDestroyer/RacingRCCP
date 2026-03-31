using System;
using System.Collections;
using System.Collections.Generic;
using HalvaStudio.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class CarSelection : MonoBehaviour
{
    public PlayerInput playerInput;
  [SerializeField] public Transform spawnCarPoint;
  [SerializeField] private Transform spawninPanel;
  [SerializeField] private CarButton uiPrefab;
  [SerializeField] private Button selectbut;
    [SerializeField] private TextMeshProUGUI selecttext;
    [SerializeField] private TextMeshProUGUI selecttextJP;
  // [SerializeField] private MoneyManager moneyManager;
  [SerializeField] public GameObject player;
  private static int indexcar;
  private GameObject PlayerCar1;
    public GarageUIController CanvasManager;
    private bool litenered;
    public SoundManager SM;
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
        if (SaveManager.Instance.IsCarBought(GlobalCarData._carlists[indexcar].carName))
        {
            selecttext.text = "select";
            selecttextJP.text = "select";
            
            SaveManager.Instance.saveData.currentCar = indexcar;
        }
        else
        {
            selecttext.text = /*"<color=#DFA93B>*/"buy "+GlobalCarData._carlists[id].price+"<sprite index=0>";
            selecttextJP.text = /*"<color=#DFA93B>*/"buy "+GlobalCarData._carlists[id].price+"<sprite index=0>";
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
            if (GlobalCarData._carlists[indexcar].price <= SaveManager.Instance.saveData.money)
            {
                SaveManager.Instance.saveData.money -= GlobalCarData._carlists[indexcar].price;
                SaveManager.Instance.SaveCar(GlobalCarData._carlists[indexcar].carName,true, GlobalCarData._carlists[indexcar].power,GlobalCarData._carlists[indexcar].speed,GlobalCarData._carlists[indexcar].turbo,GlobalCarData._carlists[indexcar].color,GlobalCarData._carlists[indexcar].steerAngle,GlobalCarData._carlists[indexcar].traction,GlobalCarData._carlists[indexcar].brake);
                
                selecttext.text = "select";
                SaveManager.Instance.saveData.currentCar = indexcar;
                SaveManager.Instance.Save();
                Debug.Log("bought");
            }
            else
            {
                Debug.Log("dont have enought Money");
            }
        }
        SM.PlayButtonClick();
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
      SM.PlayButtonClick();
      if(player!=null) Destroy(player.gameObject);
      player = Instantiate(Resources.Load<GameObject>(GlobalCarData._carlists[indexcar].carPrefabLocation), spawnCarPoint);
      player.GetComponent<Rigidbody>().isKinematic = true;
      // foreach (var mirror in player.GetComponentsInChildren<RCC_Mirror>())
      {
          // mirror.gameObject.SetActive(false); 
      }
      UpdateStats();
  }

    public void UpdateStats()
    {
        Name.text = GlobalCarData._carlists[indexcar].carName;
        NameShadow.text = GlobalCarData._carlists[indexcar].carName;
        PowerSlider.value = GlobalCarData._carlists[indexcar].power;
        PowerText.text = GlobalCarData._carlists[indexcar].power.ToString();
        SpeedSlider.value = GlobalCarData._carlists[indexcar].speed;
        SpeedText.text = GlobalCarData._carlists[indexcar].speed.ToString();
        SteerSlider.value = GlobalCarData._carlists[indexcar].steerAngle;
        SteerText.text = GlobalCarData._carlists[indexcar].steerAngle.ToString();
        BrakeSlider.value = GlobalCarData._carlists[indexcar].brake;
        BrakeText.text = GlobalCarData._carlists[indexcar].brake.ToString();
        Traction.text = TractionText[GlobalCarData._carlists[indexcar].traction].ToString();
        Turbo.gameObject.SetActive(GlobalCarData._carlists[indexcar].turbo);
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

          var rb = player.GetComponent<Rigidbody>();
          if (rb != null) rb.isKinematic = true;
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
      playerInput.actions["Navigate"].performed += Navigations;
      selectbut.Select();
      // playerInput.actions["Submit"].performed += SelectOrBuyCtx;
  }

  private void OnDisable()
  {
      RemoveEvents();
  }
  private void RemoveEvents()
  {
      playerInput.actions["Navigate"].performed -= Navigations;
      // playerInput.actions["Submit"].performed -= SelectOrBuyCtx;
  }
}
