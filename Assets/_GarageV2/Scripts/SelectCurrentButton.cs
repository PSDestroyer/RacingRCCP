using System;
using UnityEngine;
using UnityEngine.UI;

public class SelectCurrentButton : MonoBehaviour
{
    private void OnEnable()
    {
            if(GetComponent<Button>())
        GetComponent<Button>().Select();
    }
}
