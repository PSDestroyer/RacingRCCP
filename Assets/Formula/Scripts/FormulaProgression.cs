using System;
using System.Collections.Generic;
using System.Linq;
using HalvaStudio.Save;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FormulaGame
{
    // Domain rules operate on a supplied save. SaveManager remains the storage owner.
    public static class FormulaProgression
    {
        public static event Action Changed;
        public static SaveManager.SaveData Data => SaveManager.Instance.saveData;
        public static FormulaProgress State => Data.formula ?? Ensure(Data);
        public static FormulaProgress Ensure(SaveManager.SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            bool migrating = data.formula == null;
            var p = data.formula ?? (data.formula = new FormulaProgress());
            p.ownedChassis = (p.ownedChassis ?? new List<string>()).Distinct().ToList();
            p.ownedLiveries = (p.ownedLiveries ?? new List<string>()).Distinct().ToList();
            p.ownedLevels ??= new Dictionary<string, int>(); p.equippedLevels ??= new Dictionary<string, int>();
            p.medals ??= new Dictionary<string, Medal>(); p.claimedMedalMasks ??= new Dictionary<string, int>();
            p.bestLapSeconds ??= new Dictionary<string, double>(); p.processedRuns ??= new List<string>();
            if (!p.ownedChassis.Contains("generic_2027")) p.ownedChassis.Add("generic_2027");
            foreach (var id in new[] { "generic_orange", "generic_blue" }) if (!p.ownedLiveries.Contains(id)) p.ownedLiveries.Add(id);
            if (!p.ownedLiveries.Contains(p.equippedLivery) || FormulaCatalog.Current.Skin(p.equippedLivery) == null) p.equippedLivery = "generic_orange";
            p.lastGarageTab = Math.Clamp(p.lastGarageTab, 0, 2);
            p.textScale = p.textScale >= 115 ? 115 : 100;
            p.safeAreaPercent = p.safeAreaPercent == 95 ? 95 : 100;
            var catalog = FormulaCatalog.Current;
            p.catalogVersion = catalog.catalogVersion;
            if (!catalog.freeSetups.Exists(s => s.id == p.setup)) p.setup = "balanced";
            foreach (var b in catalog.upgrades)
            {
                p.ownedLevels[b.id] = Math.Clamp(p.Owned(b.id), 0, b.maxLevel);
                p.equippedLevels[b.id] = Math.Clamp(p.Equipped(b.id), 0, p.Owned(b.id));
            }
            if (migrating)
            {
                p.legacyCarIndex = data.currentCar;
                p.legacyLevelFloor = Math.Max(1, data.currentLevel);
                p.highestRewardedLevel = Math.Max(p.legacyLevelFloor, 1 + Math.Max(0, data.exp) / catalog.profile.xpPerLevel);
            }
            data.carDetails ??= new Dictionary<string, SaveManager.SaveData.CarSpecs>();
            if (!data.carDetails.ContainsKey("Formula G27"))
                data.carDetails["Formula G27"] = new SaveManager.SaveData.CarSpecs(true, 480, 340, false, 0, 28, 1, 1400);
            data.currentCar = 0;
            data.currentLevel = Level(data, catalog);
            return p;
        }
        public static int Level(SaveManager.SaveData d, FormulaCatalog c) => Math.Max(d.formula?.legacyLevelFloor ?? 1, 1 + Math.Max(0, d.exp) / c.profile.xpPerLevel);
        public static bool Unlocked(JObject rule, FormulaProgress p)
        {
            if (rule == null) return true;
            if (rule["all"] is JArray all) return all.All(r => Unlocked((JObject)r, p));
            switch ((string)rule["type"])
            {
                case "profile_created": return true;
                case "event_medal": return p.Best((string)rule["eventId"]) >= FormulaCatalog.ParseMedal((string)rule["minimumMedal"]);
                case "owned_upgrade_level": return p.Owned((string)rule["branchId"]) >= (int)rule["minimumLevel"];
                default: return false;
            }
        }
        public static string LockReason(JObject rule, FormulaProgress p)
        {
            if (Unlocked(rule, p)) return "";
            if (rule["all"] is JArray all)
            {
                if (all.Count > 3 && all.All(r => (string)r["type"] == "event_medal" && (string)r["minimumMedal"] == "gold")) return "Gold in all " + all.Count + " required events";
                return string.Join(" · ", all.Cast<JObject>().Where(r => !Unlocked(r, p)).Select(r => LockReason(r, p)));
            }
            if ((string)rule["type"] == "event_medal")
                return $"{FormulaCatalog.MedalName(FormulaCatalog.ParseMedal((string)rule["minimumMedal"]))} · {FormulaCatalog.Current.Event((string)rule["eventId"])?.name}";
            return $"Previous level required";
        }
        public static string BuyUpgrade(SaveManager.SaveData d, string branchId, int expectedLevel = -1)
        {
            var p = Ensure(d); var b = FormulaCatalog.Current.Upgrade(branchId);
            if (b == null) return "Unknown upgrade.";
            int next = p.Owned(branchId) + 1;
            if (expectedLevel > 0 && expectedLevel != next) return "This purchase has already been processed.";
            if (next > b.maxLevel) return "Maximum level already owned.";
            var level = b.levels.Find(l => l.level == next);
            if (!Unlocked(level.unlock, p)) return LockReason(level.unlock, p);
            if (d.money < level.incrementalPriceCredits) return "Not enough credits.";
            d.money -= level.incrementalPriceCredits;
            p.ownedLevels[branchId] = next; p.equippedLevels[branchId] = next;
            return null;
        }
        public static string EquipUpgrade(SaveManager.SaveData d, string branch, int level)
        {
            var p = Ensure(d);
            if (FormulaCatalog.Current.Upgrade(branch) == null || level < 0 || level > p.Owned(branch)) return "Level not owned.";
            p.equippedLevels[branch] = level; return null;
        }
        public static string BuyOrEquipLivery(SaveManager.SaveData d, string id)
        {
            var p = Ensure(d); var livery = FormulaCatalog.Current.Skin(id);
            if (livery == null) return "Unknown livery.";
            if (!p.ownedLiveries.Contains(id))
            {
                if (!Unlocked(livery.unlock, p)) return LockReason(livery.unlock, p);
                if (livery.acquisition != "credit_purchase" && livery.acquisition != "achievement_grant") return "This livery is not available.";
                int price = livery.acquisition == "achievement_grant" ? 0 : livery.priceCredits ?? int.MaxValue;
                if (d.money < price) return "Not enough credits.";
                d.money -= price; p.ownedLiveries.Add(id);
            }
            p.equippedLivery = id; return null;
        }
        public static string EquipSetup(SaveManager.SaveData d, string id)
        {
            if (!FormulaCatalog.Current.freeSetups.Exists(s => s.id == id)) return "Unknown setup.";
            Ensure(d).setup = id; return null;
        }
        public static string BeginRun(SaveManager.SaveData d, string eventId, string mode)
        {
            var p = Ensure(d); var e = FormulaCatalog.Current.Event(eventId);
            if (e == null || (mode == "career" && (eventId.Contains(":") || !Unlocked(e.unlock, p)))) return "This event is locked.";
            if (mode != "career" && mode != "quick" && mode != "time_trial") return "Unknown mode.";
            p.activeEventId = eventId; p.activeMode = mode; p.activeRunId = Guid.NewGuid().ToString("N"); p.lastEventId = eventId;
            return null;
        }
        public static FormulaReceipt Complete(SaveManager.SaveData d, string runId, string eventId, bool valid, Medal medal, double elapsed, double bestLap)
        {
            var p = Ensure(d); var e = FormulaCatalog.Current.Event(eventId);
            if (e == null || string.IsNullOrEmpty(runId) || p.activeRunId != runId || p.activeEventId != eventId) return null;
            if (p.processedRuns.Contains(runId)) return p.lastReceipt?.runId == runId ? p.lastReceipt : null;
            var receipt = new FormulaReceipt { runId = runId, eventId = eventId, medal = valid ? medal : Medal.None, elapsedSeconds = elapsed, bestLapSeconds = bestLap };
            if (valid && p.activeMode == "career")
            {
                receipt.credits = e.baseValidFinishReward.credits; receipt.xp = e.baseValidFinishReward.xp;
                p.claimedMedalMasks.TryGetValue(eventId, out int mask);
                for (int tier = 1; tier <= (int)medal; tier++)
                {
                    int bit = 1 << tier;
                    if ((mask & bit) != 0) continue;
                    var bonus = e.oneTimeMedalRewards[((Medal)tier).ToString().ToLowerInvariant()];
                    receipt.credits += bonus.credits; receipt.xp += bonus.xp; mask |= bit;
                }
                p.claimedMedalMasks[eventId] = mask;
                if (medal > p.Best(eventId)) p.medals[eventId] = medal;
                if (bestLap > 0 && (!p.bestLapSeconds.TryGetValue(eventId, out double old) || bestLap < old))
                { p.bestLapSeconds[eventId] = bestLap; receipt.personalBest = true; }
                d.exp = checked(d.exp + receipt.xp); d.currentLevel = Level(d, FormulaCatalog.Current);
                receipt.levelCredits = Math.Max(0, d.currentLevel - p.highestRewardedLevel) * FormulaCatalog.Current.profile.creditsPerLevelUp;
                p.highestRewardedLevel = Math.Max(p.highestRewardedLevel, d.currentLevel);
                d.money = checked(d.money + receipt.credits + receipt.levelCredits);
                foreach (var l in FormulaCatalog.Current.liveries)
                    if (l.acquisition == "achievement_grant" && !p.ownedLiveries.Contains(l.id) && Unlocked(l.unlock, p))
                    { p.ownedLiveries.Add(l.id); receipt.newLiveries.Add(l.id); }
            }
            if (valid && p.activeMode == "time_trial" && bestLap > 0 && (!p.bestLapSeconds.TryGetValue(eventId, out double previousLap) || bestLap < previousLap))
            { p.bestLapSeconds[eventId] = bestLap; receipt.personalBest = true; }
            p.processedRuns.Add(runId);
            // Only the active run can commit. Older IDs can be bounded safely.
            if (p.processedRuns.Count > 128) p.processedRuns.RemoveAt(0);
            p.lastReceipt = receipt;
            return receipt;
        }
        public static bool Commit(Func<SaveManager.SaveData, string> mutation, out string error)
        {
            var manager = SaveManager.Instance; var previous = manager.saveData;
            var next = JsonConvert.DeserializeObject<SaveManager.SaveData>(JsonConvert.SerializeObject(previous));
            error = mutation(next);
            if (error != null) return false;
            manager.saveData = next;
            if (!manager.TrySave(true)) { manager.saveData = previous; error = "Save failed. Your changes were not applied."; return false; }
            Changed?.Invoke(); return true;
        }
    }
}
