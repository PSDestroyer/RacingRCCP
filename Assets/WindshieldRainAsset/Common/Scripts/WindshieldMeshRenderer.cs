using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ShadedTechnology.WindshieldRainAsset
{

    [ExecuteInEditMode]
    [RequireComponent(typeof(MeshFilter))]
    [AddComponentMenu("Windshield Rain Asset/WindshieldMeshRenderer")]
    public class WindshieldMeshRenderer : MonoBehaviour
    {
        public static List<WindshieldMeshRenderer> ActiveRenderers = new List<WindshieldMeshRenderer>();

        [HideInInspector] public MeshFilter meshFilter;
        public Material material;


        private bool added = false;
        private void Init()
        {
            meshFilter = GetComponent<MeshFilter>();

            if (!material)
            {
                var mr = GetComponent<MeshRenderer>();
                if (mr) material = mr.sharedMaterial;
            }

            if (!added)
            {
                ActiveRenderers.Add(this);
                added = true;
            }
        }

        void OnEnable()
        {
            Init();
        }

        void OnDisable()
        {
            ActiveRenderers.Remove(this);
            added = false;
        }

        private void OnDestroy()
        {
            ActiveRenderers.Remove(this);
            added = false;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            AssetsManager.SetGizmoIconEnabled(typeof(WindshieldMeshRenderer), false);
            if (meshFilter == null || meshFilter.sharedMesh == null) return;

            var color = Gizmos.color;
            Gizmos.color = Color.clear;
            Gizmos.DrawMesh(meshFilter.sharedMesh, 0, transform.position, transform.rotation, transform.lossyScale);
            Gizmos.color = color;
        }

        void OnDrawGizmosSelected()
        {
            if (meshFilter == null || meshFilter.sharedMesh == null) return;

            // Draw an orange wireframe when selected
            Gizmos.color = new Color(0f, 0.5f, 0f, 0.1f); // Unity orange
            Gizmos.DrawWireMesh(meshFilter.sharedMesh, transform.position, transform.rotation, transform.lossyScale);
        }
#endif
    }

}
