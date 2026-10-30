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
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

// Observable HUD and Nintendo-button smoke checks at the handheld reference size.
[InitializeOnLoad]
public static class FormulaVisualUpgradePlayValidation
{
    const string Key = "Formula.VisualUpgradePlay", Folder = "Docs/AI/FormulaVisualUpgrade";
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
    static readonly List<string> checks = new List<string>(), errors = new List<string>();
    static readonly List<object> labels = new List<object>();
    static double began; static int phase; static string status;
    static FormulaRaceView view; static Gamepad pad;
    static FormulaVisualUpgradePlayValidation()
    {
        EditorApplication.update += Tick; EditorApplication.playModeStateChanged += Mode;
        Application.logMessageReceived += (m,s,t) => { if (SessionState.GetBool(Key,false) && (t == LogType.Error || t == LogType.Exception)) errors.Add(m + "\n" + s); };
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Preview() { if (SessionState.GetBool(Key,false)) FormulaPlatform.EditorSwitchPreview = true; }
    [MenuItem("Tools/Play Validate Formula Visual Upgrade")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++) if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
        Directory.CreateDirectory(Folder);
        SessionState.SetString(Key+".Scene",EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Key+".Save",Environment.GetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH") ?? "");
        SessionState.SetBool(Key+".Background",Application.runInBackground); SessionState.SetInt(Key+".Fps",Application.targetFrameRate);
        SessionState.SetBool(Key+".Switch",FormulaPlatform.EditorSwitchPreview);
        var type=typeof(Editor).Assembly.GetType("UnityEditor.GameView"); var window=EditorWindow.GetWindow(type);
        SessionState.SetInt(Key+".Size",(int)type.GetProperty("selectedSizeIndex",Flags).GetValue(window)); Select720();
        Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH",Path.Combine(Path.GetTempPath(),"FormulaVisualQA-"+Guid.NewGuid().ToString("N"),"save.json"));
        SessionState.SetBool(Key,true); EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Menu.unity"); EditorApplication.EnterPlaymode();
    }
    static void Select720()
    {
        var assembly=typeof(Editor).Assembly;var type=assembly.GetType("UnityEditor.GameView");var window=EditorWindow.GetWindow(type);
        var sizesType=assembly.GetType("UnityEditor.GameViewSizes");var sizes=sizesType.GetProperty("instance",Flags).GetValue(null);
        var group=sizesType.GetMethod("GetGroup",Flags).Invoke(sizes,new[]{sizesType.GetProperty("currentGroupType",Flags).GetValue(sizes)});
        int count=(int)group.GetType().GetMethod("GetTotalCount",Flags).Invoke(group,null);
        for(int i=0;i<count;i++)
        {
            var size=group.GetType().GetMethod("GetGameViewSize",Flags).Invoke(group,new object[]{i});
            if((int)size.GetType().GetProperty("width",Flags).GetValue(size)!=1280 || (int)size.GetType().GetProperty("height",Flags).GetValue(size)!=720)continue;
            type.GetProperty("selectedSizeIndex",Flags).SetValue(window,i);window.Focus();return;
        }
        throw new InvalidOperationException("Existing 1280x720 Game View preset is required.");
    }
    static void Mode(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear();errors.Clear();labels.Clear();phase=0;status="running";began=EditorApplication.timeSinceStartup;
            FormulaPlatform.EditorSwitchPreview=true; Application.runInBackground=true;pad=InputSystem.AddDevice<Gamepad>();Write();
        }
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            if(pad!=null && pad.added)InputSystem.RemoveDevice(pad);pad=null;
            FormulaPlatform.EditorSwitchPreview=SessionState.GetBool(Key+".Switch",false);Time.timeScale=1;
            Application.runInBackground=SessionState.GetBool(Key+".Background",false);Application.targetFrameRate=SessionState.GetInt(Key+".Fps",-1);
            string value=SessionState.GetString(Key+".Save","");Environment.SetEnvironmentVariable("FORMULA_VALIDATION_SAVE_PATH",string.IsNullOrEmpty(value)?null:value);
            value=SessionState.GetString(Key+".Scene","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(value)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(value);
            var type=typeof(Editor).Assembly.GetType("UnityEditor.GameView");type.GetProperty("selectedSizeIndex",Flags).SetValue(EditorWindow.GetWindow(type),SessionState.GetInt(Key+".Size",0));
            SessionState.SetBool(Key,false);Debug.Log("FORMULA_VISUAL_PLAY_"+status.ToUpperInvariant());
        }
    }
    static void Next(int value){phase=value;began=EditorApplication.timeSinceStartup;Write();}
    static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            double age=EditorApplication.timeSinceStartup-began;Require(age<120,"Visual UI QA timeout in phase "+phase);
            if(phase==0)
            {
                var menu=Object.FindFirstObjectByType<FormulaMainMenu>();if(menu==null||!FormulaLocalization.Ready||age<2)return;
                Require(FormulaLocalization.ChangeLanguage("en",out string error),error);Require(FormulaSession.Launch("race:track_01","quick")==null,"Race did not launch.");Next(1);
            }
            else if(phase==1)
            {
                var game=Object.FindFirstObjectByType<GamePlayManager>();view=Object.FindFirstObjectByType<FormulaRaceView>();
                if(game==null||view==null||!game.FormulaRaceStarted||age<3)return;
                var rig=RCCP_SceneManager.Instance.activePlayerCamera;
                Require(Mathf.Abs(rig.TPSDistance-6.3f)<.01f&&!rig.TPSAutoFocus,"Runtime camera did not load the persistent scene settings.");
                var volume=Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None).First(v=>v.isGlobal);
                Require(AssetDatabase.GetAssetPath(volume.sharedProfile) == "Assets/Formula/Racing/Surfaces/Circuit Surface Grade.asset","Runtime did not load the restored original Global Volume profile.");
                CheckLabels("default");checks.Add("Real race loads the saved camera, grade, compact HUD and live telemetry.");
                ScreenCapture.CaptureScreenshot(Folder+"/HUD-720.png");Next(2);
            }
            else if(phase==2&&age>.7)
            {
                Verify("HUD-720");Require(FormulaProgression.Commit(d=>{d.formula.textScale=115;d.formula.safeAreaPercent=95;d.formula.highContrast=true;return null;},out string error),error);Next(3);
            }
            else if(phase==3&&age>1)
            {
                CheckLabels("115-percent-text-95-safe-area-high-contrast");checks.Add("Critical HUD labels produce visible glyphs without overflow at 115% text and 95% safe area.");
                ScreenCapture.CaptureScreenshot(Folder+"/HUD-Accessible-720.png");Next(4);
            }
            else if(phase==4&&age>.7){Verify("HUD-Accessible-720");PauseGameplay.Instance.PauseGame();Next(5);}
            else if(phase==5&&age>.7)
            {
                Require(Time.timeScale==0&&!view.Hud.activeInHierarchy,"Pause did not hide HUD and stop gameplay.");
                var selected=EventSystem.current.currentSelectedGameObject;Require(selected!=null&&selected.name=="resume","Pause did not focus Resume.");
                ScreenCapture.CaptureScreenshot(Folder+"/Pause-720.png");checks.Add("Pause stops gameplay and focuses Resume using the existing navigation.");Next(6);
            }
            else if(phase==6&&age>.7)
            {
                Verify("Pause-720");InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.East));Next(7);
            }
            else if(phase==7&&age>.7)
            {
                Require(Time.timeScale>0&&view.Hud.activeInHierarchy,"Nintendo A did not resume gameplay.");
                InputSystem.QueueStateEvent(pad,new GamepadState());checks.Add("Nintendo A (physical buttonEast) resumes the race through real UI input.");
                Require(errors.Count==0,"Runtime errors were recorded during UI QA.");status="passed";Write();EditorApplication.ExitPlaymode();
            }
        }
        catch(Exception failure){errors.Add(failure.ToString());status="failed";Write();EditorApplication.ExitPlaymode();}
    }
    static void CheckLabels(string variant)
    {
        var serialized=new SerializedObject(view);
        foreach(string field in new[]{"position","lap","lapTime","speed","gear","rpm"})
        {
            var text=(TMP_Text)serialized.FindProperty(field).objectReferenceValue;text.ForceMeshUpdate(true,true);
            bool glyphs=text.textInfo.characterInfo.Take(text.textInfo.characterCount).Any(c=>c.isVisible);
            labels.Add(new{variant,field,text=text.text,text.fontSize,glyphs,overflow=text.isTextOverflowing,size=text.rectTransform.rect.size.ToString("F1")});
            Require(text.gameObject.activeInHierarchy&&glyphs&&!text.isTextOverflowing,"HUD label is clipped or missing: "+variant+" / "+field);
        }
    }
    static void Verify(string name)
    {
        byte[] b=File.ReadAllBytes(Folder+"/"+name+".png");int w=b[16]<<24|b[17]<<16|b[18]<<8|b[19],h=b[20]<<24|b[21]<<16|b[22]<<8|b[23];
        Require(w==1280&&h==720,"Expected native 1280x720 QA screenshot, got "+w+"x"+h);
    }
    static void Write(){Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/HudPlayValidation.json",JsonConvert.SerializeObject(new{status,phase,checks,labels,errors,isolatedSave=true,nintendoUiPreview=true,switchHardwareTested=false},Formatting.Indented));}
}
