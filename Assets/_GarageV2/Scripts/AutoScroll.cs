using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class AutoScroll : MonoBehaviour
{
    [Header("Scrollbar")]
    public ScrollRect scrollRect;
    private RectTransform contentRectTransform;
    private float contentTop;
    private float contentBottom;
    private bool isScrolling = false;
    private float targetNormalizedPosition;
    private float scrollDuration = 0.5f;
    private float elapsedTime = 0f;
    private float lastMoveTime = 0f;
    private float moveCooldown = 0.2f; // Adjust this value as needed
    private Vector2 targetNormPos;


    public void Start()
    {
        
        contentRectTransform = scrollRect.content.GetComponent<RectTransform>();
        contentTop = contentRectTransform.localPosition.x - 1100 + (contentRectTransform.rect.width / 2f);
        contentBottom = contentRectTransform.localPosition.x + 50- (contentRectTransform.rect.width / 2f); 
        StartCoroutine(scroll());
    }

    IEnumerator scroll()
    {
        yield return new WaitForSeconds(1);
        Canvas.ForceUpdateCanvases();
        ScrollToSelectedButton(GlobalCarData._buttonList[0]);

    }

    private void Update()
    {
        if (!isScrolling) return;

        elapsedTime += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsedTime / scrollDuration);

        scrollRect.normalizedPosition = Vector2.Lerp(scrollRect.normalizedPosition, targetNormPos, t);

        if (elapsedTime >= scrollDuration)
            isScrolling = false;
    }
    
    public void ScrollToSelectedButton(Button selectedButton)
    {
        if (scrollRect == null || scrollRect.content == null || selectedButton == null) return;

        RectTransform content = scrollRect.content;
        RectTransform viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();
        RectTransform item = selectedButton.GetComponent<RectTransform>();

        // Bounds in viewport space
        Bounds contentBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, content);
        Bounds itemBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, item);

        Vector2 norm = scrollRect.normalizedPosition;

        // --- Horizontal ---
        if (scrollRect.horizontal)
        {
            float contentWidth = contentBounds.size.x;
            float viewWidth = viewport.rect.width;

            if (contentWidth > viewWidth + 0.001f)
            {
                // item center in content-bounds space
                float itemCenterX = itemBounds.center.x - contentBounds.min.x;
                float hiddenWidth = contentWidth - viewWidth;

                // normalized 0..1 (0=left, 1=right)
                float x = Mathf.Clamp01((itemCenterX - viewWidth * 0.5f) / hiddenWidth);
                norm.x = x;
            }
        }

        // --- Vertical ---
        if (scrollRect.vertical)
        {
            float contentHeight = contentBounds.size.y;
            float viewHeight = viewport.rect.height;

            if (contentHeight > viewHeight + 0.001f)
            {
                // item center in content-bounds space
                float itemCenterY = itemBounds.center.y - contentBounds.min.y;
                float hiddenHeight = contentHeight - viewHeight;

                // normalized 0..1 (0=bottom, 1=top) in ScrollRect.normalizedPosition
                float y = Mathf.Clamp01((itemCenterY - viewHeight * 0.5f) / hiddenHeight);
                norm.y = y;
            }
        }

        targetNormPos = norm;
        isScrolling = true;
        elapsedTime = 0f;
    }

}
