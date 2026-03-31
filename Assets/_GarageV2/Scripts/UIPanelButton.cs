using UnityEngine;

public class UIPanelButton : MonoBehaviour
{
    [SerializeField] private GarageUIController uiController;
    [SerializeField] private UIPanelType targetPanel;

    public void OpenTargetPanel()
    {
        uiController.OpenPanel(targetPanel);
    }

    public void GoBack()
    {
        uiController.Back();
    }
}