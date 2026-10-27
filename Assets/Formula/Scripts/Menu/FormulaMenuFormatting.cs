using System.Linq;

namespace FormulaGame
{
    public static class FormulaMenuFormatting
    {
        public static string DriverName => string.IsNullOrWhiteSpace(FormulaProgression.Data.PlayerName) ? FormulaLocalization.T("Driver") : FormulaProgression.Data.PlayerName;
        // Internal navigation keys stay stable; only the visible labels are English.
        public static string PageName(string key)
        {
            switch (key)
            {
                case "Acasă": return FormulaLocalization.T("Home");
                case "Carieră": return FormulaLocalization.T("Career");
                case "Garaj": return FormulaLocalization.T("Garage");
                case "Curse rapide": return FormulaLocalization.T("Quick Race");
                case "Profil": return FormulaLocalization.T("Profile");
                case "Setări": return FormulaLocalization.T("Settings");
                case "Dezvoltare": return FormulaLocalization.T("Upgrade");
                case "Livree": return FormulaLocalization.T("Liveries");
                case "Setup": return FormulaLocalization.T("SETUP");
                default: return key;
            }
        }
        public static string EventGoal(FormulaCatalog.CareerEvent e)
        {
            if (e.mode == "race") return FormulaLocalization.T("Finish the race on the podium.");
            if (e.mode == "familiarization") return FormulaLocalization.T("Complete your first valid lap.");
            return FormulaLocalization.T("Target lap: ") + FormulaRaceUI.TimeText(FormulaAssets.Current.FindTrack(e.trackId).referenceLapSeconds * 1.2) + FormulaLocalization.T(" or faster.");
        }
        public static FormulaCatalog.Reward AvailableBronzeReward(FormulaCatalog.CareerEvent e)
        {
            FormulaProgression.State.claimedMedalMasks.TryGetValue(e.id, out int claimed);
            bool firstBronze = (claimed & (1 << (int)Medal.Bronze)) == 0;
            return new FormulaCatalog.Reward { credits = e.baseValidFinishReward.credits + (firstBronze ? e.oneTimeMedalRewards["bronze"].credits : 0), xp = e.baseValidFinishReward.xp + (firstBronze ? e.oneTimeMedalRewards["bronze"].xp : 0) };
        }
        public static string Experience(int totalXp, int level, out float fraction)
        {
            int step = FormulaCatalog.Current.profile.xpPerLevel;
            int within = System.Math.Max(0, totalXp - (level - 1) * step);
            fraction = UnityEngine.Mathf.Clamp01(within / (float)step);
            if (totalXp < (level - 1) * step) return (level * step - totalXp).ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + FormulaLocalization.T(" XP TO LEVEL ") + (level + 1);
            return within.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " / " + step.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + FormulaLocalization.T(" XP");
        }
        public static string NextReward()
        {
            var next = FormulaCatalog.Current.liveries.Where(l => l.acquisition == "achievement_grant" && !FormulaProgression.State.ownedLiveries.Contains(l.id) && l.id != "generic_purple").OrderBy(l => (string)l.unlock["eventId"]).FirstOrDefault();
            if (next != null) return FormulaLocalization.T(next.displayName) + " · " + FormulaProgression.LockReason(next.unlock, FormulaProgression.State);
            var prestige = FormulaCatalog.Current.Skin("generic_purple");
            return FormulaProgression.State.ownedLiveries.Contains(prestige.id) ? FormulaLocalization.T("ALL LIVERIES UNLOCKED") : FormulaLocalization.T(prestige.displayName) + " · " + FormulaProgression.LockReason(prestige.unlock, FormulaProgression.State);
        }
    }
}
