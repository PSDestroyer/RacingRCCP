using System.Collections.Generic;

namespace FormulaGame
{
    public static class FormulaAchievementRules
    {
        public static IEnumerable<string> ForCompletedLap(bool valid)
        {
            if (valid)
                yield return FormulaAchievementCatalog.FirstValidLap;
        }

        public static IEnumerable<string> ForFinishedEvent(
            FormulaReceipt receipt,
            FormulaCatalog.CareerEvent formulaEvent,
            float referenceLapSeconds)
        {
            if (receipt == null || formulaEvent == null || receipt.bestLapSeconds <= 0)
                yield break;

            yield return FormulaAchievementCatalog.CheckeredFlag;

            if (formulaEvent.mode == "race" && receipt.finishPosition > 0 && receipt.finishPosition <= 3)
                yield return FormulaAchievementCatalog.Podium;

            if (formulaEvent.mode == "race" && receipt.finishPosition == 1)
                yield return FormulaAchievementCatalog.RaceWinner;

            if (receipt.medal == Medal.Gold)
                yield return FormulaAchievementCatalog.GoldStandard;

            if (receipt.personalBest)
                yield return FormulaAchievementCatalog.PersonalBest;

            if (formulaEvent.mode == "time_attack")
                yield return FormulaAchievementCatalog.TimeAttack;

            if (referenceLapSeconds > 0 && receipt.bestLapSeconds <= referenceLapSeconds)
                yield return FormulaAchievementCatalog.ReferencePace;
        }
    }
}
