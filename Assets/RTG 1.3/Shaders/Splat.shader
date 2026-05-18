Shader "Custom/Vertex Color Splat Surf Shader"
{
    Properties
    {
        _ColorA ("Color A", Color) = (1, 1, 1, 1)
        _Splat1 ("Base A (RGB)", 2D) = "white" {}
        _IntensityA ("Intensity A", Range(0.5, 1.5)) = 1

        _ColorC ("Color C", Color) = (1, 1, 1, 1)
        _Splat3 ("Base C (RGB)", 2D) = "white" {}
        _IntensityC ("Intensity C", Range(0.5, 1.5)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            TEXTURE2D(_Splat1);
            SAMPLER(sampler_Splat1);
            TEXTURE2D(_Splat3);
            SAMPLER(sampler_Splat3);

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorA;
                float4 _Splat1_ST;
                float _IntensityA;
                float4 _ColorC;
                float4 _Splat3_ST;
                float _IntensityC;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = input.uv;
                output.color = input.color;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 splatA = SAMPLE_TEXTURE2D(_Splat1, sampler_Splat1, TRANSFORM_TEX(input.uv, _Splat1)) * _ColorA * _IntensityA;
                half4 splatC = SAMPLE_TEXTURE2D(_Splat3, sampler_Splat3, TRANSFORM_TEX(input.uv, _Splat3)) * _ColorC * _IntensityC;

                half3 albedo = lerp(splatC.rgb, splatA.rgb, saturate(input.color.b));

                Light mainLight = GetMainLight();
                half3 normalWS = normalize(input.normalWS);
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = SampleSH(normalWS) + mainLight.color * ndotl;

                return half4(albedo * lighting, 1);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
