using UnityEngine;
using UnityEngine.EventSystems;

public class UIPanel : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private GameObject defaultSelected;

    public GameObject DefaultSelected => defaultSelected;

    public virtual void Show()
    {
        root.SetActive(true);
    }

    public virtual void Hide()
    {
        root.SetActive(false);
    }
}