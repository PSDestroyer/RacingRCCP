Shader "DSRC/PC/Vertex Blend"
{
    Properties
    {
        _AlbedoR("Albedo (R)", 2D) = "white" {} _AlbedoG("Albedo (G)", 2D) = "white" {} _AlbedoB("Albedo (B)", 2D) = "white" {} _AlbedoA("Albedo (A)", 2D) = "white" {}
        _NormalR("Normal (R)", 2D) = "bump" {} _NormalG("Normal (G)", 2D) = "bump" {} _NormalB("Normal (B)", 2D) = "bump" {} _NormalA("Normal (A)", 2D) = "bump" {}
        _MRAOR("MRAO (R)", 2D) = "white" {} _MRAOG("MRAO (G)", 2D) = "white" {} _MRAOB("MRAO (B)", 2D) = "white" {}
    }
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
            CBUFFER_START(UnityPerMaterial)
                float4 _AlbedoR_ST; float4 _AlbedoG_ST; float4 _AlbedoB_ST; float4 _AlbedoA_ST;
                float4 _MRAOR_ST; float4 _MRAOG_ST; float4 _MRAOB_ST;
            CBUFFER_END
            TEXTURE2D(_AlbedoR); SAMPLER(sampler_AlbedoR); TEXTURE2D(_AlbedoG); SAMPLER(sampler_AlbedoG); TEXTURE2D(_AlbedoB); SAMPLER(sampler_AlbedoB); TEXTURE2D(_AlbedoA); SAMPLER(sampler_AlbedoA);
            TEXTURE2D(_MRAOR); SAMPLER(sampler_MRAOR); TEXTURE2D(_MRAOG); SAMPLER(sampler_MRAOG); TEXTURE2D(_MRAOB); SAMPLER(sampler_MRAOB);
            Varyings Vert(Attributes IN) { Varyings OUT; OUT.positionCS=TransformObjectToHClip(IN.positionOS.xyz); OUT.uv=IN.uv; OUT.color=IN.color; return OUT; }
            half4 Frag(Varyings IN) : SV_Target
            {
                half4 a = SAMPLE_TEXTURE2D(_AlbedoA, sampler_AlbedoA, IN.uv*_AlbedoA_ST.xy+_AlbedoA_ST.zw);
                half4 r = SAMPLE_TEXTURE2D(_AlbedoR, sampler_AlbedoR, IN.uv*_AlbedoR_ST.xy+_AlbedoR_ST.zw);
                half4 g = SAMPLE_TEXTURE2D(_AlbedoG, sampler_AlbedoG, IN.uv*_AlbedoG_ST.xy+_AlbedoG_ST.zw);
                half4 b = SAMPLE_TEXTURE2D(_AlbedoB, sampler_AlbedoB, IN.uv*_AlbedoB_ST.xy+_AlbedoB_ST.zw);
                half4 rgbBlend = lerp(lerp(r, g, IN.color.g), b, IN.color.b);
                return lerp(a, rgbBlend, IN.color.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}