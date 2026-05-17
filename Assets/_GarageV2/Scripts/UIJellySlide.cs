using UnityEngine;
using DG.Tweening;
public class UIJellySlide : MonoBehaviour
{
    [SerializeField] private RectTransform target;
    [SerializeField] private CanvasGroup canvasGroup;
    
    [SerializeField] private float startOffsetX = 900f;
    [SerializeField] private float duration = 1f;
    [SerializeField] private float delay;
    [SerializeField] private float overshoot = 70f;
    [SerializeField] private bool animateScale;
    
    private Vector2 originalPosition;
    
    private void Awake()
    {
        if (target == null)
        {
            target = GetComponent<RectTransform>();
        }
        originalPosition = target.anchoredPosition;
    }

    public void PlayIn(int direction)
    {
        direction = direction >= 0 ? 1 : -1;

        target.DOKill();
        
        target.anchoredPosition = originalPosition + new Vector2(startOffsetX * direction, 0f);
        if (animateScale)
        {
            target.localScale = new Vector3(0.92f, 1.08f, 1f);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
        
        Sequence sequence = DOTween.Sequence();
        sequence.SetDelay(delay);

        sequence.Append(target.DOAnchorPos(originalPosition + new Vector2(-overshoot * direction, 0f), duration * 0.75f)
            .SetEase(Ease.OutCubic));
        
        if (animateScale)
        {
            sequence.Join(target.DOScale(new Vector3(1.04f, 0.96f, 1f), duration * 0.75f)
                .SetEase(Ease.OutCubic));
        }

        if (canvasGroup != null)
        {
            sequence.Join(canvasGroup.DOFade(1f, duration * 0.5f));
        }
        
        sequence.Append(target.DOAnchorPos(originalPosition, duration * 0.25f)
            .SetEase(Ease.OutBack));
        
        if (animateScale)
        {
            sequence.Join(target.DOScale(Vector3.one, duration * 0.25f)
                .SetEase(Ease.OutBack));
        }
    }
}
