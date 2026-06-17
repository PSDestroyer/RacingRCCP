using UnityEngine;
using UnityEngine.InputSystem;

public class MenuCameraOrbit : MonoBehaviour
{
    [Header("Orbit Target")]
    [SerializeField] private Transform orbitTarget;

    [Header("Enable")]
    [SerializeField] private bool allowHorizontalOrbit = true;
    [SerializeField] private bool allowVerticalOrbit = false;

    [Header("Input")]
    [SerializeField] private bool useMouseDrag = true;
    [SerializeField] private bool useGamepadRightStick = true;
    [SerializeField] private bool requireMouseHold = true;
    [SerializeField] private int mouseButton = 0;
    [SerializeField] private float mouseSensitivity = 0.18f;
    [SerializeField] private float gamepadSensitivity = 90f;
    [SerializeField] private bool invertX = false;
    [SerializeField] private bool invertY = false;

    [Header("Horizontal Orbit")]
    [SerializeField] private bool horizontalWrap360 = true;
    [SerializeField] private float horizontalMinAngle = -60f;
    [SerializeField] private float horizontalMaxAngle = 60f;

    [Header("Vertical Orbit")]
    [SerializeField] private bool verticalWrap360 = false;
    [SerializeField] private float verticalMinAngle = -25f;
    [SerializeField] private float verticalMaxAngle = 25f;

    [Header("Smoothing")]
    [SerializeField] private bool smoothMotion = true;
    [SerializeField] private float smoothSpeed = 12f;

    private float currentYaw;
    private float currentPitch;
    private float targetYaw;
    private float targetPitch;

    private void Awake()
    {
        if (orbitTarget == null)
            orbitTarget = transform;

        Vector3 euler = orbitTarget.localEulerAngles;
        currentYaw = NormalizeAngle(euler.y);
        currentPitch = NormalizeAngle(euler.x);
        targetYaw = currentYaw;
        targetPitch = currentPitch;
    }

    private void OnEnable()
    {
        if (orbitTarget == null)
            orbitTarget = transform;

        Vector3 euler = orbitTarget.localEulerAngles;
        currentYaw = NormalizeAngle(euler.y);
        currentPitch = NormalizeAngle(euler.x);
        targetYaw = currentYaw;
        targetPitch = currentPitch;
        ApplyRotationImmediate();
    }

    private void Update()
    {
        if (orbitTarget == null)
            return;

        Vector2 lookInput = GetLookInput();

        if (lookInput.sqrMagnitude > 0.000001f)
            ApplyInput(lookInput);

        UpdateRotation();
    }

    private Vector2 GetLookInput()
    {
        Vector2 input = Vector2.zero;

        if (useMouseDrag && Mouse.current != null)
        {
            bool canUseMouse = !requireMouseHold || MouseButtonPressed(Mouse.current, mouseButton);

            if (canUseMouse)
                input += Mouse.current.delta.ReadValue() * mouseSensitivity;
        }

        if (useGamepadRightStick && Gamepad.current != null)
            input += Gamepad.current.rightStick.ReadValue() * gamepadSensitivity * Time.unscaledDeltaTime;

        return input;
    }

    private void ApplyInput(Vector2 lookInput)
    {
        float horizontalDelta = allowHorizontalOrbit ? lookInput.x : 0f;
        float verticalDelta = allowVerticalOrbit ? lookInput.y : 0f;

        if (invertX)
            horizontalDelta *= -1f;

        if (invertY)
            verticalDelta *= -1f;

        targetYaw += horizontalDelta;
        targetPitch -= verticalDelta;

        targetYaw = horizontalWrap360 ? NormalizeAngle(targetYaw) : Mathf.Clamp(targetYaw, horizontalMinAngle, horizontalMaxAngle);
        targetPitch = verticalWrap360 ? NormalizeAngle(targetPitch) : Mathf.Clamp(targetPitch, verticalMinAngle, verticalMaxAngle);
    }

    private void UpdateRotation()
    {
        if (smoothMotion)
        {
            currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, Time.unscaledDeltaTime * Mathf.Max(0.01f, smoothSpeed));
            currentPitch = Mathf.LerpAngle(currentPitch, targetPitch, Time.unscaledDeltaTime * Mathf.Max(0.01f, smoothSpeed));
        }
        else
        {
            currentYaw = targetYaw;
            currentPitch = targetPitch;
        }

        orbitTarget.localRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
    }

    private void ApplyRotationImmediate()
    {
        currentYaw = targetYaw;
        currentPitch = targetPitch;
        orbitTarget.localRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f)
            angle -= 360f;

        while (angle < -180f)
            angle += 360f;

        return angle;
    }

    private static bool MouseButtonPressed(Mouse mouse, int buttonIndex)
    {
        return buttonIndex switch
        {
            1 => mouse.rightButton.isPressed,
            2 => mouse.middleButton.isPressed,
            _ => mouse.leftButton.isPressed,
        };
    }
}
