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
        private readonly Dictionary<string, CareerEvent> sessions = new Dictionary<string, CareerEvent>();
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
        public CareerEvent Event(string id)
        {
            var career = careerEvents.Find(e => e.id == id);
            if (career != null || string.IsNullOrEmpty(id)) return career;
            if (sessions.TryGetValue(id, out var session)) return session;
            var parts = id.Split(':');
            if (parts.Length != 2 || (parts[0] != "race" && parts[0] != "trial" && parts[0] != "practice")) return null;
            var track = FormulaAssets.Current?.FindTrack(parts[1]);
            if (track == null) return null;
            session = new CareerEvent
            {
                id = id, trackId = track.id,
                name = parts[0] == "race" ? "Quick Race" : parts[0] == "trial" ? "Time Attack" : "Practice",
                mode = parts[0] == "race" ? "race" : parts[0] == "practice" ? "practice" : "time_attack",
                laps = parts[0] == "practice" ? 1 : 3, opponentCount = parts[0] == "race" ? 3 : 0,
                vehicleRule = parts[0] == "trial" ? "stock_balanced" : "equipped",
                baseValidFinishReward = new Reward(), oneTimeMedalRewards = new Dictionary<string, Reward>
                { { "bronze", new Reward() }, { "silver", new Reward() }, { "gold", new Reward() } }
            };
            sessions.Add(id, session); return session;
        }
        public Branch Upgrade(string id) => upgrades.Find(b => b.id == id);
        public Livery Skin(string id) => liveries.Find(l => l.id == id);
        public static string MedalName(Medal m) => m == Medal.Gold ? FormulaLocalization.T("Gold") : m == Medal.Silver ? FormulaLocalization.T("Silver") : m == Medal.Bronze ? FormulaLocalization.T("Bronze") : FormulaLocalization.T("No medal");
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
            public int laps, opponentCount, calendarRound, opponentUpgradeLevel;
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
        public int textScale = 100, safeAreaPercent = 100;
        public bool reducedMotion, highContrast;
        public int qualityLevel = -1, vSync = -1, frameLimit = -1, displayMode = -1;
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
        public int finishPosition;
        public bool personalBest;
        public double elapsedSeconds, bestLapSeconds;
        public List<string> newLiveries = new List<string>();
    }
}
