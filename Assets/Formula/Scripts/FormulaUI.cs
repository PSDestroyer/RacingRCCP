using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace FormulaGame
{
    public static class FormulaUI
    {
        // Shared by the menu, garage, confirmation dialogs and race results.
        public static readonly Color Background = new Color32(8, 9, 11, 250);
        public static readonly Color Red = new Color32(255, 30, 0, 255);
        public static readonly Color Ink = new Color32(18, 19, 22, 255);
        public static readonly Color Panel = Color.white;
        public static readonly Color Accent = new Color32(255, 225, 59, 255);
        public static readonly Color Disabled = new Color32(255, 194, 205, 255);
        public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform)); var r = go.GetComponent<RectTransform>();
            r.SetParent(parent, false); r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
        }
        public static Canvas Canvas(string name, int order)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
            var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            if (EventSystem.current == null)
            {
                var es = new GameObject("FormulaEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            return canvas;
        }
        public static RectTransform Box(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var r = Rect(parent, name, x, y, w, h); var image = r.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color;
            image.raycastTarget = false; return r;
        }
        public static TMP_Text Text(Transform parent, string text, float x, float y, float w, float h, int size = 24, Color? color = null)
        {
            var r = Rect(parent, "Text", x, y, w, h); var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.font = TMP_Settings.defaultFontAsset; t.fontSize = size; t.color = color ?? Color.white;
            t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Ellipsis; t.raycastTarget = false; return t;
        }
        public static UnityEngine.UI.Button Button(Transform parent, string id, string title, float x, float y, float w, float h, Action click, bool enabled = true)
        {
            var r = Box(parent, id, x, y, w, h, Panel);
            r.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            var b = r.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.interactable = enabled; var colors = b.colors; colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(255, 242, 164, 255);
            colors.selectedColor = Accent; colors.pressedColor = new Color32(255, 200, 0, 255);
            colors.disabledColor = Disabled; colors.colorMultiplier = 1f; colors.fadeDuration = .08f; b.colors = colors;
            Box(r, "RedAccent", 0, 0, 5, h, Red);
            var t = Text(r, title, 18, 10, w - 36, h - 20, 24, Ink); t.verticalAlignment = VerticalAlignmentOptions.Middle;
            b.onClick.AddListener(() => click()); return b;
        }
        public static void MarkActive(UnityEngine.UI.Button button, bool active)
        {
            var r = (RectTransform)button.transform;
            var marker = Box(r, "ActivePage", 0, r.sizeDelta.y - 4, r.sizeDelta.x, 4, Accent);
            marker.gameObject.SetActive(active);
        }
        public static void Clear(Transform parent)
        {
            foreach (Transform c in parent) { c.gameObject.SetActive(false); UnityEngine.Object.Destroy(c.gameObject); }
        }
        public static void Focus(Transform parent)
        {
            var first = Array.Find(parent.GetComponentsInChildren<UnityEngine.UI.Selectable>(), s => s.IsInteractable() && s.IsActive());
            if (first != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }
        public static string MedalName(Medal m) => m == Medal.Gold ? "Gold" : m == Medal.Silver ? "Silver" : m == Medal.Bronze ? "Bronze" : "No medal";
    }
}
