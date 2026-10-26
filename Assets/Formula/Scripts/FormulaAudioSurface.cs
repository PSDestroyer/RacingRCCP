using UnityEngine;

namespace FormulaGame
{
    public enum FormulaAudioSurfaceKind { Asphalt, Kerb, Grass, Gravel, WetAsphalt, Metal, Barrier, Snow }

    /// <summary>Acoustic identity only: never changes RCCP friction or collision behaviour.</summary>
    [DisallowMultipleComponent]
    public sealed class FormulaAudioSurface : MonoBehaviour
    {
        public FormulaAudioSurfaceKind kind = FormulaAudioSurfaceKind.Asphalt;
        [Range(0, 1)] public float wetness;
        public TerrainLayer[] terrainLayers = System.Array.Empty<TerrainLayer>();
        public FormulaAudioSurfaceKind[] terrainKinds = System.Array.Empty<FormulaAudioSurfaceKind>();
        [SerializeField, HideInInspector] private byte[] terrainMap = System.Array.Empty<byte>();
        [SerializeField, HideInInspector] private int terrainResolution;
        [SerializeField, HideInInspector] private Vector3 terrainSize, terrainOffset;
        public FormulaAudioSurfaceKind EffectiveKind => kind == FormulaAudioSurfaceKind.Asphalt && wetness >= .3f
            ? FormulaAudioSurfaceKind.WetAsphalt : kind;
        public int TerrainResolution => terrainResolution;
        public FormulaAudioSurfaceKind Sample(Vector3 worldPosition)
        {
            if (terrainResolution < 2 || terrainMap == null || terrainMap.Length != terrainResolution * terrainResolution)
                return EffectiveKind;
            Vector3 local = transform.InverseTransformPoint(worldPosition) - terrainOffset;
            int x = Mathf.Clamp(Mathf.FloorToInt(local.x / Mathf.Max(.01f, terrainSize.x) * terrainResolution), 0, terrainResolution - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(local.z / Mathf.Max(.01f, terrainSize.z) * terrainResolution), 0, terrainResolution - 1);
            var result = (FormulaAudioSurfaceKind)terrainMap[z * terrainResolution + x];
            return result == FormulaAudioSurfaceKind.Asphalt && wetness >= .3f ? FormulaAudioSurfaceKind.WetAsphalt : result;
        }
#if UNITY_EDITOR
        /// <summary>Bake a small acoustic lookup in the Editor, never allocate alphamaps while driving.</summary>
        public void BakeTerrain(Terrain terrain, FormulaAudioSurfaceKind[] layerKinds, int resolution = 64)
        {
            if (terrain == null || terrain.terrainData == null) { terrainMap = System.Array.Empty<byte>(); terrainResolution = 0; return; }
            var data = terrain.terrainData;
            terrainLayers = data.terrainLayers;
            terrainKinds = layerKinds != null ? (FormulaAudioSurfaceKind[])layerKinds.Clone() : System.Array.Empty<FormulaAudioSurfaceKind>();
            terrainResolution = Mathf.Clamp(resolution, 8, 128);
            terrainMap = new byte[terrainResolution * terrainResolution];
            terrainSize = data.size; terrainOffset = transform.InverseTransformPoint(terrain.transform.position);
            if (data.alphamapLayers == 0) { for (int i = 0; i < terrainMap.Length; i++) terrainMap[i] = (byte)kind; return; }
            var alpha = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
            for (int z = 0; z < terrainResolution; z++)
            for (int x = 0; x < terrainResolution; x++)
            {
                int ax = Mathf.Min(data.alphamapWidth - 1, (int)((x + .5f) / terrainResolution * data.alphamapWidth));
                int az = Mathf.Min(data.alphamapHeight - 1, (int)((z + .5f) / terrainResolution * data.alphamapHeight));
                int selected = 0; float weight = -1;
                for (int layer = 0; layer < data.alphamapLayers; layer++)
                    if (alpha[az, ax, layer] > weight) { weight = alpha[az, ax, layer]; selected = layer; }
                terrainMap[z * terrainResolution + x] = (byte)(selected < terrainKinds.Length ? terrainKinds[selected] : kind);
            }
        }
#endif
    }
}
