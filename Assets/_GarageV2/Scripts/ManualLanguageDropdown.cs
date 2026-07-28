using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// Connect this component manually to a TMP_Dropdown.
/// The dropdown options must use this order:
/// English, Deutsch, Español, Français, Italiano, 日本語, 한국어.
/// </summary>
public sealed class ManualLanguageDropdown : MonoBehaviour
{
    private static readonly string[] LanguageCodes =
    {
        "en", "de", "es", "fr", "it", "ja", "ko"
    };

    [SerializeField] private TMP_Dropdown dropdown;
    private GameObject lastVisibleSelection;

    private IEnumerator Start()
    {
        if (dropdown == null)
            dropdown = GetComponent<TMP_Dropdown>();

        if (dropdown == null)
        {
            Debug.LogError("ManualLanguageDropdown requires a TMP_Dropdown reference.", this);
            yield break;
        }

        yield return LocalizationSettings.InitializationOperation;

        dropdown.onValueChanged.RemoveListener(SelectLanguage);
        dropdown.onValueChanged.AddListener(SelectLanguage);
        RefreshSelectedLanguage(LocalizationSettings.SelectedLocale);
        LocalizationSettings.SelectedLocaleChanged += RefreshSelectedLanguage;
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= RefreshSelectedLanguage;

        if (dropdown != null)
            dropdown.onValueChanged.RemoveListener(SelectLanguage);
    }

    private void LateUpdate()
    {
        if (dropdown == null || !dropdown.IsExpanded || EventSystem.current == null)
        {
            lastVisibleSelection = null;
            return;
        }

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null || selected == lastVisibleSelection)
            return;

        ScrollRect scrollRect = selected.GetComponentInParent<ScrollRect>();
        RectTransform selectedRect = selected.GetComponent<RectTransform>();
        if (scrollRect == null || selectedRect == null || scrollRect.content == null)
            return;

        RectTransform viewport = scrollRect.viewport != null
            ? scrollRect.viewport
            : scrollRect.GetComponent<RectTransform>();
        if (viewport == null || !selectedRect.IsChildOf(scrollRect.content))
            return;

        Canvas.ForceUpdateCanvases();

        Bounds selectedBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            viewport, selectedRect);
        Rect visibleRect = viewport.rect;
        float verticalOffset = 0f;

        if (selectedBounds.min.y < visibleRect.yMin)
            verticalOffset = selectedBounds.min.y - visibleRect.yMin;
        else if (selectedBounds.max.y > visibleRect.yMax)
            verticalOffset = selectedBounds.max.y - visibleRect.yMax;

        if (!Mathf.Approximately(verticalOffset, 0f))
        {
            scrollRect.StopMovement();
            Vector2 position = scrollRect.content.anchoredPosition;
            position.y -= verticalOffset;
            scrollRect.content.anchoredPosition = position;
        }

        lastVisibleSelection = selected;
    }

    public void SelectLanguage(int index)
    {
        if (index < 0 || index >= LanguageCodes.Length)
        {
            Debug.LogWarning($"Language dropdown index {index} is not configured.", this);
            return;
        }

        LocalizationSettingsBootstrap.SetLanguage(LanguageCodes[index]);
    }

    private void RefreshSelectedLanguage(Locale locale)
    {
        if (dropdown == null || locale == null)
            return;

        string selectedCode = locale.Identifier.Code;
        for (int index = 0; index < LanguageCodes.Length; index++)
        {
            if (!selectedCode.StartsWith(LanguageCodes[index], StringComparison.OrdinalIgnoreCase))
                continue;

            dropdown.SetValueWithoutNotify(index);
            dropdown.RefreshShownValue();
            return;
        }
    }
}
