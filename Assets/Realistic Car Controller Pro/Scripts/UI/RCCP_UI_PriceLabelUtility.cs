//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class RCCP_UI_PriceLabelUtility {

    private class PriceLabelState {

        public TextAlignmentOptions alignment;
        public Color color;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public float fontSize;
        public TextOverflowModes overflowMode;
        public readonly Dictionary<GameObject, bool> iconStates = new Dictionary<GameObject, bool>();

    }

    private static readonly Dictionary<TMP_Text, PriceLabelState> states = new Dictionary<TMP_Text, PriceLabelState>();

    public static void SetPrice(TMP_Text priceText, int price) {

        if (!priceText)
            return;

        PriceLabelState state = GetState(priceText);

        priceText.text = price.ToString();
        priceText.fontSize = state.fontSize;
        priceText.overflowMode = state.overflowMode;
        priceText.alignment = state.alignment;
        priceText.color = state.color;
        RestorePosition(priceText, state);
        SetIconsActive(state, true);
        RefreshText(priceText);

    }

    public static void SetPurchased(TMP_Text priceText, string purchasedText) {

        if (!priceText)
            return;

        PriceLabelState state = GetState(priceText);

        priceText.text = UILocalization.Get("ui.owned", string.IsNullOrEmpty(purchasedText) ? "Owned" : purchasedText);
        priceText.fontSize = Mathf.Max(24f, state.fontSize * .8f);
        priceText.overflowMode = TextOverflowModes.Overflow;
        priceText.alignment = TextAlignmentOptions.Center;
        priceText.color = new Color(0.35f, 1f, 0.25f, state.color.a);
        CenterBottomPosition(priceText, state);
        SetIconsActive(state, false);
        RefreshText(priceText);

    }

    public static void SetEquipped(TMP_Text priceText, string equippedText) {

        if (!priceText)
            return;

        PriceLabelState state = GetState(priceText);

        priceText.text = UILocalization.Get("ui.in_use", string.IsNullOrEmpty(equippedText) ? "In Use" : equippedText);
        priceText.fontSize = Mathf.Max(24f, state.fontSize * .8f);
        priceText.overflowMode = TextOverflowModes.Overflow;
        priceText.alignment = TextAlignmentOptions.Center;
        priceText.color = new Color(0.35f, 1f, 0.25f, state.color.a);
        CenterBottomPosition(priceText, state);
        SetIconsActive(state, false);
        RefreshText(priceText);

    }

    private static PriceLabelState GetState(TMP_Text priceText) {

        if (states.TryGetValue(priceText, out PriceLabelState state))
            return state;

        RectTransform rectTransform = priceText.rectTransform;

        state = new PriceLabelState {
            alignment = priceText.alignment,
            color = priceText.color,
            anchorMin = rectTransform.anchorMin,
            anchorMax = rectTransform.anchorMax,
            pivot = rectTransform.pivot,
            anchoredPosition = rectTransform.anchoredPosition,
            sizeDelta = rectTransform.sizeDelta,
            fontSize = priceText.fontSize,
            overflowMode = priceText.overflowMode
        };

        Graphic[] icons = priceText.GetComponentsInChildren<Graphic>(true);

        foreach (Graphic icon in icons) {

            if (!icon || icon.gameObject == priceText.gameObject)
                continue;

            state.iconStates[icon.gameObject] = icon.gameObject.activeSelf;

        }

        states.Add(priceText, state);

        return state;

    }

    private static void SetIconsActive(PriceLabelState state, bool active) {

        foreach (KeyValuePair<GameObject, bool> iconState in state.iconStates) {

            if (!iconState.Key)
                continue;

            iconState.Key.SetActive(active ? iconState.Value : false);

        }

    }

    private static void CenterBottomPosition(TMP_Text priceText, PriceLabelState state) {

        RectTransform rectTransform = priceText.rectTransform;

        rectTransform.anchorMin = new Vector2(.5f, .5f);
        rectTransform.anchorMax = new Vector2(.5f, .5f);
        rectTransform.pivot = new Vector2(.5f, .5f);
        rectTransform.anchoredPosition = new Vector2(0f, state.anchoredPosition.y);

        RectTransform parentRect = rectTransform.parent as RectTransform;

        if (parentRect)
            rectTransform.sizeDelta = new Vector2(Mathf.Max(120f, parentRect.rect.width - 20f), state.sizeDelta.y);

    }

    private static void RestorePosition(TMP_Text priceText, PriceLabelState state) {

        RectTransform rectTransform = priceText.rectTransform;

        rectTransform.anchorMin = state.anchorMin;
        rectTransform.anchorMax = state.anchorMax;
        rectTransform.pivot = state.pivot;
        rectTransform.anchoredPosition = state.anchoredPosition;
        rectTransform.sizeDelta = state.sizeDelta;

    }

    private static void RefreshText(TMP_Text priceText) {

        if (!priceText)
            return;

        priceText.havePropertiesChanged = true;
        priceText.ForceMeshUpdate(true, true);

    }

}
