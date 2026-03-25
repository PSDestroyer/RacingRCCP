//----------------------------------------------
//        RCCP AI Setup Assistant
//
// Copyright 2026 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace BoneCrackerGames.RCCP.AIAssistant {

public partial class RCCP_AIAssistantWindow {

    #region Mesh Analysis

    private void AnalyzeMesh() {
        if (selectedVehicle == null) {
            meshAnalysis = "";
            return;
        }

        meshAnalysis = AnalyzeMeshForObject(selectedVehicle);
    }

    /// <summary>
    /// Analyzes a GameObject's mesh and returns the analysis string.
    /// Used for both single vehicle and batch processing.
    /// </summary>
    private string AnalyzeMeshForObject(GameObject vehicle) {
        if (vehicle == null) return "";

        StringBuilder sb = new StringBuilder();

        Bounds bounds = CalculateBounds(vehicle.transform);
        float length = bounds.size.z;
        float width = bounds.size.x;
        float height = bounds.size.y;

        // Output in Length × Width × Height order (standard vehicle dimensions)
        sb.AppendLine($"Dimensions: {length:F2}m (length) x {width:F2}m (width) x {height:F2}m (height)");
        sb.AppendLine($"Detected Type: {GetVehicleCategory(length, width, height)}");

        sb.AppendLine("Hierarchy:");
        AnalyzeTransform(vehicle.transform, sb, 0, 3);

        return sb.ToString();
    }

    private void AnalyzeTransform(Transform t, StringBuilder sb, int depth, int maxDepth) {
        if (depth > maxDepth) return;

        string indent = new string(' ', depth * 2);
        Vector3 pos = t.localPosition;

        string line = $"{indent}{t.name} ({pos.x:F1},{pos.y:F1},{pos.z:F1})";

        MeshRenderer mr = t.GetComponent<MeshRenderer>();
        if (mr != null) {
            bool isWheel = t.name.ToLower().Contains("wheel") ||
                          t.name.ToLower().Contains("tire") ||
                          t.name.ToLower().Contains("whl");
            if (isWheel) {
                // Use local mesh bounds for consistent radius regardless of rotation
                MeshFilter mf = t.GetComponent<MeshFilter>();
                float r = 0.3f; // Default
                if (mf != null && mf.sharedMesh != null) {
                    Vector3 meshSize = mf.sharedMesh.bounds.size;
                    // Wheel radius is typically the larger of Y or Z in local mesh space
                    r = Mathf.Max(meshSize.y, meshSize.z) / 2f;
                } else {
                    r = mr.bounds.size.y / 2f;
                }
                line += $" [WHEEL r={r:F2}]";
            }
        }

        sb.AppendLine(line);

        foreach (Transform child in t) {
            AnalyzeTransform(child, sb, depth + 1, maxDepth);
        }
    }

    /// <summary>
    /// Calculates bounds in the root transform's local space.
    /// This ensures consistent dimensions regardless of the vehicle's rotation in the scene.
    /// </summary>
    private Bounds CalculateBounds(Transform root) {
        Bounds localBounds = new Bounds(Vector3.zero, Vector3.zero);
        bool initialized = false;

        Matrix4x4 worldToLocal = root.worldToLocalMatrix;

        foreach (var renderer in root.GetComponentsInChildren<Renderer>()) {
            Bounds meshBounds;
            Matrix4x4 meshToWorld;

            // Get mesh bounds - handle MeshFilter and SkinnedMeshRenderer
            MeshFilter mf = renderer.GetComponent<MeshFilter>();
            SkinnedMeshRenderer smr = renderer as SkinnedMeshRenderer;

            if (mf != null && mf.sharedMesh != null) {
                meshBounds = mf.sharedMesh.bounds;
                meshToWorld = renderer.transform.localToWorldMatrix;
            } else if (smr != null && smr.sharedMesh != null) {
                meshBounds = smr.sharedMesh.bounds;
                meshToWorld = renderer.transform.localToWorldMatrix;
            } else {
                // Fallback: use renderer bounds corners for other renderer types
                meshBounds = renderer.bounds;
                meshToWorld = Matrix4x4.identity; // bounds already in world space
            }

            // Transform the 8 corners of the bounds to root's local space
            Vector3 min = meshBounds.min;
            Vector3 max = meshBounds.max;

            Vector3[] corners = new Vector3[8] {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };

            foreach (Vector3 corner in corners) {
                Vector3 worldPos = meshToWorld.MultiplyPoint3x4(corner);
                Vector3 localPos = worldToLocal.MultiplyPoint3x4(worldPos);

                if (!initialized) {
                    localBounds = new Bounds(localPos, Vector3.zero);
                    initialized = true;
                } else {
                    localBounds.Encapsulate(localPos);
                }
            }
        }

        // Apply root's scale to get actual world-space dimensions
        // while keeping local orientation (so rotation doesn't swap axes)
        Vector3 worldSize = Vector3.Scale(localBounds.size, root.lossyScale);
        localBounds.size = worldSize;
        return localBounds;
    }

    /// <summary>
    /// Analyzes vehicle bounds and returns a list of size-related warnings.
    /// Helps users identify if their model is unusually sized for a realistic vehicle.
    /// </summary>
    private List<SizeWarning> GetSizeWarnings(GameObject vehicle) {
        List<SizeWarning> warnings = new List<SizeWarning>();
        if (vehicle == null) return warnings;

        Bounds bounds = CalculateBounds(vehicle.transform);
        Vector3 size = bounds.size;

        // Assume Z is length (forward), X is width, Y is height
        // This is the typical Unity convention for vehicles
        float length = size.z;
        float width = size.x;
        float height = size.y;

        // Check if all dimensions are zero (no renderers found)
        if (length < 0.01f && width < 0.01f && height < 0.01f) {
            warnings.Add(new SizeWarning(SizeWarningLevel.Error,
                "No mesh renderers found - cannot determine vehicle size"));
            return warnings;
        }

        // Check for very small models (likely toy/scale model)
        if (length < SIZE_MIN_DIMENSION || width < SIZE_MIN_DIMENSION || height < SIZE_MIN_DIMENSION) {
            float minDim = Mathf.Min(length, width, height);
            warnings.Add(new SizeWarning(SizeWarningLevel.Warning,
                $"Very small ({minDim:F2}m) - may be a scale model. RCCP expects real-world sizes."));
        }

        // Categorize vehicle type based on size
        string vehicleCategory = GetVehicleCategory(length, width, height);

        // Check for larger-than-car vehicles
        if (length > SIZE_MAX_LENGTH) {
            warnings.Add(new SizeWarning(SizeWarningLevel.Info,
                $"{vehicleCategory} ({length:F1}m long) - larger than typical car ({SIZE_TYPICAL_CAR_MIN}-{SIZE_TYPICAL_CAR_MAX}m)"));
        }

        // Check for unusual width
        if (width > SIZE_MAX_WIDTH) {
            warnings.Add(new SizeWarning(SizeWarningLevel.Info,
                $"Wide vehicle ({width:F1}m) - typical cars are 1.7-2.0m wide"));
        }

        // Check for unusual height
        if (height > SIZE_MAX_HEIGHT) {
            warnings.Add(new SizeWarning(SizeWarningLevel.Info,
                $"Tall vehicle ({height:F1}m) - typical cars are 1.4-1.7m tall"));
        }

        // Check aspect ratio (length/width)
        if (width > 0.1f) {
            float aspectRatio = length / width;
            if (aspectRatio < SIZE_ASPECT_RATIO_MIN) {
                warnings.Add(new SizeWarning(SizeWarningLevel.Warning,
                    $"Unusual proportions (L/W = {aspectRatio:F1}) - very wide for length"));
            } else if (aspectRatio > SIZE_ASPECT_RATIO_MAX) {
                warnings.Add(new SizeWarning(SizeWarningLevel.Warning,
                    $"Unusual proportions (L/W = {aspectRatio:F1}) - very long for width"));
            }
        }

        return warnings;
    }

    /// <summary>
    /// Returns a category name based on vehicle dimensions.
    /// Uses length, width, and height to determine vehicle type.
    /// Categories match the presets in the Vehicle Creation prompt.
    /// </summary>
    private string GetVehicleCategory(float length, float width, float height) {
        // === VERY SMALL VEHICLES (< 3m) ===
        if (length < 3f) {
            // Go-kart: very small, very low profile
            if (length < 2.5f && height < 0.8f) return "Go-Kart";
            // ATV/Quad: small, medium height
            if (length < 2.5f && height < 1.2f && width > 1f) return "ATV/Quad";
            return "Compact/Micro";
        }

        // === SMALL VEHICLES (3m - 4.2m) ===
        if (length <= 4.2f) {
            // Sports car: low profile (< 1.3m height)
            if (height < 1.3f) return "Sports Car";
            // Compact hatchback
            if (height <= 1.6f) return "Compact Hatchback";
            // Small SUV/Crossover
            return "Compact SUV";
        }

        // === STANDARD CARS (4.2m - 5m) ===
        if (length <= SIZE_TYPICAL_CAR_MAX) {
            // Sports car / Supercar: very low profile
            if (height < 1.25f) return "Supercar";
            if (height < 1.35f) return "Sports Car";
            // Standard sedan
            if (height <= 1.55f) return "Sedan";
            // Sedan / Hatchback
            if (height <= 1.7f) return "Sedan/Hatchback";
            // SUV/Crossover: taller
            return "SUV/Crossover";
        }

        // === LARGE CARS / SMALL TRUCKS (5m - 6.5m) ===
        if (length <= 6.5f) {
            // Low sports car / GT
            if (height < 1.35f) return "GT/Sports Car";
            // Large sedan / Luxury
            if (height <= 1.55f) return "Luxury Sedan";
            // Pickup truck: medium height
            if (height <= 2f) return "Pickup Truck";
            // Van: taller
            if (height <= 2.5f) return "Van";
            // Tall van / Small bus
            return "Passenger Van";
        }

        // === LARGE VEHICLES (6.5m - 9m) ===
        if (length <= 9f) {
            // Monster truck: very tall with high ground clearance
            if (height > 3.5f) return "Monster Truck";
            // Bus: tall passenger compartment (> 2.5m)
            if (height > 2.5f) return "Minibus";
            // Large truck / Work truck
            if (height > 2f) return "Large Truck";
            // Low cargo truck
            return "Cargo Truck";
        }

        // === VERY LARGE VEHICLES (9m - 12m) ===
        if (length <= 12f) {
            // City bus: tall (> 2.5m)
            if (height > 2.5f) return "City Bus";
            // Semi-truck
            return "Semi-Truck";
        }

        // === EXTRA LARGE VEHICLES (> 12m) ===
        // Coach bus: tall
        if (height > 2.5f) return "Coach Bus";
        // Semi-truck with trailer
        return "Semi-Truck";
    }

    /// <summary>
    /// Gets the highest warning level from a list of warnings.
    /// </summary>
    private SizeWarningLevel GetHighestWarningLevel(List<SizeWarning> warnings) {
        SizeWarningLevel highest = SizeWarningLevel.Info;
        foreach (var warning in warnings) {
            if (warning.level == SizeWarningLevel.Error) return SizeWarningLevel.Error;
            if (warning.level == SizeWarningLevel.Warning) highest = SizeWarningLevel.Warning;
        }
        return highest;
    }

    /// <summary>
    /// Gets the detected vehicle type for the currently selected vehicle.
    /// Used by Quick Create mode to display the detected category.
    /// </summary>
    private string GetDetectedVehicleType() {
        if (selectedVehicle == null) return "Unknown";

        Bounds bounds = CalculateBounds(selectedVehicle.transform);
        Vector3 size = bounds.size;

        // Check for no mesh
        if (size.x < 0.01f && size.y < 0.01f && size.z < 0.01f) {
            return "No mesh found";
        }

        return GetVehicleCategory(size.z, size.x, size.y);
    }

    /// <summary>
    /// Gets a detection summary string for displaying in the Quick Create preview.
    /// Returns dimensions and name hint for the selected vehicle.
    /// </summary>
    private string GetDetectionSummary() {
        if (selectedVehicle == null) return "";

        Bounds bounds = CalculateBounds(selectedVehicle.transform);
        Vector3 size = bounds.size;

        // Check for no mesh
        if (size.x < 0.01f && size.y < 0.01f && size.z < 0.01f) {
            return "No mesh renderers found";
        }

        // Format: "4.5m × 1.8m × 1.5m"
        string dimensions = $"{size.z:F2}m × {size.x:F2}m × {size.y:F2}m";

        return dimensions;
    }

    /// <summary>
    /// Gets the vehicle name hint from the selected vehicle's GameObject name.
    /// </summary>
    private string GetVehicleNameHint() {
        if (selectedVehicle == null) return "";
        return selectedVehicle.name;
    }

    #endregion

    #region Eligibility Check System

    /// <summary>
    /// Runs a complete eligibility check on a vehicle model.
    /// Checks scale, orientation, and wheel separation.
    /// </summary>
    private EligibilityCheck RunEligibilityCheck(GameObject vehicle) {
        EligibilityCheck result = new EligibilityCheck();
        if (vehicle == null) return result;

        // Run all checks
        CheckScale(vehicle, result);
        CheckOrientation(vehicle, result);
        CheckWheels(vehicle, result);

        // Calculate overall status
        result.CalculateOverallStatus();

        return result;
    }

    /// <summary>
    /// Checks if the vehicle scale is realistic for a real-world vehicle.
    /// </summary>
    private void CheckScale(GameObject vehicle, EligibilityCheck result) {
        Bounds bounds = CalculateBounds(vehicle.transform);
        Vector3 size = bounds.size;

        result.dimensions = size;

        float length = size.z;
        float width = size.x;
        float height = size.y;

        // No mesh found
        if (length < 0.01f && width < 0.01f && height < 0.01f) {
            result.scaleStatus = EligibilityStatus.Fail;
            result.scaleMessage = "No mesh renderers found";
            return;
        }

        // Check for unrealistic scales
        float minDim = Mathf.Min(length, width, height);
        float maxDim = Mathf.Max(length, width, height);

        // Very small (likely wrong scale or toy model)
        if (maxDim < 0.5f) {
            result.scaleStatus = EligibilityStatus.Fail;
            result.scaleMessage = $"Too small ({maxDim:F2}m) - model appears to be in wrong units or scale";
            return;
        }

        // Small but could be intentional (go-kart, ATV)
        if (maxDim < 1.5f) {
            result.scaleStatus = EligibilityStatus.Warning;
            result.scaleMessage = $"Small ({maxDim:F2}m) - verify scale is correct for small vehicle";
            return;
        }

        // Very large (likely wrong scale)
        if (maxDim > 25f) {
            result.scaleStatus = EligibilityStatus.Fail;
            result.scaleMessage = $"Too large ({maxDim:F1}m) - model may be in wrong units (cm instead of m?)";
            return;
        }

        // Large but could be intentional (bus, truck)
        if (maxDim > 15f) {
            result.scaleStatus = EligibilityStatus.Warning;
            result.scaleMessage = $"Very large ({maxDim:F1}m) - verify scale is correct for large vehicle";
            return;
        }

        // Check proportions (height shouldn't exceed length significantly)
        if (height > length * 1.5f) {
            result.scaleStatus = EligibilityStatus.Warning;
            result.scaleMessage = $"Unusual proportions - taller ({height:F1}m) than long ({length:F1}m)";
            return;
        }

        // Normal range
        result.scaleStatus = EligibilityStatus.Pass;
        result.scaleMessage = $"{length:F1}m × {width:F1}m × {height:F1}m (realistic size)";
    }

    /// <summary>
    /// Checks if the vehicle orientation follows Unity convention (X=right, Y=up, Z=forward).
    /// Attempts to detect orientation by analyzing wheel positions and mesh shape.
    /// </summary>
    private void CheckOrientation(GameObject vehicle, EligibilityCheck result) {
        result.zIsForward = true;
        result.yIsUp = true;
        result.needsRotation = false;
        result.suggestedRotation = Vector3.zero;

        // Find wheel candidates first
        List<WheelCandidate> wheels = FindWheelCandidates(vehicle);

        if (wheels.Count >= 4) {
            // Analyze wheel positions to determine orientation
            AnalyzeWheelOrientation(wheels, result);
        } else {
            // Fall back to mesh shape analysis
            AnalyzeMeshOrientation(vehicle, result);
        }

        // Set status and message based on findings
        if (!result.yIsUp) {
            result.orientationStatus = EligibilityStatus.Fail;
            result.orientationMessage = "Y-axis is not up - model needs rotation";
            result.needsRotation = true;
        } else if (!result.zIsForward) {
            result.orientationStatus = EligibilityStatus.Warning;
            result.orientationMessage = "Z-axis may not be forward - verify front faces +Z";
            result.needsRotation = true;
        } else if (wheels.Count < 2) {
            result.orientationStatus = EligibilityStatus.Warning;
            result.orientationMessage = "Cannot verify orientation (no wheels found)";
        } else {
            result.orientationStatus = EligibilityStatus.Pass;
            result.orientationMessage = "Orientation OK (Y=up, Z=forward)";
        }
    }

    /// <summary>
    /// Analyzes wheel positions to determine vehicle orientation.
    /// Front wheels should have higher Z values than rear wheels.
    /// </summary>
    private void AnalyzeWheelOrientation(List<WheelCandidate> wheels, EligibilityCheck result) {
        // Sort wheels by Z position (should separate front from rear)
        var sortedByZ = wheels.OrderBy(w => w.localPosition.z).ToList();

        // Sort wheels by X position (should separate left from right)
        var sortedByX = wheels.OrderBy(w => w.localPosition.x).ToList();

        // Sort wheels by Y position (should all be near the bottom)
        var sortedByY = wheels.OrderBy(w => w.localPosition.y).ToList();

        // Check if wheels are spread along Z axis (front/rear separation)
        float zSpread = sortedByZ.Last().localPosition.z - sortedByZ.First().localPosition.z;
        float xSpread = sortedByX.Last().localPosition.x - sortedByX.First().localPosition.x;
        float ySpread = sortedByY.Last().localPosition.y - sortedByY.First().localPosition.y;

        // Y-up check: wheels should have minimal Y spread (all at bottom)
        // and significant X and Z spread
        if (ySpread > zSpread && ySpread > xSpread) {
            // Wheels are spread along Y axis - wrong orientation
            result.yIsUp = false;
            result.suggestedRotation = new Vector3(90, 0, 0);  // Rotate to fix
        }

        // Z-forward check: Z spread should be greater than X spread typically
        // (wheelbase > track width for most vehicles)
        // However, this isn't always true, so we check if X spread is significantly larger
        if (xSpread > zSpread * 1.5f && wheels.Count >= 4) {
            // Model might be sideways (X is forward instead of Z)
            result.zIsForward = false;
            result.suggestedRotation = new Vector3(0, 90, 0);  // Rotate 90 degrees
        }

        // Assign axle guesses to wheels
        if (wheels.Count >= 4) {
            float midZ = (sortedByZ.First().localPosition.z + sortedByZ.Last().localPosition.z) / 2f;
            float midX = (sortedByX.First().localPosition.x + sortedByX.Last().localPosition.x) / 2f;

            foreach (var wheel in wheels) {
                bool isFront = wheel.localPosition.z > midZ;
                bool isRight = wheel.localPosition.x > midX;

                if (isFront && !isRight) wheel.axleGuess = "FL";
                else if (isFront && isRight) wheel.axleGuess = "FR";
                else if (!isFront && !isRight) wheel.axleGuess = "RL";
                else if (!isFront && isRight) wheel.axleGuess = "RR";
            }
        }
    }

    /// <summary>
    /// Analyzes mesh shape to guess orientation when wheels aren't available.
    /// </summary>
    private void AnalyzeMeshOrientation(GameObject vehicle, EligibilityCheck result) {
        Bounds bounds = CalculateBounds(vehicle.transform);
        Vector3 size = bounds.size;

        // Typical car: longest axis is Z (forward), shortest is Y (height)
        // We expect: Z > X > Y or Z > Y > X

        float maxAxis = Mathf.Max(size.x, size.y, size.z);
        float minAxis = Mathf.Min(size.x, size.y, size.z);

        // Y should be the smallest or middle axis (height)
        if (size.y == maxAxis) {
            // Y is longest - model is probably lying on its side or upright wrong
            result.yIsUp = false;
            result.needsRotation = true;

            if (size.z < size.x) {
                // Needs rotation around Z
                result.suggestedRotation = new Vector3(90, 0, 0);
            } else {
                result.suggestedRotation = new Vector3(0, 0, 90);
            }
        }

        // Z should be the longest axis (forward/length)
        if (size.x > size.z * 1.2f && result.yIsUp) {
            // X is significantly longer than Z - model might be sideways
            result.zIsForward = false;
            result.needsRotation = true;
            result.suggestedRotation = new Vector3(0, 90, 0);
        }
    }

    /// <summary>
    /// Checks if wheels are separate GameObjects that can be used for RCCP.
    /// </summary>
    private void CheckWheels(GameObject vehicle, EligibilityCheck result) {
        List<WheelCandidate> wheels = FindWheelCandidates(vehicle);
        result.wheelCandidates = wheels;
        result.separateWheelCount = wheels.Count(w => w.isSeparateMesh);

        // Count how many are truly separate (have their own MeshFilter/MeshRenderer)
        int separateCount = result.separateWheelCount;

        if (separateCount >= 4) {
            result.wheelStatus = EligibilityStatus.Pass;
            result.wheelMessage = $"Found {separateCount} separate wheel objects";
            result.wheelsMergedWithBody = false;
        } else if (separateCount > 0) {
            result.wheelStatus = EligibilityStatus.Warning;
            result.wheelMessage = $"Found only {separateCount} wheel object(s) - need 4 for standard vehicle";
            result.wheelsMergedWithBody = separateCount < 4;
        } else if (wheels.Count > 0) {
            // Found wheel-named objects but they don't have separate meshes
            result.wheelStatus = EligibilityStatus.Fail;
            result.wheelMessage = "Wheel objects found but may be merged with body mesh";
            result.wheelsMergedWithBody = true;
        } else {
            result.wheelStatus = EligibilityStatus.Fail;
            result.wheelMessage = "No wheel objects found - wheels must be separate GameObjects";
            result.wheelsMergedWithBody = true;
        }
    }

    /// <summary>
    /// Finds potential wheel objects in the hierarchy by name and shape analysis.
    /// </summary>
    private List<WheelCandidate> FindWheelCandidates(GameObject vehicle) {
        List<WheelCandidate> candidates = new List<WheelCandidate>();
        if (vehicle == null) return candidates;

        // Common wheel-related name patterns
        string[] wheelPatterns = {
            "wheel", "tire", "tyre", "rim", "whl",
            "fl_", "fr_", "rl_", "rr_", "bl_", "br_",
            "_fl", "_fr", "_rl", "_rr", "_bl", "_br",
            "front_l", "front_r", "rear_l", "rear_r",
            "frontleft", "frontright", "rearleft", "rearright",
            "wheel_f", "wheel_r", "wheel_b"
        };

        Transform[] allTransforms = vehicle.GetComponentsInChildren<Transform>(true);

        foreach (Transform t in allTransforms) {
            if (t == vehicle.transform) continue;

            string nameLower = t.name.ToLower();
            bool matchesPattern = false;

            foreach (string pattern in wheelPatterns) {
                if (nameLower.Contains(pattern)) {
                    matchesPattern = true;
                    break;
                }
            }

            if (matchesPattern) {
                // Check if this object has its own mesh
                MeshFilter mf = t.GetComponent<MeshFilter>();
                MeshRenderer mr = t.GetComponent<MeshRenderer>();
                bool hasSeparateMesh = (mf != null && mf.sharedMesh != null) || (mr != null);

                // Estimate wheel radius from local mesh bounds (rotation-independent)
                float radius = 0.3f;  // Default guess
                if (mf != null && mf.sharedMesh != null) {
                    // Use local mesh bounds for consistent measurement
                    Vector3 meshSize = mf.sharedMesh.bounds.size;
                    // Wheel radius is the larger of Y or Z in local mesh space (circular profile)
                    float boundsRadius = Mathf.Max(meshSize.y, meshSize.z) / 2f;
                    if (boundsRadius > 0.05f) radius = boundsRadius;
                } else if (mr != null) {
                    // Fallback to world bounds if no mesh filter
                    float boundsRadius = Mathf.Min(mr.bounds.size.x, mr.bounds.size.y, mr.bounds.size.z) / 2f;
                    if (boundsRadius > 0.05f) radius = boundsRadius;
                }

                candidates.Add(new WheelCandidate(t, radius, hasSeparateMesh));
            }
        }

        // If we found less than 4 by name, try to find circular meshes
        if (candidates.Count < 4) {
            // Look for objects that might be wheels based on shape (circular, similar sizes)
            List<WheelCandidate> shapeCandidates = FindWheelsByShape(vehicle, candidates);
            foreach (var sc in shapeCandidates) {
                // Don't add duplicates
                if (!candidates.Any(c => c.transform == sc.transform)) {
                    candidates.Add(sc);
                }
            }
        }

        return candidates;
    }

    /// <summary>
    /// Attempts to find wheels by analyzing mesh shapes for circular objects.
    /// Uses local mesh bounds for rotation-independent analysis.
    /// </summary>
    private List<WheelCandidate> FindWheelsByShape(GameObject vehicle, List<WheelCandidate> existing) {
        List<WheelCandidate> shapeCandidates = new List<WheelCandidate>();

        MeshRenderer[] renderers = vehicle.GetComponentsInChildren<MeshRenderer>(true);
        Bounds vehicleBounds = CalculateBounds(vehicle.transform);
        Matrix4x4 worldToLocal = vehicle.transform.worldToLocalMatrix;

        foreach (MeshRenderer mr in renderers) {
            if (mr.transform == vehicle.transform) continue;

            // Skip if already found by name
            if (existing.Any(e => e.transform == mr.transform)) continue;

            // Use local mesh bounds for rotation-independent shape analysis
            MeshFilter mf = mr.GetComponent<MeshFilter>();
            Vector3 size;

            if (mf != null && mf.sharedMesh != null) {
                size = mf.sharedMesh.bounds.size;
            } else {
                // Fallback to world bounds
                size = mr.bounds.size;
            }

            // Wheels are typically circular - two dimensions should be similar
            // and the third (width) should be smaller
            float[] dims = { size.x, size.y, size.z };
            System.Array.Sort(dims);

            float smallest = dims[0];
            float middle = dims[1];
            float largest = dims[2];

            // Check if two larger dimensions are similar (circular profile)
            // and the smallest is the wheel width
            if (middle > 0.1f && largest > 0.1f) {
                float ratio = middle / largest;
                if (ratio > 0.7f && ratio < 1.3f) {
                    // Could be circular
                    // Check if it's in a reasonable size range for a wheel (0.2m - 0.6m radius)
                    float estimatedRadius = (middle + largest) / 4f;
                    if (estimatedRadius > 0.15f && estimatedRadius < 0.7f) {
                        // Check position in vehicle's local space - wheels should be at the bottom
                        Vector3 localPos = worldToLocal.MultiplyPoint3x4(mr.transform.position);
                        float relativeY = (localPos.y - vehicleBounds.min.y) / vehicleBounds.size.y;

                        // Wheels should be in the lower half of the vehicle
                        if (relativeY < 0.4f) {
                            shapeCandidates.Add(new WheelCandidate(mr.transform, estimatedRadius, true));
                        }
                    }
                }
            }
        }

        return shapeCandidates;
    }

    #endregion

}

} // namespace BoneCrackerGames.RCCP.AIAssistant
#endif
