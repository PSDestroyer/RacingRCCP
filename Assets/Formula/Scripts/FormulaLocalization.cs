using System;
using System.Globalization;
using HalvaStudio.Save;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace FormulaGame
{
    // Unity Localization owns the selected locale; this caches its preloaded UI table.
    public static class FormulaLocalization
    {
        public const string TableName = "FormulaUI";
        public const string FontTableName = "FormulaFonts";
        public static readonly string[] SupportedCodes = { "en", "fr", "de", "es", "it", "nl", "pt", "ru", "ja", "ko", "zh-Hans", "zh-Hant" };
        private static readonly string[] NativeNames = { "English", "Français", "Deutsch", "Español", "Italiano", "Nederlands", "Português", "Русский", "日本語", "한국어", "简体中文", "繁體中文" };
        private static StringTable table;
        public static event Action Changed;
        public static int Revision { get; private set; }
        public static bool Ready => table != null && table.LocaleIdentifier == LocalizationSettings.SelectedLocale?.Identifier;
        public static CultureInfo Culture => table?.LocaleIdentifier.CultureInfo ?? CultureInfo.InvariantCulture;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            table = null; Revision = 0; Changed = null;
            LocalizationSettings.SelectedLocaleChanged -= Load;
            LocalizationSettings.SelectedLocaleChanged += Load;
            var operation = LocalizationSettings.InitializationOperation;
            if (operation.IsDone) RestoreSavedLocale();
            else operation.Completed += _ => RestoreSavedLocale();
        }
        private static void RestoreSavedLocale()
        {
            // Editor font previews may initialize Localization before entering Play Mode.
            // Reapply the profile once per game launch, including when package initialization is cached.
            var locale = new FormulaSavedLocaleSelector().GetStartupLocale(LocalizationSettings.AvailableLocales);
            if (locale != null && locale != LocalizationSettings.SelectedLocale) LocalizationSettings.SelectedLocale = locale;
            else Load(LocalizationSettings.SelectedLocale);
        }
        private static void Load(Locale locale)
        {
            if (locale == null) return;
            table = null;
            var operation = LocalizationSettings.StringDatabase.GetTableAsync(TableName, locale);
            void Complete(AsyncOperationHandle<StringTable> result)
            {
                if (locale != LocalizationSettings.SelectedLocale) return;
                if (result.Status != AsyncOperationStatus.Succeeded) { Debug.LogError("Formula localization table could not be loaded: " + locale.Identifier.Code); return; }
                table = result.Result; Revision++; Changed?.Invoke();
            }
            if (operation.IsDone) Complete(operation); else operation.Completed += Complete;
        }
        public static string T(string source)
        {
            if (string.IsNullOrEmpty(source)) return source;
#if UNITY_EDITOR
            // Keep generated source text and Edit Mode domain checks canonical.
            if (!Application.isPlaying) return source;
#endif
            return table?.GetEntry(source)?.Value ?? source;
        }
        public static string F(string source, params object[] arguments) => string.Format(Culture, T(source), arguments);
        public static bool IsSupported(string code) => Array.IndexOf(SupportedCodes, code) >= 0;
        public static string LanguageName
        {
            get { int index = Array.IndexOf(SupportedCodes, LocalizationSettings.SelectedLocale?.Identifier.Code); return NativeNames[Math.Max(0, index)]; }
        }
        public static string NextLanguage
        {
            get { int index = Array.IndexOf(SupportedCodes, LocalizationSettings.SelectedLocale?.Identifier.Code); return SupportedCodes[(index + 1) % SupportedCodes.Length]; }
        }
        public static string SystemLanguageCode()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.French: return "fr";
                case SystemLanguage.German: return "de";
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.Italian: return "it";
                case SystemLanguage.Dutch: return "nl";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.Russian: return "ru";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.ChineseTraditional: return "zh-Hant";
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified: return "zh-Hans";
                default: return "en";
            }
        }
        public static bool ChangeLanguage(string code, out string error)
        {
            var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
            if (!IsSupported(code) || locale == null) { error = T("Language is unavailable."); return false; }
            if (!FormulaProgression.Commit(d => { d.language = code; return null; }, out error)) return false;
            LocalizationSettings.SelectedLocale = locale;
            return true;
        }
    }

    [Serializable]
    public sealed class FormulaSavedLocaleSelector : IStartupLocaleSelector
    {
        public Locale GetStartupLocale(ILocalesProvider availableLocales)
        {
            if (!Application.isPlaying) return null;
            string code = SaveManager.Instance.saveData.language;
            return availableLocales.GetLocale(FormulaLocalization.IsSupported(code) ? code : FormulaLocalization.SystemLanguageCode()) ?? availableLocales.GetLocale("en");
        }
    }
}
