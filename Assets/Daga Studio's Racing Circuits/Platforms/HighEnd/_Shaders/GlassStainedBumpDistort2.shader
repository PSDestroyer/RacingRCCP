Shader "FX/Glass/Stained BumpDistort2"
{
    Properties { _BumpAmt("Distortion", Range(0,128)) = 10 _MainTex("Tint Color (RGB)", 2D) = "white" {} _BumpMap("Normalmap", 2D) = "bump" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
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
            CBUFFER_START(UnityPerMaterial) float4 _MainTex_ST; float4 _BumpMap_ST; float _BumpAmt; CBUFFER_END
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex); TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            Varyings Vert(Attributes IN) { Varyings OUT; OUT.positionCS=TransformObjectToHClip(IN.positionOS.xyz); OUT.uv=IN.uv*_MainTex_ST.xy+_MainTex_ST.zw; OUT.color=IN.color; return OUT; }
            half4 Frag(Varyings IN) : SV_Target { half4 tint=SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv); tint.a=saturate(tint.a * 0.45h + 0.25h); return tint * IN.color; }
            ENDHLSL
        }
    }
    FallBack Off
}