using System;
using System.Reflection;
using UnityEditor;

// Uses an existing Editor resolution preset and restores the user's selection afterwards.
public static class FormulaGameViewValidation
{
    private const string Previous = "Formula.Experience.PreviousGameViewSize";
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
    public static void Select1080()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var assembly = typeof(Editor).Assembly;
        var gameView = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        var selected = gameView.GetType().GetProperty("selectedSizeIndex", Flags);
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = sizesType.GetProperty("instance", Flags).GetValue(null);
        var groupType = sizesType.GetProperty("currentGroupType", Flags).GetValue(sizes);
        var group = sizesType.GetMethod("GetGroup", Flags).Invoke(sizes, new[] { groupType });
        int count = (int)group.GetType().GetMethod("GetTotalCount", Flags).Invoke(group, null);
        for (int i = 0; i < count; i++)
        {
            var size = group.GetType().GetMethod("GetGameViewSize", Flags).Invoke(group, new object[] { i });
            int width = (int)size.GetType().GetProperty("width", Flags).GetValue(size), height = (int)size.GetType().GetProperty("height", Flags).GetValue(size);
            if (width != 1920 || height != 1080) continue;
            SessionState.SetInt(Previous, (int)selected.GetValue(gameView)); selected.SetValue(gameView, i); gameView.Focus(); return;
        }
        throw new InvalidOperationException("No existing 1920 × 1080 Game View preset is available.");
    }
    public static void Restore()
    {
        int previous = SessionState.GetInt(Previous, -1); if (previous < 0) return;
        var type = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        type.GetProperty("selectedSizeIndex", Flags).SetValue(EditorWindow.GetWindow(type), previous);
        SessionState.EraseInt(Previous);
    }
}
