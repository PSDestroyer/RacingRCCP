using UnityEngine;

/// <summary>
/// Keeps a referenced camera active only while this panel is active.
/// Attach this component to the panel and assign its camera in the Inspector.
/// </summary>
public sealed class PanelCameraController : MonoBehaviour
{
    [Tooltip("Assign a Cinemachine Camera, Unity Camera, or another camera component.")]
    [SerializeField] private Component panelCamera;

    private void OnEnable()
    {
        SetCameraActive(true);
    }

    private void OnDisable()
    {
        SetCameraActive(false);
    }

    private void SetCameraActive(bool active)
    {
        if (panelCamera == null)
            return;

        if (panelCamera.gameObject.activeSelf != active)
            panelCamera.gameObject.SetActive(active);

        if (panelCamera is Behaviour cameraBehaviour)
            cameraBehaviour.enabled = active;
    }
}
