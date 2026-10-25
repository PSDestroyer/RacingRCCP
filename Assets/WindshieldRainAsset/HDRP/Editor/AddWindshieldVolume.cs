using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace ShadedTechnology.WindshieldRainAsset
{

    public class AddWindshieldVolume
    {
        public static bool HasWindshieldVolume()
        {
            var volumes = GameObject.FindObjectsOfType<CustomPassVolume>();

            foreach(var volume in volumes)
            {
                if (!volume.isGlobal || volume.injectionPoint != CustomPassInjectionPoint.BeforePostProcess)
                {
                    continue;
                }
                bool hasBlurPass = false;
                bool hasMeshRenderPass = false;
                foreach(var pass in volume.customPasses)
                {
                    if (pass.GetType() == typeof(WindshieldBlurHdrpPass))
                    {
                        if (hasMeshRenderPass)
                        {
                            break;
                        }
                        hasBlurPass = true;
                    }
                    if (pass.GetType() == typeof(WindshieldMeshRendererHdrpPass))
                    {
                        hasMeshRenderPass = true;
                    }
                    if (hasMeshRenderPass && hasBlurPass)
                    {
                        return true;
                    }
                }
                if (hasMeshRenderPass && hasBlurPass)
                {
                    return true;
                }
            }
            return false;
        }

        public static void AddWindshieldVolumeObject()
        {
            GameObject windshieldVolumeObject = new GameObject("WindshieldRenderingVolume");
            CustomPassVolume customPassComponent = windshieldVolumeObject.AddComponent<CustomPassVolume>();

            customPassComponent.injectionPoint = CustomPassInjectionPoint.BeforePostProcess;
            WindshieldBlurHdrpPass windshieldBlurHdrpPass = new WindshieldBlurHdrpPass();
            windshieldBlurHdrpPass.name = "WindshieldBlurHdrpPass";
            windshieldBlurHdrpPass.targetColorBuffer = CustomPass.TargetBuffer.None;
            windshieldBlurHdrpPass.targetDepthBuffer = CustomPass.TargetBuffer.None;
            windshieldBlurHdrpPass.clearFlags = UnityEngine.Rendering.ClearFlag.None;
            customPassComponent.customPasses.Add(windshieldBlurHdrpPass);
            WindshieldMeshRendererHdrpPass windshieldMeshRendererHdrpPass = new WindshieldMeshRendererHdrpPass();
            windshieldMeshRendererHdrpPass.name = "WindshieldMeshRendererHdrpPass";
            windshieldMeshRendererHdrpPass.targetColorBuffer = CustomPass.TargetBuffer.Camera;
            windshieldMeshRendererHdrpPass.targetDepthBuffer = CustomPass.TargetBuffer.Camera;
            windshieldMeshRendererHdrpPass.clearFlags = UnityEngine.Rendering.ClearFlag.None;
            customPassComponent.customPasses.Add(windshieldMeshRendererHdrpPass);
        }
    }

}
