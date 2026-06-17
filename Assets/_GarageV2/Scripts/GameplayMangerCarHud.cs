using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameplayMangerCarHud : MonoBehaviour
{
    [Header("Vehicle Source")]
    public RCCP_CarController targetCar;
    public bool useActivePlayerVehicle = true;

    [Header("Speed")]
    public TMP_Text speedKmhText;
    public string speedSuffix = " KM/H";

    [Header("Gear")]
    public TMP_Text currentGearText;
    public Image currentGearImage;
    public Color lowGearColor = Color.green;
    public Color midGearColor = Color.yellow;
    public Color highGearColor = Color.red;
    public Color neutralGearColor = Color.white;
    public Color reverseGearColor = Color.white;

    [Header("Tachometer")]
    public RectTransform tachometerNeedle;
    public float tachometerMinAngle = 130f;
    public float tachometerMaxAngle = -130f;

    [Header("Assist Icons")]
    public bool showABS = true;
    public bool showESP = true;
    public bool showTCS = true;
    public Image absImage;
    public Image espImage;
    public Image tcsImage;

    private RCCP_CarController currentCar;
    private RCCP_Stability currentStability;

    private void Start()
    {
        RefreshVehicleReference(forceRefresh: true);
        RefreshStaticIconVisibility();
        UpdateHud();
    }

    private void Update()
    {
        RefreshVehicleReference(forceRefresh: false);
        UpdateHud();
    }

    private void RefreshVehicleReference(bool forceRefresh)
    {
        RCCP_CarController desiredCar = ResolveTargetCar();

        if (!forceRefresh && desiredCar == currentCar)
            return;

        currentCar = desiredCar;
        currentStability = currentCar != null ? currentCar.Stability : null;
        RefreshStaticIconVisibility();
    }

    private RCCP_CarController ResolveTargetCar()
    {
        if (!useActivePlayerVehicle)
            return targetCar;

        if (RCCP_SceneManager.Instance != null && RCCP_SceneManager.Instance.activePlayerVehicle != null)
            return RCCP_SceneManager.Instance.activePlayerVehicle;

        return targetCar;
    }

    private void UpdateHud()
    {
        if (currentCar == null)
        {
            SetDefaultHud();
            return;
        }

        UpdateSpeedText();
        UpdateGearText();
        UpdateTachometer();
        UpdateAssistIcons();
    }

    private void SetDefaultHud()
    {
        if (speedKmhText != null)
            speedKmhText.text = $"0{speedSuffix}";

        if (currentGearText != null)
        {
            currentGearText.text = "N";
            currentGearText.color = neutralGearColor;
        }

        if (currentGearImage != null)
            currentGearImage.color = neutralGearColor;

        if (tachometerNeedle != null)
            tachometerNeedle.localRotation = Quaternion.Euler(0f, 0f, tachometerMinAngle);

        SetImageEnabled(absImage, false);
        SetImageEnabled(espImage, false);
        SetImageEnabled(tcsImage, false);
    }

    private void UpdateSpeedText()
    {
        if (speedKmhText == null)
            return;

        int speedValue = Mathf.RoundToInt(currentCar.absoluteSpeed);
        speedKmhText.text = $"{speedValue}{speedSuffix}";
    }

    private void UpdateGearText()
    {
        if (currentGearText == null)
            return;

        if (currentCar.NGearNow)
        {
            currentGearText.text = "N";
            SetGearVisualColor(neutralGearColor);
            return;
        }

        if (currentCar.reversingNow || currentCar.direction < 0)
        {
            currentGearText.text = "R";
            SetGearVisualColor(reverseGearColor);
            return;
        }

        int gearValue = Mathf.Max(1, currentCar.currentGear);
        currentGearText.text = gearValue.ToString();
        SetGearVisualColor(GetGearColor(gearValue));
    }

    private Color GetGearColor(int gearValue)
    {
        if (gearValue <= 2)
            return lowGearColor;

        if (gearValue <= 4)
            return midGearColor;

        return highGearColor;
    }

    private void SetGearVisualColor(Color targetColor)
    {
        if (currentGearText != null)
            currentGearText.color = targetColor;

        if (currentGearImage != null)
            currentGearImage.color = targetColor;
    }

    private void UpdateTachometer()
    {
        if (tachometerNeedle == null)
            return;

        float minRpm = Mathf.Max(0f, currentCar.minEngineRPM);
        float maxRpm = Mathf.Max(minRpm + 1f, currentCar.maxEngineRPM);
        float rpmNormalized = Mathf.InverseLerp(minRpm, maxRpm, currentCar.engineRPM);
        float needleAngle = Mathf.Lerp(tachometerMinAngle, tachometerMaxAngle, rpmNormalized);
        tachometerNeedle.localRotation = Quaternion.Euler(0f, 0f, needleAngle);
    }

    private void UpdateAssistIcons()
    {
        if (currentStability == null)
        {
            SetImageEnabled(absImage, false);
            SetImageEnabled(espImage, false);
            SetImageEnabled(tcsImage, false);
            return;
        }

        SetImageEnabled(absImage, showABS && currentStability.ABS && currentStability.ABSEngaged);
        SetImageEnabled(espImage, showESP && currentStability.ESP && currentStability.ESPEngaged);
        SetImageEnabled(tcsImage, showTCS && currentStability.TCS && currentStability.TCSEngaged);
    }

    private void RefreshStaticIconVisibility()
    {
        if (!showABS && absImage != null)
            absImage.gameObject.SetActive(false);

        if (!showESP && espImage != null)
            espImage.gameObject.SetActive(false);

        if (!showTCS && tcsImage != null)
            tcsImage.gameObject.SetActive(false);
    }

    private void SetImageEnabled(Image image, bool state)
    {
        if (image == null)
            return;

        image.gameObject.SetActive(state);
    }
}
