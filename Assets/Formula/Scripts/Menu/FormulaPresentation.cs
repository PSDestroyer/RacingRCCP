using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FormulaGame
{
    // Keeps readable content in the safe area while decorative layers cover the display.
    [DisallowMultipleComponent]
    public sealed class FormulaPresentation : MonoBehaviour
    {
        private const float DesignWidth = 1280;
        private const float DesignHeight = 720;
        [System.Flags]
        private enum Edges { Left = 1, Right = 2, Top = 4, Bottom = 8, Sides = Left | Right, All = Sides | Top | Bottom }

        private struct Placement { public RectTransform rect; public Vector2 position; public Vector3 scale; }
        private struct HudPlacement { public RectTransform rect; public Vector2 position; public Edges edges; }

        private sealed class Backdrop
        {
            public RectTransform rect;
            public Edges edges;
            public UnityEngine.UI.RawImage artwork;
            public Rect originalUv;
            public Vector2 originalSize;
            public Vector2 appliedSize, appliedOffset;

            public void Resize(Vector2 extraSize, Vector2 offset)
            {
                Vector2 sizeChange = extraSize - appliedSize;
                Vector2 offsetChange = offset - appliedOffset;
                if (sizeChange == Vector2.zero && offsetChange == Vector2.zero) return;

                rect.sizeDelta += sizeChange;
                rect.anchoredPosition += Vector2.Scale(offsetChange, rect.localScale);
                foreach (Transform child in rect)
                {
                    if (!(child is RectTransform content)) continue;
                    // Keep child panels and hit targets in the same place when their background grows.
                    Vector2 anchor = content.anchorMin + Vector2.Scale(content.anchorMax - content.anchorMin, content.pivot);
                    content.anchoredPosition -= offsetChange + Vector2.Scale(sizeChange, anchor - rect.pivot);
                    content.sizeDelta -= Vector2.Scale(sizeChange, content.anchorMax - content.anchorMin);
                }

                if (artwork != null)
                {
                    Vector2 size = originalSize + extraSize;
                    float aspectChange = size.x * originalSize.y / (size.y * originalSize.x);
                    Vector2 visible = aspectChange >= 1 ? new Vector2(1, 1 / aspectChange) : new Vector2(aspectChange, 1);
                    Vector2 uvSize = Vector2.Scale(originalUv.size, visible);
                    artwork.uvRect = new Rect(originalUv.center - uvSize * .5f, uvSize);
                }
                appliedSize = extraSize;
                appliedOffset = offset;
            }
        }

        private readonly List<Placement> placements = new List<Placement>();
        private readonly List<Backdrop> backdrops = new List<Backdrop>();
        private readonly List<HudPlacement> hudPlacements = new List<HudPlacement>();
        private readonly Dictionary<TMP_Text, float> fonts = new Dictionary<TMP_Text, float>();
        private FormulaRacePanelGraphic[] racePanels;
        private RectTransform root;
        private Rect lastSafe;
        private Vector2 lastSize;
        private Vector2Int lastScreenSize;
        private int textScale, inset;
        private void Awake()
        {
            root = (RectTransform)transform;
            foreach (Transform child in transform) if (child is RectTransform rect) Capture(rect);
            if (TryGetComponent<FormulaMenuView>(out _)) CaptureMenu();
            foreach (var text in GetComponentsInChildren<TMP_Text>(true)) fonts[text] = text.fontSize;
            racePanels = GetComponentsInChildren<FormulaRacePanelGraphic>(true);
        }
        public void Register(RectTransform content)
        {
            Capture(content);
            foreach (var text in content.GetComponentsInChildren<TMP_Text>(true)) fonts[text] = text.fontSize;
            textScale = 0; Refresh();
        }

        private void Capture(RectTransform content)
        {
            if (placements.Exists(entry => entry.rect == content)) return;
            placements.Add(new Placement { rect = content, position = content.anchoredPosition, scale = content.localScale });
            if (content.TryGetComponent<FormulaLoadingView>(out _))
            {
                AddBackdrop(content, "Backdrop", Edges.All);
            }
            else if (content.TryGetComponent<FormulaRaceView>(out _))
            {
                AddBackdrop(content, "Pause", Edges.All);
                AddBackdrop(content, "Audio Options", Edges.All);
                AddBackdrop(content, "Save Error", Edges.All);
                AnchorHud(content, "Session", Edges.Left | Edges.Top);
                AnchorHud(content, "Session Goal", Edges.Left | Edges.Top);
                AnchorHud(content, "Standings", Edges.Left | Edges.Top);
                AnchorHud(content, "Target", Edges.Left);
                AnchorHud(content, "Timing", Edges.Right | Edges.Top);
                AnchorHud(content, "race-pause", Edges.Right | Edges.Top);
                AnchorHud(content, "Minimap", Edges.Left | Edges.Bottom);
                AnchorHud(content, "Vehicle", Edges.Right | Edges.Bottom);
            }
            else if (content.TryGetComponent<FormulaResultsView>(out _))
            {
                AddBackdrop(content, "", Edges.All);
                CaptureChrome(content);
            }
        }

        private void CaptureMenu()
        {
            CaptureChrome(root);
            AddBackdrop(root, "Cinematic frame", Edges.All);
            AddBackdrop(root, "Shell Lower Shade", Edges.Sides | Edges.Bottom);
            AddBackdrop(root, "Screen Shade", Edges.Sides);
            AddBackdrop(root, "Dialogs", Edges.All);
            AddBackdrop(root, "Screens/Home/Navigation Backdrop", Edges.Left);
            AddBackdrop(root, "Screens/Career/Figma Background", Edges.All, true);
            AddBackdrop(root, "Screens/Career/Figma Background Shade", Edges.All);
            AddBackdrop(root, "Screens/Career/Figma Header Shade", Edges.Sides | Edges.Top);
            AddBackdrop(root, "Screens/Career/Figma Footer Shade", Edges.Sides | Edges.Bottom);
            AddBackdrop(root, "Screens/Garage/Dezvoltare/World Shade", Edges.Sides | Edges.Top);
            AddBackdrop(root, "Screens/Garage/Dezvoltare/Header Backplate", Edges.Sides | Edges.Top);
            AddBackdrop(root, "Screens/Garage/Dezvoltare/Header Shade", Edges.Sides | Edges.Top);
            AddBackdrop(root, "Screens/Garage/Dezvoltare/Lower Shade", Edges.Sides | Edges.Bottom);
            AddBackdrop(root, "Screens/Garage/Dezvoltare/Footer Shade", Edges.Sides | Edges.Bottom);
        }

        private void CaptureChrome(RectTransform content)
        {
            AddBackdrop(content, "Header", Edges.Sides | Edges.Top);
            AddBackdrop(content, "Header/Formula Header Shade", Edges.Sides | Edges.Top);
            AddBackdrop(content, "Footer/Formula Footer Shade", Edges.Sides | Edges.Bottom);
        }

        private void AddBackdrop(RectTransform content, string path, Edges edges, bool cropArtwork = false)
        {
            var rect = path.Length == 0 ? content : content.Find(path) as RectTransform;
            if (rect == null || !rect.TryGetComponent<UnityEngine.UI.Graphic>(out _)) return;
            var artwork = cropArtwork ? rect.GetComponent<UnityEngine.UI.RawImage>() : null;
            backdrops.Add(new Backdrop
            {
                rect = rect, edges = edges, artwork = artwork,
                originalUv = artwork != null ? artwork.uvRect : default,
                originalSize = rect.rect.size
            });
        }

        private void AnchorHud(RectTransform content, string name, Edges edges)
        {
            if (content.Find("HUD/" + name) is RectTransform panel)
                hudPlacements.Add(new HudPlacement { rect = panel, position = panel.anchoredPosition, edges = edges });
        }
        private void OnEnable() => FormulaProgression.Changed += Refresh;
        private void OnDisable() => FormulaProgression.Changed -= Refresh;
        private void Start() => Refresh();
        private void LateUpdate()
        {
            if (root != null && (lastSafe != Screen.safeArea || lastSize != root.rect.size ||
                lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height)) Fit();
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
                    var localizedFont = text.GetComponent<FormulaLocalizedFont>();
                    if (localizedFont != null) { localizedFont.SetSize(size); continue; }
                    text.fontSize = size;
                    text.fontSizeMax = size;
                    // Accessibility sizing is a minimum, never a suggestion for auto-fit.
                    text.fontSizeMin = size; text.enableAutoSizing = false;
                }
            }
            inset = p.safeAreaPercent; Fit();
            if (racePanels != null) foreach (var panel in racePanels) if (panel != null) panel.SetHighContrast(p.highContrast);
            GetComponent<FormulaMenuView>()?.RefreshContrast();
        }
        private void Fit()
        {
            lastSafe = Screen.safeArea; lastSize = root.rect.size;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            if (lastScreenSize.x <= 0 || lastScreenSize.y <= 0 || lastSize.x <= 0 || lastSize.y <= 0) return;
            Rect safe = lastSafe.width > 0 && lastSafe.height > 0 ? lastSafe : new Rect(0, 0, lastScreenSize.x, lastScreenSize.y);
            Vector2 pixelsToCanvas = new Vector2(lastSize.x / lastScreenSize.x, lastSize.y / lastScreenSize.y);
            var area = new Rect(Vector2.Scale(safe.position, pixelsToCanvas), Vector2.Scale(safe.size, pixelsToCanvas));
            Layout(area, lastSize, inset == 95 ? .95f : 1);
        }

        private void Layout(Rect safe, Vector2 size, float safeInset)
        {
            float fit = Mathf.Min(safe.width / DesignWidth, safe.height / DesignHeight);
            float scale = fit * safeInset;
            float x = safe.center.x - DesignWidth * scale * .5f;
            float y = size.y - safe.center.y - DesignHeight * scale * .5f;

            // Restore authored hierarchies before applying a new viewport, including nested surfaces.
            for (int i = backdrops.Count - 1; i >= 0; i--)
                if (backdrops[i].rect != null) backdrops[i].Resize(Vector2.zero, Vector2.zero);

            foreach (var entry in placements)
            {
                if (entry.rect == null) continue;
                entry.rect.localScale = entry.scale * scale;
                entry.rect.anchoredPosition = entry.position * scale + new Vector2(x, -y);
            }

            Vector2 hudMargin = new Vector2(
                Mathf.Max(0, safe.width - DesignWidth * fit),
                Mathf.Max(0, safe.height - DesignHeight * fit)) * (safeInset / (2 * scale));
            foreach (var panel in hudPlacements)
            {
                if (panel.rect == null) continue;
                panel.rect.anchoredPosition = panel.position + new Vector2(
                    Has(panel.edges, Edges.Left) ? -hudMargin.x : Has(panel.edges, Edges.Right) ? hudMargin.x : 0,
                    Has(panel.edges, Edges.Top) ? hudMargin.y : Has(panel.edges, Edges.Bottom) ? -hudMargin.y : 0);
            }

            foreach (var backdrop in backdrops)
            {
                if (backdrop.rect == null) continue;
                float left = Has(backdrop.edges, Edges.Left) ? Mathf.Max(0, x / scale) : 0;
                float right = Has(backdrop.edges, Edges.Right) ? Mathf.Max(0, (size.x - x) / scale - DesignWidth) : 0;
                float top = Has(backdrop.edges, Edges.Top) ? Mathf.Max(0, y / scale) : 0;
                float bottom = Has(backdrop.edges, Edges.Bottom) ? Mathf.Max(0, (size.y - y) / scale - DesignHeight) : 0;
                var extra = new Vector2(left + right, top + bottom);
                backdrop.Resize(extra, new Vector2(-left, -bottom) + Vector2.Scale(extra, backdrop.rect.pivot));
            }
        }
        private static bool Has(Edges value, Edges edge) => (value & edge) != 0;
        public static void ApplyVideoPreferences()
        {
            if (!FormulaPlatform.AllowsVideoPreferences)
            {
                // Keep the build's quality/v-sync/display settings. Old profile
                // values from another platform must not change this configuration.
                Application.targetFrameRate = FormulaPlatform.SwitchFrameLimit;
                return;
            }
            var p = FormulaProgression.State;
            if (p.qualityLevel >= 0) QualitySettings.SetQualityLevel(Mathf.Clamp(p.qualityLevel, 0, QualitySettings.names.Length - 1));
            if (p.vSync >= 0) QualitySettings.vSyncCount = p.vSync;
            if (p.frameLimit > 0) Application.targetFrameRate = p.frameLimit;
#if !UNITY_EDITOR && !UNITY_SWITCH
            if (p.displayMode >= 0) Screen.fullScreenMode = p.displayMode == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
#endif
        }
    }
}
