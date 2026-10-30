using System;
using UnityEngine;

namespace FormulaGame
{
    // Cosmetic response only. Dry values always come from the current material,
    // so changing livery or repeatedly drying the car never compounds its finish.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class FormulaVehicleSurface : MonoBehaviour
    {
        public enum SurfaceKind { Paint, Carbon, Rim, Rubber }
        [Serializable] public sealed class Binding
        {
            public Renderer renderer;
            public int materialIndex;
            public SurfaceKind kind;
        }
        [SerializeField, Range(0, 1), Tooltip("Cosmetic water film: 0 = dry, 1 = wet. Does not affect tyre grip.")]
        private float wetness;
        [SerializeField] private Binding[] surfaces = Array.Empty<Binding>();
        private struct DrySurface
        {
            public Color color;
            public float smoothness, metallic, coatMask, coatSmoothness, detailNormal;
            public bool valid, hasCoat, hasDetail;
        }
        private DrySurface[] dry;
        private MaterialPropertyBlock block;
        private MaterialPropertyBlock[] originalOverrides;
        private bool applied;
        private bool pending;
        private static readonly int Smoothness = Shader.PropertyToID("_Smoothness"), Metallic = Shader.PropertyToID("_Metallic"), BaseColor = Shader.PropertyToID("_BaseColor"), CoatMask = Shader.PropertyToID("_ClearCoatMask"), CoatSmoothness = Shader.PropertyToID("_ClearCoatSmoothness"), DetailNormal = Shader.PropertyToID("_DetailNormalMapScale");
        public float Wetness => wetness;
        public int SurfaceCount => surfaces.Length;
        private void OnEnable() { originalOverrides = null; RefreshMaterials(); }
        private void OnValidate() { wetness = Mathf.Clamp01(wetness); pending = true; }
        private void Update() { if (pending) RefreshMaterials(); }
        private void OnDisable() => RestoreOverrides();

        public void SetWetness(float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (Mathf.Approximately(wetness, amount) && !pending && dry != null) return;
            wetness = amount;
            if (dry == null || pending) RefreshMaterials(); else Apply(wetness);
        }

        public void RefreshMaterials()
        {
            pending = false;
            block ??= new MaterialPropertyBlock();
            if (originalOverrides == null || originalOverrides.Length != surfaces.Length)
            {
                originalOverrides = new MaterialPropertyBlock[surfaces.Length];
                for (int i = 0; i < surfaces.Length; i++)
                {
                    originalOverrides[i] = new MaterialPropertyBlock();
                    if (surfaces[i].renderer != null) surfaces[i].renderer.GetPropertyBlock(originalOverrides[i], surfaces[i].materialIndex);
                }
            }
            dry = new DrySurface[surfaces.Length];
            for (int i = 0; i < surfaces.Length; i++)
            {
                var binding = surfaces[i];
                if (binding.renderer == null) continue;
                var materials = binding.renderer.sharedMaterials;
                if (binding.materialIndex < 0 || binding.materialIndex >= materials.Length) continue;
                var material = materials[binding.materialIndex];
                if (material == null || !material.HasProperty(Smoothness)) continue;
                dry[i] = new DrySurface
                {
                    valid = true, color = material.GetColor(BaseColor),
                    smoothness = material.GetFloat(Smoothness), metallic = material.GetFloat(Metallic),
                    hasCoat = material.HasProperty(CoatMask), hasDetail = material.HasProperty(DetailNormal),
                    coatMask = material.HasProperty(CoatMask) ? material.GetFloat(CoatMask) : 0,
                    coatSmoothness = material.HasProperty(CoatSmoothness) ? material.GetFloat(CoatSmoothness) : 0,
                    detailNormal = material.HasProperty(DetailNormal) ? material.GetFloat(DetailNormal) : 0
                };
            }
            Apply(wetness);
        }

        private void Apply(float amount)
        {
            if (block == null || dry == null) return;
            // No property blocks on a dry car: native URP materials can use the
            // SRP Batcher. A wet car restores any pre-existing overrides on drying.
            if (amount <= 0) { RestoreOverrides(); return; }
            for (int i = 0; i < surfaces.Length && i < dry.Length; i++)
            {
                var binding = surfaces[i]; var source = dry[i];
                if (binding.renderer == null || !source.valid) continue;
                float wetSmoothness = binding.kind == SurfaceKind.Rubber ? .3f : binding.kind == SurfaceKind.Carbon ? .48f : binding.kind == SurfaceKind.Rim ? .58f : .64f;
                float darkening = binding.kind == SurfaceKind.Rubber ? .72f : binding.kind == SurfaceKind.Carbon ? .86f : .97f;
                Color color = source.color * Mathf.Lerp(1, darkening, amount); color.a = source.color.a;
                // Per-slot blocks keep the carbon floor independent from the painted shell.
                binding.renderer.GetPropertyBlock(block, binding.materialIndex);
                block.SetColor(BaseColor, color);
                block.SetFloat(Smoothness, Mathf.Lerp(source.smoothness, Mathf.Max(source.smoothness, wetSmoothness), amount));
                block.SetFloat(Metallic, source.metallic);
                if (source.hasCoat)
                {
                    block.SetFloat(CoatMask, Mathf.Lerp(source.coatMask, 1, amount));
                    block.SetFloat(CoatSmoothness, Mathf.Lerp(source.coatSmoothness, .98f, amount));
                }
                if (source.hasDetail) block.SetFloat(DetailNormal, source.detailNormal * Mathf.Lerp(1, .45f, amount));
                binding.renderer.SetPropertyBlock(block, binding.materialIndex); block.Clear();
            }
            applied = true;
        }

        private void RestoreOverrides()
        {
            if (!applied || originalOverrides == null) return;
            for (int i = 0; i < surfaces.Length && i < originalOverrides.Length; i++)
                if (surfaces[i].renderer != null)
                    surfaces[i].renderer.SetPropertyBlock(originalOverrides[i].isEmpty ? null : originalOverrides[i], surfaces[i].materialIndex);
            applied = false;
        }
    }
}
