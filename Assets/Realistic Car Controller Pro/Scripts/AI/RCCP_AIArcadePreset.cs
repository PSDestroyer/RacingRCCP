using UnityEngine;

[CreateAssetMenu(fileName = "RCCP_AIArcadePreset", menuName = "RCCP/AI/Arcade Preset")]
public class RCCP_AIArcadePreset : ScriptableObject {

    public enum Difficulty {
        Easy,
        Medium,
        Hard,
        Expert
    }

    public Difficulty difficulty = Difficulty.Medium;

    [Header("Driving")]
    public float maxSpeedKph = 135f;
    [Range(0f, 1f)] public float acceleration = .85f;
    [Range(.2f, 3f)] public float steeringSensitivity = 1.2f;
    [Range(.2f, 3f)] public float brakeSensitivity = 1f;
    [Range(.4f, 2f)] public float grip = 1.1f;

    [Header("Cornering")]
    public float straightLookAhead = 28f;
    public float cornerLookAhead = 16f;
    public float sharpCornerLookAhead = 10f;
    public float cornerDetectionDistance = 65f;
    public float cornerBrakingDistance = 38f;
    public float sharpCornerSpeed = 70f;

    [Header("Race Feel")]
    [Range(0f, .35f)] public float rubberBandStrength = .12f;
    [Range(0f, .2f)] public float mistakeChance = .04f;
    [Range(0f, .2f)] public float paceVariation = .06f;

    [Header("Avoidance")]
    public float avoidanceDistance = 16f;
    public float avoidanceSideOffset = 3f;
    [Range(0f, 1f)] public float avoidanceBrake = .35f;

    [Header("Recovery")]
    public float stuckSeconds = 2.5f;
    public float flippedSeconds = 1.5f;
    public float offTrackDistance = 40f;

}
