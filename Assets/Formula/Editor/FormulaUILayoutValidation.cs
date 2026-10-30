using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FormulaGame;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Settings;
using Object = UnityEngine.Object;

// Measures actual glyphs in visible views, including dynamic race labels.
[InitializeOnLoad]
public static class FormulaUILayoutValidation
{
    const string Key = "Formula.LayoutAudit", Folder = "Docs/AI/FormulaUILayout";
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
    static readonly List<object> findings = new List<object>(), views = new List<object>();
    static readonly List<string> errors = new List<string>();
    static readonly List<(string name,Action show)> menuCases = new List<(string,Action)>();
    static int phase, locale, page, raceMode, state, textCount;
    static double began, entered;
    static FormulaMainMenu menu; static FormulaRaceView race; static GamePlayManager game;
    static GameObject results, loading;
    static bool Full => SessionState.GetBool(Key + ".Full", false);
    static bool Regression => SessionState.GetBool(Key + ".Regression", false);
    static bool InputsOnly => SessionState.GetBool(Key + ".InputsOnly", false);
    static bool menuNeedsShow;
    static string Code => Full ? FormulaLocalization.SupportedCodes[locale] : "ru";
    static string RaceModeName => new[]{"solo","race","careerRace","timeAttack"}[raceMode];
    static string status;
    static Action afterCapture; static int captureFrame;
    static FormulaUILayoutValidation()
    {
        EditorApplication.update += Tick; EditorApplication.playModeStateChanged += Mode;
        Application.logMessageReceived += (m,s,t) => { if (SessionState.GetBool(Key,false) && (t == LogType.Error || t == LogType.Exception)) errors.Add(m+"\n"+s); };
        if (SessionState.GetBool(Key+".Entered",false) && EditorApplication.isPlaying)
            EditorApplication.delayCall += () => Finish("interrupted_by_source_reload");
    }
    [MenuItem("Tools/Capture Russian HUD Layout Baseline")]
    public static void Baseline() => Run(false);
    [MenuItem("Tools/Validate UI Layout All Languages")]
    public static void Validate() => Run(true);
    [MenuItem("Tools/Validate UI Race Layout Regression")]
    public static void ValidateRaceRegression() => Run(true,true);
    [MenuItem("Tools/Validate UI Input Dialog Layout")]
    public static void ValidateInputs() => Run(true,true,true);
    static void Run(bool full,bool regression=false,bool inputsOnly=false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before layout validation.");
        Directory.CreateDirectory(Folder);
        SessionState.SetBool(Key+".Full",full);SessionState.SetBool(Key+".Regression",regression);SessionState.SetBool(Key+".InputsOnly",inputsOnly); SessionState.SetBool(Key+".Entered",false);
        SessionState.SetString(Key+".Save",Environment.GetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH") ?? "");
        SessionState.SetString(Key+".Scene",EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Key+".Locale",LocalizationSettings.SelectedLocale?.Identifier.Code ?? "en");
        SessionState.SetBool(Key+".Background",Application.runInBackground); SessionState.SetInt(Key+".Fps",Application.targetFrameRate);
        SessionState.SetBool(Key+".Switch",FormulaPlatform.EditorSwitchPreview);
        var type=typeof(Editor).Assembly.GetType("UnityEditor.GameView");var window=EditorWindow.GetWindow(type);
        SessionState.SetInt(Key+".Size",(int)type.GetProperty("selectedSizeIndex",Flags).GetValue(window));
        typeof(FormulaVisualUpgradePlayValidation).GetMethod("Select720",Flags).Invoke(null,null);
        string save=Path.Combine(Path.GetTempPath(),"FormulaLayout-"+Guid.NewGuid().ToString("N"),"save.json");
        Directory.CreateDirectory(Path.GetDirectoryName(save));File.WriteAllText(save,"{\"PlayerName\":\"Layout Tester\",\"money\":50000,\"language\":\"ru\"}");
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH",save);
        SessionState.SetBool(Key,true);EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Menu.unity");EditorApplication.EnterPlaymode();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Preview(){if(SessionState.GetBool(Key,false))FormulaPlatform.EditorSwitchPreview=true;}
    static void Mode(PlayModeStateChange mode)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(mode==PlayModeStateChange.EnteredPlayMode)
        {
            SessionState.SetBool(Key+".Entered",true);findings.Clear();views.Clear();errors.Clear();
            phase=locale=page=raceMode=state=textCount=0;menuNeedsShow=false;began=entered=EditorApplication.timeSinceStartup;status="running";
            Application.runInBackground=true;Write();
        }
        if(mode==PlayModeStateChange.EnteredEditMode)
        {
            Time.timeScale=1;AudioListener.pause=false;
            Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH",EmptyNull(SessionState.GetString(Key+".Save","")));
            string path=SessionState.GetString(Key+".Scene","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(path)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            Application.runInBackground=SessionState.GetBool(Key+".Background",false);Application.targetFrameRate=SessionState.GetInt(Key+".Fps",-1);
            FormulaPlatform.EditorSwitchPreview=SessionState.GetBool(Key+".Switch",false);
            var type=typeof(Editor).Assembly.GetType("UnityEditor.GameView");type.GetProperty("selectedSizeIndex",Flags).SetValue(EditorWindow.GetWindow(type),SessionState.GetInt(Key+".Size",0));
            var original=LocalizationSettings.AvailableLocales.GetLocale(SessionState.GetString(Key+".Locale","en"));if(original!=null)LocalizationSettings.SelectedLocale=original;
            SessionState.SetBool(Key,false);SessionState.SetBool(Key+".Entered",false);Debug.Log("FORMULA_LAYOUT_"+status.ToUpperInvariant());
        }
    }
    static string EmptyNull(string value)=>string.IsNullOrEmpty(value)?null:value;
    static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    static void Next(int value){phase=value;entered=EditorApplication.timeSinceStartup;Write();}
    static bool FontsReady(GameObject root)=>root.GetComponentsInChildren<FormulaLocalizedFont>().All(f=>f.Ready);
    static void Language(){Require(FormulaLocalization.ChangeLanguage(Code,out string error),error);}
    static void Finish(string value){status=value;Write();EditorApplication.ExitPlaymode();}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            double age=EditorApplication.timeSinceStartup-entered;Require(EditorApplication.timeSinceStartup-began<900,"UI layout validation timed out.");
            if(phase==9)
            {
                if(age>.1&&Time.frameCount>captureFrame+1){var next=afterCapture;afterCapture=null;next();}
                return;
            }
            if(phase==0)
            {
                menu=Object.FindFirstObjectByType<FormulaMainMenu>();if(menu==null||!FormulaLocalization.Ready||age<1)return;
                Require(FormulaProgression.Commit(d=>{d.formula.textScale=115;d.formula.safeAreaPercent=95;d.formula.reducedMotion=true;return null;},out string error),error);
                Language();if(Full){BuildMenuCases();menuNeedsShow=true;Next(1);}else LaunchRace();
            }
            else if(phase==1&&age>.2&&FormulaLocalization.Ready&&FontsReady(menu.View.gameObject))
            {
                // Dynamic dialog strings must be populated after the locale cache is ready.
                if(menuNeedsShow){ShowMenuCase();menuNeedsShow=false;Next(1);return;}
                var controls=menu.View.Settings.Controls;
                var waiting=(GameObject)new SerializedObject(controls).FindProperty("waiting").objectReferenceValue;
                Inspect(controls.IsRebinding?waiting:menu.View.Dialogs.IsOpen?menu.View.Dialogs.gameObject:menu.View.gameObject,Code+"/menu/"+menuCases[page].name);
                if(page==0&&!Regression&&(Code=="ru"||Code=="ja"))Capture(Code+"-Menu",AdvanceMenu);
                else if(menuCases[page].name=="Upgrade/brakes"&&Code=="ru")Capture("After-ru-Garage",AdvanceMenu);
                else if(menuCases[page].name=="Rebind"&&Code=="ru")Capture("After-ru-Controls",AdvanceMenu);
                else AdvanceMenu();
            }
            else if(phase==2)
            {
                game=Object.FindFirstObjectByType<GamePlayManager>();race=Object.FindFirstObjectByType<FormulaRaceView>();
                if(game==null||race==null||!game.FormulaRaceStarted||!FormulaLocalization.Ready||!FontsReady(race.gameObject))return;
                Time.timeScale=0;state=0;ShowRaceState(0);Next(3);
            }
            else if(phase==3&&age>.25&&FormulaLocalization.Ready&&FontsReady(results!=null?results:loading!=null?loading:race.gameObject))
            {
                typeof(FormulaRaceView).GetField("nextStandings",Flags).SetValue(race,0f);race.Tick(game);
                string name=Code+"/"+RaceModeName+"/"+state;
                Inspect(results!=null?results:loading!=null?loading:race.gameObject,name);
                if(state==0&&(Code=="ru"||Code=="ja"||!Full))Capture((Full?"After-":"Before-")+Code+"-"+char.ToUpperInvariant(RaceModeName[0])+RaceModeName.Substring(1),AdvanceRace);
                else if(state==1&&Code=="ru"&&raceMode==0)Capture("After-ru-InvalidLap",AdvanceRace);
                else if(state==10&&Code=="pt"&&raceMode==0)Capture("After-pt-Split",AdvanceRace);
                else AdvanceRace();
            }
            else if(phase==4)
            {
                menu=Object.FindFirstObjectByType<FormulaMainMenu>();if(menu==null||age<1)return;
                raceMode++;locale=0;Language();LaunchRace();
            }
        }
        catch(Exception failure){errors.Add(failure.ToString());Finish("failed");}
    }
    static void Capture(string name,Action continuation)
    {
        ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");afterCapture=continuation;captureFrame=Time.frameCount;Next(9);
    }
    static void AdvanceMenu()
    {
        if(++page<menuCases.Count){ShowMenuCase();Next(1);}
        else if(++locale<FormulaLocalization.SupportedCodes.Length){page=0;Language();menuNeedsShow=true;Next(1);}
        else if(InputsOnly)Finish(errors.Count==0&&findings.Count==0?"passed":"findings");
        else{locale=0;Language();LaunchRace();}
    }
    static void InvokeMenu(string method,params object[] args)=>typeof(FormulaMainMenu).GetMethod(method,Flags).Invoke(menu,args);
    static void ShowMenuCase(){menu.View.Settings.Controls.CancelRebind();InvokeMenu("CloseModal");menuCases[page].show();}
    static void BuildMenuCases()
    {
        menuCases.Clear();
        if(Regression)
        {
            menuCases.Add(("Rebind",()=>
            {
                FormulaLocalizationValidation.ShowLayoutPage(menu,8);
                var controls=new SerializedObject(menu.View.Settings.Controls);
                ((UnityEngine.UI.Button)controls.FindProperty("rows").GetArrayElementAtIndex(0).FindPropertyRelative("button").objectReferenceValue).onClick.Invoke();
            }));
            return;
        }
        for(int i=0;i<FormulaLocalizationValidation.LayoutPageCount;i++)
        {int index=i;menuCases.Add((FormulaLocalizationValidation.LayoutPageName(i),()=>FormulaLocalizationValidation.ShowLayoutPage(menu,index)));}
        foreach(var branch in FormulaCatalog.Current.upgrades)
        {
            menuCases.Add(("Upgrade/"+branch.id,()=>{menu.Show("Garaj");menu.View.Garage.ShowSection(0);menu.View.Garage.Upgrade.SelectCategory(Array.IndexOf(new[]{"engine","suspension","aero","brakes"},branch.id));}));
            menuCases.Add(("Equip/"+branch.id,()=>{menu.Show("Garaj");InvokeMenu("UpgradeLevels",branch.id);}));
            foreach(var level in branch.levels)
                menuCases.Add(("Upgrade/"+branch.id+"/"+level.level,()=>{menu.Show("Garaj");InvokeMenu("SelectUpgradeLevel",branch.id,level.level);}));
        }
        foreach(var livery in FormulaCatalog.Current.liveries)
        {
            menuCases.Add(("Livery/"+livery.id,()=>{menu.Show("Garaj");menu.View.Garage.ShowSection(2);InvokeMenu("SelectLivery",livery.id);}));
            if(!FormulaProgression.State.ownedLiveries.Contains(livery.id))
                menuCases.Add(("LiveryDialog/"+livery.id,()=>{menu.Show("Garaj");menu.View.Garage.ShowSection(2);InvokeMenu("SelectLivery",livery.id);InvokeMenu("ApplySelectedLivery");}));
        }
        foreach(var entry in FormulaCatalog.Current.careerEvents)
            menuCases.Add(("Briefing/"+entry.id,()=>{menu.Show("Carieră");InvokeMenu("EventDetails",entry,"career");}));
        foreach(string mode in new[]{"race","trial","practice"})
            menuCases.Add(("Briefing/"+mode,()=>{menu.Show("Curse rapide");InvokeMenu("EventDetails",FormulaCatalog.Current.Event(mode+":track_01"),mode=="trial"?"time_trial":"quick");}));
        menuCases.Add(("Error",()=>{menu.Show("Acasă");InvokeMenu("ShowError",FormulaLocalization.T("Purchase once. Switch between owned levels for free."));}));
    }
    static void AdvanceRace()
    {
        if(!Full){Finish("baseline_recorded");return;}
        if(++state<17){ShowRaceState(state);Next(3);}
        else if(++locale<FormulaLocalization.SupportedCodes.Length){HideRaceLayers();state=0;Language();ShowRaceState(0);Next(3);}
        else if(raceMode<3){HideRaceLayers();Time.timeScale=1;FormulaSession.ReturnToMenu();Next(4);}
        else Finish(errors.Count==0&&findings.Count==0?"passed":"findings");
    }
    static void LaunchRace()
    {
        if(raceMode==2)Require(FormulaProgression.Commit(d=>{d.formula.medals["g01"]=Medal.Gold;d.formula.medals["g02"]=Medal.Gold;return null;},out string error),error);
        string id=raceMode==0?FormulaCatalog.Current.careerEvents[0].id:raceMode==1?"race:track_01":FormulaCatalog.Current.careerEvents.First(e=>e.mode==(raceMode==2?"race":"time_attack")).id;
        Require(FormulaSession.Launch(id,raceMode==1?"quick":"career")==null,"Could not launch UI inspection race.");Next(2);
    }
    static GameObject Layer(string name)=>(GameObject)new SerializedObject(race).FindProperty(name).objectReferenceValue;
    static void HideRaceLayers()
    {
        PauseGameplay.Instance.ResumeGameplay();Time.timeScale=0;
        foreach(string field in new[]{"pause","options","error"})Layer(field).SetActive(false);
        race.Hud.SetActive(true);if(results!=null)Object.Destroy(results);if(loading!=null)Object.Destroy(loading);results=null;loading=null;
    }
    static void ShowRaceState(int value)
    {
        HideRaceLayers();
        // Isolated presentation fixtures reset timing and notifications between locales.
        typeof(FormulaSession).GetField("lapInvalid",Flags).SetValue(null,false);
        typeof(FormulaSession).GetProperty("BestLap",Flags).SetValue(null,0d);
        typeof(FormulaSession).GetProperty("ValidLaps",Flags).SetValue(null,0);
        typeof(FormulaRaceView).GetField("wasInvalid",Flags).SetValue(race,false);
        typeof(FormulaRaceView).GetField("messageUntil",Flags).SetValue(race,Time.time-1);
        typeof(FormulaRaceView).GetField("splitText",Flags).SetValue(race,"—");
        if(value==0)return;
        if(value==1){FormulaSession.InvalidateLap();return;}
        if(value>=9&&value<=11)
        {
            typeof(FormulaSession).GetProperty("BestLap",Flags).SetValue(null,90d);
            typeof(FormulaSession).GetProperty("ValidLaps",Flags).SetValue(null,1);
            if(value>9)
            {
                typeof(FormulaRaceView).GetField("splitText",Flags).SetValue(race,value==10?"+12.34":"-12.34");
                typeof(FormulaRaceView).GetField("splitDifference",Flags).SetValue(race,value==10?12.34d:-12.34d);
            }
            return;
        }
        if(value==12)
        {
            typeof(FormulaRaceView).GetMethod("ShowMessage",Flags).Invoke(race,new object[]{FormulaLocalization.T("WRONG WAY"),FormulaLocalization.T("Turn around and follow the circuit direction"),5f,true});return;
        }
        race.Hud.SetActive(false);
        if(value==2)PauseGameplay.Instance.PauseGame();
        if(value==3)PauseGameplay.Instance.OpenSettingsPanel();
        if(value==4)race.SaveError(FormulaLocalization.T("ACTION NOT COMPLETED"));
        if(value==5||value>=13)
        {
            results=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(FormulaPlayerAvatarBuilder.ResultsPath),race.transform.parent,false);
            var receipt=new FormulaReceipt{eventId=FormulaSession.CurrentEvent.id,medal=value==14?Medal.None:value==15?Medal.Gold:Medal.Bronze,credits=150,xp=200,elapsedSeconds=190,bestLapSeconds=value==13?0:value==14?190:90,finishPosition=value==14?4:1,personalBest=value==16};
            if(value==15){receipt.levelCredits=1000;receipt.newLiveries.Add(FormulaCatalog.Current.liveries[1].id);}
            results.GetComponent<FormulaResultsView>().Present(receipt);
        }
        if(value==6||value==7)
        {
            // Exercise both real confirmation actions, including their dynamic titles.
            PauseGameplay.Instance.PauseGame();
            var fields=new SerializedObject(race);
            ((UnityEngine.UI.Button)fields.FindProperty(value==6?"restart":"leave").objectReferenceValue).onClick.Invoke();
        }
        if(value==8)loading=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Formula/Resources/Formula/UI/FormulaLoading.prefab"),race.transform.parent,false);
    }
    static string PathOf(Transform t,Transform root)=>t==root?t.name:PathOf(t.parent,root)+"/"+t.name;
    static void Inspect(GameObject root,string view)
    {
        Canvas.ForceUpdateCanvases();int count=0;var visible=new List<(TMP_Text text,Rect glyph)>();
        foreach(var label in root.GetComponentsInChildren<TMP_Text>())
        {
            if(!label.enabled||label.color.a<.01f||label.canvasRenderer.GetInheritedAlpha()<.01f)continue;
            label.ForceMeshUpdate(true,true);Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=new Vector2(float.MinValue,float.MinValue);bool any=false;
            foreach(var c in label.textInfo.characterInfo.Take(label.textInfo.characterCount).Where(c=>c.isVisible))
            {
                any=true;min=Vector2.Min(min,c.bottomLeft);max=Vector2.Max(max,c.topRight);
            }
            if(!any)continue;count++;var rect=label.rectTransform.rect;const float tolerance=2;
            string path=PathOf(label.transform,root.transform);
            if(label.isTextTruncated)findings.Add(new{view,path,kind="truncated localized text",text=label.text});
            if(min.x<rect.xMin-tolerance||max.x>rect.xMax+tolerance||min.y<rect.yMin-tolerance||max.y>rect.yMax+tolerance)
                findings.Add(new{view,path,kind="glyphs outside assigned rectangle",text=label.text,font=label.fontSize,minimum=label.fontSizeMin,rect=rect.ToString(),glyph=new Rect(min,max-min).ToString()});
            var lo=(Vector2)root.transform.InverseTransformPoint(label.transform.TransformPoint(min));var hi=(Vector2)root.transform.InverseTransformPoint(label.transform.TransformPoint(max));
            visible.Add((label,Rect.MinMaxRect(lo.x,lo.y,hi.x,hi.y)));
        }
        for(int i=0;i<visible.Count;i++)for(int j=i+1;j<visible.Count;j++)
        {
            var a=visible[i];var b=visible[j];
            float w=Mathf.Min(a.glyph.xMax,b.glyph.xMax)-Mathf.Max(a.glyph.xMin,b.glyph.xMin),h=Mathf.Min(a.glyph.yMax,b.glyph.yMax)-Mathf.Max(a.glyph.yMin,b.glyph.yMin);
            if(w>2&&h>2)findings.Add(new{view,kind="overlapping text bounds",first=PathOf(a.text.transform,root.transform),second=PathOf(b.text.transform,root.transform),width=w,height=h});
        }
        Require(count>0,"No visible text inspected: "+view);
        textCount+=count;views.Add(new{view,visibleTexts=count});Write();
    }
    static void Write()=>File.WriteAllText(Folder+"/"+(InputsOnly?"InputDialogs":Full?Regression?"RaceRegression":"Validation":"Baseline")+".json",JsonConvert.SerializeObject(new{status,phase,locale,page,raceMode,state,textCount,views,findings,errors,isolatedSave=true,referenceResolution="1280x720",textScale=115,safeArea=95},Formatting.Indented));
}
