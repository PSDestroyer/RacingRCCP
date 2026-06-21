Shader "DSRC/PC/Standard MR"
{
    Properties { _Albedo("Albedo", 2D) = "white" {} _Color("Color", Color) = (1,1,1,1) }
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
            CBUFFER_START(UnityPerMaterial) float4 _Albedo_ST; half4 _Color; CBUFFER_END
            TEXTURE2D(_Albedo); SAMPLER(sampler_Albedo);
            Varyings Vert(Attributes IN) { Varyings OUT; OUT.positionCS=TransformObjectToHClip(IN.positionOS.xyz); OUT.uv=IN.uv*_Albedo_ST.xy+_Albedo_ST.zw; OUT.color=IN.color; return OUT; }
            half4 Frag(Varyings IN) : SV_Target { return SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, IN.uv) * _Color * IN.color; }
            ENDHLSL
        }
    }
    FallBack Off
}
