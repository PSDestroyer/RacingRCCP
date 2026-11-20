using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FormulaGame;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public static class FormulaSteamAchievementValidation
{
    private const string Root = "Docs/Steam";

    [MenuItem("Tools/Formula Career/Validate Steam Achievements")]
    public static void Run()
    {
        var errors = new List<string>();
        var definitions = FormulaAchievementCatalog.All;
        var ids = definitions.Select(definition => definition.ApiName).ToArray();

        Check(definitions.Count == 8, "Expected exactly eight achievements.", errors);
        Check(ids.Distinct(StringComparer.Ordinal).Count() == ids.Length, "Achievement API names are not unique.", errors);
        foreach (FormulaAchievementCatalog.Definition definition in definitions)
        {
            Check(Regex.IsMatch(definition.ApiName ?? "", "^ACH_FORMULA_[A-Z0-9_]+$"), $"Invalid API name: {definition.ApiName}", errors);
            Check(!string.IsNullOrWhiteSpace(definition.Name), $"Missing name: {definition.ApiName}", errors);
            Check(!string.IsNullOrWhiteSpace(definition.Description), $"Missing description: {definition.ApiName}", errors);

            ValidateIcon(definition.ApiName + "_ACHIEVED.png", false, errors);
            ValidateIcon(definition.ApiName + "_UNACHIEVED.png", true, errors);
        }

        ValidateCsv(Path.Combine(Root, "achievements_definitions.csv"), ids, errors);
        ValidateCsv(Path.Combine(Root, "achievements_localization.csv"), ids, errors);
        ValidateRules(errors);

        var report = new
        {
            status = errors.Count == 0 ? "passed" : "failed",
            generatedUtc = DateTime.UtcNow.ToString("O"),
            sdk = "Steamworks.NET 2025.165.0",
            achievementCount = definitions.Count,
            iconCount = Directory.GetFiles(Path.Combine(Root, "achievement-icons"), "*.png").Length,
            saveSystemTouched = false,
            errors
        };

        File.WriteAllText(Path.Combine(Root, "validation.json"), JsonConvert.SerializeObject(report, Formatting.Indented));
        AssetDatabase.Refresh();

        if (errors.Count > 0)
            throw new InvalidOperationException("Steam achievement validation failed: " + string.Join(" | ", errors));

        Debug.Log("[Formula Steam] Achievement validation passed: 8 definitions, 16 icons, CSV data and unlock rules.");
    }

    private static void ValidateIcon(string filename, bool expectGray, ICollection<string> errors)
    {
        string path = Path.Combine(Root, "achievement-icons", filename);
        if (!File.Exists(path))
        {
            errors.Add("Missing icon: " + path);
            return;
        }

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Check(ImageConversion.LoadImage(texture, File.ReadAllBytes(path)), "Unreadable icon: " + filename, errors);
            Check(texture.width == 256 && texture.height == 256, $"Icon must be 256x256: {filename}", errors);
            if (expectGray)
            {
                Color[] pixels = texture.GetPixels();
                int step = Math.Max(1, pixels.Length / 256);
                for (int index = 0; index < pixels.Length; index += step)
                {
                    Color pixel = pixels[index];
                    if (Mathf.Abs(pixel.r - pixel.g) > 0.02f || Mathf.Abs(pixel.g - pixel.b) > 0.02f)
                    {
                        errors.Add("Unachieved icon is not grayscale: " + filename);
                        break;
                    }
                }
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void ValidateCsv(string path, IEnumerable<string> ids, ICollection<string> errors)
    {
        if (!File.Exists(path))
        {
            errors.Add("Missing CSV: " + path);
            return;
        }

        string csv = File.ReadAllText(path);
        foreach (string id in ids)
            Check(csv.Contains(id), $"{Path.GetFileName(path)} does not contain {id}.", errors);
    }

    private static void ValidateRules(ICollection<string> errors)
    {
        Check(!FormulaAchievementRules.ForCompletedLap(false).Any(), "An invalid lap unlocked an achievement.", errors);
        Check(FormulaAchievementRules.ForCompletedLap(true).SequenceEqual(new[] { FormulaAchievementCatalog.FirstValidLap }), "Valid-lap rule mismatch.", errors);

        var race = new FormulaCatalog.CareerEvent { mode = "race" };
        var winningReceipt = new FormulaReceipt { bestLapSeconds = 50, finishPosition = 1, medal = Medal.Gold, personalBest = true };
        HashSet<string> raceUnlocks = FormulaAchievementRules.ForFinishedEvent(winningReceipt, race, 55).ToHashSet();
        string[] expectedRace =
        {
            FormulaAchievementCatalog.CheckeredFlag,
            FormulaAchievementCatalog.Podium,
            FormulaAchievementCatalog.RaceWinner,
            FormulaAchievementCatalog.GoldStandard,
            FormulaAchievementCatalog.PersonalBest,
            FormulaAchievementCatalog.ReferencePace
        };
        Check(expectedRace.All(raceUnlocks.Contains), "Race-win rule coverage mismatch.", errors);

        var timeAttack = new FormulaCatalog.CareerEvent { mode = "time_attack" };
        var validReceipt = new FormulaReceipt { bestLapSeconds = 60 };
        HashSet<string> timeUnlocks = FormulaAchievementRules.ForFinishedEvent(validReceipt, timeAttack, 55).ToHashSet();
        Check(timeUnlocks.SetEquals(new[] { FormulaAchievementCatalog.CheckeredFlag, FormulaAchievementCatalog.TimeAttack }), "Time-attack rule mismatch.", errors);
        Check(!FormulaAchievementRules.ForFinishedEvent(new FormulaReceipt(), race, 55).Any(), "An invalid result unlocked an achievement.", errors);
    }

    private static void Check(bool condition, string message, ICollection<string> errors)
    {
        if (!condition)
            errors.Add(message);
    }
}
