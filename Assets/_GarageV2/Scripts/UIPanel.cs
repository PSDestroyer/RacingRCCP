using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIPanel : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private GameObject defaultSelected;
    [SerializeField] private GameObject panelCamera;

    public GameObject Root => root;
    public GameObject PanelCamera => panelCamera;

    private void Awake()
    {
        if (root == null)
            root = gameObject;
    }

    public void SetRoot(GameObject rootObject)
    {
        root = rootObject != null ? rootObject : gameObject;
    }

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
        if (root == null)
            root = gameObject;

        root.SetActive(true);
    }

    public virtual void Hide()
    {
        if (root == null)
            root = gameObject;

        root.SetActive(false);
    }
}
