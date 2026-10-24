using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FormulaGame
{
    // Fits the authored 1280 × 720 composition into the actual safe area.
    [DisallowMultipleComponent]
    public sealed class FormulaPresentation : MonoBehaviour
    {
        private struct Placement { public RectTransform rect; public Vector2 position; public Vector3 scale; }
        private readonly List<Placement> placements = new List<Placement>();
        private readonly Dictionary<TMP_Text, float> fonts = new Dictionary<TMP_Text, float>();
        private RectTransform root;
        private Rect lastSafe;
        private Vector2 lastSize;
        private int textScale, inset;
        private void Awake()
        {
            root = (RectTransform)transform;
            foreach (Transform child in transform) if (child is RectTransform rect) placements.Add(new Placement { rect = rect, position = rect.anchoredPosition, scale = rect.localScale });
            foreach (var text in GetComponentsInChildren<TMP_Text>(true)) fonts[text] = text.fontSize;
        }
        public void Register(RectTransform content)
        {
            placements.Add(new Placement { rect = content, position = content.anchoredPosition, scale = content.localScale });
            foreach (var text in content.GetComponentsInChildren<TMP_Text>(true)) fonts[text] = text.fontSize;
            textScale = 0; Refresh();
        }
        private void OnEnable() => FormulaProgression.Changed += Refresh;
        private void OnDisable() => FormulaProgression.Changed -= Refresh;
        private void Start() => Refresh();
        private void LateUpdate()
        {
            if (root != null && (lastSafe != Screen.safeArea || lastSize != root.rect.size)) Fit();
        }
        public void Refresh()
        {
            if (root == null) return;
            var p = FormulaProgression.State;
            if (textScale != p.textScale)
            {
                textScale = p.textScale;
                foreach (var entry in fonts)
                {
                    var text = entry.Key; if (text == null || entry.Value > 32) continue;
                    float size = entry.Value * textScale / 100f;
                    text.fontSize = size;
                    text.fontSizeMax = size;
                    // Accessibility sizing is a minimum, never a suggestion for auto-fit.
                    text.fontSizeMin = size; text.enableAutoSizing = false;
                }
            }
            inset = p.safeAreaPercent; Fit();
            GetComponent<FormulaMenuView>()?.RefreshContrast();
        }
        private void Fit()
        {
            lastSafe = Screen.safeArea; lastSize = root.rect.size;
            if (Screen.width <= 0 || Screen.height <= 0 || lastSize.x <= 0 || lastSize.y <= 0) return;
            float width = lastSafe.width / Screen.width * lastSize.x, height = lastSafe.height / Screen.height * lastSize.y;
            float fit = Mathf.Min(width / 1280, height / 720) * (inset == 95 ? .95f : 1);
            float x = lastSafe.x / Screen.width * lastSize.x + (width - 1280 * fit) * .5f;
            float y = (Screen.height - lastSafe.yMax) / Screen.height * lastSize.y + (height - 720 * fit) * .5f;
            foreach (var entry in placements)
            {
                if (entry.rect == null) continue;
                entry.rect.localScale = entry.scale * fit;
                entry.rect.anchoredPosition = entry.position * fit + new Vector2(x, -y);
            }
        }
        public static void ApplyVideoPreferences()
        {
            var p = FormulaProgression.State;
            if (p.qualityLevel >= 0) QualitySettings.SetQualityLevel(Mathf.Clamp(p.qualityLevel, 0, QualitySettings.names.Length - 1));
            if (p.vSync >= 0) QualitySettings.vSyncCount = p.vSync;
#if UNITY_SWITCH
            Application.targetFrameRate = p.frameLimit == 60 ? 60 : 30;
#else
            if (p.frameLimit > 0) Application.targetFrameRate = p.frameLimit;
#endif
#if !UNITY_EDITOR && !UNITY_SWITCH
            if (p.displayMode >= 0) Screen.fullScreenMode = p.displayMode == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
#endif
        }
    }
}
