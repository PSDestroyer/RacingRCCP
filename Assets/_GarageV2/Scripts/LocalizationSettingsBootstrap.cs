using System;
using System.Collections;
using HalvaStudio.Save;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
#if UNITY_SWITCH && !UNITY_EDITOR
using _Assets._PlatformSpeciffics.Switch;
#endif

public static class LocalizationSettingsBootstrap
{
    private const string ResourcePath = "Localization/Localization Settings";
    private const string FallbackLanguageCode = "en";
    private static LocalizationRuntimeController runtimeController;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        var settings = Resources.Load<LocalizationSettings>(ResourcePath);

        if (settings == null)
        {
            Debug.LogError($"Localization settings resource not found at Resources/{ResourcePath}. Localization cannot initialize.");
            return;
        }

        if (!LocalizationSettings.HasSettings || LocalizationSettings.Instance != settings)
            LocalizationSettings.Instance = settings;

        if (runtimeController == null)
        {
            var controllerObject = new GameObject(nameof(LocalizationRuntimeController));
            UnityEngine.Object.DontDestroyOnLoad(controllerObject);
            runtimeController = controllerObject.AddComponent<LocalizationRuntimeController>();
        }
    }

    /// <summary>
    /// Changes the active locale and persists it through SaveManager.
    /// This can be connected directly to a future language selector.
    /// </summary>
    public static void SetLanguage(string languageCode)
    {
        if (runtimeController == null)
        {
            Debug.LogWarning("Localization is not initialized yet.");
            return;
        }

        runtimeController.SetLanguage(languageCode, true);
    }

    public static string CurrentLanguageCode
    {
        get
        {
            Locale locale = LocalizationSettings.SelectedLocale;
            return locale != null ? locale.Identifier.Code : FallbackLanguageCode;
        }
    }

    private sealed class LocalizationRuntimeController : MonoBehaviour
    {
        private bool localizationReady;
        private SaveManager saveManager;

        private IEnumerator Start()
        {
            var initialization = LocalizationSettings.InitializationOperation;
            if (!initialization.IsDone)
                yield return initialization;

            if (initialization.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
            {
                Debug.LogError("Localization initialization failed. Check the Localization Addressables groups.");
                yield break;
            }

            localizationReady = true;

            while (saveManager == null || saveManager.saveData == null)
            {
                saveManager = UnityEngine.Object.FindFirstObjectByType<SaveManager>();
                yield return null;
            }

#if UNITY_SWITCH && !UNITY_EDITOR
            // Nintendo's desired language already accounts for the supported-language
            // list and the priority configured in the console's system settings.
            string switchLanguageCode = NormalizeNintendoLanguageCode(NintendoManager.GetDesiredLanguage());
            if (SetLanguage(switchLanguageCode, true))
                yield break;

            Debug.LogWarning($"Nintendo Switch desired language '{switchLanguageCode}' is unavailable. Falling back to English.");
            if (SetLanguage(FallbackLanguageCode, true))
                yield break;
#endif

            string savedLanguageCode = saveManager.saveData.languageCode;

            if (!string.IsNullOrWhiteSpace(savedLanguageCode) && SetLanguage(savedLanguageCode, false))
                yield break;

            // On a fresh save the Startup Locale Selectors choose the device locale.
            // If it is unsupported, the Specific Locale Selector selects English.
            Locale selectedLocale = LocalizationSettings.SelectedLocale;
            if (selectedLocale == null)
                selectedLocale = FindLocale(FallbackLanguageCode);

            if (selectedLocale != null)
                LocalizationSettings.SelectedLocale = selectedLocale;

            SaveSelectedLanguage();
        }

        public bool SetLanguage(string languageCode, bool saveSelection)
        {
            if (!localizationReady)
            {
                StartCoroutine(SetLanguageWhenReady(languageCode, saveSelection));
                return false;
            }

            Locale locale = FindLocale(languageCode);
            if (locale == null)
            {
                Debug.LogWarning($"Locale '{languageCode}' is not available.");
                return false;
            }

            LocalizationSettings.SelectedLocale = locale;

            if (saveSelection)
                SaveSelectedLanguage();

            return true;
        }

        private IEnumerator SetLanguageWhenReady(string languageCode, bool saveSelection)
        {
            while (!localizationReady)
                yield return null;

            SetLanguage(languageCode, saveSelection);
        }

        private static Locale FindLocale(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
                return null;

            foreach (Locale locale in LocalizationSettings.AvailableLocales.Locales)
            {
                if (string.Equals(locale.Identifier.Code, languageCode, StringComparison.OrdinalIgnoreCase))
                    return locale;
            }

            return null;
        }

#if UNITY_SWITCH && !UNITY_EDITOR
        private static string NormalizeNintendoLanguageCode(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
                return FallbackLanguageCode;

            string normalizedCode = languageCode.Trim().Replace('_', '-').ToLowerInvariant();

            if (normalizedCode.StartsWith("en-"))
                return "en";
            if (normalizedCode.StartsWith("fr-"))
                return "fr";
            if (normalizedCode.StartsWith("es-"))
                return "es";
            if (normalizedCode.StartsWith("de-"))
                return "de";
            if (normalizedCode.StartsWith("it-"))
                return "it";
            if (normalizedCode.StartsWith("ja-"))
                return "ja";
            if (normalizedCode.StartsWith("ko-"))
                return "ko";

            return normalizedCode;
        }
#endif

        private void SaveSelectedLanguage()
        {
            if (saveManager == null)
                saveManager = UnityEngine.Object.FindFirstObjectByType<SaveManager>();

            Locale selectedLocale = LocalizationSettings.SelectedLocale;
            if (saveManager == null || saveManager.saveData == null || selectedLocale == null)
                return;

            string selectedCode = selectedLocale.Identifier.Code;
            if (saveManager.saveData.languageCode == selectedCode)
                return;

            saveManager.saveData.languageCode = selectedCode;
            saveManager.Save();
        }
    }
}

public static class UILocalization
{
    public static string Get(string key, string fallback)
    {
        if (LocalizationSettings.StringDatabase == null)
            return fallback;

        string localized = LocalizationSettings.StringDatabase.GetLocalizedString(
            "UI",
            key);

        return string.IsNullOrEmpty(localized) ? fallback : localized;
    }

    public static string GetKnownText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        string key;
        switch (value.Trim().Replace("_", " ").ToLowerInvariant())
        {
            case "racing": key = "ui.racing"; break;
            case "drift": key = "ui.drift"; break;
            case "grip": key = "ui.grip"; break;
            case "next tour": key = "ui.next_tour"; break;
            case "previous tour": key = "ui.previous_tour"; break;
            case "leaderboard": key = "ui.leaderboard"; break;
            default: return value;
        }

        return Get(key, value);
    }
}
