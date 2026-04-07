using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonScrollCall : MonoBehaviour , ISelectHandler
{
    public AutoScroll autoScroll;

    public void OnSelect(BaseEventData eventData)
    {
        autoScroll.ScrollToSelectedButton(GetComponent<Button>());
    }

}
