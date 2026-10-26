using System;
using System.Linq;
using FormulaGame;
using TMPro;
using UnityEditor;
using UnityEngine;

// Extends the authored Upgrade visual language to the existing menu controls.
public static class FormulaMenuUnifiedBuilder
{
    private const string Art = "Assets/Formula/UI/FigmaCareer/";
    private static readonly Action NoAction = () => { };
    [MenuItem("Tools/Formula Career/Unify Garage UX and Upgrade Style")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var root = PrefabUtility.LoadPrefabContents(FormulaMenuHierarchyBuilder.PrefabPath);
        try
        {
            var view = root.GetComponent<FormulaMenuView>(); Style(view); BuildResults(view);
            PrefabUtility.SaveAsPrefabAsset(root, FormulaMenuHierarchyBuilder.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets(); Debug.Log("FORMULA_UNIFIED_GARAGE_UI_APPLIED");
    }
    [MenuItem("Tools/Formula Career/Refine Upgrade Dialog UX")]
    public static void RefineUpgradeDialog()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var root = PrefabUtility.LoadPrefabContents(FormulaMenuHierarchyBuilder.PrefabPath);
        try { StyleDialogs(root.GetComponent<FormulaMenuView>().Dialogs); PrefabUtility.SaveAsPrefabAsset(root, FormulaMenuHierarchyBuilder.PrefabPath); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets(); Debug.Log("FORMULA_UPGRADE_DIALOG_UX_APPLIED");
    }
    [MenuItem("Tools/Formula Career/Refine Livery Purchase UX")]
    public static void RefineLiveries()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var root = PrefabUtility.LoadPrefabContents(FormulaMenuHierarchyBuilder.PrefabPath);
        try { var view = root.GetComponent<FormulaMenuView>(); StyleLiveries(view.Garage); StyleDialogs(view.Dialogs); PrefabUtility.SaveAsPrefabAsset(root, FormulaMenuHierarchyBuilder.PrefabPath); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets(); Debug.Log("FORMULA_LIVERY_PURCHASE_UX_APPLIED");
    }
    public static void Style(FormulaMenuView view)
    {
        SetBool(view, "upgradeTheme", true);
        StyleShell(view); StyleHome(view.Home); StyleGarage(view.Garage);
        StyleCareer(view.Career); StyleQuick(view.Quick); StyleProfile(view.Profile); StyleSettings(view.Settings); StyleDialogs(view.Dialogs);
        FormulaEnglishBuilder.TranslateHierarchy(view.gameObject);
        FormulaExperienceBuilder.Style(view);
    }
    private static void StyleShell(FormulaMenuView view)
    {
        var upgrade = view.Garage.Upgrade.transform; var header = view.transform.Find("Header");
        Place(header, 0, 0, 1280, 100); header.GetComponent<UnityEngine.UI.Image>().color = new Color(0,0,0,.96f);
        var logo = Copy(upgrade.Find("Upgrade Logo"), header, "Formula Logo"); Place(logo,23,0,429,147);
        var account = Copy(upgrade.Find("Upgrade Driver and Wallet"),header,"Formula Driver and Wallet"); Place(account,953,21,301,46);
        var shade = Copy(upgrade.Find("Header Shade"),header,"Formula Header Shade"); Place(shade,0,0,1280,133); shade.SetAsFirstSibling();
        foreach (Transform child in header.Cast<Transform>().ToArray())
            if (child != logo && child != account && child != shade) UnityEngine.Object.DestroyImmediate(child.gameObject);
        Set(view,"wallet",account.Find("Credits").GetComponent<TMP_Text>()); Set(view,"driver",account.Find("Player Name").GetComponent<TMP_Text>()); Set(view,"xpFill",null);
        var frame = view.transform.Find("Cinematic frame"); if (frame != null) frame.gameObject.SetActive(false);
        var lower = Copy(upgrade.Find("Lower Shade"),view.transform,"Shell Lower Shade"); Place(lower,0,500,1280,220); lower.SetAsFirstSibling();lower.SetSiblingIndex(view.transform.Find("Screens").GetSiblingIndex()-1);
        var footer = view.transform.Find("Footer"); Place(footer,0,624,1280,96); ClearImage(footer);
        var footerShade = Copy(upgrade.Find("Footer Shade"),footer,"Formula Footer Shade");Place(footerShade,0,-12,1280,108);footerShade.SetAsFirstSibling();
        var title = footer.Find("Page Title").GetComponent<TMP_Text>(); Format(title,79,22,570,62,60,true);
        Place(footer.Find("Title Rule"),610,52,230,1);
        var hints=footer.Find("Input Hints").GetComponent<TMP_Text>();Format(hints,870,18,235,64,18);hints.textWrappingMode=TextWrappingModes.Normal;
        var back=footer.Find("nav-0").GetComponent<UnityEngine.UI.Button>();Place(back.transform,1125,36,105,32);
        var label=back.GetComponentInChildren<TMP_Text>(true);Format(label,33,0,72,32,24);label.text="Back";
        Raw(back.transform,"Back Prompt",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"BackPrompt.png"),0,0,30,30);
        FormulaMenuTheme.StyleButton(back);Set(view,"back",back);
        Raw(footer,"Glow Left",FormulaMenuAssets.Current.footerGlow,72,0,350,22);Raw(footer,"Glow Right",FormulaMenuAssets.Current.footerGlow,661,4,218,14);
    }
    public static void StyleHome(FormulaHomeView view)
    {
        FormulaHomeReferenceBuilder.StyleHome(view);
    }
    public static void StyleGarage(FormulaGarageView garage)
    {
        var menu=garage.GetComponentInParent<FormulaMenuView>();FullScreen(garage.transform);
        var context=Label(garage.transform,"Event Context","",462,20,465,60,17);context.textWrappingMode=TextWrappingModes.Normal;Set(garage,"contextLabel",context);
        var tabs=garage.transform.Find("Tabs");Place(tabs,0,100,1280,40);
        var band=Raw(tabs,"Tab Surface",LoadUpgrade("CategorySurface"),-20,-16,1320,78);band.transform.SetAsFirstSibling();
        var markers=new UnityEngine.Object[3];string[] tabNames={"UPGRADE","SETUP","LIVERIES"};
        for(int i=0;i<3;i++)
        {
            var button=tabs.Find("garage-"+i).GetComponent<UnityEngine.UI.Button>();Place(button.transform,30+i*240,0,230,40);
            var text=button.GetComponentInChildren<TMP_Text>(true);text.text=tabNames[i];Format(text,18,0,194,40,26);FormulaMenuTheme.StyleButton(button);
            var marker=button.transform.Find("ActivePage");if(marker==null)marker=FormulaUI.Box(button.transform,"ActivePage",18,38,194,2,FormulaUI.Red);
            Place(marker,18,38,194,2);marker.GetComponent<UnityEngine.UI.Image>().color=FormulaUI.Red;markers[i]=marker.gameObject;
        }
        SetArray(garage,"tabMarkers",markers);Set(garage,"back",menu.Back);
        var tabHints=Label(tabs,"Tab Hints","Q/E · Tabs",1005,0,240,40,18);Set(garage,"tabHints",tabHints);
        var upgrade=garage.Upgrade;Place(upgrade.transform,0,0,1280,720);
        var veil=Group(upgrade.transform,"World Shade",0,0,1280,624);var veilImage=veil.GetComponent<UnityEngine.UI.Image>()??veil.gameObject.AddComponent<UnityEngine.UI.Image>();veilImage.color=new Color(0,0,0,.2f);veilImage.raycastTarget=false;veil.SetAsFirstSibling();
        var top=Group(upgrade.transform,"Header Backplate",0,0,1280,100);var topImage=top.GetComponent<UnityEngine.UI.Image>()??top.gameObject.AddComponent<UnityEngine.UI.Image>();topImage.color=new Color(0,0,0,.96f);topImage.raycastTarget=false;top.SetSiblingIndex(1);
        var categories=upgrade.transform.Find("Component Categories");Place(categories,30,147,920,42);
        var catSurface=categories.Find("Category Surface");if(catSurface!=null)catSurface.gameObject.SetActive(false);
        var categoryButtons=categories.GetComponentsInChildren<UnityEngine.UI.Button>(true);float[] categoryX={0,162,404,544};
        for(int categoryIndex=0;categoryIndex<categoryButtons.Length;categoryIndex++)
        {
            var button=categoryButtons[categoryIndex];
            var rect=(RectTransform)button.transform;Place(rect,categoryX[categoryIndex],0,rect.sizeDelta.x,40);
            var text=button.transform.Find("Name").GetComponent<TMP_Text>();Format(text,8,0,rect.sizeDelta.x-16,38,27);
            var glow=button.transform.Find("Active Category");if(glow!=null)Place(glow,(rect.sizeDelta.x-125)*.5f,35,125,8);
        }
        Place(upgrade.transform.Find("Component Levels"),30,201,237,357);
        Place(upgrade.transform.Find("Level Details"),30,561,470,60);
        SetArray(upgrade,"standardChrome",new UnityEngine.Object[]{menu.transform.Find("Header").gameObject,menu.transform.Find("Footer").gameObject,menu.transform.Find("Screen Shade").gameObject});
        var upTitle=upgrade.transform.Find("Upgrade Footer/Page Title").GetComponent<TMP_Text>();Format(upTitle,79,22,220,62,60,true);upTitle.alignment=TextAlignmentOptions.MidlineLeft;
        var setup=garage.transform.Find("Setup");Place(setup,0,0,1280,720);var cards=setup.Find("Setup Cards");Place(cards,30,170,340,385);
        var data=new SerializedObject(garage);var rows=data.FindProperty("setups");
        for(int i=0;i<rows.arraySize;i++)
        {
            var entry=rows.GetArrayElementAtIndex(i);var button=(UnityEngine.UI.Button)entry.FindPropertyRelative("button").objectReferenceValue;Place(button.transform,0,i*122,340,110);
            var setupId=entry.FindPropertyRelative("id").stringValue;var text=(TMP_Text)entry.FindPropertyRelative("label").objectReferenceValue;Format(text,20,6,300,40,37,true);text.text=FormulaCatalog.Current.freeSetups.Find(preset=>preset.id==setupId).name.ToUpperInvariant();
            var desc=Label(button.transform,"Setup Effect",setupId=="balanced"?"Balance between corners and straights.":setupId=="speed"?"Downforce −8% · Drag −4%":"Downforce +8% · Drag +4%",20,49,300,30,18);desc.textWrappingMode=TextWrappingModes.Normal;
            var status=Label(button.transform,"Setup Status",setupId=="balanced"?"APPLIED":"FREE · APPLY",20,83,300,22,16);
            entry.FindPropertyRelative("description").objectReferenceValue=desc;entry.FindPropertyRelative("status").objectReferenceValue=status;
            var marker=(GameObject)entry.FindPropertyRelative("equippedMarker").objectReferenceValue;Place(marker.transform,20,108,300,2);marker.GetComponent<UnityEngine.UI.Image>().color=FormulaUI.Accent;
            FormulaMenuTheme.StyleButton(button);
            var option=button.GetComponent<FormulaSetupOption>()??button.gameObject.AddComponent<FormulaSetupOption>();Set(option,"owner",garage);SetString(option,"setupId",entry.FindPropertyRelative("id").stringValue);
        }
        data.ApplyModifiedPropertiesWithoutUndo();Label(setup,"Setup Policy","Preview the balance. Apply any setup for free.",30,562,470,44,18).textWrappingMode=TextWrappingModes.Normal;
        StyleStatistics(setup.Find("RCCP Statistics").GetComponent<FormulaStatisticsView>());SetBool(setup.Find("RCCP Statistics").GetComponent<FormulaStatisticsView>(),"setupComparison",true);
        StyleLiveries(garage);
        tabs.SetAsLastSibling();context.transform.SetAsLastSibling();
    }
    private static void StyleLiveries(FormulaGarageView garage)
    {
        var liveries=garage.transform.Find("Livree");Place(liveries,0,0,1280,720);var details=liveries.Find("Livery Details");Place(details,30,150,430,322);Panel(details);details.GetComponent<UnityEngine.UI.Image>().color=new Color32(8,9,11,255);
        Format(details.Find("Category").GetComponent<TMP_Text>(),20,9,390,24,16);
        Format(details.Find("Livery Name").GetComponent<TMP_Text>(),20,36,390,62,42,true);
        Format(details.Find("Ownership").GetComponent<TMP_Text>(),20,100,390,29,20);
        Set(garage,"liveryFunds",Label(details,"Funds","Cosmetic only · No performance changes",20,134,390,26,18));
        Set(garage,"liveryCost",Label(details,"Cost","Free to equip",20,162,390,36,24,true));
        var condition=details.Find("Unlock Condition").GetComponent<TMP_Text>();Format(condition,20,200,390,54,18);condition.textWrappingMode=TextWrappingModes.Normal;condition.alignment=TextAlignmentOptions.TopLeft;
        var apply=details.Find("livery-apply").GetComponent<UnityEngine.UI.Button>();Place(apply.transform,20,260,390,46);Format(apply.GetComponentInChildren<TMP_Text>(true),16,0,358,46,23);FormulaMenuTheme.StyleButton(apply);
        var carousel=liveries.Find("Livery Cards");Place(carousel,30,487,1220,128);
        foreach(var card in carousel.GetComponentsInChildren<FormulaLiveryCard>(true))
        {
            int i=card.transform.GetSiblingIndex();Place(card.transform,i*153,0,145,128);FormulaMenuTheme.StyleButton(card.GetComponent<UnityEngine.UI.Button>());
            Format(card.transform.Find("Livery Name").GetComponent<TMP_Text>(),9,3,127,30,23,true);
            Place(card.transform.Find("Generic Livery Thumbnail"),5,35,135,68.6f);Format(card.transform.Find("Ownership").GetComponent<TMP_Text>(),9,103,127,24,16);
        }
    }
    private static void StyleStatistics(FormulaStatisticsView view)
    {
        var root=view.transform;Place(root,535,433,523,180);ClearImage(root);
        var art=Raw(root,"Statistics Surface",LoadUpgrade("StatsSurface"),0,0,523,180);art.transform.SetAsFirstSibling();root.Find("Heading").gameObject.SetActive(false);
        var data=new SerializedObject(view);data.FindProperty("figmaLevels").boolValue=true;var rows=data.FindProperty("development");int[] order={0,3,1,2};string[] fields={"torque","brakes","spring","aero"};
        for(int i=0;i<4;i++)
        {
            float y=8+order[i]*40;var row=rows.GetArrayElementAtIndex(i);Format((TMP_Text)row.FindPropertyRelative("label").objectReferenceValue,20,y,142,30,18);
            var value=(TMP_Text)data.FindProperty(fields[i]).objectReferenceValue;Format(value,342,y,161,30,21);value.alignment=TextAlignmentOptions.MidlineRight;
            string id=row.FindPropertyRelative("branchId").stringValue;var bars=root.Find("Stats-"+id);Place(bars,170,y+13,166,6);var segments=row.FindPropertyRelative("segments");segments.arraySize=9;
            for(int j=0;j<9;j++)
            {
                var tick=j<bars.childCount?bars.GetChild(j):FormulaUI.Box(bars,"Level Tick "+j,0,0,14,6,Color.white);Place(tick,j*19,0,14,6);
                var image=tick.GetComponent<UnityEngine.UI.Image>();image.sprite=FormulaMenuAssets.Current.panelSprite;image.type=UnityEngine.UI.Image.Type.Sliced;segments.GetArrayElementAtIndex(j).objectReferenceValue=image;
            }
        }
        data.ApplyModifiedPropertiesWithoutUndo();Format(root.Find("Drag and Mass").GetComponent<TMP_Text>(),20,160,483,20,16);
    }
    public static void StyleCareer(FormulaCareerView view)
    {
        var footer=view.transform.Find("Figma Career Footer");
        footer.Find("Page Heading").GetComponent<TMP_Text>().text="CAREER";
        Format(footer.Find("Page Heading").GetComponent<TMP_Text>(),79,22,300,62,60,true);
        Place(footer.Find("Title Rule"),390,52,650,1);Place(footer.Find("career-back"),1125,36,105,32);
        foreach(var card in view.GetComponentsInChildren<FormulaEventCard>(true))
        {
            Hide(card.transform,"Figma Card Surface");Hide(card.transform,"Figma Selected Surface");FormulaMenuTheme.StyleButton(card.GetComponent<UnityEngine.UI.Button>());
            card.GetComponent<FormulaMenuFocus>().StyleFigma(card.transform.Find("Upgrade Focus").GetComponent<FormulaPanelGraphic>(),card.transform.Find("Figma To Race").GetComponent<UnityEngine.UI.RawImage>(),card.transform.Find("Upgrade Normal").GetComponent<FormulaPanelGraphic>());
        }
        FormulaCareerExpansionBuilder.StyleCareer(view);
    }
    private static void StyleQuick(FormulaQuickView view)
    {
        FullScreen(view.transform);var list=view.transform.Find("Event Cards");Place(list,0,0,1280,600);int i=0;
        foreach(var card in list.GetComponentsInChildren<FormulaEventCard>(true))
        {
            Place(card.transform,23+i%3*414,i<3?124:374,398,216);FormulaMenuTheme.StyleButton(card.GetComponent<UnityEngine.UI.Button>());
            Format(card.transform.Find("Mode").GetComponent<TMP_Text>(),20,16,358,51,40,true);Format(card.transform.Find("Circuit").GetComponent<TMP_Text>(),20,74,358,31,21);
            Format(card.transform.Find("Reward Rule").GetComponent<TMP_Text>(),20,176,358,25,16);Place(card.transform.Find("Circuit Outline"),176,91,198,87);i++;
        }
        var policy=view.transform.Find("Career Policy");if(policy!=null){Place(policy,30,599,1220,25);Format(policy.GetComponent<TMP_Text>(),30,599,1220,25,16);policy.GetComponent<TMP_Text>().text="QUICK SESSIONS / NO CAREER REWARDS";}
    }
    private static void StyleProfile(FormulaProfileView view)
    {
        FullScreen(view.transform);var pilot=view.transform.Find("Pilot Summary");Place(pilot,30,150,360,438);Panel(pilot);
        Format(pilot.Find("Pilot Name").GetComponent<TMP_Text>(),24,24,312,68,52,true);var progress=pilot.Find("Progress").GetComponent<TMP_Text>();Format(progress,24,120,312,275,27);progress.textWrappingMode=TextWrappingModes.Normal;progress.lineSpacing=12;
        var medals=view.transform.Find("Career Medals");Place(medals,420,150,830,438);int i=0;
        foreach(Transform row in medals)
        {
            Place(row,0,i++*73,830,63);Panel(row);Format(row.Find("Event").GetComponent<TMP_Text>(),20,8,585,47,26);Format(row.Find("Medal").GetComponent<TMP_Text>(),620,8,190,47,24);
        }
        FormulaCareerExpansionBuilder.StyleProfile(view);
    }
    private static void StyleSettings(FormulaSettingsView view)
    {
        FullScreen(view.transform);var overview=view.transform.Find("Assists");Place(overview,0,0,1280,624);var heading=overview.Find("Assist Information");Place(heading,30,115,1220,60);ClearImage(heading);
        Format(heading.Find("Information").GetComponent<TMP_Text>(),0,0,590,50,36,true);Format(heading.Find("Assistance Policy").GetComponent<TMP_Text>(),620,11,580,38,20);
        string[] ids={"abs","tcs","esp"};for(int i=0;i<3;i++)
        {
            var b=overview.Find("assist-"+ids[i]).GetComponent<UnityEngine.UI.Button>();Place(b.transform,30,194+i*122,540,106);Format(b.GetComponentInChildren<TMP_Text>(true),20,7,500,38,29);Format(b.transform.Find("Description").GetComponent<TMP_Text>(),20,51,500,34,20);FormulaMenuTheme.StyleButton(b);
        }
        var audio=overview.Find("existing-settings").GetComponent<UnityEngine.UI.Button>();Place(audio.transform,600,194,650,350);FormulaMenuTheme.StyleButton(audio);
        Format(audio.GetComponentInChildren<TMP_Text>(true),24,83,602,65,48,true);var desc=audio.transform.Find("Description").GetComponent<TMP_Text>();Format(desc,24,159,602,90,25);desc.textWrappingMode=TextWrappingModes.Normal;Format(audio.transform.Find("Action").GetComponent<TMP_Text>(),24,284,602,35,21);
        var details=view.transform.Find("Audio and Controls");Place(details,0,0,1280,624);var tabs=details.Find("Tabs");Place(tabs,30,115,620,42);
        var buttons=tabs.GetComponentsInChildren<UnityEngine.UI.Button>(true);for(int i=0;i<buttons.Length;i++){Place(buttons[i].transform,i*270,0,260,42);Format(buttons[i].GetComponentInChildren<TMP_Text>(true),18,0,224,42,25);FormulaMenuTheme.StyleButton(buttons[i]);
            var marker=buttons[i].transform.Find("Active Section")as RectTransform??FormulaUI.Box(buttons[i].transform,"Active Section",18,40,224,2,FormulaUI.Red);Place(marker,18,40,224,2);marker.gameObject.SetActive(i==0);Set(view,i==0?"audioMarker":"controlsMarker",marker.gameObject);}
        var audioPage=details.Find("Audio");Place(audioPage,0,0,1280,624);int index=0;
        foreach(Transform row in audioPage)
        {
            if(row.name.StartsWith("Volume ")){Place(row,30,190+index++*94,1220,78);Panel(row);}
            if(row.name=="settings-vibration"){Place(row,30,490,1220,66);FormulaMenuTheme.StyleButton(row.GetComponent<UnityEngine.UI.Button>());}
        }
        var controls=details.Find("Controls");Place(controls,0,0,1280,624);var bindings=controls.Find("Bindings");Place(bindings,0,0,1280,624);
        string[] devices={"controls-keyboard","controls-gamepad","controls-wheel"};for(int i=0;i<3;i++){var b=bindings.Find(devices[i]).GetComponent<UnityEngine.UI.Button>();Place(b.transform,30+i*257,173,245,42);Format(b.GetComponentInChildren<TMP_Text>(true),16,0,213,42,23);FormulaMenuTheme.StyleButton(b);}
        Place(bindings.Find("Binding Page"),820,173,430,42);
        for(int i=0;i<6;i++){var b=bindings.Find("rebind-"+i).GetComponent<UnityEngine.UI.Button>();Place(b.transform,30+i%2*620,231+i/2*94,600,82);Format(b.transform.Find("Action").GetComponent<TMP_Text>(),18,8,564,29,24);Format(b.transform.Find("Binding").GetComponent<TMP_Text>(),18,44,564,27,21);FormulaMenuTheme.StyleButton(b);}
        string[] paging={"controls-previous","controls-next","controls-reset"};for(int i=0;i<3;i++){var b=bindings.Find(paging[i]).GetComponent<UnityEngine.UI.Button>();Place(b.transform,i==2?650:30+i*257,523,i==2?600:245,54);FormulaMenuTheme.StyleButton(b);}
        var waiting=controls.Find("Waiting for Input");Place(waiting,30,173,1220,420);Panel(waiting);
        foreach(var text in view.GetComponentsInChildren<TMP_Text>(true))if(text.font!=FormulaMenuAssets.Current.titleFont)text.font=FormulaMenuAssets.Current.bodyFont;
    }
    private static void StyleDialogs(FormulaMenuDialogs view)
    {
        view.GetComponent<UnityEngine.UI.Image>().color=new Color(0,0,0,.92f);
        foreach(string name in new[]{"Confirmation","Upgrade Level Selection","Race Briefing"})
        {
            var panel=view.transform.Find(name);Panel(panel);
            foreach(var button in panel.GetComponentsInChildren<UnityEngine.UI.Button>(true))FormulaMenuTheme.StyleButton(button);
        }
        var briefing=view.transform.Find("Race Briefing");var circuit=briefing.Find("Circuit");Panel(circuit);
        foreach(Transform child in briefing)if(child.name.StartsWith("Medal "))Panel(child);
        var back=briefing.Find("cancel");Place(back,32,486,176,50);Format(back.GetComponentInChildren<TMP_Text>(true),18,0,140,50,24);
        var garage=Button(briefing,"briefing-garage","GARAGE",224,486,230,50);Set(view,"eventGarage",garage);
        var accept=briefing.Find("confirm");garage.transform.SetAsLastSibling();accept.SetAsLastSibling();Place(accept,470,486,558,50);Format(accept.GetComponentInChildren<TMP_Text>(true),20,0,518,50,26);
        FormulaMenuTheme.StyleButton(back.GetComponent<UnityEngine.UI.Button>());FormulaMenuTheme.StyleButton(accept.GetComponent<UnityEngine.UI.Button>());
        var confirmation=view.transform.Find("Confirmation");Place(confirmation,230,116,820,488);Panel(confirmation);
        confirmation.GetComponent<UnityEngine.UI.Image>().color=new Color32(8,9,11,255);
        Place(confirmation.Find("Accent"),32,0,100,3);
        var fields=new SerializedObject(view);
        Format((TMP_Text)fields.FindProperty("title").objectReferenceValue,32,18,756,64,42,true);
        var generic=(TMP_Text)fields.FindProperty("details").objectReferenceValue;Format(generic,32,94,756,290,24);generic.textWrappingMode=TextWrappingModes.Normal;
        var content=Group(confirmation,"Upgrade Details",32,94,756,320);Set(view,"upgradeContent",content.gameObject);
        Set(view,"upgradeEffect",Label(content,"Effect","Downforce  +0% → +4%",0,0,756,46,30,true));
        Label(content,"Comparison Note","CHANGES VS STOCK",520,8,236,30,17);
        Set(view,"upgradeTradeoff",Label(content,"Tradeoff","Drag  +0% → +1%",0,44,756,34,23));
        var benefit=Label(content,"Benefit","More grip in fast corners, with increased air resistance.",0,88,756,60,22);benefit.textWrappingMode=TextWrappingModes.Normal;benefit.alignment=TextAlignmentOptions.TopLeft;Set(view,"upgradeBenefit",benefit);
        var requirement=Label(content,"Requirement","LOCKED\nEarn Bronze in Shakedown to unlock this upgrade.",0,166,756,68,22);requirement.textWrappingMode=TextWrappingModes.Normal;requirement.alignment=TextAlignmentOptions.TopLeft;requirement.color=FormulaUI.Accent;Set(view,"upgradeRequirement",requirement);
        Set(view,"upgradeFunds",Label(content,"Funds","Balance: 0 CR · Upgrade cost: 1,100 CR",0,236,756,30,20));
        Set(view,"upgradeCost",Label(content,"Cost","Upgrade cost · 1,100 CR",252,270,504,44,30,true));content.gameObject.SetActive(false);
        var livery=Group(confirmation,"Livery Details",32,94,756,320);Set(view,"liveryContent",livery.gameObject);
        Set(view,"liveryAcquisition",Label(livery,"Acquisition","CREDIT PURCHASE",0,0,756,46,30,true));
        Label(livery,"Cosmetic Policy","Cosmetic only · Your upgrades and setup are retained.",0,48,756,30,20);
        var requirements=Label(livery,"Requirements","NEEDED · Bronze in Race Debut",0,80,756,180,22);requirements.textWrappingMode=TextWrappingModes.Normal;requirements.alignment=TextAlignmentOptions.TopLeft;Set(view,"liveryRequirements",requirements);
        Set(view,"liveryFunds",Label(livery,"Funds","Balance: 0 CR · Price: 1,500 CR",0,236,756,30,20));
        Set(view,"liveryCost",Label(livery,"Cost","MISSING 1,500 CR",252,270,504,44,30,true));livery.gameObject.SetActive(false);
        var cancel=(UnityEngine.UI.Button)fields.FindProperty("cancel").objectReferenceValue;Place(cancel.transform,32,414,220,54);Format(cancel.GetComponentInChildren<TMP_Text>(true),18,0,184,54,26);
        var confirm=(UnityEngine.UI.Button)fields.FindProperty("accept").objectReferenceValue;Place(confirm.transform,268,414,520,54);Format(confirm.GetComponentInChildren<TMP_Text>(true),18,0,484,54,26);
        cancel.transform.SetAsLastSibling();confirm.transform.SetAsLastSibling();FormulaMenuTheme.StyleButton(cancel);FormulaMenuTheme.StyleButton(confirm);
    }
    private static void BuildResults(FormulaMenuView menu)
    {
        const string path="Assets/Formula/Resources/Formula/UI/FormulaResults.prefab";
        var root=new GameObject("Formula Results",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(FormulaResultsView));Place(root.transform,0,0,1280,720);root.GetComponent<UnityEngine.UI.Image>().color=new Color32(8,9,11,255);
        try
        {
            var view=root.GetComponent<FormulaResultsView>();var header=Copy(menu.transform.Find("Header"),root.transform,"Header");var footer=Copy(menu.transform.Find("Footer"),root.transform,"Footer");
            var account=header.Find("Formula Driver and Wallet");Set(view,"driver",account.Find("Player Name").GetComponent<TMP_Text>());Set(view,"wallet",account.Find("Credits").GetComponent<TMP_Text>());
            footer.Find("nav-0").gameObject.SetActive(false);footer.Find("Input Hints").gameObject.SetActive(false);footer.Find("Page Title").GetComponent<TMP_Text>().text="RESULTS";
            Label(root.transform,"Result Caption","SESSION COMPLETE",30,118,1220,25,18);
            Set(view,"eventName",Label(root.transform,"Event","SHAKEDOWN",30,151,820,66,58,true));Set(view,"medal",Label(root.transform,"Medal","BRONZE",930,151,320,66,52,true));
            string[] captions={"SESSION TIME","BEST LAP","CREDITS","EXPERIENCE"};string[] fields={"elapsed","bestLap","credits","xp"};
            for(int i=0;i<4;i++){var panel=Group(root.transform,"Result "+fields[i],30+i*310,244,290,128);Panel(panel);Label(panel,"Caption",captions[i],20,16,250,30,19);Set(view,fields[i],Label(panel,"Value","—",20,57,250,58,48,true));}
            Set(view,"bonus",Label(root.transform,"Level Bonus","Level bonus: +0 CR",30,401,580,38,24));Set(view,"unlocks",Label(root.transform,"Unlocks","Progress saved",650,401,600,85,24));
            var retry=Button(root.transform,"retry","Retry event",30,532,390,64);var next=Button(root.transform,"next","Continue career",440,532,390,64);var back=Button(root.transform,"menu","Menu",850,532,400,64);
            Set(view,"retry",retry);Set(view,"next",next);Set(view,"menu",back);
            FormulaExperienceBuilder.StyleResults(root);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);FormulaMenuAssets.Current.resultsPrefab=prefab;EditorUtility.SetDirty(FormulaMenuAssets.Current);
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
    private static void FullScreen(Transform root){var parent=(RectTransform)root.parent;Place(root,-parent.anchoredPosition.x,parent.anchoredPosition.y,1280,720);}
    private static void Place(Transform root,float x,float y,float w,float h)=>FormulaMenuTheme.Place((RectTransform)root,x,y,w,h);
    private static void ClearImage(Transform root){var image=root.GetComponent<UnityEngine.UI.Image>();if(image!=null)image.color=Color.clear;}
    private static void Panel(Transform root){ClearImage(root);var r=(RectTransform)root;FormulaMenuTheme.Surface(root,"Upgrade Surface",0,0,r.sizeDelta.x,r.sizeDelta.y).transform.SetAsFirstSibling();}
    private static void Hide(Transform root,string name){var child=root.Find(name);if(child!=null)child.gameObject.SetActive(false);}
    private static RectTransform Group(Transform p,string name,float x,float y,float w,float h){var r=p.Find(name)as RectTransform??FormulaUI.Rect(p,name,x,y,w,h);Place(r,x,y,w,h);return r;}
    private static RectTransform Copy(Transform source,Transform parent,string name){var current=parent.Find(name)as RectTransform;if(current!=null)return current;var go=UnityEngine.Object.Instantiate(source.gameObject,parent,false);go.name=name;return(RectTransform)go.transform;}
    private static UnityEngine.UI.RawImage Raw(Transform p,string name,Texture2D texture,float x,float y,float w,float h){var r=Group(p,name,x,y,w,h);var image=r.GetComponent<UnityEngine.UI.RawImage>()??r.gameObject.AddComponent<UnityEngine.UI.RawImage>();image.texture=texture;image.color=Color.white;image.raycastTarget=false;return image;}
    private static Texture2D LoadUpgrade(string name)=>AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Formula/UI/FigmaUpgrade/"+name+".png");
    private static TMP_Text Label(Transform p,string name,string text,float x,float y,float w,float h,int size,bool title=false){var t=p.Find(name)?.GetComponent<TMP_Text>()??FormulaMenuStyle.Text(p,text,x,y,w,h,size);t.name=name;t.text=text;Format(t,x,y,w,h,size,title);return t;}
    private static void Format(TMP_Text text,float x,float y,float w,float h,int size,bool title=false){Place(text.transform,x,y,w,h);text.font=title?FormulaMenuAssets.Current.titleFont:FormulaMenuAssets.Current.bodyFont;text.fontSize=text.fontSizeMax=text.fontSizeMin=size;text.enableAutoSizing=false;text.fontStyle=FontStyles.Normal;text.color=Color.white;text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Overflow;text.alignment=TextAlignmentOptions.MidlineLeft;text.raycastTarget=false;}
    private static UnityEngine.UI.Button Button(Transform p,string name,string label,float x,float y,float w,float h){var b=p.Find(name)?.GetComponent<UnityEngine.UI.Button>()??FormulaMenuStyle.Button(p,name,label,x,y,w,h,NoAction);Place(b.transform,x,y,w,h);Format(b.GetComponentInChildren<TMP_Text>(true),18,0,w-36,h,26);b.GetComponentInChildren<TMP_Text>(true).text=label;FormulaMenuTheme.StyleButton(b);return b;}
    private static void Set(UnityEngine.Object target,string field,UnityEngine.Object value){var s=new SerializedObject(target);s.FindProperty(field).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    private static void SetBool(UnityEngine.Object target,string field,bool value){var s=new SerializedObject(target);s.FindProperty(field).boolValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    private static void SetString(UnityEngine.Object target,string field,string value){var s=new SerializedObject(target);s.FindProperty(field).stringValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    private static void SetArray(UnityEngine.Object target,string field,UnityEngine.Object[] values){var s=new SerializedObject(target);var p=s.FindProperty(field);p.arraySize=values.Length;for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];s.ApplyModifiedPropertiesWithoutUndo();}
}
