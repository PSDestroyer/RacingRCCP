using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIPanel : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private GameObject defaultSelected;

    public GameObject DefaultSelected
    {
        get
        {
            if (defaultSelected != null)
                return defaultSelected;

            if (root == null)
                return null;

            Selectable[] selectables = root.GetComponentsInChildren<Selectable>(true);
            foreach (Selectable selectable in selectables)
            {
                if (selectable == null || !selectable.IsInteractable())
                    continue;

                return selectable.gameObject;
            }

            return null;
        }
    }

    public virtual void Show()
    {
        root.SetActive(true);
    }

    public virtual void Hide()
    {
        root.SetActive(false);
    }
}
