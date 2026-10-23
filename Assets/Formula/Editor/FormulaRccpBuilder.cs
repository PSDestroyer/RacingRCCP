using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class FormulaRccpBuilder
{
    public const string Output = "Assets/Formula/RCCP";
    private const string A1Source = "Assets/Formula/Formula_A1/Prefabs/Formula_A1_01.prefab";
    private const string GenericSource = "Assets/Formula/formula-1-generico-2/source/QJPZBX4SFB2TFZU5B6KC4U43X_obj/QJPZBX4SFB2TFZU5B6KC4U43X.obj";
    private const string Donor = "Assets/_GarageV2/Resources/Cars/F1.prefab";
    private static readonly Dictionary<Material, Material> Materials = new Dictionary<Material, Material>();

    [MenuItem("Tools/Formula RCCP/Enable Automatic Reverse and Test")]
    public static void EnableAutomaticReverseAndTest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before updating Formula prefabs.");

        // Migrate only input settings; preserve existing handling, visuals and
        // scene references instead of rebuilding the generated vehicles.
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Output + "/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                RCCP_Input input = root.GetComponentInChildren<RCCP_Input>(true);
                if (input == null) throw new InvalidOperationException("Missing Formula input: " + path);
                if (input.autoReverse && input.inverseThrottleBrakeOnReverse) continue;
                input.autoReverse = true;
                input.inverseThrottleBrakeOnReverse = true;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        FormulaRccpDrivingValidation.Run();
    }

    [MenuItem("Tools/Formula RCCP/Build and Test Both Models")]
    public static void BuildAndTestBothModels()
    {
        BuildBothModels();
        FormulaRccpDrivingValidation.Run();
    }

    [MenuItem("Tools/Formula RCCP/Build Both Models")]
    public static void BuildBothModels()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before building Formula prefabs.");

        foreach (string folder in new[] { "Prefabs/A1", "Prefabs/Generic", "Materials", "Meshes", "Scenes" })
            Directory.CreateDirectory(Output + "/" + folder);
        AssetDatabase.Refresh();
        Materials.Clear();
        Scene preview = EditorSceneManager.NewPreviewScene();
        var created = new List<string>();
        try
        {
            for (int index = 1; index <= 14; index++)
            {
                string name = "Formula_A1_RCCP_" + index.ToString("00");
                string source = "Assets/Formula/Formula_A1/Prefabs/Formula_A1_" + index.ToString("00") + ".prefab";
                GameObject root = CreateRoot(name, preview);
                try
                {
                    GameObject visual = AddVisual(root, source);
                    ConvertMaterials(visual);
                    var wheels = new Transform[4];
                    string[] names = { "A1_TLF", "A1_TRF", "A1_TLR", "A1_TRR" };
                    for (int i = 0; i < wheels.Length; i++)
                    {
                        Transform original = Find(visual.transform, names[i]);
                        wheels[i] = Child(visual.transform, "WheelPivot_" + i);
                        wheels[i].position = original.GetComponent<Renderer>().bounds.center;
                        original.SetParent(wheels[i], true);
                    }
                    ConfigureVehicle(root, wheels, Find(visual.transform, "A1_Stearingwl"));
                    SaveVehicle(root, Output + "/Prefabs/A1/" + name + ".prefab", created);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }

            GameObject generic = CreateRoot("Formula_Generic_RCCP_01", preview);
            try
            {
                GameObject visual = AddVisual(generic, GenericSource);
                visual.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                Bounds bounds = VisualBounds(visual);
                visual.transform.localPosition += Vector3.up * -bounds.min.y;
                ConvertMaterials(visual);
                Transform[] wheels = SplitGenericWheels(generic, visual);
                ConfigureVehicle(generic, wheels, null);
                var body = Find(visual.transform, "Object_Material_1_3").GetComponent<MeshRenderer>();
                string materialFolder = Path.GetDirectoryName(GenericSource).Replace('\\', '/');
                for (int index = 0; index < 8; index++)
                {
                    string materialPath = materialFolder + "/Material_1" + (index == 0 ? "" : " " + index) + ".mat";
                    body.sharedMaterial = ConvertMaterial(AssetDatabase.LoadAssetAtPath<Material>(materialPath));
                    generic.name = "Formula_Generic_RCCP_" + (index + 1).ToString("00");
                    SaveVehicle(generic, Output + "/Prefabs/Generic/" + generic.name + ".prefab", created);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(generic); }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }

        AssetDatabase.SaveAssets();
        CreateTestScene(created[0], "Formula_A1_TestDrive");
        CreateTestScene(created[14], "Formula_Generic_TestDrive");
        ValidatePrefabs();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(created[0]);
        Debug.Log("FORMULA_RCCP_BUILD_COMPLETE: " + created.Count + " prefabs and two test-drive scenes.");
    }

    private static GameObject CreateRoot(string name, Scene scene)
    {
        var root = new GameObject(name);
        SceneManager.MoveGameObjectToScene(root, scene);
        return root;
    }

    private static GameObject AddVisual(GameObject root, string source)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(source);
        if (asset == null)
            throw new InvalidOperationException("Missing source model: " + source);
        GameObject visual = UnityEngine.Object.Instantiate(asset, root.transform, false);
        visual.name = "Visuals";
        return visual;
    }

    private static Transform Child(Transform parent, string name)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static Transform Find(Transform root, string name)
    {
        Transform match = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        if (match == null)
            throw new InvalidOperationException("Model is missing part " + name);
        return match;
    }

    private static Bounds VisualBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            throw new InvalidOperationException("Model has no renderers: " + root.name);
        Bounds result = renderers[0].bounds;
        foreach (Renderer renderer in renderers)
            result.Encapsulate(renderer.bounds);
        return result;
    }

    private static void ConvertMaterials(GameObject root)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterials = renderer.sharedMaterials.Select(ConvertMaterial).ToArray();
    }

    private static Material ConvertMaterial(Material source)
    {
        if (source == null)
            throw new InvalidOperationException("Formula model contains a missing material.");
        if (Materials.TryGetValue(source, out Material existing))
            return existing;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("URP Lit shader is unavailable.");
        var material = new Material(shader) { name = source.name + "_URP", enableInstancing = true };
        material.SetTexture("_BaseMap", source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : source.GetTexture("_MainTex"));
        material.SetColor("_BaseColor", source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color);
        if (source.HasProperty("_MainTex"))
        {
            material.SetTextureScale("_BaseMap", source.GetTextureScale("_MainTex"));
            material.SetTextureOffset("_BaseMap", source.GetTextureOffset("_MainTex"));
        }
        foreach (string property in new[] { "_BumpMap", "_MetallicGlossMap", "_OcclusionMap", "_EmissionMap" })
            if (source.HasProperty(property)) material.SetTexture(property, source.GetTexture(property));
        foreach (string property in new[] { "_Metallic", "_BumpScale", "_OcclusionStrength", "_Cutoff" })
            if (source.HasProperty(property)) material.SetFloat(property, source.GetFloat(property));
        material.SetFloat("_Smoothness", source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : 0.45f);
        if (material.GetTexture("_BumpMap") != null) material.EnableKeyword("_NORMALMAP");
        if (material.GetTexture("_MetallicGlossMap") != null) material.EnableKeyword("_METALLICSPECGLOSSMAP");
        if (material.GetTexture("_OcclusionMap") != null) material.EnableKeyword("_OCCLUSIONMAP");
        if (source.IsKeywordEnabled("_EMISSION") && source.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", source.GetColor("_EmissionColor"));
            material.EnableKeyword("_EMISSION");
        }
        if (source.HasProperty("_Mode") && Mathf.RoundToInt(source.GetFloat("_Mode")) == 1)
        {
            material.SetFloat("_AlphaClip", 1f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.AlphaTest;
        }
        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
        string path = Output + "/Materials/" + source.name.Replace('/', '_') + "_" + guid.Substring(0, 8) + ".mat";
        material = SaveAsset(material, path);
        Materials[source] = material;
        return material;
    }

    private static T SaveAsset<T>(T asset, string path) where T : UnityEngine.Object
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(asset, existing);
            UnityEngine.Object.DestroyImmediate(asset);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    // The OBJ is grouped by material, not by moving parts. Extract its four tyres
    // and matching rim triangles without changing the imported source geometry.
    private static Transform[] SplitGenericWheels(GameObject root, GameObject visual)
    {
        MeshFilter tyres = Find(visual.transform, "Object_Material_4_1").GetComponent<MeshFilter>();
        MeshFilter hardware = Find(visual.transform, "Object_Material_3_0").GetComponent<MeshFilter>();
        List<List<int>> tyreParts = ConnectedTriangles(tyres.sharedMesh);
        if (tyreParts.Count != 4)
            throw new InvalidOperationException("Expected four disconnected OBJ tyres; found " + tyreParts.Count);

        Vector3[] vertices = tyres.sharedMesh.vertices;
        var parts = tyreParts.Select(indices => new
        {
            indices,
            bounds = BoundsOf(indices.Select(i => tyres.transform.TransformPoint(vertices[i])))
        }).OrderByDescending(p => p.bounds.center.z).ToArray();
        // Sort each axle left-to-right in the normalized vehicle coordinate frame.
        var ordered = parts.Take(2).OrderBy(p => p.bounds.center.x)
            .Concat(parts.Skip(2).OrderBy(p => p.bounds.center.x)).ToArray();
        var wheels = new Transform[4];
        for (int i = 0; i < 4; i++)
        {
            wheels[i] = Child(root.transform, "WheelPivot_" + i);
            wheels[i].position = ordered[i].bounds.center;
            AddExtractedPart(tyres, ordered[i].indices, wheels[i], "Generic_Tyre_" + i);
        }

        var moving = Enumerable.Range(0, 4).Select(_ => new List<int>()).ToArray();
        var stationary = new List<int>();
        foreach (List<int> component in ConnectedTriangles(hardware.sharedMesh))
        {
            Vector3[] points = hardware.sharedMesh.vertices;
            Bounds bounds = BoundsOf(component.Select(i => hardware.transform.TransformPoint(points[i])));
            int closest = Enumerable.Range(0, 4).OrderBy(i => (wheels[i].position - bounds.center).sqrMagnitude).First();
            Bounds tyreBounds = ordered[closest].bounds;
            Vector3 delta = bounds.center - tyreBounds.center;
            bool rotates = new Vector2(delta.y, delta.z).magnitude < tyreBounds.extents.y * 0.85f
                && Mathf.Abs(delta.x) < tyreBounds.extents.x + 0.12f
                && bounds.size.y < tyreBounds.size.y * 1.1f
                && bounds.size.z < tyreBounds.size.z * 1.1f;
            (rotates ? moving[closest] : stationary).AddRange(component);
        }
        for (int i = 0; i < 4; i++)
        {
            if (moving[i].Count == 0) throw new InvalidOperationException("Missing OBJ wheel hardware " + i);
            AddExtractedPart(hardware, moving[i], wheels[i], "Generic_Rim_" + i);
        }
        if (stationary.Count > 0)
        {
            Transform holder = Child(root.transform, "StationaryBrakeHardware");
            AddExtractedPart(hardware, stationary, holder, "Generic_StationaryHardware");
        }
        int sourceIndices = hardware.sharedMesh.triangles.Length;
        if (moving.Sum(x => x.Count) + stationary.Count != sourceIndices)
            throw new InvalidOperationException("OBJ hardware triangle conservation failed.");
        UnityEngine.Object.DestroyImmediate(tyres.gameObject);
        UnityEngine.Object.DestroyImmediate(hardware.gameObject);
        return wheels;
    }

    private static Bounds BoundsOf(IEnumerable<Vector3> points)
    {
        using (IEnumerator<Vector3> iterator = points.GetEnumerator())
        {
            if (!iterator.MoveNext()) throw new InvalidOperationException("Empty geometry.");
            var bounds = new Bounds(iterator.Current, Vector3.zero);
            while (iterator.MoveNext()) bounds.Encapsulate(iterator.Current);
            return bounds;
        }
    }

    private static List<List<int>> ConnectedTriangles(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        int[] parents = Enumerable.Range(0, vertices.Length).ToArray();
        var positions = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i] * 100000f;
            var key = new Vector3Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z));
            if (positions.TryGetValue(key, out int match)) parents[FindRoot(parents, i)] = FindRoot(parents, match);
            else positions.Add(key, i);
        }
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int root = FindRoot(parents, triangles[i]);
            parents[FindRoot(parents, triangles[i + 1])] = root;
            parents[FindRoot(parents, triangles[i + 2])] = root;
        }
        var groups = new Dictionary<int, List<int>>();
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int key = FindRoot(parents, triangles[i]);
            if (!groups.TryGetValue(key, out List<int> group)) groups.Add(key, group = new List<int>());
            group.Add(triangles[i]); group.Add(triangles[i + 1]); group.Add(triangles[i + 2]);
        }
        return groups.Values.ToList();
    }

    private static int FindRoot(int[] parents, int index)
    {
        while (parents[index] != index)
        {
            parents[index] = parents[parents[index]];
            index = parents[index];
        }
        return index;
    }

    private static void AddExtractedPart(MeshFilter source, List<int> indices, Transform parent, string name)
    {
        Vector3[] vertices = source.sharedMesh.vertices;
        Vector3[] normals = source.sharedMesh.normals;
        Vector2[] uv = source.sharedMesh.uv;
        Vector4[] tangents = source.sharedMesh.tangents;
        var remap = new Dictionary<int, int>();
        var outputVertices = new List<Vector3>();
        var outputNormals = new List<Vector3>();
        var outputUv = new List<Vector2>();
        var outputTangents = new List<Vector4>();
        var triangles = new List<int>();
        Matrix4x4 matrix = parent.worldToLocalMatrix * source.transform.localToWorldMatrix;
        foreach (int index in indices)
        {
            if (!remap.TryGetValue(index, out int mapped))
            {
                remap.Add(index, mapped = outputVertices.Count);
                outputVertices.Add(matrix.MultiplyPoint3x4(vertices[index]));
                if (normals.Length == vertices.Length) outputNormals.Add(matrix.MultiplyVector(normals[index]).normalized);
                if (uv.Length == vertices.Length) outputUv.Add(uv[index]);
                if (tangents.Length == vertices.Length)
                {
                    Vector3 direction = matrix.MultiplyVector(tangents[index]).normalized;
                    outputTangents.Add(new Vector4(direction.x, direction.y, direction.z, tangents[index].w));
                }
            }
            triangles.Add(mapped);
        }
        var mesh = new Mesh { name = name, indexFormat = outputVertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        mesh.SetVertices(outputVertices);
        if (outputNormals.Count > 0) mesh.SetNormals(outputNormals);
        if (outputUv.Count > 0) mesh.SetUVs(0, outputUv);
        if (outputTangents.Count > 0) mesh.SetTangents(outputTangents);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        if (outputNormals.Count == 0) mesh.RecalculateNormals();
        mesh = SaveAsset(mesh, Output + "/Meshes/" + name + ".asset");
        Transform part = Child(parent, name);
        part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        part.gameObject.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<Renderer>().sharedMaterials;
    }

    private static void ConfigureVehicle(GameObject root, Transform[] wheels, Transform steeringWheel)
    {
        var car = root.AddComponent<RCCP_CarController>();
        Rigidbody rigid = root.GetComponent<Rigidbody>();
        rigid.mass = 820f;
        rigid.linearDamping = 0.0025f;
        rigid.angularDamping = 0.7f;
        rigid.interpolation = RigidbodyInterpolation.Interpolate;
        rigid.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        car.ineffectiveBehavior = true;
        car.canControl = true;
        RCCP_CreateNewVehicle.AddAllComponents(car);
        RCCP_CreateNewVehicle.AddInputs(car);
        RCCP_CreateNewVehicle.AddAero(car);
        RCCP_CreateNewVehicle.AddStability(car);
        RCCP_CreateNewVehicle.AddAudio(car);
        RCCP_CreateNewVehicle.AddLights(car);
        RCCP_CreateNewVehicle.AddDamage(car);
        RCCP_CreateNewVehicle.AddParticles(car);
        RCCP_CreateNewVehicle.AddOtherAddons(car);
        car.GetAllComponents();

        RCCP_Axle[] axles = root.GetComponentsInChildren<RCCP_Axle>(true);
        RCCP_Axle front = axles.Single(a => a.name == "RCCP_Axle_Front");
        RCCP_Axle rear = axles.Single(a => a.name == "RCCP_Axle_Rear");
        ConfigureAxle(front, wheels[0], wheels[1], false);
        ConfigureAxle(rear, wheels[2], wheels[3], true);
        RCCP_Differential differential = root.GetComponentInChildren<RCCP_Differential>();
        differential.connectedAxle = rear;
        differential.differentialType = RCCP_Differential.DifferentialType.Limited;
        differential.limitedSlipRatio = 55f;

        RCCP_Gearbox gearbox = root.GetComponentInChildren<RCCP_Gearbox>();
        gearbox.gearRatios = new[] { 3f, 2.4f, 1.95f, 1.65f, 1.42f, 1.24f, 1.1f, 0.98f };
        gearbox.transmissionType = RCCP_Gearbox.TransmissionType.Automatic;
        gearbox.shiftingTime = 0.08f;
        gearbox.shiftUpRPM = 11400f;
        gearbox.shiftDownRPM = 6500f;
        gearbox.shiftThreshold = 0.92f;
        var engine = root.GetComponentInChildren<RCCP_Engine>();
        engine.minEngineRPM = 1800f;
        engine.maxEngineRPM = 12000f;
        engine.peakRPM = 9500f;
        engine.maximumTorqueAsNM = 480f;
        engine.maximumSpeed = 340f;
        engine.engineRunning = true;
        engine.engineInertia = 0.12f;
        engine.engineFriction = 0.15f;
        engine.turboCharged = false;
        engine.autoCreateNMCurve = false;
        engine.NMCurve = new AnimationCurve(new Keyframe(1800f, 0.45f), new Keyframe(6000f, 0.8f), new Keyframe(9500f, 1f), new Keyframe(12000f, 0.86f));
        differential.finalDriveRatio = engine.maxEngineRPM * (2f * Mathf.PI * rear.leftWheelCollider.WheelCollider.radius) * 3.6f
            / (60f * gearbox.gearRatios[7] * engine.maximumSpeed);

        var input = root.GetComponentInChildren<RCCP_Input>();
        input.autoReverse = true;
        input.inverseThrottleBrakeOnReverse = true;
        input.counterSteering = true;
        input.counterSteerFactor = 0.35f;
        input.steeringLimiter = true;
        input.steeringCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(80f, 0.55f), new Keyframe(180f, 0.27f), new Keyframe(340f, 0.14f));
        var stability = root.GetComponentInChildren<RCCP_Stability>();
        stability.ABS = true; stability.TCS = true; stability.ESP = true;
        stability.steerHelperStrength = 0.25f;
        stability.tractionHelperStrength = 0.2f;
        stability.angularDragHelperStrength = 0.1f;
        var aero = root.GetComponentInChildren<RCCP_AeroDynamics>();
        aero.downForce = 6f;
        aero.airResistance = 12f;
        aero.wheelResistance = 0.5f;
        aero.autoReset = true;
        Transform com = Child(aero.transform, "COM");
        com.position = root.transform.TransformPoint(new Vector3(0f, 0.25f, -0.15f));
        aero.COM = com;

        Bounds bounds = VisualBounds(root);
        AddBodyCollider(root.transform, "Chassis", new Vector3(0f, 0.27f, -0.1f), new Vector3(0.65f, 0.45f, 3.65f));
        AddBodyCollider(root.transform, "FrontWing", new Vector3(0f, 0.13f, bounds.max.z - 0.2f), new Vector3(bounds.size.x * 0.92f, 0.12f, 0.32f));
        AddBodyCollider(root.transform, "RearWing", new Vector3(0f, 0.82f, bounds.min.z + 0.2f), new Vector3(bounds.size.x * 0.8f, 0.18f, 0.35f));
        ConfigureAudio(car);
        ConfigureLights(car, bounds);
        ConfigureCameras(car, wheels, steeringWheel);
        var damage = root.GetComponentInChildren<RCCP_Damage>();
        damage.automaticInstallation = false;
        damage.meshDeformation = false;
        damage.meshFilters = Array.Empty<MeshFilter>();
        damage.wheels = root.GetComponentsInChildren<RCCP_WheelCollider>(true);
        damage.lights = root.GetComponentsInChildren<RCCP_Light>(true);
        damage.parts = Array.Empty<RCCP_DetachablePart>();
        damage.wheelDamage = true;
        damage.wheelDetachment = false;
        damage.lightDamage = true;
        damage.partDamage = false;
        var donor = AssetDatabase.LoadAssetAtPath<GameObject>(Donor);
        RCCP_Particles sourceParticles = donor.GetComponentInChildren<RCCP_Particles>(true);
        var particles = root.GetComponentInChildren<RCCP_Particles>();
        particles.contactSparklePrefab = sourceParticles.contactSparklePrefab;
        particles.scratchSparklePrefab = sourceParticles.scratchSparklePrefab;
        particles.wheelSparklePrefab = sourceParticles.wheelSparklePrefab;
        RCCP_Exhaust sourceExhaust = donor.GetComponentInChildren<RCCP_Exhaust>(true);
        if (sourceExhaust != null)
        {
            Transform exhausts = Child(root.GetComponentInChildren<RCCP_OtherAddons>().transform, "Exhausts");
            exhausts.gameObject.AddComponent<RCCP_Exhausts>();
            var exhaust = UnityEngine.Object.Instantiate(sourceExhaust, exhausts, false);
            exhaust.name = "Formula_Exhaust";
            exhaust.transform.localPosition = new Vector3(0f, 0.48f, bounds.min.z + 0.5f);
            exhaust.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }
        car.GetAllComponents();
        int vehicleLayer = LayerMask.NameToLayer(RCCP_Settings.Instance.RCCPLayer);
        int wheelLayer = LayerMask.NameToLayer(RCCP_Settings.Instance.RCCPWheelColliderLayer);
        if (vehicleLayer < 0 || wheelLayer < 0)
            throw new InvalidOperationException("The project's RCCP vehicle/wheel physics layers are missing.");
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            transform.gameObject.layer = transform.GetComponent<WheelCollider>() == null ? vehicleLayer : wheelLayer;
    }

    private static void ConfigureAxle(RCCP_Axle axle, Transform left, Transform right, bool powered)
    {
        axle.autoAlignWheelColliders = false;
        axle.leftWheelModel = left; axle.rightWheelModel = right;
        axle.isPower = powered; axle.isSteer = !powered;
        axle.isBrake = true; axle.isHandbrake = false;
        axle.maxSteerAngle = powered ? 0f : 28f;
        axle.steerSpeed = 3f;
        axle.maxBrakeTorque = 2800f;
        axle.brakeMultiplier = powered ? 0.8f : 1f;
        axle.antirollForce = 7000f;
        RCCP_WheelCollider[] colliders = { axle.leftWheelCollider, axle.rightWheelCollider };
        Transform[] models = { left, right };
        for (int i = 0; i < 2; i++)
        {
            RCCP_WheelCollider wheel = colliders[i];
            Bounds bounds = VisualBounds(models[i].gameObject);
            wheel.wheelModel = models[i];
            wheel.connectedAxle = axle;
            wheel.grip = 1.15f;
            wheel.driftMode = false;
            wheel.transform.position = models[i].position + Vector3.up * 0.045f;
            wheel.transform.rotation = axle.transform.root.rotation;
            WheelCollider collider = wheel.WheelCollider;
            collider.radius = bounds.extents.y;
            collider.mass = 20f;
            collider.suspensionDistance = 0.09f;
            collider.forceAppPointDistance = 0.05f;
            collider.suspensionSpring = new JointSpring { spring = powered ? 80000f : 70000f, damper = 8500f, targetPosition = 0.5f };
            collider.forwardFriction = new WheelFrictionCurve { extremumSlip = 0.3f, extremumValue = 1f, asymptoteSlip = 0.8f, asymptoteValue = 0.75f, stiffness = 1f };
            collider.sidewaysFriction = new WheelFrictionCurve { extremumSlip = 0.2f, extremumValue = 1f, asymptoteSlip = 0.6f, asymptoteValue = 0.8f, stiffness = 1f };
        }
    }

    private static void AddBodyCollider(Transform root, string name, Vector3 position, Vector3 size)
    {
        Transform child = Child(root, "Collider_" + name);
        child.localPosition = position;
        child.gameObject.AddComponent<BoxCollider>().size = size;
    }

    private static void ConfigureAudio(RCCP_CarController car)
    {
        var donor = AssetDatabase.LoadAssetAtPath<GameObject>(Donor).GetComponentInChildren<RCCP_Audio>(true);
        var audio = car.GetComponentInChildren<RCCP_Audio>();
        EditorUtility.CopySerialized(donor, audio);
        foreach (RCCP_Audio.EngineSound sound in audio.engineSounds)
        {
            sound.localPosition = new Vector3(0f, 0.55f, -0.9f);
            sound.minDistance = 5f;
            sound.maxDistance = 100f;
            sound.maxVolume = 0.8f;
        }
    }

    private static void ConfigureLights(RCCP_CarController car, Bounds bounds)
    {
        RCCP_Lights manager = car.GetComponentInChildren<RCCP_Lights>();
        manager.lowBeamHeadlights = true;
        Material source = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        source.name = "Formula_Rear_LED";
        source.SetColor("_BaseColor", new Color(0.2f, 0.005f, 0.005f));
        source.SetColor("_EmissionColor", Color.black);
        source.EnableKeyword("_EMISSION");
        Material material = SaveAsset(source, Output + "/Materials/Formula_Rear_LED.mat");
        for (int i = 0; i < 3; i++)
        {
            GameObject lens = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lens.name = i == 0 ? "Rear_Brake_LED" : "Rear_Safety_LED_" + i;
            lens.transform.SetParent(manager.transform, false);
            lens.transform.localPosition = i == 0 ? new Vector3(0f, 0.35f, bounds.min.z - 0.015f)
                : new Vector3(i == 1 ? -0.55f : 0.55f, 0.85f, bounds.min.z + 0.02f);
            lens.transform.localScale = i == 0 ? new Vector3(0.14f, 0.075f, 0.035f) : new Vector3(0.12f, 0.04f, 0.025f);
            UnityEngine.Object.DestroyImmediate(lens.GetComponent<Collider>());
            MeshRenderer renderer = lens.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var light = lens.AddComponent<Light>();
            light.type = UnityEngine.LightType.Point; light.color = Color.red; light.range = 2f; light.intensity = 0f;
            light.shadows = LightShadows.None;
            var rccp = lens.AddComponent<RCCP_Light>();
            rccp.lightType = i == 0 ? RCCP_Light.LightType.Brakelight : RCCP_Light.LightType.Taillight;
            rccp.emissiveRenderer = renderer;
            rccp.emissiveColor = new Color(6f, 0.025f, 0.005f);
            rccp.intensity = i == 0 ? 1.2f : 0.5f;
            rccp.useLensFlares = false;
        }
    }

    private static void ConfigureCameras(RCCP_CarController car, Transform[] wheels, Transform steeringWheel)
    {
        Transform addons = car.GetComponentInChildren<RCCP_OtherAddons>().transform;
        var cameras = Child(addons, "Cameras").gameObject.AddComponent<RCCP_Exterior_Cameras>();
        Transform onboard = Child(cameras.transform, "Onboard");
        onboard.localPosition = new Vector3(0f, 0.88f, 0.15f);
        cameras._hoodCamera = onboard.gameObject.AddComponent<RCCP_HoodCamera>();
        Transform wheelCamera = Child(cameras.transform, "WheelView");
        wheelCamera.position = wheels[1].position + new Vector3(0.35f, 0.25f, -0.7f);
        cameras._wheelCamera = wheelCamera.gameObject.AddComponent<RCCP_WheelCamera>();
        Child(addons, "Recorder").gameObject.AddComponent<RCCP_Recorder>();
        if (steeringWheel != null)
        {
            var dashboard = Child(addons, "SteeringWheelAnimation").gameObject.AddComponent<RCCP_Visual_Dashboard>();
            dashboard.steeringWheel = steeringWheel;
            dashboard.steeringAngleMultiplier = 4f;
            dashboard.steeringWheelRotateAround = RCCP_Visual_Dashboard.SteeringWheelRotateAround.ZAxis;
        }
    }

    private static void SaveVehicle(GameObject root, string path, List<string> saved)
    {
        if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
            throw new InvalidOperationException("Unable to save Formula prefab: " + path);
        saved.Add(path);
    }

    private static void CreateTestScene(string prefabPath, string name)
    {
        Scene original = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var services = new GameObject("RCCP Services");
            services.AddComponent<RCCP_SceneManager>().registerLastVehicleAsPlayer = true;
            services.AddComponent<RCCP_InputManager>();
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Test Pad";
            ground.transform.position = new Vector3(0f, -0.1f, 150f);
            ground.transform.localScale = new Vector3(200f, 0.2f, 600f);
            var surface = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Formula_Test_Asphalt" };
            surface.color = new Color(0.19f, 0.2f, 0.22f);
            ground.GetComponent<Renderer>().sharedMaterial = SaveAsset(surface, Output + "/Materials/Formula_Test_Asphalt.mat");
            GameObject car = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), scene);
            car.transform.position = new Vector3(0f, 0.15f, 0f);
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = UnityEngine.LightType.Directional; light.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            var cameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Realistic Car Controller Pro/Prefabs/Cameras/RCCP_Camera.prefab");
            if (cameraPrefab == null) throw new InvalidOperationException("RCCP follow-camera prefab is missing.");
            PrefabUtility.InstantiatePrefab(cameraPrefab, scene);
            EditorSceneManager.SaveScene(scene, Output + "/Scenes/" + name + ".unity");
        }
        finally
        {
            SceneManager.SetActiveScene(original);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [MenuItem("Tools/Formula RCCP/Validate Prefabs")]
    public static void ValidatePrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { Output + "/Prefabs" });
        var output = new StringBuilder();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            RCCP_CarController car = asset.GetComponent<RCCP_CarController>();
            if (car == null || asset.GetComponentsInChildren<RCCP_WheelCollider>(true).Length != 4)
                throw new InvalidOperationException("Invalid RCCP controller/wheels: " + path);
            foreach (RCCP_WheelCollider wheel in asset.GetComponentsInChildren<RCCP_WheelCollider>(true))
                if (wheel.wheelModel == null || wheel.connectedAxle == null || wheel.WheelCollider.radius < 0.2f)
                    throw new InvalidOperationException("Invalid wheel setup: " + path);
            foreach (Transform transform in asset.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                    throw new InvalidOperationException("Missing script in " + path);
            if (asset.GetComponentInChildren<RCCP_Lod>(true) != null || asset.GetComponentInChildren<RCCP_Nos>(true) != null)
                throw new InvalidOperationException("Unexpected physics LOD or NOS in Formula prefab.");
            RCCP_Engine engine = asset.GetComponentInChildren<RCCP_Engine>(true);
            RCCP_Clutch clutch = asset.GetComponentInChildren<RCCP_Clutch>(true);
            RCCP_Gearbox gearbox = asset.GetComponentInChildren<RCCP_Gearbox>(true);
            RCCP_Input input = asset.GetComponentInChildren<RCCP_Input>(true);
            if (input == null || !input.autoReverse || !input.inverseThrottleBrakeOnReverse
                || gearbox.transmissionType != RCCP_Gearbox.TransmissionType.Automatic)
                throw new InvalidOperationException("Automatic reverse is not configured: " + path);
            if (engine.outputEvent.GetPersistentEventCount() != 1 || clutch.outputEvent.GetPersistentEventCount() != 1
                || gearbox.outputEvent.GetPersistentEventCount() != 1)
                throw new InvalidOperationException("Drivetrain events are not wired: " + path);
            if (asset.GetComponentsInChildren<RCCP_Light>(true).Length != 3)
                throw new InvalidOperationException("Missing rear lighting: " + path);
            RCCP_Audio audio = asset.GetComponentInChildren<RCCP_Audio>(true);
            if (audio.audioMixer == null || audio.engineSounds.Any(s => s.audioClipOn == null || s.audioClipOff == null))
                throw new InvalidOperationException("Audio is incomplete: " + path);
            foreach (Renderer renderer in asset.GetComponentsInChildren<Renderer>(true))
                if (renderer.sharedMaterials.Any(m => m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader"))
                    throw new InvalidOperationException("Missing material/shader: " + path);
            output.AppendLine("PASS " + path);
        }
        if (guids.Length != 22) throw new InvalidOperationException("Expected 22 Formula livery prefabs, found " + guids.Length);
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "formula-rccp-prefab-validation.txt"), output.ToString());
        Debug.Log("FORMULA_RCCP_PREFAB_VALIDATION_PASSED: " + guids.Length);
    }

    [MenuItem("Tools/Formula RCCP/Inspect Sources")]
    public static void InspectSources()
    {
        var output = new StringBuilder();
        foreach (string path in new[] { A1Source, GenericSource })
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null)
                throw new InvalidOperationException("Missing Formula source: " + path);

            var instance = UnityEngine.Object.Instantiate(source);
            try
            {
                output.AppendLine("SOURCE " + path);
                foreach (Transform part in instance.GetComponentsInChildren<Transform>(true))
                {
                    var meshFilter = part.GetComponent<MeshFilter>();
                    var renderer = part.GetComponent<Renderer>();
                    output.Append(part.name).Append(" pos=").Append(part.position.ToString("F4"))
                        .Append(" rotation=").Append(part.eulerAngles.ToString("F2"))
                        .Append(" scale=").Append(part.lossyScale.ToString("F4"));
                    if (meshFilter != null && meshFilter.sharedMesh != null)
                    {
                        var mesh = meshFilter.sharedMesh;
                        output.Append(" vertices=").Append(mesh.vertexCount)
                            .Append(" submeshes=").Append(mesh.subMeshCount)
                            .Append(" localBounds=").Append(mesh.bounds.ToString("F4"));
                    }
                    if (renderer != null)
                        output.Append(" worldBounds=").Append(renderer.bounds.ToString("F4"));
                    output.AppendLine();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "formula-rccp-source-inspection.txt"), output.ToString());
        Debug.Log("FORMULA_SOURCE_INSPECTION_COMPLETE");
    }
}
