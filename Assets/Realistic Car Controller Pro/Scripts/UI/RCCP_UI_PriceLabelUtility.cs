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
        public readonly Dictionary<GameObject, bool> iconStates = new Dictionary<GameObject, bool>();

    }

    private static readonly Dictionary<TMP_Text, PriceLabelState> states = new Dictionary<TMP_Text, PriceLabelState>();

    public static void SetPrice(TMP_Text priceText, int price) {

        if (!priceText)
            return;

        PriceLabelState state = GetState(priceText);

        priceText.text = price.ToString();
        priceText.alignment = state.alignment;
        priceText.color = state.color;
        RestorePosition(priceText, state);
        SetIconsActive(state, true);

    }

    public static void SetPurchased(TMP_Text priceText, string purchasedText) {

        if (!priceText)
            return;

        PriceLabelState state = GetState(priceText);

        priceText.text = string.IsNullOrEmpty(purchasedText) ? "Owned" : purchasedText;
        priceText.alignment = TextAlignmentOptions.Center;
        priceText.color = new Color(0.35f, 1f, 0.25f, state.color.a);
        CenterBottomPosition(priceText, state);
        SetIconsActive(state, false);

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
            anchoredPosition = rectTransform.anchoredPosition
        };

        Image[] icons = priceText.GetComponentsInChildren<Image>(true);

        foreach (Image icon in icons) {

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

    }

    private static void RestorePosition(TMP_Text priceText, PriceLabelState state) {

        RectTransform rectTransform = priceText.rectTransform;

        rectTransform.anchorMin = state.anchorMin;
        rectTransform.anchorMax = state.anchorMax;
        rectTransform.pivot = state.pivot;
        rectTransform.anchoredPosition = state.anchoredPosition;

    }

}
