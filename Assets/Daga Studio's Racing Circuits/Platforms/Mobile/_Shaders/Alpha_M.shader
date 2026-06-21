Shader "DSRC/Mobile/Alpha"
{
    Properties { _Base("Base",2D)="white"{} _AlphaValue("Alpha Value", Float)=1 _Color("Color", Color)=(1,1,1,1) }
    SubShader { Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" } Blend SrcAlpha OneMinusSrcAlpha ZWrite Off
        Pass { Name "UniversalForward" Tags { "LightMode"="UniversalForward" } Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial) float4 _Base_ST; half _AlphaValue; half4 _Color; CBUFFER_END
            TEXTURE2D(_Base); SAMPLER(sampler_Base);
            Varyings Vert(Attributes IN){ Varyings OUT; OUT.positionCS=TransformObjectToHClip(IN.positionOS.xyz); OUT.uv=IN.uv; OUT.color=IN.color; return OUT; }
            half4 Frag(Varyings IN):SV_Target { half4 c=SAMPLE_TEXTURE2D(_Base,sampler_Base,IN.uv*_Base_ST.xy+_Base_ST.zw)*_Color; c.a *= _AlphaValue; return c; }
            ENDHLSL
        }
    }
}