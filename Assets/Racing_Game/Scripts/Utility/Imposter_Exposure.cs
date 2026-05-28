using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ALIyerEdon
{
    [ExecuteInEditMode]
    public class Imposter_Exposure : MonoBehaviour
    {
        public Color color = Color.white;
        public string propertyName = "_BaseColor";
        public Material[] materials;

        public bool apply = false;

        void Start()
        {
            Apply_Imposter_Exposure(color);
        }

        // Update is called once per frame
        void Update()
        {
            if (apply)
            {
                Apply_Imposter_Exposure(color);

                apply = false;
            }
        }

        public void Apply_Imposter_Exposure(Color color)
        {
            foreach(Material mat in materials)
                mat.SetColor(propertyName, color);
        }
    }
}