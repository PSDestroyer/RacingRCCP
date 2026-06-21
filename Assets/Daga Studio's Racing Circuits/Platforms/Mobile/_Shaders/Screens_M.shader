Shader "DSRC/Mobile/Screens"
{
    Properties { _BaseR("Base (R)",2D)="white"{} _Base("Base",2D)="white"{} _BaseB("Base (B)",2D)="white"{} _FPS("FPS",Float)=8 _MaxFrames("MaxFrames",Float)=144 _W("W",Float)=9 _H("H",Float)=16 }
    SubShader { Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass { Name "UniversalForward" Tags { "LightMode"="UniversalForward" } Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial) float4 _BaseB_ST; half _FPS,_MaxFrames,_W,_H; CBUFFER_END
            TEXTURE2D(_BaseR); SAMPLER(sampler_BaseR); TEXTURE2D(_Base); SAMPLER(sampler_Base); TEXTURE2D(_BaseB); SAMPLER(sampler_BaseB);
            Varyings Vert(Attributes IN){ Varyings OUT; OUT.positionCS=TransformObjectToHClip(IN.positionOS.xyz); OUT.uv=IN.uv; OUT.color=IN.color; return OUT; }
            half2 FlipbookUV(half2 uv){ half w=max(_W,1); half h=max(_H,1); half maxFrames=max(_MaxFrames,1); half frame=floor(fmod(_Time.y*_FPS,maxFrames)); half x=fmod(frame,w); half y=floor(frame/w); return (uv + half2(x, h-1-y)) / half2(w,h); }
            half4 Frag(Varyings IN):SV_Target { half2 fuv=FlipbookUV(IN.uv); half4 r=SAMPLE_TEXTURE2D(_BaseR,sampler_BaseR,fuv); half4 g=SAMPLE_TEXTURE2D(_Base,sampler_Base,fuv); half4 b=SAMPLE_TEXTURE2D(_BaseB,sampler_BaseB,IN.uv*_BaseB_ST.xy+_BaseB_ST.zw); return lerp(lerp(r,g,IN.color.g), b, IN.color.b); }
            ENDHLSL
        }
    }
}