using System;
using System.Linq;
using FormulaGame;
using TMPro;
using UnityEditor;
using UnityEngine;

// Authored adaptation of Figma 204:767. Live Generic and RCCP values supply the vehicle content.
public static class FormulaUpgradeFigmaBuilder
{
    private const string Folder = "Assets/Formula/UI/FigmaUpgrade/";
    private const string Shared = "Assets/Formula/UI/FigmaCareer/";
    [MenuItem("Tools/Formula Career/Match Figma Upgrade")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        foreach (string path in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder.TrimEnd('/') }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path); importer.textureType=TextureImporterType.Default;
            importer.sRGBTexture=true; importer.alphaIsTransparency=true; importer.mipmapEnabled=false; importer.maxTextureSize=2048;
            importer.textureCompression=TextureImporterCompression.Uncompressed; importer.wrapMode=TextureWrapMode.Clamp; importer.SaveAndReimport();
        }
        var root=PrefabUtility.LoadPrefabContents(FormulaMenuHierarchyBuilder.PrefabPath);
        try { Style(root.GetComponentInChildren<FormulaGarageView>(true)); PrefabUtility.SaveAsPrefabAsset(root,FormulaMenuHierarchyBuilder.PrefabPath); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets(); Debug.Log("FORMULA_FIGMA_UPGRADE_APPLIED");
    }
    public static void Style(FormulaGarageView garage)
    {
        var menu=garage.GetComponentInParent<FormulaMenuView>();
        var homeUpgrade = menu.Home.transform.Find("Navigation/home-development");if(homeUpgrade!=null)homeUpgrade.GetComponentInChildren<TMP_Text>(true).text="UPGRADE";
        var root=(RectTransform)garage.transform.Find("Dezvoltare");
        var screens=(RectTransform)garage.transform.parent;
        Place(root,-screens.anchoredPosition.x,screens.anchoredPosition.y,1280,720);
        var view=root.GetComponent<FormulaUpgradeView>()??root.gameObject.AddComponent<FormulaUpgradeView>(); Set(garage,"upgrade",view);
        var legacy=root.Find("Upgrade Cards");
        if(legacy!=null) UnityEngine.Object.DestroyImmediate(legacy.gameObject);
        var garageData=new SerializedObject(garage);garageData.FindProperty("development").arraySize=0;garageData.ApplyModifiedPropertiesWithoutUndo();
        var stats=root.Find("RCCP Statistics").GetComponent<FormulaStatisticsView>();
        var shade=AssetDatabase.LoadAssetAtPath<Material>(Shared+"FigmaBlackOverlay.mat");
        Art(root,"Header Shade","HeaderShade",0,0,1280,133,true).material=shade;
        Art(root,"Lower Shade","LowerShade",0,500,1280,220).material=shade;
        Art(root,"Footer Shade","FooterShade",0,612,1280,108).material=shade;
        var logo=Copy(menu.Career.transform.Find("Figma Logo 2027"),root,"Upgrade Logo"); Place(logo,23,0,429,147);
        var account=Copy(menu.Career.transform.Find("Figma Driver and Wallet"),root,"Upgrade Driver and Wallet"); Place(account,953,21,301,46);
        Set(view,"driver",account.Find("Player Name").GetComponent<TMP_Text>()); Set(view,"wallet",account.Find("Credits").GetComponent<TMP_Text>());
        var categoryRoot=Group(root,"Component Categories",0,100,1280,47);
        Art(categoryRoot,"Category Surface","CategorySurface",-20,-20,1320,95).transform.SetAsFirstSibling();
        string[] ids={"engine","suspension","aero","brakes"}; string[] names={"ENGINE","SUSPENSION","AERO","BRAKES"};
        float[] xs={56,218,460,600}, widths={154,230,128,180};
        var categories=new UnityEngine.UI.Button[4]; var markers=new GameObject[4];
        for(int i=0;i<4;i++)
        {
            var b=Button(categoryRoot,"upgrade-"+ids[i],xs[i],0,widths[i],47); categories[i]=b;
            Label(b.transform,"Name",names[i],8,0,widths[i]-16,43,34,FormulaMenuAssets.Current.bodyFont);
            if(i>0)Box(b.transform,"Divider",0,0,1,47,new Color(1,1,1,.35f));
            var active=Art(b.transform,"Active Category","CategoryGlow",(widths[i]-125)*.5f,43,125,8); markers[i]=active.gameObject; markers[i].SetActive(i==0);
            var focus=b.GetComponent<FormulaMenuFocus>()??b.gameObject.AddComponent<FormulaMenuFocus>();
            focus.Initialize(Box(b.transform,"Focus",8,45,widths[i]-16,1,Color.clear).GetComponent<UnityEngine.UI.Image>());
            Option(b,view,true,i);
        }
        SetArray(view,"categories",categories); SetArray(view,"categoryMarkers",markers);
        var levelRoot=Group(root,"Component Levels",30,167,237,357);
        var ser=new SerializedObject(view); var rows=ser.FindProperty("levels"); rows.arraySize=4;
        for(int i=0;i<4;i++)
        {
            var b=Button(levelRoot,"upgrade-level-"+i,0,i==0?1:i*90.5f,237,85.5f);
            var normal=Art(b.transform,"Normal Surface",i==0?"LevelInstalled":"LevelNormal",0,-1,237,87);normal.transform.SetAsFirstSibling();
            var selected=Art(b.transform,"Selected Surface","LevelSelected",0,-1,237,87);selected.transform.SetSiblingIndex(1);
            var value=Label(b.transform,"Physical Value",(480*(1+.03f*i)).ToString("0.#",System.Globalization.CultureInfo.InvariantCulture)+" Nm",29.5f,6,190,40,40,FormulaMenuAssets.Current.titleFont);value.fontStyle=FontStyles.Bold;
            Label(b.transform,"Level",i==0?"STOCK":"L"+i,181,8,42,20,14,FormulaMenuAssets.Current.bodyFont);
            var status=Label(b.transform,"Ownership and Price",i==0?"INSTALLED":FormulaCatalog.Current.Upgrade("engine").levels[i-1].incrementalPriceCredits.ToString("N0",System.Globalization.CultureInfo.InvariantCulture),25,49,170,28,i==0?28:20,FormulaMenuAssets.Current.bodyFont);
            var coin=Art(b.transform,"Currency","CoinMatte",169,53,20,20,true);coin.color=new Color(.72f,.72f,.72f,1);coin.gameObject.SetActive(i>0);
            var focus=b.GetComponent<FormulaMenuFocus>()??b.gameObject.AddComponent<FormulaMenuFocus>();focus.StyleFigma(selected,null,normal);
            Option(b,view,false,i);
            var row=rows.GetArrayElementAtIndex(i);row.FindPropertyRelative("button").objectReferenceValue=b;row.FindPropertyRelative("value").objectReferenceValue=value;row.FindPropertyRelative("status").objectReferenceValue=status;row.FindPropertyRelative("surface").objectReferenceValue=normal;row.FindPropertyRelative("coin").objectReferenceValue=coin;
        }
        ser.ApplyModifiedPropertiesWithoutUndo();
        Set(view,"normalSurface",Texture("LevelNormal"));Set(view,"installedSurface",Texture("LevelInstalled"));
        var description=Label(root,"Level Details","ENGINE · LEVEL 1\nSelect to review purchase\nTorque +3% per level",30,548,470,62,18,FormulaMenuAssets.Current.bodyFont);description.textWrappingMode=TextWrappingModes.Normal;Set(view,"description",description);
        StyleStatistics(stats);stats.transform.SetAsLastSibling();Set(view,"statistics",stats);
        var footer=Group(root,"Upgrade Footer",0,624,1280,96);
        Art(footer,"Footer Glow Left","FooterGlow",79,0,350,22,true,true);Art(footer,"Footer Glow Right","FooterGlow",668,4,218,14,true,true);
        var page=Label(footer,"Page Title","UPGRADE",86,22,200,62,60,FormulaMenuAssets.Current.titleFont);page.alignment=TextAlignmentOptions.Center;
        Box(footer,"Title Rule",308,52,285,1,new Color(1,1,1,.45f));
        var backRoot=Copy(menu.Career.transform.Find("Figma Career Footer/career-back"),footer,"upgrade-back");Place(backRoot,1125,36,75,31);
        Set(view,"back",backRoot.GetComponent<UnityEngine.UI.Button>());
        var chrome=new UnityEngine.Object[] {menu.transform.Find("Header").gameObject,menu.transform.Find("Footer").gameObject,menu.transform.Find("Screen Shade").gameObject,garage.transform.Find("Tabs").gameObject};SetArray(view,"standardChrome",chrome);
        if(menu.UpgradeTheme)FormulaMenuUnifiedBuilder.StyleGarage(garage);
    }
    private static void StyleStatistics(FormulaStatisticsView stats)
    {
        var root=(RectTransform)stats.transform;Place(root,535,433,523,180);root.GetComponent<UnityEngine.UI.Image>().color=Color.clear;
        Art(root,"Statistics Surface","StatsSurface",0,0,523,180).transform.SetAsFirstSibling();root.Find("Heading").gameObject.SetActive(false);
        var s=new SerializedObject(stats);s.FindProperty("figmaLevels").boolValue=true;var rows=s.FindProperty("development");
        int[] order={0,3,1,2};string[] fields={"torque","brakes","spring","aero"};
        for(int i=0;i<4;i++)
        {
            int rowIndex=order[i];float y=8+rowIndex*40;
            var row=rows.GetArrayElementAtIndex(i);var label=(TMP_Text)row.FindPropertyRelative("label").objectReferenceValue;Configure(label,20,y,142,30,18,FormulaMenuAssets.Current.bodyFont);
            var value=(TMP_Text)s.FindProperty(fields[i]).objectReferenceValue;Configure(value,350,y,153,30,22,FormulaMenuAssets.Current.strongFont);value.alignment=TextAlignmentOptions.MidlineRight;
            var branch=row.FindPropertyRelative("branchId").stringValue;var bars=root.Find("Stats-"+branch);Place((RectTransform)bars,170,y+13,166,6);
            var segments=row.FindPropertyRelative("segments");segments.arraySize=9;
            for(int j=0;j<9;j++)
            {
                var tick=j<bars.childCount?(RectTransform)bars.GetChild(j):Box(bars,"Level Tick "+j,0,0,14,6,new Color32(116,18,5,255));
                Place(tick,j*19,0,14,6);var image=tick.GetComponent<UnityEngine.UI.Image>();image.sprite=FormulaMenuAssets.Current.panelSprite;image.type=UnityEngine.UI.Image.Type.Sliced;image.raycastTarget=false;segments.GetArrayElementAtIndex(j).objectReferenceValue=image;
            }
        }
        s.ApplyModifiedPropertiesWithoutUndo();Configure(root.Find("Drag and Mass").GetComponent<TMP_Text>(),20,160,483,20,16,FormulaMenuAssets.Current.bodyFont);
    }
    private static void Option(UnityEngine.UI.Button button,FormulaUpgradeView owner,bool category,int index)
    {
        var option=button.GetComponent<FormulaUpgradeOption>()??button.gameObject.AddComponent<FormulaUpgradeOption>();var s=new SerializedObject(option);s.FindProperty("owner").objectReferenceValue=owner;s.FindProperty("category").boolValue=category;s.FindProperty("index").intValue=index;s.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Set(UnityEngine.Object target,string name,UnityEngine.Object value) {var s=new SerializedObject(target);s.FindProperty(name).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    private static void SetArray(UnityEngine.Object target,string name,UnityEngine.Object[] values) {var s=new SerializedObject(target);var p=s.FindProperty(name);p.arraySize=values.Length;for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];s.ApplyModifiedPropertiesWithoutUndo();}
    private static void Place(RectTransform r,float x,float y,float w,float h) {r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
    private static RectTransform Group(Transform p,string name,float x,float y,float w,float h) {var r=p.Find(name)as RectTransform??FormulaUI.Rect(p,name,x,y,w,h);Place(r,x,y,w,h);return r;}
    private static RectTransform Copy(Transform source,Transform parent,string name) {var r=parent.Find(name)as RectTransform;if(r!=null)return r;var go=UnityEngine.Object.Instantiate(source.gameObject,parent,false);go.name=name;return(RectTransform)go.transform;}
    private static RectTransform Box(Transform p,string name,float x,float y,float w,float h,Color color) {var r=Group(p,name,x,y,w,h);var im=r.GetComponent<UnityEngine.UI.Image>()??r.gameObject.AddComponent<UnityEngine.UI.Image>();im.color=color;im.raycastTarget=false;return r;}
    private static Texture2D Texture(string name,bool shared=false) => AssetDatabase.LoadAssetAtPath<Texture2D>((shared?Shared:Folder)+name+".png");
    private static UnityEngine.UI.RawImage Art(Transform p,string name,string file,float x,float y,float w,float h,bool shared=false,bool common=false)
    {var r=Group(p,name,x,y,w,h);var im=r.GetComponent<UnityEngine.UI.RawImage>()??r.gameObject.AddComponent<UnityEngine.UI.RawImage>();im.texture=common?FormulaMenuAssets.Current.footerGlow:Texture(file,shared);if(im.texture==null)throw new InvalidOperationException("Missing Upgrade art: "+file);im.color=Color.white;im.raycastTarget=false;return im;}
    private static UnityEngine.UI.Button Button(Transform p,string name,float x,float y,float w,float h) {var r=Box(p,name,x,y,w,h,Color.clear);var im=r.GetComponent<UnityEngine.UI.Image>();im.raycastTarget=true;var b=r.GetComponent<UnityEngine.UI.Button>()??r.gameObject.AddComponent<UnityEngine.UI.Button>();b.transition=UnityEngine.UI.Selectable.Transition.None;b.targetGraphic=im;return b;}
    private static TMP_Text Label(Transform p,string name,string text,float x,float y,float w,float h,int size,TMP_FontAsset font) {var t=p.Find(name)?.GetComponent<TMP_Text>()??FormulaMenuStyle.Text(p,text,x,y,w,h,size);t.name=name;t.text=text;Configure(t,x,y,w,h,size,font);return t;}
    private static void Configure(TMP_Text t,float x,float y,float w,float h,int size,TMP_FontAsset font) {Place(t.rectTransform,x,y,w,h);t.font=font;t.fontSize=t.fontSizeMin=t.fontSizeMax=size;t.enableAutoSizing=false;t.fontStyle=FontStyles.Normal;t.color=Color.white;t.alignment=TextAlignmentOptions.MidlineLeft;t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Overflow;t.raycastTarget=false;}
}
