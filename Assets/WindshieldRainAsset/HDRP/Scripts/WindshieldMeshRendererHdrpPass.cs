using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System.Collections.Generic;
using UnityEngine.Rendering.RendererUtils;

namespace ShadedTechnology.WindshieldRainAsset
{
    public class WindshieldMeshRendererHdrpPass : CustomPass
    {
        protected override void Execute(CustomPassContext ctx)
        {
            // Skip if list empty
            if (WindshieldMeshRenderer.ActiveRenderers == null || WindshieldMeshRenderer.ActiveRenderers.Count == 0)
            {
                return;
            }

            // Get command buffer and camera matrices
            var cmd = ctx.cmd;
            //CoreUtils.SetRenderTarget(cmd, ctx.cameraColorBuffer, ctx.cameraDepthBuffer);

            foreach (var entry in WindshieldMeshRenderer.ActiveRenderers)
            {
                if (entry.meshFilter == null || entry.meshFilter.sharedMesh == null || entry.material == null || entry.transform == null)
                {
                    continue;
                }

                int forwardPass = entry.material.FindPass("ForwardOnly");
                if (forwardPass < 0)
                {
                    forwardPass = entry.material.FindPass("Forward");
                }
                if (forwardPass < 0)
                {
                    forwardPass = 0;
                }
                // Draw mesh with provided material
                cmd.DrawMesh(entry.meshFilter.sharedMesh, entry.transform.localToWorldMatrix, entry.material, 0, forwardPass);

            }
        }

        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            // Optional initialization logic
        }

        protected override void Cleanup()
        {
            // Cleanup resources if needed
        }

        protected override bool executeInSceneView => true;
    }
}