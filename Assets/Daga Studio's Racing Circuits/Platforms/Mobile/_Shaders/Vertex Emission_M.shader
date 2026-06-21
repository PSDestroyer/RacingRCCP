Shader "DSRC/Mobile/Vertex Emission"
{
    Properties { _Emission("Emission", Float)=1 _Color("Color", Color)=(1,1,1,1) }
    SubShader { Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass { Name "UniversalForward" Tags { "LightMode"="UniversalForward" } Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; };
            CBUFFER_START(UnityPerMaterial) half _Emission; half4 _Color; CBUFFER_END
            Varyings Vert(Attributes IN){ Varyings OUT; OUT.positionCS=TransformObjectToHClip(IN.positionOS.xyz); return OUT; }
            half4 Frag(Varyings IN):SV_Target { return half4(_Color.rgb*_Emission,1); }
            ENDHLSL
        }
    }
}