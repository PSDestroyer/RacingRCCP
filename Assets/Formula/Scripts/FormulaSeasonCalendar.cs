using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using UnityEngine;

namespace FormulaGame
{
    // Presentation calendar; event unlocks continue to use progression medals.
    public sealed class FormulaSeasonCalendar
    {
        public int year;
        public string verifiedOn;
        public string[] sources;
        public Weekend preSeasonTesting;
        public List<Weekend> rounds;
        private static FormulaSeasonCalendar cached;
        public static FormulaSeasonCalendar Current
        {
            get
            {
                if (cached == null)
                {
                    var asset = Resources.Load<TextAsset>("Formula/Season2027");
                    if (asset == null) throw new InvalidOperationException("Formula season calendar is missing.");
                    cached = JsonConvert.DeserializeObject<FormulaSeasonCalendar>(asset.text);
                }
                return cached;
            }
        }
        public Weekend Round(int number) => rounds.Find(r => r.round == number);
        public string Caption(FormulaCatalog.CareerEvent e)
        {
            var weekend = Round(e.calendarRound);
            if (weekend == null) return FormulaLocalization.T("SEASON ") + year;
            var start = DateTime.ParseExact(weekend.startDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var end = DateTime.ParseExact(weekend.endDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            string code = FormulaLocalization.Culture.TwoLetterISOLanguageName;
            if (code == "ja" || code == "zh" || code == "ko")
            {
                string month = code == "ko" ? "월" : "月", day = code == "ko" ? "일" : "日";
                return start.Month + month + start.Day + (start.Month == end.Month ? "–" : day + "–" + end.Month + month) + end.Day + day;
            }
            return (start.Month == end.Month ? start.ToString("dd", CultureInfo.InvariantCulture) : start.ToString("dd MMM", FormulaLocalization.Culture)).ToUpperInvariant()
                + "–" + end.ToString("dd MMM", FormulaLocalization.Culture).ToUpperInvariant();
        }
        public sealed class Weekend
        {
            public int round;
            public string grandPrix, startDate, endDate;
        }
    }
}
