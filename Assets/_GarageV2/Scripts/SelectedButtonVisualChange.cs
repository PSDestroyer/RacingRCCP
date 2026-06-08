using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SelectedButtonVisualChange : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    [Header("Targets")]
    [SerializeField] private Image firstImage;
    [SerializeField] private Image secondImage;
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private Selectable targetSelectable;

    [Header("Colors")]
    [SerializeField] private Color normalImageColor = Color.white;
    [SerializeField] private Color selectedImageColor = Color.yellow;
    [SerializeField] private Color disabledImageColor = Color.gray;
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private Color selectedTextColor = Color.yellow;
    [SerializeField] private Color disabledTextColor = Color.gray;

    private bool lastInteractableState = true;

    private void OnEnable()
    {
        ResolveSelectable();
        lastInteractableState = IsInteractable();

        if (lastInteractableState)
            ApplyNormalState();
        else
            ApplyDisabledState();
    }

    private void Update()
    {
        ResolveSelectable();

        bool isInteractable = IsInteractable();

        if (isInteractable == lastInteractableState)
            return;

        lastInteractableState = isInteractable;

        if (isInteractable)
            ApplyNormalState();
        else
            ApplyDisabledState();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!IsInteractable())
            return;

        ApplySelectedState();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (!IsInteractable())
        {
            ApplyDisabledState();
            return;
        }

        ApplyNormalState();
    }

    private void ApplySelectedState()
    {
        if (firstImage != null)
            firstImage.color = selectedImageColor;

        if (secondImage != null)
            secondImage.color = selectedImageColor;

        if (targetText != null)
            targetText.color = selectedTextColor;
    }

    private void ApplyNormalState()
    {
        if (firstImage != null)
            firstImage.color = normalImageColor;

        if (secondImage != null)
            secondImage.color = normalImageColor;

        if (targetText != null)
            targetText.color = normalTextColor;
    }

    private void ApplyDisabledState()
    {
        if (firstImage != null)
            firstImage.color = disabledImageColor;

        if (secondImage != null)
            secondImage.color = disabledImageColor;

        if (targetText != null)
            targetText.color = disabledTextColor;
    }

    private void ResolveSelectable()
    {
        if (targetSelectable == null)
            targetSelectable = GetComponent<Selectable>();
    }

    private bool IsInteractable()
    {
        if (targetSelectable == null)
            return true;

        return targetSelectable.IsInteractable();
    }
}
