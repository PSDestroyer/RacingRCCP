Shader "DSRC/Mobile/Vertex Alpha Blend"
{
    Properties { _Cutoff("Mask Clip Value", Float)=0.5 _BaseR("Base (R)",2D)="white"{} _BaseG("Base (G)",2D)="white"{} }
    SubShader { Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Pass { Name "UniversalForward" Tags { "LightMode"="UniversalForward" } Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; float3 normalWS:TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial) float4 _BaseR_ST,_BaseG_ST; half _Cutoff; CBUFFER_END
            TEXTURE2D(_BaseR); SAMPLER(sampler_BaseR); TEXTURE2D(_BaseG); SAMPLER(sampler_BaseG);
            Varyings Vert(Attributes IN){ Varyings OUT; VertexPositionInputs pos=GetVertexPositionInputs(IN.positionOS.xyz); OUT.positionCS=pos.positionCS; OUT.uv=IN.uv; OUT.color=IN.color; OUT.normalWS=TransformObjectToWorldNormal(IN.normalOS); return OUT; }
            half4 Frag(Varyings IN):SV_Target { half4 r=SAMPLE_TEXTURE2D(_BaseR,sampler_BaseR,IN.uv*_BaseR_ST.xy+_BaseR_ST.zw); half4 g=SAMPLE_TEXTURE2D(_BaseG,sampler_BaseG,IN.uv*_BaseG_ST.xy+_BaseG_ST.zw); half4 c=lerp(r,g,IN.color.g); clip(c.a-_Cutoff); half light=saturate(dot(normalize(IN.normalWS), normalize(float3(.35,.75,.45))))*.55+.45; return half4(c.rgb*light,1); }
            ENDHLSL
        }
    }
}