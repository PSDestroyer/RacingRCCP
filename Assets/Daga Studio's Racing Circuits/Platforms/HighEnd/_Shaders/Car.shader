Shader "DSRC/PC/Car"
{
    Properties { _CarMain_Base_Color("CarMain_Base_Color", 2D) = "white" {} _CarMain_MRAO("CarMain_MRAO", 2D) = "white" {} _Value("Value", Range(0,1)) = 0.75 }
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
            CBUFFER_START(UnityPerMaterial) float4 _CarMain_Base_Color_ST; float4 _CarMain_MRAO_ST; float _Value; CBUFFER_END
            TEXTURE2D(_CarMain_Base_Color); SAMPLER(sampler_CarMain_Base_Color);
            TEXTURE2D(_CarMain_MRAO); SAMPLER(sampler_CarMain_MRAO);
            Varyings Vert(Attributes IN) { Varyings OUT; OUT.positionCS=TransformObjectToHClip(IN.positionOS.xyz); OUT.uv=IN.uv; OUT.color=IN.color; return OUT; }
            half4 Frag(Varyings IN) : SV_Target
            {
                half4 baseCol = SAMPLE_TEXTURE2D(_CarMain_Base_Color, sampler_CarMain_Base_Color, IN.uv*_CarMain_Base_Color_ST.xy+_CarMain_Base_Color_ST.zw);
                half4 mrao = SAMPLE_TEXTURE2D(_CarMain_MRAO, sampler_CarMain_MRAO, IN.uv*_CarMain_MRAO_ST.xy+_CarMain_MRAO_ST.zw);
                baseCol.rgb *= lerp(0.85h, 1.25h, saturate(mrao.g * _Value + 0.25h));
                return baseCol * IN.color;
            }
            ENDHLSL
        }
    }
    FallBack Off
}