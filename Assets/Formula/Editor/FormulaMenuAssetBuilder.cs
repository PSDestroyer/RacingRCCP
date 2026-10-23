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
        skin.liveries = new Texture2D[FormulaCatalog.Current.liveries.Count];
        for (int i = 0; i < skin.liveries.Length; i++) skin.liveries[i] = RenderLivery(i);
        EditorUtility.SetDirty(skin); AssetDatabase.SaveAssets();
        Debug.Log("Formula Figma menu: fonts, original icons and 8 Generic livery previews built.");
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
        var source = FormulaAssets.Current.vehiclePrefab;
        var root = new GameObject("Generic thumbnail meshes");
        var utility = new PreviewRenderUtility();
        try
        {
            foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>(); if (renderer == null || !renderer.enabled) continue;
                var go = new GameObject(filter.name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(root.transform, false);
                go.transform.localPosition = source.transform.InverseTransformPoint(filter.transform.position);
                go.transform.localRotation = Quaternion.Inverse(source.transform.rotation) * filter.transform.rotation;
                go.transform.localScale = filter.transform.lossyScale;
                go.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                go.GetComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                if (renderer == source.GetComponent<FormulaVehicle>().body) go.GetComponent<MeshRenderer>().sharedMaterial = FormulaAssets.Current.liveryMaterials[index];
            }
            utility.AddSingleGO(root);
            var bounds = root.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
            utility.camera.fieldOfView = 25; utility.camera.clearFlags = CameraClearFlags.SolidColor; utility.camera.backgroundColor = Color.clear;
            utility.camera.nearClipPlane = .1f; utility.camera.farClipPlane = 50;
            utility.camera.transform.position = bounds.center + new Vector3(8.5f, 3.1f, 4.6f);
            utility.camera.transform.LookAt(bounds.center);
            utility.lights[0].intensity = 2.2f; utility.lights[0].transform.rotation = Quaternion.Euler(35, 20, 0);
            utility.lights[1].intensity = 1.4f; utility.lights[1].transform.rotation = Quaternion.Euler(315, 210, 0);
            utility.ambientColor = new Color(.65f, .65f, .65f);
            utility.BeginPreview(new Rect(0, 0, 504, 256), GUIStyle.none);
            utility.Render(true);
            var render = (RenderTexture)utility.EndPreview();
            var previous = RenderTexture.active;
            var texture = new Texture2D(504, 256, TextureFormat.RGBA32, false);
            try { RenderTexture.active = render; texture.ReadPixels(new Rect(0, 0, 504, 256), 0, 0); texture.Apply(); File.WriteAllBytes(Folder + "Livery" + index + ".png", texture.EncodeToPNG()); }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(texture); }
        }
        finally { utility.Cleanup(); if (root != null) UnityEngine.Object.DestroyImmediate(root); }
        AssetDatabase.ImportAsset(Folder + "Livery" + index + ".png");
        return Texture("Livery" + index);
    }
}
