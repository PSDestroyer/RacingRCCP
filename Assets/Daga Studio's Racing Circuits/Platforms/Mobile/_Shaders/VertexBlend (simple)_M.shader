Shader "DSRC/Mobile/VertexBlend (Simple)"
{
    Properties { _Intensity("Intensity", Float)=0 _Metallic("Metallic", Range(0,1))=1 _Smoothness("Smoothness", Range(0,1))=0.85 _BaseR("Base (R)",2D)="white"{} _BaseG("Base (G)",2D)="white"{} _BaseB("Base (B)",2D)="white"{} _BaseA("Base (A)",2D)="white"{} _Cubemap1("Cubemap",CUBE)="white"{} _CubeMapValue("CubeMap Value",2D)="white"{} }
    SubShader { Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass { Name "UniversalForwardOnly" Tags { "LightMode"="UniversalForwardOnly" } Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; float3 normalWS:TEXCOORD1; float3 positionWS:TEXCOORD2; };
            CBUFFER_START(UnityPerMaterial) float4 _BaseR_ST,_BaseG_ST,_BaseB_ST,_BaseA_ST; half _Intensity; half _Metallic; half _Smoothness; CBUFFER_END
            TEXTURE2D(_BaseR); SAMPLER(sampler_BaseR); TEXTURE2D(_BaseG); SAMPLER(sampler_BaseG); TEXTURE2D(_BaseB); SAMPLER(sampler_BaseB); TEXTURE2D(_BaseA); SAMPLER(sampler_BaseA);
            Varyings Vert(Attributes IN){ Varyings OUT; VertexPositionInputs pos=GetVertexPositionInputs(IN.positionOS.xyz); OUT.positionCS=pos.positionCS; OUT.positionWS=pos.positionWS; OUT.uv=IN.uv; OUT.color=IN.color; OUT.normalWS=TransformObjectToWorldNormal(IN.normalOS); return OUT; }
            half4 Frag(Varyings IN):SV_Target
            {
                half4 r=SAMPLE_TEXTURE2D(_BaseR,sampler_BaseR,IN.uv*_BaseR_ST.xy+_BaseR_ST.zw);
                half4 g=SAMPLE_TEXTURE2D(_BaseG,sampler_BaseG,IN.uv*_BaseG_ST.xy+_BaseG_ST.zw);
                half4 b=SAMPLE_TEXTURE2D(_BaseB,sampler_BaseB,IN.uv*_BaseB_ST.xy+_BaseB_ST.zw);
                half4 a=SAMPLE_TEXTURE2D(_BaseA,sampler_BaseA,IN.uv*_BaseA_ST.xy+_BaseA_ST.zw);
                half4 c=lerp(a, lerp(lerp(r,g,IN.color.g), b, IN.color.b), IN.color.a);

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(IN.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask = half4(1,1,1,1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = c.rgb;
                surfaceData.metallic = _Metallic;
                surfaceData.specular = half3(0,0,0);
                surfaceData.smoothness = _Smoothness;
                surfaceData.occlusion = 1;
                surfaceData.emission = c.rgb * _Intensity;
                surfaceData.alpha = 1;

                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }
    }
}
