Shader "Formula/Track Rubber Wear"
{
    Properties
    {
        _BaseMap ("Tread coverage", 2D) = "white" {}
        _Strength ("Rubber darkening", Range(0, 1)) = 0.62
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-20" "IgnoreProjector"="True" }
        Pass
        {
            Name "RubberWear"
            Tags { "LightMode"="SRPDefaultUnlit" }
            // Modulate the already lit road, including its reflections. White is
            // neutral, so distant/fogged marks disappear without a bright halo.
            Blend DstColor Zero
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _Strength;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half coverage : TEXCOORD1; half fog : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.coverage = input.color.a;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half tread = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).r;
                half darkening = saturate(tread * input.coverage * _Strength);
                return half4(MixFogColor((1.0h - darkening).xxx, half3(1, 1, 1), input.fog), 1);
            }
            ENDHLSL
        }
    }
}
