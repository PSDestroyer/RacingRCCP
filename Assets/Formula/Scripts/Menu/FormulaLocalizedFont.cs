using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;

namespace FormulaGame
{
    [Serializable]
    public sealed class FormulaFontReference : LocalizedAsset<FormulaFontProfile> { }

    // The Asset Table owns locale/font selection and shares loads across labels.
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(TMP_Text))]
    public sealed class FormulaLocalizedFont : MonoBehaviour
    {
        [SerializeField] private FormulaFontReference fontReference = new FormulaFontReference();
        [SerializeField] private float authoredSize;
        [SerializeField] private bool condensedHeading;
        [SerializeField] private TMP_FontAsset authoredFont;
        private TMP_Text label;
        private float requestedSize;
        private string appliedLocale;
        public bool Ready => appliedLocale != null && appliedLocale == LocalizationSettings.SelectedLocale?.Identifier.Code;

        public void Configure(string key, float size, bool heading)
        {
            fontReference.AssetChanged -= Apply;
            fontReference.TableReference = FormulaLocalization.FontTableName;
            fontReference.TableEntryReference = key;
            authoredSize = size; condensedHeading = heading;
            if (isActiveAndEnabled) OnEnable();
        }
        private void OnEnable()
        {
            label = GetComponent<TMP_Text>();
            if (authoredFont == null) authoredFont = label.font;
            if (requestedSize <= 0) requestedSize = authoredSize > 0 ? authoredSize : label.fontSize;
            appliedLocale = null;
            if (fontReference.IsEmpty) return;
            fontReference.AssetChanged += Apply;
        }
        private void OnDisable() { fontReference.AssetChanged -= Apply; appliedLocale = null; }
        private void Apply(FormulaFontProfile profile)
        {
            if (profile == null || label == null) return;
            if (Application.isPlaying)
            {
                // Text and font callbacks can complete in a different order. Update the
                // label before changing its face so old CJK text never reaches the new font.
                var textBinding = GetComponent<LocalizeStringEvent>();
                if (textBinding != null && textBinding.StringReference.TableReference.TableCollectionName == FormulaLocalization.TableName)
                    label.text = FormulaLocalization.T(textBinding.StringReference.TableEntryReference.Key);
                if (GetComponent<FormulaDevicePrompt>() != null) label.text = FormulaInputHints.Back;
            }
            label.font = profile.font != null ? profile.font : authoredFont; appliedLocale = LocalizationSettings.SelectedLocale?.Identifier.Code;
            SetSize(requestedSize);
        }
        public void SetSize(float size)
        {
            requestedSize = size;
            if (label == null) label = GetComponent<TMP_Text>();
            bool cjk = label.font != null && label.font.name.StartsWith("Formula CJK", StringComparison.Ordinal);
            // Noto's full em height differs from the original condensed display face.
            float max = size * (cjk && condensedHeading ? .72f : cjk ? .88f : 1f);
            label.fontSizeMax = max;
            label.fontSizeMin = Mathf.Min(max, Mathf.Max(14, max * .72f));
            label.fontSize = max;
            label.enableAutoSizing = true;
            label.SetAllDirty();
        }
    }
}
