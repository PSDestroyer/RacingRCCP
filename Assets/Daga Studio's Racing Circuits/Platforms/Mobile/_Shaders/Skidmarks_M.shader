Shader "DSRC/Mobile/Skidmarks"
{
    Properties { _MainTex("Base",2D)="white"{} _Base("Base",2D)="white"{} _Color("Color", Color)=(1,1,1,1) }
    SubShader { Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" } Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Offset -4,-4
        Pass { Name "UniversalForward" Tags { "LightMode"="UniversalForward" } Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial) float4 _MainTex_ST; half4 _Color; CBUFFER_END
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            Varyings Vert(Attributes IN){ Varyings OUT; OUT.positionCS=TransformObjectToHClip(IN.positionOS.xyz); OUT.uv=IN.uv; OUT.color=IN.color; return OUT; }
            half4 Frag(Varyings IN):SV_Target { half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,IN.uv*_MainTex_ST.xy+_MainTex_ST.zw)*_Color*IN.color; return c; }
            ENDHLSL
        }
    }
}