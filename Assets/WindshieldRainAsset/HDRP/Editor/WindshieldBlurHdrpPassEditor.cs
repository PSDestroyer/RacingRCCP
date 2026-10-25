using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;

namespace ShadedTechnology.WindshieldRainAsset
{

    [CustomPassDrawerAttribute(typeof(WindshieldBlurHdrpPass))]
    public class WindshieldBlurHdrpPassEditor : CustomPassDrawer
    {
        WindshieldBlurHdrpPass _target;

        WindshieldBlurHdrpPass GetTarget(SerializedProperty customPass)
        {
            if (_target == null)
            {
                object obj = GetTargetObjectOfProperty(customPass);
                _target = obj as WindshieldBlurHdrpPass;
            }
            return _target;
        }

        // Works in older Unity versions: recursively extracts the actual object from a SerializedProperty
        static object GetTargetObjectOfProperty(SerializedProperty prop)
        {
            if (prop == null) return null;

            object obj = prop.serializedObject.targetObject;
            string path = prop.propertyPath.Replace(".Array.data[", "[");
            string[] elements = path.Split('.');

            foreach (var element in elements)
            {
                if (element.Contains("["))
                {
                    string elementName = element.Substring(0, element.IndexOf("["));
                    int index = System.Convert.ToInt32(
                        element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", "")
                    );
                    obj = GetValue_Imp(obj, elementName, index);
                }
                else
                {
                    obj = GetValue_Imp(obj, element);
                }
            }
            return obj;
        }

        static object GetValue_Imp(object source, string name)
        {
            if (source == null)
                return null;

            var type = source.GetType();
            var f = type.GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (f != null)
                return f.GetValue(source);

            var p = type.GetProperty(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            if (p != null)
                return p.GetValue(source, null);

            return null;
        }

        static object GetValue_Imp(object source, string name, int index)
        {
            var enumerable = GetValue_Imp(source, name) as System.Collections.IEnumerable;
            if (enumerable == null) return null;
            var enm = enumerable.GetEnumerator();
            for (int i = 0; i <= index; i++)
            {
                if (!enm.MoveNext()) return null;
            }
            return enm.Current;
        }


        protected override void Initialize(SerializedProperty customPass)
        {
            // Called once when the inspector opens
        }

        protected override void DoPassGUI(SerializedProperty customPass, Rect rect)
        {
            WindshieldBlurHdrpPass myTarget = GetTarget(customPass);
            myTarget.iterations = Mathf.Clamp(myTarget.iterations, 1, 7);
            myTarget.targetColorBuffer = UnityEngine.Rendering.HighDefinition.CustomPass.TargetBuffer.None;
            myTarget.targetDepthBuffer = UnityEngine.Rendering.HighDefinition.CustomPass.TargetBuffer.None;
            myTarget.clearFlags = UnityEngine.Rendering.ClearFlag.None;
            base.DoPassGUI(customPass, rect);

            if (!AssetsManager.HasBlurShaderCorrectIterationsCount(myTarget.iterations))
            {
                EditorGUILayout.HelpBox("Current blur shader has different `iteration` count set. Regenarete blur shader!", MessageType.Warning);
            }
            if (GUILayout.Button("Regenarate blur shader"))
            {
                AssetsManager.RegenerateBlurShader(myTarget.iterations);
            }
        }
    }
}