using FormulaGame;
using UnityEditor;
using UnityEngine;

public static class FormulaGameplayPolishAuthoring
{
    public static void Audio()
    {
        var assets = FormulaMenuAssets.Current;
        string[] names = { "Focus", "Confirm", "Record", "Result", "Unlock", "End" };
        string[] fields = { "focusSound", "confirmSound", "recordSound", "resultSound", "unlockSound", "endSound" };
        for (int i = 0; i < names.Length; i++)
        {
            string path = "Assets/Formula/Audio/UI/" + names[i] + ".wav";
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            if (!importer.forceToMono || settings.compressionFormat != AudioCompressionFormat.PCM)
            {
                importer.forceToMono = true; settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.loadType = AudioClipLoadType.DecompressOnLoad; importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
            FormulaExperienceBuilder.Set(assets, fields[i], AssetDatabase.LoadAssetAtPath<AudioClip>(path));
        }
        EditorUtility.SetDirty(assets);
    }

    [MenuItem("Tools/Formula Career/Apply Gameplay Surface Polish")]
    public static void Surfaces() => FormulaTrackSurfaceBuilder.Apply();
}
