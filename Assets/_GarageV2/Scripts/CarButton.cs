using System;
using System.Collections;
using System.Collections.Generic;
using HalvaStudio.Save;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CarButton : MonoBehaviour 
{
    [SerializeField] private Image CarIcon;
    [SerializeField] private Image CtrIcon;
    [SerializeField] private TextMeshProUGUI carText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private TextMeshProUGUI PowerText;
    [SerializeField] private TextMeshProUGUI AvaliableText;
    [SerializeField] private GameObject selected;
    [NonSerialized] private int id;
    [NonSerialized] private CarSelection carSelection;
    // [NonSerialized] private Animator animator;

    private void Awake()
    {
        NormalizeVisuals();
    }

    private void OnValidate()
    {
        NormalizeVisuals();
    }

    public void SetUpButton(CarSO carData,CarSelection carSelections)
    {
        NormalizeVisuals();
        CarIcon.sprite = carData.carsprite;
        CtrIcon.sprite = carData.CarClass;
        carText.text = carData.carName;
        PowerText.text = carData.power+" HP";
        id = carData.id;
        if (carData.price != 0)
        {
            priceText.text = carData.price.ToString() +"<sprite index=0>";
        }
        else
        { 
            priceText.text = "Free!";
        }
        carSelection = carSelections;
        // animator = GetComponent<Animator>();
        if (SaveManager.Instance.IsCarBought(carData.carName))
        {
            AvaliableText.text = "Purchased";
            if (carData.id == SaveManager.Instance.saveData.currentCar)
            {
                AvaliableText.text = "Selected";
 
            }
        }
        else
        {
            AvaliableText.text = /*"<color=#DFA93B>*/"Buy! ";//+GlobalCarData._carlists[id].price+"<sprite index=0>";
        }
    }

    
    public void isPressed()
    {
        selected.SetActive(true);
        NormalizeVisuals();
        // animator.SetTrigger("Selected");
    }

    public void UnPressed()
    {
        selected.SetActive(false);
        // animator.SetTrigger("Normal");

    }

    public void OnSelect(BaseEventData eventData)
    {
        // carSelection.ScrollToSelectedButton(GlobalCarData._buttonList[id]);
        // carSelection.OnPressedButton(id);
        // animator.SetTrigger("Selected");
// Debug.Log("ALili");
    }

    private void NormalizeVisuals()
    {
        RectTransform rect = GetComponent<RectTransform>();

        if (rect != null && (rect.sizeDelta.x <= 0.01f || rect.sizeDelta.y <= 0.01f))
            rect.sizeDelta = new Vector2(330f, 150f);

        Graphic rootGraphic = GetComponent<Graphic>();
        if (rootGraphic != null)
            rootGraphic.enabled = true;

        if (selected != null)
            selected.transform.SetAsFirstSibling();

        EnsureGraphicVisible(CarIcon);
        EnsureGraphicVisible(CtrIcon);
        EnsureGraphicVisible(carText);
        EnsureGraphicVisible(priceText);
        EnsureGraphicVisible(PowerText);
        EnsureGraphicVisible(AvaliableText);
    }

    private void EnsureGraphicVisible(Graphic graphic)
    {
        if (graphic == null)
            return;

        graphic.gameObject.SetActive(true);
        graphic.enabled = true;
    }
}
