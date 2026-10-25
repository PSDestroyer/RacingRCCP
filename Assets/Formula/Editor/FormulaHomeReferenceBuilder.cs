using System;
using System.IO;
using System.Linq;
using FormulaGame;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// A targeted, repeatable authoring pass. Home retains its five existing actions.
public static class FormulaHomeReferenceBuilder
{
    private const string StagePath = "Assets/Formula/Garage/Formula_Paddock_Garage.prefab";
    [MenuItem("Tools/Formula Career/Refine Home Reference")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        foreach (string path in new[] { "Assets/Scenes/Menu.unity", FormulaGarageBuilder.GarageScene })
        {
            var scene = SceneManager.GetSceneByPath(path);
            if (scene.IsValid() && scene.isDirty) throw new InvalidOperationException("Save scene changes first: " + path);
        }
        var original = SceneManager.GetActiveScene();
        var root = PrefabUtility.LoadPrefabContents(FormulaMenuHierarchyBuilder.PrefabPath);
        try
        {
            var view = root.GetComponent<FormulaMenuView>(); StyleHome(view.Home); StyleChrome(view);
            PrefabUtility.SaveAsPrefabAsset(root, FormulaMenuHierarchyBuilder.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        RefineStage();
        foreach (string path in new[] { "Assets/Scenes/Menu.unity", FormulaGarageBuilder.GarageScene }) RefineScene(path);
        AssetDatabase.SaveAssets();
        if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
        FormulaMenuHierarchyBuilder.Validate();
        Debug.Log("FORMULA_HOME_REFERENCE_APPLIED");
    }
    public static void StyleHome(FormulaHomeView view)
    {
        var parent = (RectTransform)view.transform.parent;
        Place(view.transform, -parent.anchoredPosition.x, parent.anchoredPosition.y, 1280, 720);
        var fade = Group(view.transform, "Navigation Backdrop", 0, 100, 900, 524);
        var graphic = fade.GetComponent<FormulaHomeBackdrop>() ?? fade.gameObject.AddComponent<FormulaHomeBackdrop>();
        graphic.color = Color.white; graphic.raycastTarget = false; fade.SetAsFirstSibling();
        var nav = view.transform.Find("Navigation"); Place(nav, 48, 174, 380, 390);
        var obsolete = nav.Find("home-development"); if (obsolete != null) UnityEngine.Object.DestroyImmediate(obsolete.gameObject);
        string[] ids = { "home-career", "home-garage", "home-quick", "home-profile", "home-settings" };
        string[] labels = { "CAREER", "GARAGE", "QUICK RACE", "PROFILE", "SETTINGS" };
        FormulaMissionIcon.Symbol[] symbols = { FormulaMissionIcon.Symbol.Race, FormulaMissionIcon.Symbol.Garage, FormulaMissionIcon.Symbol.Stopwatch, FormulaMissionIcon.Symbol.Helmet, FormulaMissionIcon.Symbol.Settings };
        for (int i=0;i<ids.Length;i++)
        {
            bool primary = i < 3; float width = primary ? 370 : 177, height = primary ? 76 : 50;
            var button = nav.Find(ids[i]).GetComponent<UnityEngine.UI.Button>();
            Place(button.transform, i==4 ? 193 : 0, primary ? i*92 : 310, width, height);
            var label = button.GetComponentInChildren<TMP_Text>(true); label.text = labels[i];
            Format(label, primary ? 68 : 43, 0, primary ? 254 : 126, height, primary ? 32 : 21);
            label.font = FormulaMenuAssets.Current.strongFont; label.characterSpacing = primary ? 1.8f : .6f;
            var oldIcon = button.transform.Find("Figma Icon");
            if (oldIcon != null) UnityEngine.Object.DestroyImmediate(oldIcon.gameObject);
            Icon(button.transform, "Navigation Icon", symbols[i], primary ? 23 : 14, primary ? 23 : 14, primary ? 30 : 22);
            var arrow = Icon(button.transform, "Selection Arrow", FormulaMissionIcon.Symbol.Chevron, width-37, (height-20)*.5f, 20);
            FormulaMenuTheme.StyleButton(button);
            arrow.gameObject.SetActive(primary);
            var focus = button.GetComponent<FormulaMenuFocus>(); focus.UseHomeNavigation();
            focus.StyleFigma(button.transform.Find("Upgrade Focus").GetComponent<FormulaPanelGraphic>(), primary ? arrow : null, button.transform.Find("Upgrade Normal").GetComponent<FormulaPanelGraphic>());
        }
        view.WireNavigation();
    }
    public static void StyleChrome(FormulaMenuView view)
    {
        var header = view.transform.Find("Header"); Place(header.Find("Formula Logo"), 40, 1, 280, 96);
        var account = header.Find("Formula Driver and Wallet"); Place(account, 940, 28, 300, 44);
        foreach (var image in account.GetComponentsInChildren<UnityEngine.UI.RawImage>(true)) image.enabled = false;
        Format(account.Find("Player Name").GetComponent<TMP_Text>(), 0, 0, 132, 44, 21);
        var credits = account.Find("Credits").GetComponent<TMP_Text>(); Format(credits, 142, 0, 119, 44, 23); credits.alignment = TextAlignmentOptions.MidlineRight;
        var unit = Text(account, "Currency Unit", "CR", 269, 0, 31, 44, 18); unit.color = FormulaUI.Accent;
        var line = Group(account, "Account Rule", 145, 44, 155, 1);
        var ink = line.GetComponent<UnityEngine.UI.Image>() ?? line.gameObject.AddComponent<UnityEngine.UI.Image>(); ink.color = new Color(1,1,1,.25f); ink.raycastTarget = false;
        var footer = view.transform.Find("Footer");
        foreach (string name in new[] { "Footer Glow", "Glow Left", "Glow Right" }) { var glow = footer.Find(name); if (glow != null) glow.gameObject.SetActive(false); }
        Format(footer.Find("Page Title").GetComponent<TMP_Text>(), 48, 23, 500, 60, 45); footer.Find("Page Title").GetComponent<TMP_Text>().font = FormulaMenuAssets.Current.titleFont;
        Place(footer.Find("Title Rule"), 48, 0, 1184, 1);
        Place(view.transform.Find("Shell Lower Shade"),0,568,1280,152);
    }
    private static FormulaMissionIcon Icon(Transform parent, string name, FormulaMissionIcon.Symbol symbol, float x, float y, float size)
    {
        var rect = Group(parent,name,x,y,size,size);
        var previous = rect.GetComponent<UnityEngine.UI.Graphic>();
        if (previous != null && !(previous is FormulaMissionIcon)) UnityEngine.Object.DestroyImmediate(previous);
        var icon = rect.GetComponent<FormulaMissionIcon>() ?? rect.gameObject.AddComponent<FormulaMissionIcon>(); icon.SetSymbol(symbol); icon.color = Color.white; return icon;
    }
    private static void RefineStage()
    {
        var root = PrefabUtility.LoadPrefabContents(StagePath);
        try
        {
            var ground = Material("Home Studio Floor",new Color32(12,2,4,255),.12f,.28f);
            var pad = Material("Home Display Pad",new Color32(27,3,6,255),.15f,.38f);
            var wall = Material("Home Studio Wall",new Color32(18,2,4,255),.02f,.14f);
            var stripe = Material("Home Team Panel",new Color32(54,3,8,255),.08f,.25f);
            root.transform.Find("Architecture/Epoxy Floor").GetComponent<Renderer>().sharedMaterial = ground;
            root.transform.Find("Service Bay Markings/Service Pad").GetComponent<Renderer>().sharedMaterial = pad;
            foreach (string name in new[] { "Rear Wall", "Left Wall", "Right Wall" }) root.transform.Find("Architecture/"+name).GetComponent<Renderer>().sharedMaterial = wall;
            root.transform.Find("Architecture/Rear Team Stripe").GetComponent<Renderer>().sharedMaterial = stripe;
            root.transform.Find("Team Identity").gameObject.SetActive(false);
            foreach (Transform station in root.transform.Find("Pit Equipment"))
            {
                if (station.name.StartsWith("Engineering Station",StringComparison.Ordinal) && station.localPosition.x < 0) station.gameObject.SetActive(false);
                var display = station.Find("Station Display"); if (display != null) display.gameObject.SetActive(false);
            }
            foreach (Transform item in root.transform.Find("Service Bay Markings")) if (item.name.StartsWith("Safety Chevron",StringComparison.Ordinal)) item.gameObject.SetActive(false);
            var lights = root.transform.Find("Lighting");
            var key = lights.Find("Main Softbox").GetComponent<Light>(); key.intensity = .85f; key.color = new Color(1,.97f,.94f); key.transform.localRotation = Quaternion.Euler(62,-32,0); key.shadowStrength = .78f;
            Spot(lights,"Front Fill",new Vector3(3.5f,4.5f,4),22,75);
            Spot(lights,"Rear Fill",new Vector3(-4,3.5f,-3.2f),28,70);
            Spot(lights,"Contour Fill",new Vector3(4,1.6f,-2.8f),16,65);
            foreach (Transform item in lights) if (item.name.StartsWith("Overhead LED",StringComparison.Ordinal)) item.localScale = new Vector3(7.4f,.04f,1);
            var probe = root.GetComponentInChildren<ReflectionProbe>();
            const string reflection = "Assets/Formula/Garage/Garage Reflection.exr";
            probe.bakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(reflection);
            PrefabUtility.SaveAsPrefabAsset(root,StagePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void Spot(Transform parent,string name,Vector3 position,float intensity,float angle)
    {
        var child = parent.Find(name); if (child == null) { child = new GameObject(name).transform; child.SetParent(parent,false); }
        child.localPosition = position; child.localRotation = Quaternion.LookRotation(new Vector3(0,.45f,.2f)-position);
        var light = child.GetComponent<Light>(); if (light == null) light = child.gameObject.AddComponent<Light>();
        light.type = LightType.Spot; light.color = Color.white; light.intensity = intensity; light.range = 12; light.spotAngle = angle; light.innerSpotAngle = angle*.65f; light.shadows = LightShadows.None;
    }
    private static Material Material(string name,Color color,float metallic,float smoothness)
    {
        string path = "Assets/Formula/Garage/Materials/"+name+".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,path); }
        material.SetColor("_BaseColor",color); material.SetFloat("_Metallic",metallic); material.SetFloat("_Smoothness",smoothness); EditorUtility.SetDirty(material); return material;
    }
    private static void RefineScene(string path)
    {
        var scene = SceneManager.GetSceneByPath(path); bool loaded = scene.IsValid() && scene.isLoaded;
        if (!loaded) scene = EditorSceneManager.OpenScene(path,OpenSceneMode.Additive); SceneManager.SetActiveScene(scene);
        try
        {
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.16f,.11f,.10f); RenderSettings.skybox = null;
            if(path=="Assets/Scenes/Menu.unity")
            {
                // Reflection baking needs the real loaded scene, rather than the prefab
                // isolation scene, so the capture contains this studio's actual geometry.
                var stage=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ReflectionProbe>(true)).Single().transform.parent.gameObject;
                const string reflection="Assets/Formula/Garage/Garage Reflection.exr";
                var probe=stage.GetComponentInChildren<ReflectionProbe>();
                FormulaGarageBuilder.BakeStageReflection(stage,probe,reflection); AssetDatabase.ImportAsset(reflection);
                probe.bakedTexture=AssetDatabase.LoadAssetAtPath<Cubemap>(reflection);
            }
            var preview = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FormulaGarageVehiclePreview>(true)).Single();
            var parked = (GameObject)new SerializedObject(preview).FindProperty("editorVehicle").objectReferenceValue;
            if (parked != null)
            {
                string paintedName = FormulaAssets.Current.vehiclePrefab.GetComponent<FormulaVehicle>().body.name;
                foreach (var renderer in parked.GetComponentsInChildren<MeshRenderer>())
                {
                    bool painted=renderer.name==paintedName, rim=renderer.name.StartsWith("Generic_Rim",StringComparison.Ordinal), tyre=renderer.name.StartsWith("Generic_Tyre",StringComparison.Ordinal);
                    if (!painted && !rim && !tyre) continue;
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(DisplayMaterial).ToArray();
                }
            }
            preview.Present(path==FormulaGarageBuilder.GarageScene ? "Garaj" : "Acasă","Livree",true);
            var camera = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).Single(c=>c.CompareTag("MainCamera"));
            var data = camera.GetComponent<UniversalAdditionalCameraData>(); if (data != null) { data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; data.antialiasingQuality = AntialiasingQuality.High; }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        finally { if (!loaded) EditorSceneManager.CloseScene(scene,true); }
    }
    private static Material DisplayMaterial(Material source)
    {
        const string folder="Assets/Formula/Garage/Materials/Vehicle";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Formula/Garage/Materials","Vehicle");
        string sourcePath=AssetDatabase.GetAssetPath(source), filename=Path.GetFileNameWithoutExtension(sourcePath);
        if (filename.EndsWith("_Display",StringComparison.Ordinal))
        {
            var original=AssetDatabase.LoadAssetAtPath<Material>("Assets/Formula/RCCP/Materials/"+filename.Replace("_Display","")+".mat");
            if(original!=null) { source.shader=original.shader; source.CopyPropertiesFromMaterial(original); EditorUtility.SetDirty(source); }
            return source;
        }
        string path=folder+"/"+filename+"_Display.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material==null) { material=new Material(source); AssetDatabase.CreateAsset(material,path); }
        material.shader=source.shader; material.CopyPropertiesFromMaterial(source); EditorUtility.SetDirty(material); return material;
    }
    private static RectTransform Group(Transform parent,string name,float x,float y,float w,float h)
    {
        var rect = parent.Find(name) as RectTransform ?? FormulaUI.Rect(parent,name,x,y,w,h); Place(rect,x,y,w,h); return rect;
    }
    private static void Place(Transform target,float x,float y,float w,float h) => FormulaMenuTheme.Place((RectTransform)target,x,y,w,h);
    private static TMP_Text Text(Transform parent,string name,string value,float x,float y,float w,float h,int size)
    {
        var text = parent.Find(name)?.GetComponent<TMP_Text>() ?? FormulaMenuStyle.Text(parent,value,x,y,w,h,size); text.name=name; text.text=value; Format(text,x,y,w,h,size); return text;
    }
    private static void Format(TMP_Text text,float x,float y,float w,float h,int size)
    {
        Place(text.transform,x,y,w,h); text.font=FormulaMenuAssets.Current.bodyFont; text.fontSize=text.fontSizeMin=text.fontSizeMax=size; text.enableAutoSizing=false; text.color=Color.white; text.textWrappingMode=TextWrappingModes.NoWrap; text.overflowMode=TextOverflowModes.Overflow; text.alignment=TextAlignmentOptions.MidlineLeft; text.raycastTarget=false;
    }
}
