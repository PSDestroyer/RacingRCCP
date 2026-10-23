using TMPro;
using UnityEngine;
namespace FormulaGame
{
    public static class FormulaMenuTheme
    {
        public static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
        }
        public static FormulaPanelGraphic Surface(Transform parent, string name, float x, float y, float w, float h, bool selected = false)
        {
            var rect = parent.Find(name) as RectTransform ?? FormulaUI.Rect(parent, name, x, y, w, h);
            Place(rect, x, y, w, h);
            if (rect.GetComponent<CanvasRenderer>() == null) rect.gameObject.AddComponent<CanvasRenderer>();
            var surface = rect.GetComponent<FormulaPanelGraphic>() ?? rect.gameObject.AddComponent<FormulaPanelGraphic>();
            surface.Configure(selected); surface.color = selected ? Color.clear : Color.white; return surface;
        }
        public static void StyleButton(UnityEngine.UI.Button button)
        {
            var root = (RectTransform)button.transform; float w = root.sizeDelta.x, h = root.sizeDelta.y;
            var image = root.GetComponent<UnityEngine.UI.Image>(); if (image != null) { image.color = Color.clear; image.raycastTarget = true; }
            foreach (Transform child in root)
                if (child.name.StartsWith("Focus ") || child.name.StartsWith("Selection ") || child.name == "Separator" || child.name == "RedAccent") child.gameObject.SetActive(false);
            var normal = Surface(root, "Upgrade Normal", 0, 0, w, h); normal.transform.SetAsFirstSibling();
            var selected = Surface(root, "Upgrade Focus", 0, 0, w, h, true); selected.transform.SetSiblingIndex(1);
            button.transition = UnityEngine.UI.Selectable.Transition.None;
            var focus = root.GetComponent<FormulaMenuFocus>() ?? root.gameObject.AddComponent<FormulaMenuFocus>(); focus.StyleFigma(selected, null, normal);
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) text.color = Color.white;
        }
    }
}
