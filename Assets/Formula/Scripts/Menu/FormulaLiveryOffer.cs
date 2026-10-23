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
        private static string Credits(int value) => value.ToString("N0", CultureInfo.InvariantCulture) + " CR";
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
                return "<color=" + (met ? "#FFFFFF" : "#FFE13B") + ">" + (met ? "MET" : "NEEDED") + " · " + FormulaCatalog.MedalName(FormulaCatalog.ParseMedal((string)r["minimumMedal"])) + " in " + FormulaCatalog.Current.Event((string)r["eventId"]).name + "</color>";
            }));
            if (Owned)
            {
                Status = Equipped ? "EQUIPPED" : "OWNED"; Condition = "Your upgrades and setup are retained.";
                Requirements = "OWNED\nSwitch between owned liveries for free."; Cost = "Free to equip"; Action = Equipped ? "Equipped" : "Equip · Free";
                return;
            }
            Status = !Unlocked ? "LOCKED" : Purchase && Shortfall > 0 ? "INSUFFICIENT CREDITS" : Purchase ? "AVAILABLE" : "REWARD READY";
            Condition = missing.Length == 0 ? Purchase ? "Medal requirement met. Cosmetic only." : "All requirements met. Claim your reward." : rules.Length > 1
                ? "Gold medals: " + (rules.Length - missing.Length) + " / " + rules.Length + " · Next: " + Event.name
                : "Earn " + FormulaCatalog.MedalName(FormulaCatalog.ParseMedal((string)missing[0]["minimumMedal"])) + " in " + Event.name + ".";
            Funds = Purchase ? "Balance: " + Credits(balance) + " · Price: " + Credits(Price) : null;
            Cost = Shortfall > 0 ? "MISSING " + Credits(Shortfall) : Purchase ? "Price · " + Credits(Price) : "Career reward · Free";
            Action = !Unlocked ? "View requirements" : !Purchase ? "Claim reward · Free" : Shortfall > 0 ? "Earn credits" : "Buy and equip · " + Credits(Price);
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
