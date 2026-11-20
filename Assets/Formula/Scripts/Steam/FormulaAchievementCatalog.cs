using System;
using System.Collections.Generic;

namespace FormulaGame
{
    public static class FormulaAchievementCatalog
    {
        public const string FirstValidLap = "ACH_FORMULA_FIRST_VALID_LAP";
        public const string CheckeredFlag = "ACH_FORMULA_CHECKERED_FLAG";
        public const string Podium = "ACH_FORMULA_PODIUM";
        public const string RaceWinner = "ACH_FORMULA_RACE_WINNER";
        public const string GoldStandard = "ACH_FORMULA_GOLD_STANDARD";
        public const string PersonalBest = "ACH_FORMULA_PERSONAL_BEST";
        public const string TimeAttack = "ACH_FORMULA_TIME_ATTACK";
        public const string ReferencePace = "ACH_FORMULA_REFERENCE_PACE";

        public sealed class Definition
        {
            public readonly string ApiName;
            public readonly string Name;
            public readonly string Description;

            public Definition(string apiName, string name, string description)
            {
                ApiName = apiName;
                Name = name;
                Description = description;
            }
        }

        public static readonly IReadOnlyList<Definition> All = Array.AsReadOnly(new[]
        {
            new Definition(FirstValidLap, "First Clean Lap", "Complete your first valid lap."),
            new Definition(CheckeredFlag, "Take the Flag", "Finish a Formula event with a valid result."),
            new Definition(Podium, "On the Podium", "Finish a Formula race in the top three."),
            new Definition(RaceWinner, "Race Winner", "Win a Formula race."),
            new Definition(GoldStandard, "Gold Standard", "Earn a gold medal in a Formula event."),
            new Definition(PersonalBest, "Finding Time", "Set a new personal-best lap time."),
            new Definition(TimeAttack, "Against the Clock", "Complete a valid time-attack event."),
            new Definition(ReferencePace, "Factory Pace", "Beat the circuit reference lap time."),
        });
    }
}
