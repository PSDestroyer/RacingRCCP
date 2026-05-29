Shader "Rain/RainScrollerMobile"
{
    Properties
    {
        _DropletMask("Droplet Mask", 2D) = "white" {}
        _Distortion("Distortion", Float) = 0.01
        _Tiling("Tiling", Vector) = (1, 1, 0, 0)
        _Droplets_Strength("Droplets_Strength", Range(0, 1)) = 1
        _RivuletMask("Rivulet Mask", 2D) = "white" {}
        _GlobalRotation("Global Rotation", Range(-180, 180)) = 0
        _RivuletRotation("Rivulet Rotation", Range(-180, 180)) = 0
        _RivuletSpeed("Rivulet Speed", Range(0, 2)) = 0.2
        _RivuletsStrength("Rivulets Strength", Range(0, 3)) = 1
        _DropletsGravity("Droplets Gravity", Range(0, 1)) = 0
        _DropletsStrikeSpeed("Droplets Strike Speed", Range(0, 2)) = 0.3
        _grazingTerm("grazingTerm", Range(0, 1)) = 0.1
        [HideInInspector] _texcoord("", 2D) = "white" {}
        [HideInInspector] __dirty("", Int) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_DropletMask);
            SAMPLER(sampler_DropletMask);
            TEXTURE2D(_RivuletMask);
            SAMPLER(sampler_RivuletMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _Tiling;
                float _Droplets_Strength;
                float _RivuletsStrength;
                float _DropletsGravity;
                float _RivuletSpeed;
                float _GlobalRotation;
                float _RivuletRotation;
                float _grazingTerm;
            CBUFFER_END

            float2 RotateUV(float2 uv, float degrees)
            {
                float radiansValue = radians(degrees);
                float sine = sin(radiansValue);
                float cosine = cos(radiansValue);
                float2 centered = uv - 0.5;
                return float2(
                    centered.x * cosine - centered.y * sine,
                    centered.x * sine + centered.y * cosine
                ) + 0.5;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 baseUV = input.uv * _Tiling.xy;
                float2 dropletUV = RotateUV(baseUV + float2(0.0, _Time.y * _DropletsGravity), _GlobalRotation);
                float2 rivuletUV = RotateUV(baseUV + float2(0.0, _Time.y * _RivuletSpeed), _RivuletRotation);

                half4 droplet = SAMPLE_TEXTURE2D(_DropletMask, sampler_DropletMask, dropletUV);
                half4 rivulet = SAMPLE_TEXTURE2D(_RivuletMask, sampler_RivuletMask, rivuletUV);

                half alpha = saturate(max(droplet.a * _Droplets_Strength, rivulet.a * (_RivuletsStrength * 0.5h)));
                half3 color = lerp(half3(0.55h, 0.6h, 0.65h), half3(0.95h, 0.98h, 1.0h), saturate(_grazingTerm + alpha));

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
