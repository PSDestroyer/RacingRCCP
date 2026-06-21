Shader "DSRC/PC/Screens"
{
    Properties { _Screens01("Screens01", 2D) = "white" {} _Screens02("Screens02", 2D) = "white" {} _Screens03("Screens03", 2D) = "white" {} _FPS("FPS", Float) = 8 _MaxFrames("MaxFrames", Float) = 144 _Width("Width", Float) = 9 _Height("Height", Float) = 16 _Intensity("Intensity", Float) = 1 _Tint("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            CBUFFER_START(UnityPerMaterial) float4 _Screens01_ST; float4 _Screens02_ST; float4 _Screens03_ST; float _FPS; float _MaxFrames; float _Width; float _Height; float _Intensity; half4 _Tint; CBUFFER_END
            TEXTURE2D(_Screens01); SAMPLER(sampler_Screens01); TEXTURE2D(_Screens02); SAMPLER(sampler_Screens02); TEXTURE2D(_Screens03); SAMPLER(sampler_Screens03);
            Varyings Vert(Attributes IN) { Varyings OUT; OUT.positionCS=TransformObjectToHClip(IN.positionOS.xyz); OUT.uv=IN.uv; OUT.color=IN.color; return OUT; }
            half4 Frag(Varyings IN) : SV_Target
            {
                float frame = fmod(floor(_Time.y * max(_FPS, 1.0)), max(_MaxFrames, 1.0));
                float2 grid = max(float2(_Width, _Height), float2(1.0, 1.0));
                float2 cell = float2(fmod(frame, grid.x), floor(frame / grid.x));
                float2 uv = (frac(IN.uv) + cell) / grid;
                half4 c1 = SAMPLE_TEXTURE2D(_Screens01, sampler_Screens01, uv);
                half4 c2 = SAMPLE_TEXTURE2D(_Screens02, sampler_Screens02, uv);
                half4 c3 = SAMPLE_TEXTURE2D(_Screens03, sampler_Screens03, uv);
                half selector = saturate(IN.color.r + IN.color.g + IN.color.b);
                half4 col = lerp(c1, lerp(c2, c3, step(0.66h, selector)), step(0.33h, selector));
                col.rgb *= _Tint.rgb * _Intensity;
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
