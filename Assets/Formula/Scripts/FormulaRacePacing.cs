using UnityEngine;

namespace FormulaGame
{
    // Continuous, bounded assistance. Positive gaps mean the opponent is ahead.
    // No transform/velocity changes, checkpoint skips, or extra runtime objects.
    public static class FormulaRacePacing
    {
        public static float Multiplier(float gapMeters, float finishFade)
        {
            float strength = gapMeters < 0
                ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(18, 300, -gapMeters)) * .25f
                : -Mathf.SmoothStep(0, 1, Mathf.InverseLerp(25, 250, gapMeters)) * .05f;
            return 1 + strength * Mathf.Clamp01(finishFade);
        }
        public static float SpeedFactor(float multiplier, float plannedSpeed)
        {
            if (multiplier <= 1) return Mathf.Clamp(multiplier, .95f, 1);
            float cornerAllowance = Mathf.Lerp(.025f, .10f, Mathf.InverseLerp(55, 155, plannedSpeed));
            return 1 + cornerAllowance * Mathf.InverseLerp(1, 1.25f, multiplier);
        }
    }
}
