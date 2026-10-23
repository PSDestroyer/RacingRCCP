using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FormulaGame
{
    public enum Medal { None, Bronze, Silver, Gold }

    public sealed class FormulaCatalog
    {
        public Vehicle vehicle;
        public ProfileRules profile;
        public List<Branch> upgrades;
        public List<Livery> liveries;
        public List<CareerEvent> careerEvents;
        public List<Setup> freeSetups;
        public string catalogVersion;
        private static FormulaCatalog cached;
        public static FormulaCatalog Current
        {
            get
            {
                if (cached == null)
                {
                    var asset = Resources.Load<TextAsset>("Formula/GenericCatalog");
                    if (asset == null) throw new InvalidOperationException("Formula catalog is missing.");
                    cached = JsonConvert.DeserializeObject<FormulaCatalog>(asset.text);
                }
                return cached;
            }
        }
        public static FormulaCatalog Parse(string json) => JsonConvert.DeserializeObject<FormulaCatalog>(json);
        public CareerEvent Event(string id) => careerEvents.Find(e => e.id == id);
        public Branch Upgrade(string id) => upgrades.Find(b => b.id == id);
        public Livery Skin(string id) => liveries.Find(l => l.id == id);
        public static string MedalName(Medal m) => m == Medal.Gold ? "Gold" : m == Medal.Silver ? "Silver" : m == Medal.Bronze ? "Bronze" : "No medal";
        public static Medal ParseMedal(string value) => Enum.TryParse(value, true, out Medal result) ? result : Medal.None;
        public sealed class Vehicle { public string id, displayName, defaultLiveryId, defaultSetupId; }
        public sealed class ProfileRules { public int initialCredits, initialXp, initialLevel, xpPerLevel, creditsPerLevelUp; }
        public sealed class Setup { public string id, name; }
        public sealed class Branch { public string id, name, intendedEffect; public int maxLevel; public List<UpgradeLevel> levels; }
        public sealed class UpgradeLevel { public int level, incrementalPriceCredits; public JObject unlock; }
        public sealed class Livery { public string id, displayName, prefabPath, acquisition; public int? priceCredits; public JObject unlock; }
        public sealed class Reward { public int credits, xp; }
        public sealed class CareerEvent
        {
            public string id, name, mode, trackId, vehicleRule, missionIcon;
            public int laps, opponentCount, calendarRound;
            public JObject unlock, medalTargets;
            public Reward baseValidFinishReward;
            public Dictionary<string, Reward> oneTimeMedalRewards;
        }
    }

    [Serializable]
    public sealed class FormulaProgress
    {
        public int schemaVersion = 1;
        public int lastGarageTab;
        public string catalogVersion = "generic_v1";
        public List<string> ownedChassis = new List<string>();
        public List<string> ownedLiveries = new List<string>();
        public string equippedLivery = "generic_orange", setup = "balanced";
        public bool abs = true, tcs = true, esp = true;
        public Dictionary<string, int> ownedLevels = new Dictionary<string, int>();
        public Dictionary<string, int> equippedLevels = new Dictionary<string, int>();
        public Dictionary<string, Medal> medals = new Dictionary<string, Medal>();
        public Dictionary<string, int> claimedMedalMasks = new Dictionary<string, int>();
        public Dictionary<string, double> bestLapSeconds = new Dictionary<string, double>();
        public List<string> processedRuns = new List<string>();
        public string activeRunId, activeEventId, activeMode, lastEventId;
        public int highestRewardedLevel = 1, legacyLevelFloor = 1, legacyCarIndex;
        public FormulaReceipt lastReceipt;
        public int Owned(string branch) => ownedLevels.TryGetValue(branch, out int n) ? n : 0;
        public int Equipped(string branch) => equippedLevels.TryGetValue(branch, out int n) ? n : 0;
        public Medal Best(string e) => medals.TryGetValue(e, out Medal m) ? m : Medal.None;
    }

    [Serializable]
    public sealed class FormulaReceipt
    {
        public string runId, eventId;
        public Medal medal;
        public int credits, xp, levelCredits;
        public double elapsedSeconds, bestLapSeconds;
        public List<string> newLiveries = new List<string>();
    }
}
