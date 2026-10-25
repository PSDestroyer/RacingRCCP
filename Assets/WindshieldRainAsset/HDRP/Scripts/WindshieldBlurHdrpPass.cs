using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace ShadedTechnology.WindshieldRainAsset
{
    public class WindshieldBlurHdrpPass : CustomPass
    {
        public int iterations = 3;
        public float blurStrength = 2f;

        private Material blurMaterial;
        private int _iterations;
        // temporary RTs
        RTHandle tempRT_Grab;
        RTHandle[] tempRTsH;
        RTHandle[] tempRTsV;

        void AllocTextures()
        {
            float sizeDivider = 2;
            tempRT_Grab = RTHandles.Alloc(Vector2.one,
                                              TextureXR.slices, dimension: TextureXR.dimension,
                                              colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
                                              depthBufferBits: DepthBits.None,
                                              filterMode: FilterMode.Bilinear,
                                              name: $"WindshieldBlur_Grab");
            for (int i = 0; i < tempRTsH.Length; i++)
            {
                // For downsizing, dividing by 2^i
                tempRTsH[i] = RTHandles.Alloc(Vector2.one / sizeDivider,
                                              TextureXR.slices, dimension: TextureXR.dimension,
                                              colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
                                              depthBufferBits: DepthBits.None,
                                              filterMode: FilterMode.Bilinear,
                                              name: $"WindshieldBlur_H_{i}");
                tempRTsV[i] = RTHandles.Alloc(Vector2.one / sizeDivider,
                                              TextureXR.slices, dimension: TextureXR.dimension,
                                              colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
                                              depthBufferBits: DepthBits.None,
                                              filterMode: FilterMode.Bilinear,
                                              name: $"WindshieldBlur_V_{i}");
                sizeDivider *= 2;
            }
        }

        void DeallocTextures()
        {
            // release allocated RTHandles
            if (tempRT_Grab != null)
            {
                RTHandles.Release(tempRT_Grab);
            }
            for (int i = 0; i < tempRTsH.Length; i++)
            {
                if (tempRTsH != null && tempRTsH[i] != null)
                    RTHandles.Release(tempRTsH[i]);
                if (tempRTsV != null && tempRTsV[i] != null)
                    RTHandles.Release(tempRTsV[i]);
            }
        }

        protected override void Setup(ScriptableRenderContext ctx, CommandBuffer cmd)
        {
            iterations = Mathf.Clamp(iterations, 1, 7);
            _iterations = iterations;
            // Allocate RTHandles
            blurMaterial = new Material(Shader.Find("Hidden/SeparableGlassBlurHDRP"));
            tempRTsH = new RTHandle[_iterations];
            tempRTsV = new RTHandle[_iterations];
            AllocTextures();
        }

        protected override void Execute(CustomPassContext ctx)
        {
            if (blurMaterial == null)
                return;

            // initial source is camera’s color buffer
            RTHandle source = ctx.cameraColorBuffer;
            HDUtils.BlitCameraTexture(ctx.cmd, source, tempRT_Grab);

            source = tempRT_Grab;
            ctx.cmd.SetGlobalTexture("_WindshieldGrabTexture", tempRT_Grab);
            RTHandle destination;

            for (int i = 0; i < _iterations; i++)
            {
                // horizontal pass
                ctx.cmd.SetGlobalVector("_WindshieldRain_Blur_Offsets", new Vector4(blurStrength / tempRTsH[i].rt.width, 0f, 0f, 0f));
                destination = tempRTsH[i];
                HDUtils.BlitCameraTexture(ctx.cmd, source, destination, blurMaterial, 0);

                // vertical pass
                ctx.cmd.SetGlobalVector("_WindshieldRain_Blur_Offsets", new Vector4(0f, blurStrength / tempRTsV[i].rt.height, 0f, 0f));
                destination = tempRTsV[i];
                HDUtils.BlitCameraTexture(ctx.cmd, tempRTsH[i], destination, blurMaterial, 0);

                // set global texture so shaders can access blurred texture at this iteration
                ctx.cmd.SetGlobalTexture($"_GrabBlurTexture_{i}", tempRTsV[i]);
                source = tempRTsV[i];
            }

        }

        protected override void Cleanup()
        {
            DeallocTextures();
        }
    }
}