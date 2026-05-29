Shader "Imposter/Built-in/Imposter Unlit"
{
    Properties
    {
        _Cutoff("Mask Clip Value", Float) = 0.5
        _Color("Color", Color) = (1,1,1,1)
        _MainTex("Albedo", 2D) = "white" {}
        [HideInInspector] _texcoord("", 2D) = "white" {}
        [HideInInspector] __dirty("", Int) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite On
        AlphaToMask Off

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Cutoff;
            CBUFFER_END

            float ImposterExposure;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(texColor.a - _Cutoff);

                half exposure = max((half)ImposterExposure, 1.0h);
                half3 color = texColor.rgb * _Color.rgb * exposure;

                return half4(color, 1.0h);
            }
            ENDHLSL
        }

    }

    Fallback Off
}
