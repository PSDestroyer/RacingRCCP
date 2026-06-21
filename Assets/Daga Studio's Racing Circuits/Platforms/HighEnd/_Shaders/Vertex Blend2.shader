Shader "DSRC/PC/Vertex Blend (RG)"
{
    Properties { _AlbedoR("Albedo (R)", 2D) = "white" {} _AlbedoG("Albedo (G)", 2D) = "white" {} _NormalR("Normal (R)", 2D) = "bump" {} _MRAOR("MRAO (R)", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            CBUFFER_START(UnityPerMaterial) float4 _AlbedoR_ST; float4 _AlbedoG_ST; float4 _MRAOR_ST; CBUFFER_END
            TEXTURE2D(_AlbedoR); SAMPLER(sampler_AlbedoR); TEXTURE2D(_AlbedoG); SAMPLER(sampler_AlbedoG); TEXTURE2D(_MRAOR); SAMPLER(sampler_MRAOR);
            Varyings Vert(Attributes IN) { Varyings OUT; OUT.positionCS=TransformObjectToHClip(IN.positionOS.xyz); OUT.uv=IN.uv; OUT.color=IN.color; return OUT; }
            half4 Frag(Varyings IN) : SV_Target
            {
                half4 r = SAMPLE_TEXTURE2D(_AlbedoR, sampler_AlbedoR, IN.uv*_AlbedoR_ST.xy+_AlbedoR_ST.zw);
                half4 g = SAMPLE_TEXTURE2D(_AlbedoG, sampler_AlbedoG, IN.uv*_AlbedoG_ST.xy+_AlbedoG_ST.zw);
                return lerp(r, g, IN.color.g);
            }
            ENDHLSL
        }
    }
    FallBack Off
}