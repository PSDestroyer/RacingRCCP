using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace FormulaGame
{
    // One presentation of catalog requirements for cards, the inspector and confirmation.
    public sealed class FormulaLiveryOffer
    {
        public readonly bool Owned, Equipped, Purchase, Unlocked;
        public readonly int Price, Shortfall;
        public readonly string Status, Condition, Requirements, Funds, Cost, Action;
        public readonly FormulaCatalog.CareerEvent Event;
        private static string Credits(int value) => value.ToString("N0", CultureInfo.InvariantCulture) + FormulaLocalization.T(" CR");
        public FormulaLiveryOffer(FormulaCatalog.Livery livery, FormulaProgress progress, int balance)
        {
            Owned = progress.ownedLiveries.Contains(livery.id); Equipped = progress.equippedLivery == livery.id;
            Purchase = livery.acquisition == "credit_purchase"; Unlocked = FormulaProgression.Unlocked(livery.unlock, progress);
            Price = Purchase ? livery.priceCredits.Value : 0; Shortfall = Owned || !Purchase ? 0 : Math.Max(0, Price - balance);
            var rules = MedalRules(livery.unlock).ToArray();
            var missing = rules.Where(r => !FormulaProgression.Unlocked(r, progress)).ToArray();
            Event = FormulaCatalog.Current.Event((string)(missing.FirstOrDefault() ?? rules.FirstOrDefault())?["eventId"]);
            Requirements = string.Join("\n", rules.Select(r =>
            {
                bool met = FormulaProgression.Unlocked(r, progress);
                return "<color=" + (met ? "#FFFFFF" : "#FFE13B") + ">" + FormulaLocalization.T(met ? "MET" : "NEEDED") + " · " + FormulaCatalog.MedalName(FormulaCatalog.ParseMedal((string)r["minimumMedal"])) + FormulaLocalization.T(" in ") + FormulaLocalization.T(FormulaCatalog.Current.Event((string)r["eventId"]).name) + "</color>";
            }));
            if (Owned)
            {
                Status = Equipped ? FormulaLocalization.T("EQUIPPED") : FormulaLocalization.T("OWNED"); Condition = FormulaLocalization.T("Your upgrades and setup are retained.");
                Requirements = FormulaLocalization.T("OWNED\nSwitch between owned liveries for free."); Cost = FormulaLocalization.T("Free to equip"); Action = Equipped ? FormulaLocalization.T("Equipped") : FormulaLocalization.T("Equip · Free");
                return;
            }
            Status = !Unlocked ? FormulaLocalization.T("LOCKED") : Purchase && Shortfall > 0 ? FormulaLocalization.T("INSUFFICIENT CREDITS") : Purchase ? FormulaLocalization.T("AVAILABLE") : FormulaLocalization.T("REWARD READY");
            Condition = missing.Length == 0 ? Purchase ? FormulaLocalization.T("Medal requirement met. Cosmetic only.") : FormulaLocalization.T("All requirements met. Claim your reward.") : rules.Length > 1
                ? FormulaLocalization.T("Gold medals: ") + (rules.Length - missing.Length) + " / " + rules.Length + FormulaLocalization.T(" · Next: ") + FormulaLocalization.T(Event.name)
                : FormulaLocalization.T("Earn ") + FormulaCatalog.MedalName(FormulaCatalog.ParseMedal((string)missing[0]["minimumMedal"])) + FormulaLocalization.T(" in ") + FormulaLocalization.T(Event.name) + ".";
            Funds = Purchase ? FormulaLocalization.T("Balance: ") + Credits(balance) + FormulaLocalization.T(" · Price: ") + Credits(Price) : null;
            Cost = Shortfall > 0 ? FormulaLocalization.T("MISSING ") + Credits(Shortfall) : Purchase ? FormulaLocalization.T("Price · ") + Credits(Price) : FormulaLocalization.T("Career reward · Free");
            Action = !Unlocked ? FormulaLocalization.T("View requirements") : !Purchase ? FormulaLocalization.T("Claim reward · Free") : Shortfall > 0 ? FormulaLocalization.T("Earn credits") : FormulaLocalization.T("Buy and equip · ") + Credits(Price);
        }
        private static IEnumerable<JObject> MedalRules(JObject rule)
        {
            if (rule == null) yield break;
            if ((string)rule["type"] == "event_medal") yield return rule;
            if (rule["all"] is JArray all)
                foreach (var child in all.OfType<JObject>()) foreach (var medal in MedalRules(child)) yield return medal;
        }
    }
}
