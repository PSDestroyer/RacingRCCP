Shader "DSRC/Mobile/Car"
{
    Properties { _V("V", Float)=0.8 _Intensity("Intensity", Float)=0 _Base("Base",2D)="white"{} _CubeMapValue("CubeMap Value",2D)="white"{} _Cubemap("Cubemap",CUBE)="white"{} _CubeInensity("Cube Inensity", Float)=0 }
    SubShader { Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass { Name "UniversalForward" Tags { "LightMode"="UniversalForward" } Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 normalWS:TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial) float4 _Base_ST; half _V; CBUFFER_END
            TEXTURE2D(_Base); SAMPLER(sampler_Base);
            Varyings Vert(Attributes IN){ Varyings OUT; VertexPositionInputs pos=GetVertexPositionInputs(IN.positionOS.xyz); OUT.positionCS=pos.positionCS; OUT.uv=IN.uv; OUT.normalWS=TransformObjectToWorldNormal(IN.normalOS); return OUT; }
            half4 Frag(Varyings IN):SV_Target { half4 c=SAMPLE_TEXTURE2D(_Base,sampler_Base,IN.uv*_Base_ST.xy+_Base_ST.zw); half light=saturate(dot(normalize(IN.normalWS), normalize(float3(.35,.75,.45))))*.55+.45; return half4(c.rgb*_V*light,1); }
            ENDHLSL
        }
    }
}