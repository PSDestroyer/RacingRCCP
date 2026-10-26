Shader "Formula/Camera Rain Lite"
{
    Properties
    {
        [Normal] _MainTex ("Windshield Rain Normal", 2D) = "bump" {}
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "LensDroplets"
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv; output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half2 bead = input.uv * 2.0h - 1.0h;
                bead.x *= 1.0h + .16h * bead.y;
                half radiusSquared = dot(bead, bead);
                // A smooth individual meniscus, rather than tiling hundreds of
                // drops. The imported rain normal adds only subtle surface detail.
                half edge = 1.0h - smoothstep(.72h, 1.0h, radiusSquared);
                half2 detail = UnpackNormal(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv * .08)).xy;
                half3 normal = normalize(half3(bead + detail * .06h, sqrt(saturate(1.0h - radiusSquared)) + .15h));
                half highlight = saturate(dot(normal.xy, half2(-.65h, .76h)) * 1.5h);
                half shade = saturate(dot(normal.xy, half2(.65h, -.76h)));
                half opacity = edge * .55h * input.color.a;
                half3 tint = lerp(half3(.12h, .17h, .21h), half3(.85h, .94h, 1.0h), highlight);
                opacity *= saturate(.3h + highlight + shade);
                return half4(tint, opacity);
            }
            ENDHLSL
        }
    }
}
