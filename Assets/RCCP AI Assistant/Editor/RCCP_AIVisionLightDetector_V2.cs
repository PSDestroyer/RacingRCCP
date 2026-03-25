//----------------------------------------------
//        RCCP AI Setup Assistant
//
// Copyright 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEngine.Networking;
using Unity.EditorCoroutines.Editor;

namespace BoneCrackerGames.RCCP.AIAssistant {

    /// <summary>
    /// Vision-based light detector for RCCP vehicles.
    /// Takes orthographic screenshots from front, rear, and side views,
    /// then uses AI to detect light positions.
    /// </summary>
    public class RCCP_AIVisionLightDetector_V2 {

        #region Constants

        private const int CAPTURE_RESOLUTION = 512;
        private const float CAMERA_DISTANCE = 15f;
        public const float CAMERA_PADDING = 1.15f;  // 15% padding around vehicle

        #endregion

        #region Settings

        /// <summary>
        /// Multiplier for orthographic camera size. 1.0 = vehicle fills frame with padding.
        /// </summary>
        public float OrthoSizeMultiplier { get; set; } = 1.0f;

        /// <summary>
        /// When enabled, saves captured screenshots to debug folder.
        /// </summary>
        public bool SaveDebugScreenshots {
            get => RCCP_AIEditorPrefs.SaveVisionDebugScreenshots;
            set => RCCP_AIEditorPrefs.SaveVisionDebugScreenshots = value;
        }

        #endregion

        #region Data Classes

        /// <summary>
        /// A detected light from AI vision analysis.
        /// </summary>
        [Serializable]
        public class DetectedLight {
            public string lightType;        // headlight_low, headlight_high, brakelight, taillight, indicator, reverse
            public string side;             // left, right, center
            public string view;             // front, rear
            public float confidence;        // 0-1

            // Normalized coordinates (0-1 range relative to vehicle bounds)
            public float normalizedX;       // 0 = left side of vehicle, 1 = right side
            public float normalizedY;       // 0 = bottom, 1 = top
            public float normalizedZ;       // 0 = rear, 1 = front

            // Computed positions
            [NonSerialized] public Vector3 worldPosition;
            [NonSerialized] public Vector3 localPosition;
            [NonSerialized] public Vector3 userOffset;
            [NonSerialized] public bool enabled = true;

            public Vector3 FinalWorldPosition => worldPosition + userOffset;
        }

        /// <summary>
        /// AI response structure.
        /// </summary>
        [Serializable]
        public class VisionResponse {
            public DetectedLight[] lights;
            public string vehicleType;
            public string explanation;
        }

        /// <summary>
        /// Complete detection result.
        /// </summary>
        public class DetectionResult {
            public bool success;
            public string error;
            public string vehicleType;
            public string explanation;

            public GameObject vehicle;
            public Bounds localBounds;
            public float orthoSizeMultiplier = 1f;
            public float calculatedOrthoSize;

            public Texture2D frontCapture;
            public Texture2D rearCapture;
            public Texture2D sideCapture;

            public List<DetectedLight> lights = new List<DetectedLight>();

            public void Cleanup() {
                if (frontCapture != null) UnityEngine.Object.DestroyImmediate(frontCapture);
                if (rearCapture != null) UnityEngine.Object.DestroyImmediate(rearCapture);
                if (sideCapture != null) UnityEngine.Object.DestroyImmediate(sideCapture);
            }
        }

        /// <summary>
        /// Stored render settings for restoration.
        /// </summary>
        private class CaptureEnvironment {
            public bool fog;
            public AmbientMode ambientMode;
            public Color ambientLight;
            public Color ambientSky;
            public Color ambientEquator;
            public Color ambientGround;
            public float ambientIntensity;
            public float reflectionIntensity;
            public List<Light> disabledLights = new List<Light>();
            public GameObject tempLight;
        }

        #endregion

        #region Singleton

        private static RCCP_AIVisionLightDetector_V2 _instance;
        public static RCCP_AIVisionLightDetector_V2 Instance => _instance ?? (_instance = new RCCP_AIVisionLightDetector_V2());

        #endregion

        #region State

        private bool isProcessing;
        private bool cancelRequested;
        private DetectionResult currentResult;
        private EditorCoroutine currentCoroutine;
        private Action<DetectionResult> onComplete;

        // Cleanup tracking
        private GameObject cleanupVehicle;
        private Vector3 cleanupPosition;
        private Quaternion cleanupRotation;
        private CaptureEnvironment cleanupEnv;

        public bool IsProcessing => isProcessing;
        public DetectionResult CurrentResult => currentResult;

        #endregion

        #region Public API

        /// <summary>
        /// Starts light detection for a vehicle.
        /// </summary>
        public void DetectLights(GameObject vehicle, string apiKey, Action<DetectionResult> callback, EditorWindow owner) {
            if (isProcessing) {
                callback?.Invoke(new DetectionResult { success = false, error = "Detection already in progress" });
                return;
            }

            if (vehicle == null) {
                callback?.Invoke(new DetectionResult { success = false, error = "No vehicle provided" });
                return;
            }

            bool useProxy = RCCP_AISettings.Instance?.useServerProxy ?? false;
            if (string.IsNullOrEmpty(apiKey) && !useProxy) {
                callback?.Invoke(new DetectionResult { success = false, error = "API key or server proxy required" });
                return;
            }

            cancelRequested = false;

            // Auto-register with proxy if needed
            if (useProxy && !RCCP_ServerProxy.IsRegistered) {
                isProcessing = true;
                RCCP_ServerProxy.RegisterDevice(owner, (success, message) => {
                    if (cancelRequested) {
                        isProcessing = false;
                        callback?.Invoke(new DetectionResult { success = false, error = "Cancelled" });
                        return;
                    }
                    if (success) {
                        StartDetection(vehicle, apiKey, callback, owner);
                    } else {
                        isProcessing = false;
                        callback?.Invoke(new DetectionResult { success = false, error = $"Registration failed: {message}" });
                    }
                });
                return;
            }

            StartDetection(vehicle, apiKey, callback, owner);
        }

        /// <summary>
        /// Cancels ongoing detection and restores scene state.
        /// </summary>
        public void Cancel() {
            cancelRequested = true;

            if (currentCoroutine != null) {
                EditorCoroutineUtility.StopCoroutine(currentCoroutine);
                currentCoroutine = null;
            }

            RestoreCleanupState();
            isProcessing = false;
        }

        /// <summary>
        /// Converts detected light to world position.
        /// normalizedX/Y must already be vehicle-relative (0-1 across vehicle bounds).
        /// Applies coordinate system conversion (front view is mirrored).
        /// </summary>
        public static Vector3 ConvertToWorldPosition(DetectedLight light, GameObject vehicle, Bounds localBounds) {
            // Convert image X to vehicle X (front view is mirrored)
            float vehicleX = light.normalizedX;
            if (light.view?.ToLower() == "front") {
                vehicleX = 1.0f - light.normalizedX;
            }

            Vector3 localPos = new Vector3(
                Mathf.Lerp(localBounds.min.x, localBounds.max.x, vehicleX),
                Mathf.Lerp(localBounds.min.y, localBounds.max.y, light.normalizedY),
                Mathf.Lerp(localBounds.min.z, localBounds.max.z, light.normalizedZ)
            );
            return vehicle.transform.TransformPoint(localPos);
        }

        /// <summary>
        /// Converts image-space normalized coordinates (0-1 across full captured image)
        /// to vehicle-space normalized coordinates (0-1 across vehicle bounds).
        /// The AI returns coordinates relative to the image, but the vehicle only
        /// occupies a portion of the image due to camera padding and aspect ratio.
        /// </summary>
        public static void ImageToVehicleCoords(
            float imageNormX, float imageNormY,
            Bounds localBounds, float orthoMultiplier,
            out float vehicleNormX, out float vehicleNormY) {

            Vector3 size = localBounds.size;
            float vehicleWidth = size.x;
            float vehicleHeight = size.y;

            // Match CaptureView: orthoSize = Max(width, height) / 2 * CAMERA_PADDING * multiplier
            // Total visible size in each axis = Max(width, height) * CAMERA_PADDING * multiplier
            float relevantSize = Mathf.Max(vehicleWidth, vehicleHeight);
            float totalVisibleSize = relevantSize * CAMERA_PADDING * orthoMultiplier;

            float extentX = vehicleWidth / totalVisibleSize;
            float extentY = vehicleHeight / totalVisibleSize;
            float paddingX = (1f - extentX) / 2f;
            float paddingY = (1f - extentY) / 2f;

            vehicleNormX = Mathf.Clamp01((imageNormX - paddingX) / extentX);
            vehicleNormY = Mathf.Clamp01((imageNormY - paddingY) / extentY);
        }

        #endregion

        #region Detection Coroutine

        private void StartDetection(GameObject vehicle, string apiKey, Action<DetectionResult> callback, EditorWindow owner) {
            onComplete = callback;
            currentCoroutine = EditorCoroutineUtility.StartCoroutine(DetectionProcess(vehicle, apiKey), owner);
        }

        private IEnumerator DetectionProcess(GameObject vehicle, string apiKey) {
            isProcessing = true;

            currentResult = new DetectionResult {
                vehicle = vehicle,
                orthoSizeMultiplier = OrthoSizeMultiplier
            };

            // Store original transform
            Vector3 originalPos = vehicle.transform.position;
            Quaternion originalRot = vehicle.transform.rotation;

            cleanupVehicle = vehicle;
            cleanupPosition = originalPos;
            cleanupRotation = originalRot;

            // Move to isolated position
            vehicle.transform.position = new Vector3(0, -5000f, 0);
            vehicle.transform.rotation = Quaternion.identity;

            Debug.Log("[Vision] Starting light detection...");

            // Calculate bounds
            currentResult.localBounds = CalculateBounds(vehicle);
            float size = Mathf.Max(currentResult.localBounds.size.x, currentResult.localBounds.size.y, currentResult.localBounds.size.z);
            currentResult.calculatedOrthoSize = (size / 2f) * CAMERA_PADDING * OrthoSizeMultiplier;

            Debug.Log($"[Vision] Bounds: {currentResult.localBounds.size}, OrthoSize: {currentResult.calculatedOrthoSize:F2}");

            // Setup capture environment
            cleanupEnv = StoreEnvironment();
            SetupCaptureEnvironment();
            yield return null;

            // Capture views
            Debug.Log("[Vision] Capturing front view...");
            currentResult.frontCapture = CaptureView(vehicle, currentResult.localBounds, ViewDirection.Front);
            yield return null;

            Debug.Log("[Vision] Capturing rear view...");
            currentResult.rearCapture = CaptureView(vehicle, currentResult.localBounds, ViewDirection.Rear);
            yield return null;

            Debug.Log("[Vision] Capturing side view...");
            currentResult.sideCapture = CaptureView(vehicle, currentResult.localBounds, ViewDirection.Left);
            yield return null;

            // Save debug screenshots if enabled
            if (SaveDebugScreenshots) {
                SaveScreenshotsToDebugFolder(vehicle.name, currentResult);
            }

            // Restore environment
            RestoreEnvironment(cleanupEnv);
            cleanupEnv = null;

            // Send to AI
            bool useProxy = RCCP_AISettings.Instance?.useServerProxy ?? false;

            if (useProxy) {
                yield return SendToProxy(currentResult);
            } else {
                yield return SendToAPI(currentResult, apiKey);
            }

            // Restore vehicle transform BEFORE calculating world positions
            vehicle.transform.position = originalPos;
            vehicle.transform.rotation = originalRot;
            cleanupVehicle = null;

            // Calculate world positions for each light
            if (currentResult.success) {
                // Convert AI image-space coordinates to vehicle-space coordinates.
                // The AI returns normalizedX/Y as positions in the full image (0-1),
                // but the vehicle only occupies a portion due to camera padding.
                // normalizedZ is already vehicle-relative (per the AI prompt).
                foreach (var light in currentResult.lights) {
                    ImageToVehicleCoords(
                        light.normalizedX, light.normalizedY,
                        currentResult.localBounds, OrthoSizeMultiplier,
                        out float vehX, out float vehY);
                    light.normalizedX = vehX;
                    light.normalizedY = vehY;
                }

                foreach (var light in currentResult.lights) {
                    // normalizedX/Y are now vehicle-relative (0-1 across vehicle bounds)
                    float vehicleX = light.normalizedX;
                    if (light.view?.ToLower() == "front") {
                        // Front view is mirrored: image-left (0) = vehicle-right (1)
                        vehicleX = 1.0f - light.normalizedX;
                    }
                    // Rear view: no flip needed, image-left = vehicle-left

                    light.localPosition = new Vector3(
                        Mathf.Lerp(currentResult.localBounds.min.x, currentResult.localBounds.max.x, vehicleX),
                        Mathf.Lerp(currentResult.localBounds.min.y, currentResult.localBounds.max.y, light.normalizedY),
                        Mathf.Lerp(currentResult.localBounds.min.z, currentResult.localBounds.max.z, light.normalizedZ)
                    );
                    light.worldPosition = vehicle.transform.TransformPoint(light.localPosition);
                    light.userOffset = Vector3.zero;

                    Debug.Log($"[Vision] {light.lightType} ({light.side}): imgX={light.normalizedX:F2}, vehX={vehicleX:F2}, Y={light.normalizedY:F2}, Z={light.normalizedZ:F2}");
                }
            }

            isProcessing = false;
            onComplete?.Invoke(currentResult);
        }

        #endregion

        #region Bounds Calculation

        private Bounds CalculateBounds(GameObject vehicle) {
            MeshRenderer[] renderers = vehicle.GetComponentsInChildren<MeshRenderer>(false);
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;

            foreach (var renderer in renderers) {
                string name = renderer.gameObject.name.ToLower();

                // Skip non-body parts
                if (name.Contains("shadow") || name.Contains("reflection") || name.Contains("ground"))
                    continue;

                MeshFilter mf = renderer.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null)
                    continue;

                // Get mesh bounds in vehicle local space
                Bounds meshBounds = GetMeshBoundsInLocalSpace(mf, vehicle.transform);

                if (first) {
                    bounds = meshBounds;
                    first = false;
                } else {
                    bounds.Encapsulate(meshBounds);
                }
            }

            if (first) {
                // Fallback if no meshes found
                return new Bounds(Vector3.zero, new Vector3(2f, 1.5f, 4.5f));
            }

            return bounds;
        }

        private Bounds GetMeshBoundsInLocalSpace(MeshFilter mf, Transform vehicleRoot) {
            Mesh mesh = mf.sharedMesh;
            Vector3 c = mesh.bounds.center;
            Vector3 e = mesh.bounds.extents;

            // Transform 8 corners to vehicle local space
            Vector3[] corners = {
                c + new Vector3(-e.x, -e.y, -e.z),
                c + new Vector3(-e.x, -e.y,  e.z),
                c + new Vector3(-e.x,  e.y, -e.z),
                c + new Vector3(-e.x,  e.y,  e.z),
                c + new Vector3( e.x, -e.y, -e.z),
                c + new Vector3( e.x, -e.y,  e.z),
                c + new Vector3( e.x,  e.y, -e.z),
                c + new Vector3( e.x,  e.y,  e.z)
            };

            Bounds localBounds = new Bounds();
            for (int i = 0; i < corners.Length; i++) {
                Vector3 worldCorner = mf.transform.TransformPoint(corners[i]);
                Vector3 localCorner = vehicleRoot.InverseTransformPoint(worldCorner);

                if (i == 0)
                    localBounds = new Bounds(localCorner, Vector3.zero);
                else
                    localBounds.Encapsulate(localCorner);
            }

            return localBounds;
        }

        #endregion

        #region Environment Management

        private CaptureEnvironment StoreEnvironment() {
            var env = new CaptureEnvironment {
                fog = RenderSettings.fog,
                ambientMode = RenderSettings.ambientMode,
                ambientLight = RenderSettings.ambientLight,
                ambientSky = RenderSettings.ambientSkyColor,
                ambientEquator = RenderSettings.ambientEquatorColor,
                ambientGround = RenderSettings.ambientGroundColor,
                ambientIntensity = RenderSettings.ambientIntensity,
                reflectionIntensity = RenderSettings.reflectionIntensity
            };

            // Store directional lights
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) {
                if (light.type == LightType.Directional && light.enabled) {
                    env.disabledLights.Add(light);
                }
            }

            return env;
        }

        private void SetupCaptureEnvironment() {
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white;
            RenderSettings.ambientIntensity = 2.5f;
            RenderSettings.ambientSkyColor = Color.white;
            RenderSettings.ambientEquatorColor = Color.white;
            RenderSettings.ambientGroundColor = new Color(0.9f, 0.9f, 0.9f);
            RenderSettings.reflectionIntensity = 0f;

            // Disable scene lights
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) {
                if (light.type == LightType.Directional && light.enabled) {
                    light.enabled = false;
                }
            }

            // Create fill light
            var lightObj = new GameObject("RCCP_CaptureLight_Temp");
            var fillLight = lightObj.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.color = Color.white;
            fillLight.intensity = 1.5f;
            fillLight.shadows = LightShadows.None;
            fillLight.transform.rotation = Quaternion.Euler(30f, 45f, 0f);

            if (cleanupEnv != null)
                cleanupEnv.tempLight = lightObj;
        }

        private void RestoreEnvironment(CaptureEnvironment env) {
            if (env == null) return;

            RenderSettings.fog = env.fog;
            RenderSettings.ambientMode = env.ambientMode;
            RenderSettings.ambientLight = env.ambientLight;
            RenderSettings.ambientSkyColor = env.ambientSky;
            RenderSettings.ambientEquatorColor = env.ambientEquator;
            RenderSettings.ambientGroundColor = env.ambientGround;
            RenderSettings.ambientIntensity = env.ambientIntensity;
            RenderSettings.reflectionIntensity = env.reflectionIntensity;

            foreach (var light in env.disabledLights) {
                if (light != null) light.enabled = true;
            }

            if (env.tempLight != null) {
                UnityEngine.Object.DestroyImmediate(env.tempLight);
            }
        }

        private void RestoreCleanupState() {
            if (cleanupVehicle != null) {
                cleanupVehicle.transform.position = cleanupPosition;
                cleanupVehicle.transform.rotation = cleanupRotation;
                cleanupVehicle = null;
            }

            if (cleanupEnv != null) {
                RestoreEnvironment(cleanupEnv);
                cleanupEnv = null;
            }

            currentResult?.Cleanup();
        }

        #endregion

        #region Screenshot Capture

        private enum ViewDirection { Front, Rear, Left }

        private Texture2D CaptureView(GameObject vehicle, Bounds localBounds, ViewDirection direction) {
            var camObj = new GameObject("RCCP_Camera_Temp");
            var cam = camObj.AddComponent<Camera>();

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.2f, 0.22f);
            cam.orthographic = true;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.allowHDR = false;
            cam.allowMSAA = true;

            // Calculate ortho size based on view direction
            float orthoSize;
            if (direction == ViewDirection.Left) {
                orthoSize = Mathf.Max(localBounds.size.z, localBounds.size.y) / 2f;
            } else {
                orthoSize = Mathf.Max(localBounds.size.x, localBounds.size.y) / 2f;
            }
            cam.orthographicSize = orthoSize * CAMERA_PADDING * OrthoSizeMultiplier;

            // Position camera
            Vector3 center = vehicle.transform.TransformPoint(localBounds.center);
            Vector3 offset = GetCameraOffset(direction, vehicle.transform);
            cam.transform.position = center + offset * CAMERA_DISTANCE;
            cam.transform.LookAt(center);

            // Render
            var rt = new RenderTexture(CAPTURE_RESOLUTION, CAPTURE_RESOLUTION, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(CAPTURE_RESOLUTION, CAPTURE_RESOLUTION, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, CAPTURE_RESOLUTION, CAPTURE_RESOLUTION), 0, 0);
            tex.Apply();

            RenderTexture.active = null;
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(camObj);

            return tex;
        }

        private Vector3 GetCameraOffset(ViewDirection direction, Transform vehicle) {
            switch (direction) {
                case ViewDirection.Front: return vehicle.forward;
                case ViewDirection.Rear: return -vehicle.forward;
                case ViewDirection.Left: return -vehicle.right;
                default: return vehicle.forward;
            }
        }

        private void SaveScreenshotsToDebugFolder(string vehicleName, DetectionResult result) {
            string folder = RCCP_AIUtility.DebugScreenshotsPath;
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string safeName = string.Join("_", vehicleName.Split(Path.GetInvalidFileNameChars()));
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            File.WriteAllBytes($"{folder}/{safeName}_{timestamp}_front.png", result.frontCapture.EncodeToPNG());
            File.WriteAllBytes($"{folder}/{safeName}_{timestamp}_rear.png", result.rearCapture.EncodeToPNG());
            File.WriteAllBytes($"{folder}/{safeName}_{timestamp}_side.png", result.sideCapture.EncodeToPNG());

            AssetDatabase.Refresh();
            Debug.Log($"[Vision] Debug screenshots saved to: {folder}/");
        }

        #endregion

        #region AI Communication

        private IEnumerator SendToProxy(DetectionResult result) {
            Debug.Log("[Vision] Sending to server proxy...");

            string frontB64 = Convert.ToBase64String(result.frontCapture.EncodeToPNG());
            string rearB64 = Convert.ToBase64String(result.rearCapture.EncodeToPNG());
            string sideB64 = Convert.ToBase64String(result.sideCapture.EncodeToPNG());

            string[] images = { frontB64, rearB64, sideB64 };
            string[] labels = {
                "FRONT VIEW - Camera facing front of vehicle. Detect headlights and front indicators.",
                "REAR VIEW - Camera facing rear of vehicle. Detect brakelights, taillights, reverse lights, rear indicators.",
                "LEFT SIDE VIEW - Use this to estimate normalizedZ (depth). Left edge = front of car (Z~0.95), Right edge = rear (Z~0.05). Output your JSON response now."
            };

            string systemPrompt = GetSystemPrompt();

            bool completed = false;
            RCCP_ServerProxy.QueryResult proxyResult = null;

            RCCP_ServerProxy.SendVisionQuery(
                this,
                RCCP_AISettings.Instance.visionModel,
                RCCP_AISettings.Instance.maxTokens,
                systemPrompt,
                images,
                labels,
                (r) => { proxyResult = r; completed = true; }
            );

            while (!completed) yield return null;

            if (proxyResult != null && proxyResult.Success) {
                try {
                    ParseAIResponse(proxyResult.Content, result);
                    result.success = true;
                    Debug.Log($"[Vision] Detected {result.lights.Count} lights via proxy");
                } catch (Exception ex) {
                    result.success = false;
                    result.error = $"Parse error: {ex.Message}";
                    Debug.LogError($"[Vision] {ex}");
                }
            } else {
                result.success = false;
                result.error = proxyResult?.Error ?? "Proxy error";
                Debug.LogError($"[Vision] Proxy error: {result.error}");
            }
        }

        private IEnumerator SendToAPI(DetectionResult result, string apiKey) {
            Debug.Log("[Vision] Sending to Claude API...");

            string requestJson = BuildRequest(result);

            using (var request = new UnityWebRequest(RCCP_AISettings.Instance.apiEndpoint, "POST")) {
                byte[] body = Encoding.UTF8.GetBytes(requestJson);
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("x-api-key", apiKey);
                request.SetRequestHeader("anthropic-version", "2023-06-01");
                request.timeout = 90;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success) {
                    try {
                        ParseAPIResponse(request.downloadHandler.text, result);
                        result.success = true;
                        Debug.Log($"[Vision] Detected {result.lights.Count} lights");
                    } catch (Exception ex) {
                        result.success = false;
                        result.error = $"Parse error: {ex.Message}";
                        Debug.LogError($"[Vision] {ex}");
                    }
                } else {
                    result.success = false;
                    result.error = $"API error: {request.error}";
                    Debug.LogError($"[Vision] {request.error}\n{request.downloadHandler?.text}");
                }
            }
        }

        private string BuildRequest(DetectionResult result) {
            string frontB64 = Convert.ToBase64String(result.frontCapture.EncodeToPNG());
            string rearB64 = Convert.ToBase64String(result.rearCapture.EncodeToPNG());
            string sideB64 = Convert.ToBase64String(result.sideCapture.EncodeToPNG());

            string systemPrompt = GetSystemPrompt();

            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"model\":\"{RCCP_AISettings.Instance.visionModel}\",");
            sb.Append($"\"max_tokens\":{RCCP_AISettings.Instance.maxTokens},");
            sb.Append($"\"system\":{JsonEscape(systemPrompt)},");
            sb.Append("\"messages\":[{\"role\":\"user\",\"content\":[");

            // Front
            sb.Append("{\"type\":\"image\",\"source\":{\"type\":\"base64\",\"media_type\":\"image/png\",");
            sb.Append($"\"data\":\"{frontB64}\"");
            sb.Append("}},");
            sb.Append("{\"type\":\"text\",\"text\":\"FRONT VIEW - Camera facing front of vehicle. Detect headlights and front indicators.\"},");

            // Rear
            sb.Append("{\"type\":\"image\",\"source\":{\"type\":\"base64\",\"media_type\":\"image/png\",");
            sb.Append($"\"data\":\"{rearB64}\"");
            sb.Append("}},");
            sb.Append("{\"type\":\"text\",\"text\":\"REAR VIEW - Camera facing rear of vehicle. Detect brakelights, taillights, reverse lights, rear indicators.\"},");

            // Side
            sb.Append("{\"type\":\"image\",\"source\":{\"type\":\"base64\",\"media_type\":\"image/png\",");
            sb.Append($"\"data\":\"{sideB64}\"");
            sb.Append("}},");
            sb.Append("{\"type\":\"text\",\"text\":\"LEFT SIDE VIEW - Use this to estimate normalizedZ (depth). Left edge = front of car (Z~0.95), Right edge = rear (Z~0.05). Output your JSON response now.\"}");

            sb.Append("]}]}");
            return sb.ToString();
        }

        private string GetSystemPrompt() {
            return @"You are detecting vehicle lights in orthographic images. You will receive 3 images:
1. FRONT VIEW - Camera facing front of vehicle
2. REAR VIEW - Camera facing rear of vehicle
3. LEFT SIDE VIEW - Camera on left side, looking at vehicle's left flank

COORDINATE SYSTEM (all values 0.0 to 1.0):

For FRONT and REAR views:
- normalizedX: 0.0 = left edge of image, 1.0 = right edge of image
- normalizedY: 0.0 = bottom of image, 1.0 = top of image

For DEPTH (normalizedZ) - use the SIDE VIEW to estimate:
- Look at the side view image to see where each light is positioned along the vehicle length
- In the side view: LEFT edge = front of vehicle, RIGHT edge = rear of vehicle
- normalizedZ: 1.0 = front of vehicle (left in side view), 0.0 = rear of vehicle (right in side view)
- Example: Headlights are near the front bumper, so normalizedZ should be around 0.92-0.98
- Example: Taillights are near the rear bumper, so normalizedZ should be around 0.02-0.08

LIGHT TYPES:

FROM FRONT VIEW ONLY:
- headlight_low: Main headlights (always present, usually largest)
- headlight_high: High beams (only if visibly separate from low beams)
- indicator: Front turn signals (usually at corners, often amber/orange)

FROM REAR VIEW ONLY:
- brakelight: Brake lights (red, usually prominent)
- taillight: Running/parking lights (red, may overlap with brakelights)
- reverse: Backup/reverse lights (white or clear)
- indicator: Rear turn signals (may be amber or red)

OUTPUT FORMAT (JSON only, no markdown):
{
  ""lights"": [
    {
      ""lightType"": ""headlight_low"",
      ""side"": ""left"",
      ""view"": ""front"",
      ""normalizedX"": 0.25,
      ""normalizedY"": 0.45,
      ""normalizedZ"": 0.95,
      ""confidence"": 0.95
    }
  ],
  ""vehicleType"": ""sedan"",
  ""explanation"": ""Brief summary""
}

IMPORTANT:
- Report EACH light individually (left headlight, right headlight, etc.)
- Be precise with X, Y coordinates - estimate the CENTER of each light
- Use the side view to estimate Z (depth) - how far forward/back the light is
- Set 'side' based on which side of the IMAGE the light appears on";
        }

        #endregion

        #region Response Parsing

        [Serializable]
        private class APIResponse {
            public ContentBlock[] content;
        }

        [Serializable]
        private class ContentBlock {
            public string type;
            public string text;
        }

        private void ParseAPIResponse(string responseText, DetectionResult result) {
            var apiResponse = JsonUtility.FromJson<APIResponse>(responseText);

            if (apiResponse?.content == null || apiResponse.content.Length == 0)
                throw new Exception("Empty API response");

            string jsonContent = null;
            foreach (var block in apiResponse.content) {
                if (block.type == "text") {
                    jsonContent = block.text;
                    break;
                }
            }

            if (string.IsNullOrEmpty(jsonContent))
                throw new Exception("No text content in response");

            ParseAIResponse(jsonContent, result);
        }

        private void ParseAIResponse(string content, DetectionResult result) {
            string json = CleanJson(content);
            Debug.Log($"[Vision] Parsing: {json.Substring(0, Mathf.Min(300, json.Length))}...");

            var response = JsonUtility.FromJson<VisionResponse>(json);

            if (response == null)
                throw new Exception("Failed to parse response");

            result.vehicleType = response.vehicleType;
            result.explanation = response.explanation;

            if (response.lights != null) {
                foreach (var light in response.lights) {
                    // Clamp coordinates - keep raw image coordinates for preview display
                    light.normalizedX = Mathf.Clamp01(light.normalizedX);
                    light.normalizedY = Mathf.Clamp01(light.normalizedY);
                    light.confidence = Mathf.Clamp01(light.confidence);
                    light.enabled = true;

                    // Use AI-provided Z if valid, otherwise fall back to default
                    if (light.normalizedZ > 0.001f && light.normalizedZ < 0.999f) {
                        // AI provided a Z value - clamp it
                        light.normalizedZ = Mathf.Clamp01(light.normalizedZ);
                    } else {
                        // Fall back to default Z based on view
                        light.normalizedZ = GetDefaultZForLight(light);
                    }

                    // Update side to match vehicle perspective (for front view, sides are flipped)
                    UpdateSideForVehicle(light);

                    result.lights.Add(light);
                }
            }
        }

        /// <summary>
        /// Updates the side field to match vehicle perspective.
        /// For front view, image-left is actually vehicle-right.
        /// </summary>
        private void UpdateSideForVehicle(DetectedLight light) {
            string view = light.view?.ToLower() ?? "";

            if (view == "front") {
                // Front view is mirrored: image-left = vehicle-right
                if (light.side?.ToLower() == "left")
                    light.side = "right";
                else if (light.side?.ToLower() == "right")
                    light.side = "left";
            }
            // Rear view: no change needed
        }

        /// <summary>
        /// Default Z coordinate based on view (used as fallback if AI doesn't provide Z).
        /// </summary>
        private float GetDefaultZForLight(DetectedLight light) {
            string view = light.view?.ToLower() ?? "";

            if (view == "front") {
                // Front lights near front bumper (high Z value = front of vehicle)
                return 0.95f;
            } else {
                // Rear lights near rear bumper (low Z value = rear of vehicle)
                return 0.05f;
            }
        }

        private string CleanJson(string input) {
            string s = input.Trim();

            // Remove markdown code blocks
            if (s.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(7);
            else if (s.StartsWith("```"))
                s = s.Substring(3);

            if (s.EndsWith("```"))
                s = s.Substring(0, s.Length - 3);

            s = s.Trim();

            // Find JSON object
            int start = s.IndexOf('{');
            if (start > 0) s = s.Substring(start);

            // Find matching closing brace
            int depth = 0;
            int end = -1;
            for (int i = 0; i < s.Length; i++) {
                if (s[i] == '{') depth++;
                else if (s[i] == '}') {
                    depth--;
                    if (depth == 0) { end = i; break; }
                }
            }

            if (end > 0 && end < s.Length - 1)
                s = s.Substring(0, end + 1);

            return s.Trim();
        }

        private string JsonEscape(string str) {
            var sb = new StringBuilder();
            sb.Append("\"");
            foreach (char c in str) {
                switch (c) {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            sb.Append("\"");
            return sb.ToString();
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Gets display color for a light type.
        /// </summary>
        public static Color GetLightColor(string lightType) {
            switch (lightType?.ToLower()) {
                case "headlight_low":
                case "headlight_high":
                    return new Color(1f, 1f, 0.9f);
                case "brakelight":
                case "taillight":
                    return new Color(1f, 0.1f, 0.05f);
                case "indicator":
                    return new Color(1f, 0.6f, 0f);
                case "reverse":
                    return new Color(0.9f, 0.95f, 1f);
                default:
                    return Color.white;
            }
        }

        /// <summary>
        /// Converts detected light type to RCCP light type.
        /// </summary>
        public static RCCP_Light.LightType ToRCCPLightType(DetectedLight light) {
            switch (light.lightType?.ToLower()) {
                case "headlight_low":
                    return RCCP_Light.LightType.Headlight_LowBeam;
                case "headlight_high":
                    return RCCP_Light.LightType.Headlight_HighBeam;
                case "brakelight":
                    return RCCP_Light.LightType.Brakelight;
                case "taillight":
                    return RCCP_Light.LightType.Taillight;
                case "reverse":
                    return RCCP_Light.LightType.Reverselight;
                case "indicator":
                    return light.side?.ToLower() == "right"
                        ? RCCP_Light.LightType.IndicatorRightLight
                        : RCCP_Light.LightType.IndicatorLeftLight;
                default:
                    return RCCP_Light.LightType.Headlight_LowBeam;
            }
        }

        #endregion
    }

}
#endif
