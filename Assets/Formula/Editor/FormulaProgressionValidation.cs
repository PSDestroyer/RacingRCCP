using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using HalvaStudio.Save;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public static class FormulaProgressionValidation
{
    [Serializable] private sealed class Report { public string editor; public List<string> passed = new List<string>(); public List<string> failed = new List<string>(); }
    [MenuItem("Tools/Formula Career/Validate Progression and Saves")]
    public static void Run()
    {
        var report = new Report { editor = Application.unityVersion };
        void Check(string name, Action action) { try { action(); report.passed.Add(name); } catch (Exception e) { report.failed.Add(name + ": " + e.Message); } }
        void Assert(bool condition, string reason) { if (!condition) throw new Exception(reason); }
        SaveManager.SaveData New()
        {
            var d = new SaveManager.SaveData { money = 0, exp = 0, currentLevel = 1 }; FormulaProgression.Ensure(d); return d;
        }
        FormulaReceipt Finish(SaveManager.SaveData d, string e, Medal m, string mode = "career", bool valid = true)
        {
            string error = FormulaProgression.BeginRun(d, e, mode); Assert(error == null, error);
            return FormulaProgression.Complete(d, d.formula.activeRunId, e, valid, m, 100, 30);
        }
        Check("New profile grants one chassis, two liveries and no credits", () => { var d = New(); Assert(d.money == 0 && d.formula.ownedChassis.Count == 1 && d.formula.ownedLiveries.Count == 2, "Invalid starter grant"); });
        Check("Fresh profile uses catalog economy instead of legacy scene defaults", () =>
        {
            var template = new SaveManager.SaveData { PlayerName = "Legacy", money = 417, exp = 9999, currentLevel = 8, soundLevel = .25f };
            var d = SaveManager.CreateNewProfile(template);
            Assert(d.PlayerName == null && d.money == 0 && d.exp == 0 && d.currentLevel == 1 && d.formula.ownedLiveries.Count == 2 && d.soundLevel == .25f, "Starter catalog or default settings incorrect");
            Assert(template.money == 417 && template.currentLevel == 8, "Template was mutated");
        });
        Check("Repeated serialization never duplicates starter ownership", () =>
        {
            var d = New();
            for (int i = 0; i < 10; i++) { d = JsonConvert.DeserializeObject<SaveManager.SaveData>(JsonConvert.SerializeObject(d)); FormulaProgression.Ensure(d); }
            Assert(d.formula.ownedChassis.Count == 1 && d.formula.ownedLiveries.Count == 2, "Ownership duplicated during reload");
        });
        Check("Legacy money, XP, level floor and car ownership survive migration", () =>
        {
            var d = JsonConvert.DeserializeObject<SaveManager.SaveData>("{\"money\":9000,\"exp\":12345,\"currentLevel\":7,\"currentCar\":3,\"futureField\":{\"value\":99}}");
            FormulaProgression.Ensure(d); FormulaProgression.Ensure(d);
            Assert(d.money == 9000 && d.exp == 12345 && d.currentLevel >= 7 && d.formula.legacyCarIndex == 3, "Legacy progress changed");
            Assert(JsonConvert.SerializeObject(d).Contains("futureField"), "Unknown legacy field was dropped");
        });
        Check("Locked event cannot start", () => Assert(FormulaProgression.BeginRun(New(), "g06", "career") != null, "Gate bypassed"));
        Check("Locked upgrade cannot debit wallet", () => { var d = New(); d.money = 9999; Assert(FormulaProgression.BuyUpgrade(d, "engine") != null && d.money == 9999, "Locked purchase changed balance"); });
        Check("G01 bronze grants exactly 1200 CR and 1000 XP", () => { var d = New(); Finish(d, "g01", Medal.Bronze); Assert(d.money == 1200 && d.exp == 1000, "Wrong first reward"); });
        Check("Repeated result ID pays nothing twice", () =>
        {
            var d = New(); var r = Finish(d, "g01", Medal.Gold); int cr = d.money, xp = d.exp;
            FormulaProgression.Complete(d, r.runId, r.eventId, true, Medal.Gold, 100, 30);
            Assert(d.money == cr && d.exp == xp, "Duplicate payment");
        });
        Check("Expected upgrade level prevents double purchase", () =>
        {
            var d = New(); Finish(d, "g01", Medal.Bronze); Assert(FormulaProgression.BuyUpgrade(d, "engine", 1) == null, "First purchase failed");
            int cr = d.money; Assert(FormulaProgression.BuyUpgrade(d, "engine", 1) != null && d.money == cr, "Duplicate upgrade charge");
        });
        Check("Insufficient balance cannot buy", () => { var d = New(); Finish(d, "g01", Medal.Bronze); d.money = 0; Assert(FormulaProgression.BuyUpgrade(d, "brakes") != null && d.formula.Owned("brakes") == 0, "Purchase without funds"); });
        Check("Changing livery preserves owned and equipped levels", () =>
        {
            var d = New(); Finish(d, "g01", Medal.Bronze); FormulaProgression.BuyUpgrade(d, "engine"); FormulaProgression.BuyOrEquipLivery(d, "generic_blue");
            Assert(d.formula.Owned("engine") == 1 && d.formula.Equipped("engine") == 1, "Livery reset development");
        });
        Check("Owned lower level can be equipped without refund or cost", () => { var d = New(); Finish(d, "g01", Medal.Bronze); FormulaProgression.BuyUpgrade(d, "brakes"); int cr = d.money; FormulaProgression.EquipUpgrade(d, "brakes", 0); Assert(d.money == cr && d.formula.Owned("brakes") == 1 && d.formula.Equipped("brakes") == 0, "Unequip changed ownership"); });
        Check("Gold grants all lower bonuses and Pulse once", () => { var d = New(); Finish(d, "g01", Medal.Bronze); var r = Finish(d, "g02", Medal.Gold); Assert(r.credits == 2200 && r.xp == 1800 && d.formula.ownedLiveries.Count(x => x == "generic_pulse") == 1, "Gold grant mismatch"); });
        Check("Bronze then gold then gold pays only new medal bonuses", () =>
        {
            var d = New(); Finish(d, "g01", Medal.Bronze); Finish(d, "g02", Medal.Bronze);
            var a = Finish(d, "g03", Medal.Bronze); var b = Finish(d, "g03", Medal.Gold); var c = Finish(d, "g03", Medal.Gold);
            Assert(a.credits == 2000 && b.credits == 1600 && c.credits == 1000, "Medal bonuses replayed");
        });
        Check("Quick mode cannot grant career credits, XP or medals", () => { var d = New(); Finish(d, "g06", Medal.Gold, "quick"); Assert(d.money == 0 && d.exp == 0 && d.formula.Best("g06") == Medal.None, "Quick rewards leaked"); });
        Check("Invalid finish pays no rewards", () => { var d = New(); Finish(d, "g01", Medal.Gold, "career", false); Assert(d.money == 0 && d.exp == 0 && d.formula.Best("g01") == Medal.None, "Invalid finish rewarded"); });
        foreach (var tier in new[] { Medal.Bronze, Medal.Gold })
            Check("Full first career " + tier + " matches catalog balance", () =>
            {
                var d = New(); foreach (var e in FormulaCatalog.Current.careerEvents) Finish(d, e.id, tier);
                Assert(d.money == (tier == Medal.Gold ? 18400 : 14500) && d.exp == (tier == Medal.Gold ? 13200 : 9600), "Economy totals differ");
                Assert(d.formula.ownedLiveries.Contains("generic_black"), "Final livery missing");
                Assert(d.formula.ownedLiveries.Contains("generic_purple") == (tier == Medal.Gold), "Prestige livery gate incorrect");
            });
        Check("Purchased livery charges once and preserves progress", () =>
        {
            var d = New(); foreach (var e in FormulaCatalog.Current.careerEvents.Take(3)) Finish(d, e.id, Medal.Bronze);
            int cr = d.money; Assert(FormulaProgression.BuyOrEquipLivery(d, "generic_red") == null, "Purchase failed"); FormulaProgression.BuyOrEquipLivery(d, "generic_red");
            Assert(d.money == cr - 1500, "Repeated equip charged again");
        });
        Check("All development purchases match 27600 CR", () =>
        {
            var d = New(); foreach (var e in FormulaCatalog.Current.careerEvents) Finish(d, e.id, Medal.Bronze); d.money = 50000;
            foreach (var branch in FormulaCatalog.Current.upgrades) for (int i = 1; i <= 3; i++) Assert(FormulaProgression.BuyUpgrade(d, branch.id, i) == null, "Purchase failed");
            Assert(d.money == 22400, "Upgrade total mismatch");
        });
        Check("Setup changes are free", () => { var d = New(); Assert(FormulaProgression.EquipSetup(d, "speed") == null && d.money == 0 && d.exp == 0, "Setup charged"); });
        Check("Atomic file save, backup recovery and corrupt preservation", () =>
        {
            string directory = Path.Combine(Path.GetTempPath(), "FormulaSaveValidation-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "save.json");
            try
            {
                var d = New(); Finish(d, "g01", Medal.Bronze); FormulaSaveStorage.Write(path, d);
                d.money = 500; FormulaSaveStorage.Write(path, d);
                var loaded = (SaveManager.SaveData)FormulaSaveStorage.Read(path, typeof(SaveManager.SaveData)); Assert(loaded.money == 500, "Roundtrip failed");
                File.WriteAllText(path, "{broken"); loaded = (SaveManager.SaveData)FormulaSaveStorage.Read(path, typeof(SaveManager.SaveData)); Assert(loaded.money == 1200, "Backup not recovered");
                FormulaSaveStorage.Write(path, loaded);
                File.WriteAllText(path, "{broken"); loaded = (SaveManager.SaveData)FormulaSaveStorage.Read(path, typeof(SaveManager.SaveData)); Assert(loaded.money == 1200, "Writing recovered data destroyed the valid backup");
                File.WriteAllText(path + ".bak", "{broken"); bool rejected = false;
                try { FormulaSaveStorage.Read(path, typeof(SaveManager.SaveData)); } catch (InvalidDataException) { rejected = true; }
                Assert(rejected && File.ReadAllText(path) == "{broken", "Corrupt save overwritten");
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        });
        Directory.CreateDirectory("Docs/AI/FormulaCareer"); File.WriteAllText("Docs/AI/FormulaCareer/ProgressionValidation.json", JsonUtility.ToJson(report, true));
        Debug.Log("FORMULA_PROGRESSION_VALIDATION: " + report.passed.Count + " passed, " + report.failed.Count + " failed");
        if (report.failed.Count > 0) throw new InvalidOperationException(string.Join("\n", report.failed));
    }
}
