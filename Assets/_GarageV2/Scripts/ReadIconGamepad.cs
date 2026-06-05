using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ReadIconGamepad : MonoBehaviour
{
    [Header("Binding Source")]
    [SerializeField] private InputActionReference actionReference;
    [SerializeField] private string bindingId = string.Empty;
    [SerializeField] private int bindingIndexOverride = -1;

    [Header("UI")]
    [SerializeField] private TMP_Text keyboardText;
    [SerializeField] private Image gamepadIcon;

    [Header("Dependencies")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private RCCP_GamepadIcons gamepadIcons;

    private string lastControlScheme = string.Empty;
    private string lastDisplayText = string.Empty;
    private string lastDeviceLayout = string.Empty;
    private string lastControlPath = string.Empty;

    private void OnEnable()
    {
        RefreshReferences();
        RefreshDisplay(true);
    }

    private void Update()
    {
        RefreshDisplay(false);
    }

    public void RefreshDisplayNow()
    {
        RefreshDisplay(true);
    }

    private void RefreshDisplay(bool forceRefresh)
    {
        RefreshReferences();

        if (actionReference == null || actionReference.action == null)
            return;

        string currentControlScheme = playerInput != null ? playerInput.currentControlScheme : string.Empty;

        if (!TryGetBindingDisplayData(actionReference.action, currentControlScheme, out string displayString, out string deviceLayoutName, out string controlPath))
            return;

        if (!forceRefresh &&
            lastControlScheme == currentControlScheme &&
            lastDisplayText == displayString &&
            lastDeviceLayout == deviceLayoutName &&
            lastControlPath == controlPath)
            return;

        lastControlScheme = currentControlScheme;
        lastDisplayText = displayString;
        lastDeviceLayout = deviceLayoutName;
        lastControlPath = controlPath;

        ApplyDisplay(displayString, deviceLayoutName, controlPath);
    }

    private void ApplyDisplay(string displayString, string deviceLayoutName, string controlPath)
    {
        Sprite iconSprite = gamepadIcons != null ? gamepadIcons.GetSpriteForBinding(deviceLayoutName, controlPath) : null;
        bool shouldShowGamepadIcon = iconSprite != null && gamepadIcons != null && gamepadIcons.IsGamepadLayout(deviceLayoutName);

        if (keyboardText != null)
        {
            keyboardText.text = displayString;
            keyboardText.gameObject.SetActive(!shouldShowGamepadIcon);
        }

        if (gamepadIcon != null)
        {
            gamepadIcon.sprite = iconSprite;
            gamepadIcon.gameObject.SetActive(shouldShowGamepadIcon);
        }
    }

    private bool TryGetBindingDisplayData(InputAction action, string controlScheme, out string displayString, out string deviceLayoutName, out string controlPath)
    {
        displayString = string.Empty;
        deviceLayoutName = string.Empty;
        controlPath = string.Empty;

        int bindingIndex = ResolveBindingIndex(action, controlScheme);

        if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            return false;

        displayString = action.GetBindingDisplayString(bindingIndex, out deviceLayoutName, out controlPath);
        return true;
    }

    private int ResolveBindingIndex(InputAction action, string controlScheme)
    {
        if (!string.IsNullOrWhiteSpace(bindingId))
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].id.ToString() == bindingId)
                    return i;
            }
        }

        if (bindingIndexOverride >= 0 && bindingIndexOverride < action.bindings.Count)
            return bindingIndexOverride;

        int schemeBindingIndex = FindFirstBindingForScheme(action, controlScheme);
        if (schemeBindingIndex >= 0)
            return schemeBindingIndex;

        return FindFirstUsableBinding(action);
    }

    private int FindFirstBindingForScheme(InputAction action, string controlScheme)
    {
        if (string.IsNullOrWhiteSpace(controlScheme))
            return -1;

        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];

            if (binding.isComposite || binding.isPartOfComposite)
                continue;

            if (string.IsNullOrWhiteSpace(binding.groups))
                continue;

            string[] groups = binding.groups.Split(';');

            for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                if (string.Equals(groups[groupIndex].Trim(), controlScheme, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            }
        }

        return -1;
    }

    private int FindFirstUsableBinding(InputAction action)
    {
        for (int i = 0; i < action.bindings.Count; i++)
        {
            if (!action.bindings[i].isComposite && !action.bindings[i].isPartOfComposite)
                return i;
        }

        return -1;
    }

    private void RefreshReferences()
    {
        if (playerInput == null && InputManager.Instance != null)
            playerInput = InputManager.Instance.GetPlayerInput();

        if (playerInput == null)
            playerInput = FindFirstObjectByType<PlayerInput>(FindObjectsInactive.Include);

        if (gamepadIcons == null)
            gamepadIcons = FindFirstObjectByType<RCCP_GamepadIcons>(FindObjectsInactive.Include);
    }
}
