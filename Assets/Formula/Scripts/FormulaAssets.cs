using System;
using UnityEngine;

namespace FormulaGame
{
    [CreateAssetMenu(menuName = "Formula/Assets")]
    public sealed class FormulaAssets : ScriptableObject
    {
        public GameObject vehiclePrefab;
        public Material[] liveryMaterials;
        public Track[] tracks;
        public static FormulaAssets Current => Resources.Load<FormulaAssets>("Formula/FormulaAssets");
        public Track FindTrack(string id) => Array.Find(tracks, t => t.id == id);
        [Serializable] public sealed class Track
        {
            public string id, displayName, scenePath;
            public float referenceLapSeconds;
            public bool referenceCalibrated;
            public string sourcePack;
            public float lengthMeters;
        }
    }
}
