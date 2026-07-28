using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Linq;

[RequireComponent(typeof(TMP_Dropdown))]
public class LanguageDropdown : MonoBehaviour
{
    private static readonly string[] Codes = { "en", "de", "es", "fr", "it", "ja", "ko" };
    private static readonly string[] Names =
    {
        "English", "Deutsch", "Español", "Français", "Italiano", "日本語", "한국어"
    };

    private TMP_Dropdown dropdown;
    [SerializeField] private TMP_FontAsset languageFont;
    private bool initialized;
    private float lastMoveTime;

    private void Awake()
    {
        dropdown = GetComponent<TMP_Dropdown>();
        PopulateOptions();
        Navigation navigation = dropdown.navigation;
        navigation.mode = Navigation.Mode.Automatic;
        navigation.wrapAround = true;
        dropdown.navigation = navigation;
        ApplyFont();
        LinkSiblingNavigation();
    }

    private void Update()
    {
        if (dropdown == null || !dropdown.IsExpanded || EventSystem.current == null || Gamepad.current == null)
            return;

        List<Toggle> items = FindOpenItems();
        if (items.Count == 0)
            return;

        int current = items.FindIndex(item => item.gameObject == EventSystem.current.currentSelectedGameObject);
        if (current < 0)
            current = Mathf.Clamp(dropdown.value, 0, items.Count - 1);

        int direction = 0;
        if (Gamepad.current.dpad.up.wasPressedThisFrame)
            direction = -1;
        else if (Gamepad.current.dpad.down.wasPressedThisFrame)
            direction = 1;

        if (direction != 0 && Time.unscaledTime - lastMoveTime > 0.12f)
        {
            current = (current + direction + items.Count) % items.Count;
            EventSystem.current.SetSelectedGameObject(items[current].gameObject);
            lastMoveTime = Time.unscaledTime;
            return;
        }

        if (Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            dropdown.value = current;
            dropdown.Hide();
            EventSystem.current.SetSelectedGameObject(dropdown.gameObject);
        }
        else if (Gamepad.current.buttonEast.wasPressedThisFrame)
        {
            dropdown.Hide();
            EventSystem.current.SetSelectedGameObject(dropdown.gameObject);
        }
    }

    public void ConfigureFont(TMP_FontAsset font)
    {
        languageFont = font;
        ApplyFont();
    }

    private void ApplyFont()
    {
        if (languageFont == null || dropdown == null)
            return;

        if (dropdown.captionText != null)
            dropdown.captionText.font = languageFont;
        if (dropdown.itemText != null)
            dropdown.itemText.font = languageFont;

        foreach (TMP_Text text in dropdown.GetComponentsInChildren<TMP_Text>(true))
            text.font = languageFont;
    }

    private void LinkSiblingNavigation()
    {
        TMP_Dropdown sibling = transform.parent != null
            ? transform.parent.GetComponentsInChildren<TMP_Dropdown>(true).FirstOrDefault(item => item != dropdown)
            : null;
        if (sibling == null)
            return;

        Navigation siblingNavigation = sibling.navigation;
        siblingNavigation.mode = Navigation.Mode.Explicit;
        siblingNavigation.selectOnDown = dropdown;
        sibling.navigation = siblingNavigation;

        Navigation languageNavigation = dropdown.navigation;
        languageNavigation.mode = Navigation.Mode.Explicit;
        languageNavigation.selectOnUp = sibling;
        languageNavigation.selectOnDown = sibling;
        dropdown.navigation = languageNavigation;
    }

    private List<Toggle> FindOpenItems()
    {
        return dropdown.transform.root.GetComponentsInChildren<Toggle>(false)
            .Where(toggle => toggle.transform.parent != null &&
                             toggle.transform.parent.name.Contains("Content"))
            .OrderByDescending(toggle => ((RectTransform)toggle.transform).anchoredPosition.y)
            .ToList();
    }

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        StartCoroutine(InitializeWhenReady());
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private IEnumerator InitializeWhenReady()
    {
        yield return LocalizationSettings.InitializationOperation;

        if (dropdown == null)
            dropdown = GetComponent<TMP_Dropdown>();

        PopulateOptions();
        RefreshSelection(LocalizationSettings.SelectedLocale);
    }

    private void PopulateOptions()
    {
        if (dropdown == null)
            return;

        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>(Names));
        dropdown.onValueChanged.RemoveListener(SetSelectedLanguage);
        dropdown.onValueChanged.AddListener(SetSelectedLanguage);
        initialized = true;
        dropdown.RefreshShownValue();
    }

    private void SetSelectedLanguage(int index)
    {
        if (!initialized || index < 0 || index >= Codes.Length)
            return;

        LocalizationSettingsBootstrap.SetLanguage(Codes[index]);
    }

    private void OnLocaleChanged(Locale locale)
    {
        RefreshSelection(locale);
    }

    private void RefreshSelection(Locale locale)
    {
        if (!initialized || dropdown == null || locale == null)
            return;

        string code = locale.Identifier.Code;
        for (int i = 0; i < Codes.Length; i++)
        {
            if (!code.StartsWith(Codes[i], System.StringComparison.OrdinalIgnoreCase))
                continue;

            dropdown.SetValueWithoutNotify(i);
            dropdown.RefreshShownValue();
            return;
        }
    }
}
