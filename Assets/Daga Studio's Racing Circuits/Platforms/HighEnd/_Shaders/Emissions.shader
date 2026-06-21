Shader "Emissions"
{
    Properties
    {
        _Base("Base", 2D) = "white" {}
        _Normal("Normal", 2D) = "bump" {}
        _Color("Color", Color) = (1,1,1,1)
        _Cutoff("Mask Clip Value", Float) = 0
        _BaseLight("Base Light", Float) = 0
        _Fade("Fade", Float) = 1
        _FadeValue("Fade Value", Float) = 1
        _Intensity("Intensity", Float) = 1
        _Value("Value", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Blend One Zero
        ZWrite On
        Cull Back
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
                float4 _Base_ST;
                half4 _Color;
                float _Cutoff;
                float _BaseLight;
                float _Fade;
                float _FadeValue;
                float _Intensity;
                float _Value;
            CBUFFER_END
            TEXTURE2D(_Base); SAMPLER(sampler_Base);
            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv * _Base_ST.xy + _Base_ST.zw;
                OUT.color = IN.color;
                return OUT;
            }
            half4 Frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_Base, sampler_Base, IN.uv);
                half4 col = tex * _Color * _Intensity;
                col.a *= 1;
                
                col.rgb += tex.rgb * _BaseLight;
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
