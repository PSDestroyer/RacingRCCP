using UnityEngine;
using UnityEngine.Localization.Settings;

public static class LocalizationSettingsBootstrap
{
    private const string ResourcePath = "Localization/Localization Settings";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (LocalizationSettings.HasSettings)
            return;

        var settings = Resources.Load<LocalizationSettings>(ResourcePath);

        if (settings == null)
        {
            Debug.LogWarning($"Localization settings resource not found at Resources/{ResourcePath}. Localization will remain disabled.");
            return;
        }

        LocalizationSettings.Instance = settings;
    }
}
