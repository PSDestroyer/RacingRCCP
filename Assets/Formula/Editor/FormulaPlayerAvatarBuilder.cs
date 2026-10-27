using System;
using FormulaGame;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class FormulaPlayerAvatarBuilder
{
    public const string ResultsPath = "Assets/Formula/Resources/Formula/UI/FormulaResults.prefab";

    [MenuItem("Tools/Formula Career/Add Nintendo Profile Photos")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        foreach (string path in new[] { FormulaMenuHierarchyBuilder.PrefabPath, ResultsPath })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { Configure(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Debug.Log("FORMULA_NINTENDO_PROFILE_PHOTOS_AUTHORED");
    }
    public static void Configure(GameObject root)
    {
        foreach (var view in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (!(view is FormulaMenuView || view is FormulaCareerView || view is FormulaUpgradeView || view is FormulaResultsView)) continue;
            var driver = (TMP_Text)new SerializedObject(view).FindProperty("driver").objectReferenceValue;
            if (driver != null) Configure(driver);
        }
    }
    public static void Configure(TMP_Text driver)
    {
        var parent = driver.transform.parent;
        var existing = parent.Find("Nintendo Profile Photo");
        var avatar = existing != null ? existing.GetComponent<FormulaPlayerAvatar>() :
            new GameObject("Nintendo Profile Photo", typeof(RectTransform), typeof(UnityEngine.UI.RawImage), typeof(FormulaPlayerAvatar)).GetComponent<FormulaPlayerAvatar>();
        avatar.transform.SetParent(parent, false);
        var nameRect = (RectTransform)driver.transform;
        FormulaMenuTheme.Place((RectTransform)avatar.transform, nameRect.anchoredPosition.x - 48, -nameRect.anchoredPosition.y + (nameRect.rect.height - 38) / 2, 38, 38);
        var image = avatar.GetComponent<UnityEngine.UI.RawImage>();
        image.raycastTarget = false; image.texture = null; image.enabled = false;
        var helmet = parent.Find("Helmet")?.GetComponent<UnityEngine.UI.RawImage>();
        var fields = new SerializedObject(avatar);
        fields.FindProperty("fallback").objectReferenceValue = helmet;
        fields.ApplyModifiedPropertiesWithoutUndo();
        driver.richText = false;
        driver.overflowMode = TextOverflowModes.Ellipsis;
    }
}
