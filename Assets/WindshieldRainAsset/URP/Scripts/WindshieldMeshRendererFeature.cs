using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if !URP_COMPATIBILITY_MODE && UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace ShadedTechnology.WindshieldRainAsset
{

    public class WindshieldMeshRendererFeature : ScriptableRendererFeature
    {
        class WindshieldMeshRenderPass : ScriptableRenderPass
        {

            public WindshieldMeshRenderPass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            }

            private class PassData
            {
                public List<WindshieldMeshRenderer> entries;
            }

#if !URP_COMPATIBILITY_MODE && UNITY_6000_0_OR_NEWER
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameContext)
            {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Draw Custom Meshes", out var passData))
                {
                    passData.entries = WindshieldMeshRenderer.ActiveRenderers;

                    UniversalResourceData resourceData = frameContext.Get<UniversalResourceData>();
                    if (resourceData.activeColorTexture.IsValid())
                    {
                        builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                    }
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);

                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        foreach (var e in data.entries)
                        {
                            if (e.enabled && e.meshFilter && e.meshFilter.sharedMesh && e.material)
                            {
                                ctx.cmd.DrawMesh(e.meshFilter.sharedMesh, e.transform.localToWorldMatrix, e.material, 0, 0);
                            }
                        }
                    });
                }
            }
#else
#if URP_COMPATIBILITY_MODE
            [System.Obsolete]
#endif
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                const string profilerTag = "Draw Windshield Meshes";
                var cmd = CommandBufferPool.Get(profilerTag);
#if UNITY_2022_1_OR_NEWER
                var renderer = renderingData.cameraData.renderer;
                var colorTarget = renderer.cameraColorTargetHandle;
                var depthTarget = renderer.cameraDepthTargetHandle;
                cmd.SetRenderTarget(colorTarget, depthTarget);
#endif

                using (new ProfilingScope(cmd, profilingSampler))
                {
                    var camera = renderingData.cameraData.camera;
                    var entries = WindshieldMeshRenderer.ActiveRenderers;

                    foreach (var e in entries)
                    {
                        if (e && e.enabled && e.meshFilter && e.meshFilter.sharedMesh && e.material)
                        {
                            cmd.DrawMesh(e.meshFilter.sharedMesh, e.transform.localToWorldMatrix, e.material, 0, 0);
                        }
                    }
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
#endif

        }

        private WindshieldMeshRenderPass pass;

        public override void Create()
        {
            pass = new WindshieldMeshRenderPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            if (descriptor.graphicsFormat == UnityEngine.Experimental.Rendering.GraphicsFormat.None)
            {
                return;
            }
            renderer.EnqueuePass(pass);
        }
    }
}
