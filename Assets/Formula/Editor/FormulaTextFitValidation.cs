using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormulaGame;
using TMPro;
using UnityEngine;

// Measures rendered glyphs at the requested accessibility size, not auto-fit sizes.
public static class FormulaTextFitValidation
{
    private static readonly Dictionary<TMP_Text,float> Baselines = new Dictionary<TMP_Text,float>();
    public static void Remember(FormulaMenuView view)
    {
        Baselines.Clear(); foreach (var text in view.GetComponentsInChildren<TMP_Text>(true)) Baselines[text]=text.fontSize;
    }
    public static void Validate(FormulaMainMenu menu)
    {
        var failures = new List<string>(); int measured=0;
        void Measure(string page)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var text in menu.View.GetComponentsInChildren<TMP_Text>())
            {
                if (string.IsNullOrWhiteSpace(text.text) || !Baselines.TryGetValue(text,out float baseline) || baseline>32 || text.font==FormulaMenuAssets.Current.titleFont) continue;
                measured++; text.ForceMeshUpdate();
                if (text.enableAutoSizing || text.fontSize+ .01f < baseline*1.15f) failures.Add(page+" / "+text.name+" silently shrank below 115%.");
                var bounds=text.textBounds; var rect=text.rectTransform.rect;
                if (bounds.min.x < rect.xMin-2 || bounds.max.x > rect.xMax+2 || bounds.min.y < rect.yMin-2 || bounds.max.y > rect.yMax+2)
                    failures.Add(page+" / "+PathOf(text.transform,menu.View.transform)+" / "+text.text.Replace('\n',' ')+" / glyphs "+bounds+" / slot "+rect);
            }
        }
        foreach (string page in new[] { "Acasă", "Carieră", "Curse rapide", "Profil", "Setări" }) { menu.Show(page); Measure(page); }
        foreach (string section in new[] { "existing-settings", "settings-controls", "tab-DISPLAY", "tab-GRAPHICS", "tab-ACCESSIBILITY" })
        { Click(menu,section); Measure(section); }
        menu.Back();
        menu.Show("Garaj");
        for(int i=0;i<3;i++) { Click(menu,"garage-"+i); Measure("Garage "+i); }
        menu.Show("Carieră"); Click(menu,"g01"); Measure("Event briefing"); menu.Back();
        Directory.CreateDirectory("Docs/AI/FormulaUI");
        File.WriteAllText("Docs/AI/FormulaUI/Text115Validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(new { measured,scale=115,failures },Newtonsoft.Json.Formatting.Indented));
        if (failures.Count>0) throw new InvalidOperationException("115% text has "+failures.Count+" layout issues. See Text115Validation.json.");
    }
    private static void Click(FormulaMainMenu menu,string name) => menu.View.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.name==name).onClick.Invoke();
    private static string PathOf(Transform current,Transform root) => current==root ? current.name : PathOf(current.parent,root)+"/"+current.name;
}
