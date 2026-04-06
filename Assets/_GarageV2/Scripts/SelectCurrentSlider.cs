using System;
using UnityEngine;
using UnityEngine.UI;

public class SelectCurrentSlider : MonoBehaviour
{
    private void OnEnable()
    {
            if(GetComponent<Slider>())
        GetComponent<Slider>().Select();
    }
}
