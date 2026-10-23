using TMPro;
using UnityEngine;

namespace FormulaGame
{
    // Presentation assets only. The catalog and save remain authoritative for ownership.
    public sealed class FormulaMenuAssets : ScriptableObject
    {
        public TMP_FontAsset bodyFont, titleFont;
        public GameObject resultsPrefab;
        public Texture2D career, garage, upgrade, settings, helmet, footerGlow;
        public Texture2D[] liveries;
        public Sprite panelSprite;
        public Texture2D vignette;
        public TMP_FontAsset strongFont;
        [System.Serializable] public sealed class TrackGraphic { public string id; public Vector2[] points; }
        public TrackGraphic[] trackMaps;
        public static FormulaMenuAssets Current => Resources.Load<FormulaMenuAssets>("Formula/UI/MenuAssets");
    }

    public static class FormulaMenuStyle
    {
        public static readonly Color SurfaceColor = new Color32(28, 29, 32, 240);
        public static readonly Color RaisedColor = new Color32(20, 21, 24, 250);
        private static Sprite rounded;
        private static Texture2D shade;
        public static RectTransform Surface(Transform parent, string name, float x, float y, float w, float h, Color? color = null)
        {
            var rect = FormulaUI.Box(parent, name, x, y, w, h, color ?? SurfaceColor);
            var image = rect.GetComponent<UnityEngine.UI.Image>(); image.sprite = Rounded(); image.type = UnityEngine.UI.Image.Type.Sliced;
            return rect;
        }
        private static Sprite Rounded()
        {
            if (FormulaMenuAssets.Current != null && FormulaMenuAssets.Current.panelSprite != null) return FormulaMenuAssets.Current.panelSprite;
            if (rounded != null) return rounded;
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "Formula rounded panel", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[1024];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float dx = Mathf.Max(7.5f - x, x - 23.5f), dy = Mathf.Max(7.5f - y, y - 23.5f);
                float distance = new Vector2(Mathf.Max(dx, 0), Mathf.Max(dy, 0)).magnitude;
                pixels[y * 32 + x] = new Color(1, 1, 1, Mathf.Clamp01(8.5f - distance));
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            rounded = Sprite.Create(texture, new Rect(0, 0, 32, 32), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect, new Vector4(10, 10, 10, 10));
            rounded.hideFlags = HideFlags.HideAndDontSave; return rounded;
        }
        public static void Backdrop(Transform parent)
        {
            if (FormulaMenuAssets.Current != null && FormulaMenuAssets.Current.vignette != null)
            {
                Art(parent, "Cinematic frame", FormulaMenuAssets.Current.vignette, 0, 0, 1280, 720); return;
            }
            if (shade == null)
            {
                shade = new Texture2D(64, 32, TextureFormat.RGBA32, false) { name = "Formula menu vignette", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[64 * 32];
                for (int y = 0; y < 32; y++) for (int x = 0; x < 64; x++)
                {
                    float xx = x / 63f, yy = y / 31f;
                    float radius = new Vector2((xx - .70f) / .52f, (yy - .55f) / .34f).magnitude;
                    float frame = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.30f, 1.05f, radius));
                    pixels[y * 64 + x] = new Color(.07f, .003f, .014f, Mathf.Lerp(.08f, .96f, frame));
                }
                shade.SetPixels(pixels); shade.Apply(false, true);
            }
            Art(parent, "Cinematic frame", shade, 0, 0, 1280, 720);
        }
        public static TMP_Text Text(Transform parent, string text, float x, float y, float w, float h, int size = 24, Color? color = null)
        {
            var label = FormulaUI.Text(parent, text, x, y, w, h, size, color);
            var assets = FormulaMenuAssets.Current;
            if (assets != null && assets.bodyFont != null) label.font = assets.bodyFont;
            label.enableAutoSizing = true; label.fontSizeMin = size * .8f; label.fontSizeMax = size;
            return label;
        }
        public static TMP_Text Title(Transform parent, string text, float x, float y, float w, float h, int size = 48, Color? color = null)
        {
            var label = Text(parent, text, x, y, w, h, size, color);
            var assets = FormulaMenuAssets.Current;
            if (assets != null && assets.titleFont != null) label.font = assets.titleFont;
            // Teko's font metrics include generous ascender/descender space; the
            // design aligns its visible capitals inside compact title slots.
            label.enableAutoSizing = false; label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            return label;
        }
        public static UnityEngine.UI.RawImage Art(Transform parent, string name, Texture2D texture, float x, float y, float w, float h, Color? color = null)
        {
            var r = FormulaUI.Rect(parent, name, x, y, w, h);
            var image = r.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            image.texture = texture; image.color = color ?? Color.white; image.raycastTarget = false;
            return image;
        }
        public static UnityEngine.UI.Button Button(Transform parent, string id, string title, float x, float y, float w, float h, System.Action click, bool enabled = true)
        {
            var rect = Surface(parent, id, x, y, w, h, Color.white);
            var image = rect.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.interactable = enabled;
            var colors = button.colors; colors.normalColor = SurfaceColor;
            colors.highlightedColor = new Color32(75, 8, 23, 250); colors.selectedColor = new Color32(103, 9, 28, 255);
            colors.pressedColor = FormulaUI.Red; colors.disabledColor = new Color32(36, 6, 14, 235); colors.fadeDuration = .12f; button.colors = colors;
            var label = Text(rect, title, 18, 5, w - 36, h - 10, 24, enabled ? Color.white : new Color(1, 1, 1, .6f));
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            label.enableAutoSizing = true; label.fontSizeMin = 18; label.fontSizeMax = 24;
            if (h < 44) { label.enableAutoSizing = false; label.fontSize = 18; label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Overflow; }
            // Separate graphics keep yellow focus independent of Button's red color tint.
            var top = FormulaUI.Box(rect, "Focus Top", 8, 0, w - 16, 2, Color.clear).GetComponent<UnityEngine.UI.Image>();
            var bottom = FormulaUI.Box(rect, "Focus Bottom", 8, h - 2, w - 16, 2, Color.clear).GetComponent<UnityEngine.UI.Image>();
            var left = FormulaUI.Box(rect, "Focus Left", 0, 8, 2, h - 16, Color.clear).GetComponent<UnityEngine.UI.Image>();
            var right = FormulaUI.Box(rect, "Focus Right", w - 2, 8, 2, h - 16, Color.clear).GetComponent<UnityEngine.UI.Image>();
            rect.gameObject.AddComponent<FormulaMenuFocus>().Initialize(top, bottom, left, right);
            button.onClick.AddListener(() => click());
            return button;
        }
        public static UnityEngine.UI.Button MenuButton(Transform parent, string id, string title, Texture2D icon, float x, float y, float w, float h, System.Action click)
        {
            var button = Button(parent, id, "", x, y, w, h, click);
            var colors = button.colors;
            colors.normalColor = new Color32(28, 29, 32, 240); colors.highlightedColor = new Color32(100, 7, 25, 245);
            colors.selectedColor = FormulaUI.Red; colors.pressedColor = FormulaUI.Ink; button.colors = colors;
            var text = button.GetComponentInChildren<TMP_Text>(); text.text = title;
            text.color = Color.white; text.enableAutoSizing = false; text.fontSize = 28;
            text.rectTransform.anchoredPosition = new Vector2(94, -6); text.rectTransform.sizeDelta = new Vector2(w - 110, h - 12);
            if (w < 240) { text.fontSize = 22; text.rectTransform.anchoredPosition = new Vector2(56, -6); text.rectTransform.sizeDelta = new Vector2(w - 64, h - 12); }
            if (icon != null)
            {
                float slot = w < 240 ? 30 : 54;
                float iw = icon.width * Mathf.Min(slot / icon.width, 30f / icon.height);
                float ih = icon.height * Mathf.Min(slot / icon.width, 30f / icon.height);
                Art(button.transform, "Figma Icon", icon, (w < 240 ? 12 : 20) + (slot - iw) * .5f, (h - ih) * .5f, iw, ih);
            }
            FormulaUI.Box(button.transform, "Separator", 0, 0, w, 1, new Color(1, .82f, .84f, .3f));
            return button;
        }
        public static void Segments(Transform parent, string id, float x, float y, float w, int equipped, int owned, int count = 3)
        {
            for (int i = 0; i < count; i++)
                FormulaUI.Box(parent, id + "-" + i, x + i * w / count, y, w / count - 5, 6,
                    i < equipped ? FormulaUI.Accent : i < owned ? Color.white : FormulaUI.Red);
        }
    }
}
