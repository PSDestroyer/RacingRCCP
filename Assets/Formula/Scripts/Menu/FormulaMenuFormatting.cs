using System.Linq;

namespace FormulaGame
{
    public static class FormulaMenuFormatting
    {
        public static string DriverName => string.IsNullOrWhiteSpace(FormulaProgression.Data.PlayerName) ? "Driver" : FormulaProgression.Data.PlayerName;
        // Internal navigation keys stay stable; only the visible labels are English.
        public static string PageName(string key)
        {
            switch (key)
            {
                case "Acasă": return "Home";
                case "Carieră": return "Career";
                case "Garaj": return "Garage";
                case "Curse rapide": return "Quick Race";
                case "Profil": return "Profile";
                case "Setări": return "Settings";
                case "Dezvoltare": return "Upgrade";
                case "Livree": return "Liveries";
                default: return key;
            }
        }
        public static string EventGoal(FormulaCatalog.CareerEvent e)
        {
            if (e.mode == "race") return "Finish the race on the podium.";
            if (e.mode == "familiarization") return "Complete your first valid lap.";
            return "Target lap: " + FormulaRaceUI.TimeText(FormulaAssets.Current.FindTrack(e.trackId).referenceLapSeconds * 1.2) + " or faster.";
        }
        public static FormulaCatalog.Reward AvailableBronzeReward(FormulaCatalog.CareerEvent e)
        {
            FormulaProgression.State.claimedMedalMasks.TryGetValue(e.id, out int claimed);
            bool firstBronze = (claimed & (1 << (int)Medal.Bronze)) == 0;
            return new FormulaCatalog.Reward { credits = e.baseValidFinishReward.credits + (firstBronze ? e.oneTimeMedalRewards["bronze"].credits : 0), xp = e.baseValidFinishReward.xp + (firstBronze ? e.oneTimeMedalRewards["bronze"].xp : 0) };
        }
        public static string NextReward()
        {
            var next = FormulaCatalog.Current.liveries.Where(l => l.acquisition == "achievement_grant" && !FormulaProgression.State.ownedLiveries.Contains(l.id) && l.id != "generic_purple").OrderBy(l => (string)l.unlock["eventId"]).FirstOrDefault();
            return next != null ? next.displayName + " · " + FormulaProgression.LockReason(next.unlock, FormulaProgression.State) : "Nova Purple · Gold in every event";
        }
    }
}
