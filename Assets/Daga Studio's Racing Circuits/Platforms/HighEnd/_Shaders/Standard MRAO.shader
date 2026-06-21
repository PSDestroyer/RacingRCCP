Shader "DSRC/PC/Standard MRAO"
{
    Properties { _Albedo("Albedo", 2D) = "white" {} _MRAO("MRAO", 2D) = "white" {} _Normal("Normal", 2D) = "bump" {} _Color("Color", Color) = (1,1,1,1) }
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
            CBUFFER_START(UnityPerMaterial) float4 _Albedo_ST; float4 _MRAO_ST; float4 _Normal_ST; half4 _Color; CBUFFER_END
            TEXTURE2D(_Albedo); SAMPLER(sampler_Albedo);
            TEXTURE2D(_MRAO); SAMPLER(sampler_MRAO);
            TEXTURE2D(_Normal); SAMPLER(sampler_Normal);
            Varyings Vert(Attributes IN) { Varyings OUT; OUT.positionCS=TransformObjectToHClip(IN.positionOS.xyz); OUT.uv=IN.uv; OUT.color=IN.color; return OUT; }
            half4 Frag(Varyings IN) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, IN.uv*_Albedo_ST.xy+_Albedo_ST.zw);
                half4 mrao = SAMPLE_TEXTURE2D(_MRAO, sampler_MRAO, IN.uv*_MRAO_ST.xy+_MRAO_ST.zw);
                albedo.rgb *= lerp(0.75h, 1.15h, mrao.g);
                return albedo * _Color * IN.color;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
