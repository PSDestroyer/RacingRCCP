using TMPro;
using UnityEngine;

public class LoadingScreenTextAnimator : MonoBehaviour
{
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private string baseText = "Prepairing Track";
    [SerializeField] private float stepTime = 0.35f;

    private float timer;
    private int dots;

    private void Awake()
    {
        if (targetText == null)
            targetText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        timer = 0f;
        dots = 0;
        UpdateText();
    }

    private void Update()
    {
        timer += Time.unscaledDeltaTime;

        if (timer < stepTime)
            return;

        timer = 0f;
        dots = (dots + 1) % 4;
        UpdateText();
    }

    private void UpdateText()
    {
        if (targetText == null)
            return;

        targetText.text = baseText + new string('.', dots);
    }
}
