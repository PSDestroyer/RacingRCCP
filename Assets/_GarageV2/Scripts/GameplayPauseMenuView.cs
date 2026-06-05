using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameplayPauseMenuView : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button homeButton;
    [SerializeField] private TMP_Text titleText;

    public Button ContinueButton => continueButton;
    public Button SettingsButton => settingsButton;
    public Button HomeButton => homeButton;
    public GameObject DefaultSelected => continueButton != null ? continueButton.gameObject : null;

    public void Show()
    {
        if (root != null)
            root.SetActive(true);
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
    }

    public void SetTitle(string value)
    {
        if (titleText != null)
            titleText.text = value;
    }
}
