using System;
using System.Globalization;
using HalvaStudio.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MoneyManager : MonoBehaviour
{
    [Header("Fuel")]
    public bool useFuel;
    [SerializeField] private int maxFuel = 5;
    [SerializeField] private int minutesToRechargeFuel = 5;
    [SerializeField] private int fuel;

    [Header("UI")]
    public TextMeshProUGUI fuelText;
    public TextMeshProUGUI moneyText;
    public TextMeshProUGUI accname;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI expText;
    public Slider expProgressSlider;

    [Header("EXP")]
    public int expPerLevel = 10000;

    [Header("Money")]
    public int money;
    public int exp;
    public int currentLevel;

    private void Start()
    {
        LoadData();
        RefreshFuelState();
        SetUpNik();
        UpdateText();
    }

    public void SetUpNik()
    {
        if (accname != null)
            accname.text = SaveManager.Instance.saveData.PlayerName;
    }
    public void MoneyToTake(int totake)
    {
        if (totake < 0)
        {
            Debug.LogWarning("MoneyToTake received a negative value.");
            return;
        }

        money = Mathf.Max(0, money - totake);
        SaveMoney();
        UpdateText();
    }

    public void MoneyToAdd(int toadd)
    {
        if (toadd < 0)
        {
            Debug.LogWarning("MoneyToAdd received a negative value.");
            return;
        }

        money += toadd;
        SaveMoney();
        UpdateText();
    }

    public void addFuel(int toadd)
    {
        if (!useFuel)
            return;

        if (toadd <= 0)
            return;

        fuel = Mathf.Clamp(fuel + toadd, 0, maxFuel);
        UpdateText();
    }

    public void takeFuel()
    {
        if (!useFuel)
            return;

        if (fuel <= 0)
            return;

        fuel = Mathf.Clamp(fuel - 1, 0, maxFuel);
        UpdateText();

        // Dacă vei reactiva save-ul pentru timp, aici e locul bun:
        // dacă fuel a scăzut sub max și nu există timp salvat, salvezi momentul curent
    }

    public void checkTime()
    {
        if (!useFuel)
            return;

        if (fuel >= maxFuel)
            return;

        DateTime now = DateTime.UtcNow;
        DateTime last = getTime(now);

        TimeSpan timePassed = now - last;
        int minutes = Mathf.Max(0, (int)timePassed.TotalMinutes);

        if (minutes < minutesToRechargeFuel)
            return;

        int toadd = minutes / minutesToRechargeFuel;
        DateTime newtime = last.AddMinutes(toadd * minutesToRechargeFuel);

        saveTime(newtime);
        addFuel(toadd);
    }

    private void LoadData()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.saveData == null)
        {
            Debug.LogWarning("SaveManager or saveData is null.");
            return;
        }

        money = Mathf.Max(0, SaveManager.Instance.saveData.money);
        exp = Mathf.Max(0, SaveManager.Instance.saveData.exp);
        currentLevel = Mathf.Max(1, SaveManager.Instance.saveData.currentLevel);

        // Când vei avea fuel în save:
        // fuel = Mathf.Clamp(SaveManager.Instance.saveData.fuel, 0, maxFuel);
    }

    private void SaveMoney()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.saveData == null)
            return;

        SaveManager.Instance.saveData.money = money;
        SaveManager.Instance.saveData.exp = exp;
        SaveManager.Instance.saveData.currentLevel = currentLevel;
    }

    private void RefreshFuelState()
    {
        if (!useFuel)
        {
            if (fuelText != null)
                fuelText.gameObject.SetActive(false);

            return;
        }

        fuel = Mathf.Clamp(fuel, 0, maxFuel);

        if (fuelText != null)
            fuelText.gameObject.SetActive(true);

        checkTime();
    }

    private void UpdateText()
    {
        SaveMoney();

        if (moneyText != null)
            moneyText.text = money + "<sprite index=0>";

        int safeExpPerLevel = Mathf.Max(1, expPerLevel);
        currentLevel = Mathf.Max(1, currentLevel);
        exp = Mathf.Max(0, exp);

        if (levelText != null)
            levelText.text = $"Level {currentLevel}";

        if (expText != null)
            expText.text = $"{exp % safeExpPerLevel:N0}/{safeExpPerLevel:N0} EXP";

        if (expProgressSlider != null)
            expProgressSlider.value = (float)(exp % safeExpPerLevel) / safeExpPerLevel;

        if (useFuel && fuelText != null)
            fuelText.text = fuel + "/" + maxFuel;
    }

    private void saveTime(DateTime time)
    {
        string convertedtime = time.ToString("u", CultureInfo.InvariantCulture);

        // Când reactivezi save-ul:
        // _PlayersPrefs.SetString(SaveKeys.time, convertedtime);
    }

    private DateTime getTime(DateTime value)
    {
        // Când reactivezi save-ul:
        // if (_PlayersPrefs.HasKey(SaveKeys.time))
        // {
        //     string convertedtime = _PlayersPrefs.GetString(SaveKeys.time);
        //     DateTime last_time = DateTime.ParseExact(convertedtime, "u", CultureInfo.InvariantCulture);
        //     return last_time;
        // }

        return value;
    }
}
