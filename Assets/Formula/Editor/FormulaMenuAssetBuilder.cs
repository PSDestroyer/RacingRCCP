using System;
using System.IO;
using System.Linq;
using FormulaGame;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class FormulaMenuAssetBuilder
{
    private const string Folder = "Assets/Formula/Resources/Formula/UI/";
    [MenuItem("Tools/Formula Career/Build Figma Menu Assets")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before rebuilding menu assets.");
        AssetDatabase.Refresh();
        var skin = AssetDatabase.LoadAssetAtPath<FormulaMenuAssets>(Folder + "MenuAssets.asset");
        if (skin == null) { skin = ScriptableObject.CreateInstance<FormulaMenuAssets>(); AssetDatabase.CreateAsset(skin, Folder + "MenuAssets.asset"); }
        skin.bodyFont = Font("Assets/_GarageV2/UI/Fonts/Rajdhani/Rajdhani-Regular.ttf", "Rajdhani");
        skin.titleFont = Font(Folder + "Teko-Regular.ttf", "Teko");
        skin.career = Texture("Career"); skin.garage = Texture("Garage"); skin.upgrade = Texture("Upgrade");
        skin.settings = Texture("Settings"); skin.helmet = Texture("Helmet"); skin.footerGlow = Texture("FooterGlow");
        BuildLiveryPreviews(skin);
        EditorUtility.SetDirty(skin); AssetDatabase.SaveAssets();
        Debug.Log("Formula Figma menu: fonts, original icons and 8 Generic livery previews built.");
    }
    public static void BuildLiveryPreviews(FormulaMenuAssets skin)
    {
        skin.liveries = new Texture2D[FormulaCatalog.Current.liveries.Count];
        for (int i = 0; i < skin.liveries.Length; i++) skin.liveries[i] = RenderLivery(i);
        EditorUtility.SetDirty(skin);
    }
    private static Texture2D Texture(string name)
    {
        string path = Folder + name + ".png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) throw new FileNotFoundException("Missing menu artwork", path);
        importer.textureType = TextureImporterType.Default; importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false; importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 1024;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    private static TMP_FontAsset Font(string source, string name)
    {
        string path = Folder + name + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (asset != null) return asset;
        var font = AssetDatabase.LoadAssetAtPath<UnityEngine.Font>(source);
        if (font == null) throw new FileNotFoundException("Missing menu font", source);
        asset = TMP_FontAsset.CreateFontAsset(font, 64, 8, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
        asset.name = name;
        string glyphs = new string(Enumerable.Range(32, 95).Select(n => (char)n).ToArray()) + "ăâîșțĂÂÎȘȚşţŞŢ→←−×·≤–—…";
        asset.TryAddCharacters(glyphs, out string missing);
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        foreach (var atlas in asset.atlasTextures) if (atlas != null) AssetDatabase.AddObjectToAsset(atlas, asset);
        EditorUtility.SetDirty(asset);
        if (!string.IsNullOrEmpty(missing)) Debug.LogWarning(name + " uses TMP fallback for: " + missing);
        return asset;
    }
    private static Texture2D RenderLivery(int index)
    {
        var texture = FormulaLiveryArtBuilder.RenderCar(index, 504, 256, new Vector3(7, 3.2f, 5.1f), true);
        try { File.WriteAllBytes(Folder + "Livery" + index + ".png", texture.EncodeToPNG()); }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
        AssetDatabase.ImportAsset(Folder + "Livery" + index + ".png");
        return Texture("Livery" + index);
    }
}
