Shader "WindshieldRainAsset/BuiltIn/WindshieldRainShader" {
    Properties{
        [Header(Assigned By Script)]
        [Space]
        _HeightMap("Rain Height Map", 2D) = "black" {}
        _WipersTexture("Wipers Texture", 2D) = "black" {}
        [Header(Surface Settings)]
        [Space]
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode("Cull Mode", Int) = 2
        _ColorTex("Color Texture", 2D) = "black" {}
        _ColorTexColorMultiplier("Color Texture Color Multiplier", Color) = (1, 1, 1, 1)
        _ColorTexScaleAndOffset("Color Texture Scale & Offset", vector) = (1, 1, 0, 0)
        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0
        _SmoothnessMap("Smoothness Map", 2D) = "white" {}
        _SmoothnessMapScaleAndOffset("Smoothness Map Scale & Offset", vector) = (1, 1, 0, 0)
        _AlphaThreshold("Alpha Threshold", Range(0, 10)) = 5

        [Header(Rain Drops)]
        [Space]
        _RainStrength("Rain Strength", Range(0, 1)) = 1
        _NormalStrength("Rain Normal Strength", Range(0, 2)) = 1.0
        _NormalTexelSize("Rain Normal Texel Size", Range(0, 1)) = 0.001
        _HeightThreshold("Rain Height Threshold", Range(0, 1)) = 0.9 
        _Distortion("Rain Distortion", Range(0, 1)) = 0.1
        _RainSmoothness("Rain Smoothness", Range(0, 1)) = 0.9

        [Header(Rain Reflection)]
        [Space]
        _ReflectMaskThreshold("Reflection Mask Threshold", Range(0.9, 1.0)) = 0.95
        _ReflectColor("Reflection Color", Color) = (1,1,1,0.5)
        _ReflectPower("Reflection Power", float) = 10
        _ReflectStren("Reflection Strength", float) = 1

        [Header(Rain Background Blur)]
        [Space]
        _RainAmount("Rain Background Strength", Range(0, 1)) = 1
        _RainStreakImpact("Rain Streak Impact", float) = 10
        _RainStreakThreshold("Rain Streak Threshold", Range(0, 1)) = 0.5
        _RainStreakBlurStrength("Rain Streak Blur Strength", Range(0, 1)) = 1
        _BlurColor("Blur Color", Color) = (1, 1, 1, 0)
        _BlurStrength("Blur Strength", Range(0, 1)) = 0.1
        _BlurNormalStrean("Background Normal Strength", float) = 1
        _BlurNormalMap("Background Normal Map", 2D) = "bump" {}
        _BlurNormalScaleAndOffset("Background Normal Scale & Offset", vector) = (1, 1, 0, 0)

        [Header(Rain Smudges)]
        [Space]
        _SmudgesThreshold("Smudges Threshold", Range(0, 1)) = 0.5
        //_SmudgesTexelSize("Smudges Texel Size", Range(0, 1)) = 0.002
        _SmudgeBlurStrength("Smudges Blur Strength", Range(0, 1)) = 1
        _SmudgesColor("Smudges Color", Color) = (1, 1, 1, 0.05)
        _SmudgeSmoothness("Smudge Smoothness", Range(0, 1)) = 0
    }


        SubShader{
            Tags {"Queue" = "Transparent+1000" "RenderType" = "Transparent"}

            Blend SrcAlpha OneMinusSrcAlpha
            // Use a GrabPass to capture the screen behind the object and pass it to the surface shader as a secondary UV
            GrabPass{ "_WindshieldGrabTexture" }

            Cull[_CullMode]

            CGPROGRAM
            #pragma target 3.0
            #pragma surface surf Standard fullforwardshadows alpha//StandardTranslucent alpha
            #include "UnityPBSLighting.cginc"
            #include "../../Common/Shaders/SampleBlur.hlsl"

            struct Input {
                float2 uv_HeightMap;
                float4 screenPos;
#if _CullMode < 2
                float facing : VFACE;
#endif
                float3 worldPos;
                float3 worldNormal;
                INTERNAL_DATA
            };

            fixed4 _ReflectColor;
            float _ReflectPower;
            float _ReflectStren;
            float _ReflectMaskThreshold;
            float _AlphaThreshold;
            float _RainStrength;

            sampler2D _ColorTex;
            float4 _ColorTexScaleAndOffset, _ColorTexColorMultiplier;

            sampler2D _SmoothnessMap;
            float4 _SmoothnessMapScaleAndOffset;

            sampler2D _HeightMap;
            float _HeightThreshold;
            float _NormalTexelSize;
            float _NormalStrength;
        
            sampler2D _WipersTexture;
            float _Distortion;
            float _Metallic, _Smoothness;
            float _RainSmoothness, _SmudgeSmoothness;

            sampler2D _BlurNormalMap;
            float4 _BlurNormalScaleAndOffset;
            float _BlurNormalStrean;
            float4 _BlurColor;
            float _BlurStrength, _RainAmount;
            float _SmudgesThreshold, _SmudgeBlurStrength;
            float4 _SmudgesColor;
            float _RainStreakImpact, _RainStreakBlurStrength;
            float _RainStreakThreshold;
            //float _SmudgesTexelSize;

            inline fixed4 LightingStandardTranslucent(SurfaceOutputStandard s, fixed3 viewDir, UnityGI gi)
            {
                fixed4 pbr = LightingStandard(s, viewDir, gi);
                return pbr;
            }

            inline void LightingStandardTranslucent_GI(
                SurfaceOutputStandard s,
                UnityGIInput data,
                inout UnityGI gi)
            {
                gi = UnityGlobalIllumination(data, 1.0, s.Normal);
            }

            sampler2D _CameraDepthTexture;
            float4 _CameraDepthTexture_TexelSize;

            float2 AlignWithGrabTexel(float2 uv) {
#if UNITY_UV_STARTS_AT_TOP
                if (_CameraDepthTexture_TexelSize.y < 0) {
                    uv.y = 1 - uv.y;
                }
#endif

                return
                    (floor(uv * _CameraDepthTexture_TexelSize.zw) + 0.5) *
                    abs(_CameraDepthTexture_TexelSize.xy);
            }

            float getHeight(float2 uv) {
                return clamp((tex2D(_HeightMap, uv).r - _HeightThreshold) / (1 - _HeightThreshold), 0, 1);
            }

            float3 SampleNormalFromHeight(float2 uv)
            {
                float height = getHeight(uv);
                float heightX = getHeight(uv + float2(_NormalTexelSize, 0));
                float heightY = getHeight(uv + float2(0, _NormalTexelSize));
                float3 normal = float3(heightX - height, heightY - height, 0) * _NormalStrength;
                normal.z = sqrt(1 - (normal.x * normal.x) - (normal.y * normal.y));
                return normalize(normal);
            }

            float4 SampleBlur(float2 uv, float strength)
            {
                PRESAMPLE_BLUR_TEXTURES(uv)
                float4 out_color;
                GET_BLUR_COLOR_FROM_PRESAMPLE(out_color, strength)
                return out_color;
            }

            void surf(Input IN, inout SurfaceOutputStandard o) {
                // o.Albedo = float3(1, 1, 1);// float3(0.01, 0.01, 0.01); //skyColor.rgb;//float3(0.01, 0.01, 0.01);
                // o.Emission = float3(0, 0, 0);//skyColor.rgb;//grabColor.rgb;// *reflcol.rgb* _ReflectColor.rgb;
                // //o.Normal = normalize(normal.xyz);
                // o.Metallic = _Metallic;
                // o.Smoothness = _Smoothness;
                // o.Alpha = 0;

                // Sample the normal map
                float3 normal = SampleNormalFromHeight(IN.uv_HeightMap);//tex2D(_NormalMap, IN.uv_NormalMap);
                float trail = tex2D(_HeightMap, IN.uv_HeightMap).x * _RainStrength;
                //normal.xyz = normal.xyz * 2 - float3(1, 1, 1);
                //normal.xyz = float3(normal.x, normal.z, normal.y);// normalize(reflect(float3(normal.y, normal.x, normal.z), float3(0, -1, 0)));

                //float3 tnormal = float3(normal.x, normal.z, normal.y);// UnpackNormal(normal);

                //float3 tangent = normalize(cross(IN.worldNormal, float3(1, 0, 0)/*IN.worldTangent.xyz*/));// * (IN.tangent.w * unity_WorldTransformParams.w);
                //float3 binormal = normalize(cross(IN.worldNormal, tangent));
                //float3 worldNormal = normalize(normal.x * tangent + normal.y * -binormal + normal.z * IN.worldNormal);

                // half3 worldViewDir = -normalize(UnityWorldSpaceViewDir(IN.worldPos));
                // half3 worldRefl = reflect(-worldViewDir, worldNormal);
                // half4 skyData = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, worldRefl);
                // half3 skyColor = DecodeHDR(skyData, unity_SpecCube0_HDR);

                //float3 reflColor = texCUBE(_ReflCube, worldRefl) * _ReflectColor;

                // Calculate distortion vector based on the normal map and distortion value
                normal.xy *= _RainStrength;
                normal = normalize(normal);
                float2 uvOffset = _Distortion * normal.xy;
                float2 uv = AlignWithGrabTexel((IN.screenPos.xy + uvOffset) / IN.screenPos.w);

                // float backgroundDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));
                // float surfaceDepth = UNITY_Z_0_FAR_FROM_CLIPSPACE(IN.screenPos.z);
                // float depthDifference = backgroundDepth - surfaceDepth;
                // uvOffset *= saturate(depthDifference);
                // uv = AlignWithGrabTexel((IN.screenPos.xy + uvOffset) / IN.screenPos.w);

                // Sample the grab texture using the distorted UV coordinates
                float4 grabColor = tex2D(_WindshieldGrabTexture, uv);

                float4 wipersColor = tex2D(_WipersTexture, IN.uv_HeightMap) * _RainStrength;
                
                float3 blurNormal = normalize(UnpackNormal(tex2D(_BlurNormalMap, IN.uv_HeightMap * _BlurNormalScaleAndOffset.xy + _BlurNormalScaleAndOffset.zw)));
                float2 blurNormalOffset = blurNormal.xy * _BlurNormalStrean;
                float4 smudgeColor;
                float4 blurColor;
                float4 rainStreakBlur;

                blurColor = SampleBlur(saturate(uv + blurNormalOffset), _BlurStrength);

                {
                        PRESAMPLE_BLUR_TEXTURES(uv)
                        GET_BLUR_COLOR_FROM_PRESAMPLE(smudgeColor, _SmudgeBlurStrength)
                        GET_BLUR_COLOR_FROM_PRESAMPLE(rainStreakBlur, _RainStreakBlurStrength)
                }

                float smudgeBlured = wipersColor.w;

                // float smudgeBlured = 0;
                // float smudgeWeightSum = 0;
                // for (int x = -3; x <= 3; x++) {
                //     for (int y = -3; y <= 3; y++) {
                //         float2 offset = float2(x, y) * _SmudgesTexelSize;
                //         float smudgeWeight = exp(-(x * x + y * y) / (_SmudgeBlurRadius * _SmudgeBlurRadius));
                //         if (x == 0 && y == 0) {
                //             smudgeBlured += wipersColor.w * smudgeWeight;
                //         } else {
                //             smudgeBlured += tex2D(_WipersTexture, saturate(IN.uv_HeightMap + offset * 0.2f)).w * smudgeWeight;
                //         }
                //         smudgeWeightSum += smudgeWeight;
                //     }
                // }
                // smudgeBlured = smudgeBlured / smudgeWeightSum;

                float smudges = saturate((smudgeBlured - _SmudgesThreshold) / (1 - _SmudgesThreshold));
                float rainStreak = saturate(smoothstep(_RainStreakThreshold, 1.0, trail) * _RainStreakImpact);
                float blurStren = saturate(saturate((rainStreak + wipersColor.z * 2)) + (1 - _RainAmount));
                //float blurStren = saturate((smoothstep(_RainStreakThreshold, 1.0, trail) * _RainStreakImpact));
                float smudgeStren = saturate(smudges * 2);
                //grabColor = lerp(blurColor, grabColor, blurStren);
                //smudgeColor *= (1 - _SmudgesColor.a);
                grabColor = lerp(blurColor, grabColor, blurStren);

                grabColor = lerp(grabColor, rainStreakBlur, rainStreak);
                grabColor = lerp(grabColor, smudgeColor, smudgeStren);

                float alpha = saturate(((1 - blurStren) * saturate((saturate(length(blurNormalOffset) * 10) + _BlurStrength) * _AlphaThreshold)) + ((rainStreak + smudgeStren) * _AlphaThreshold));

                
                
                //grabColor.rgb = lerp(grabColor.rgb, grabColor.rgb * _SmudgesColor.rgb, smudges * _SmudgesColor.a);

                //fixed4 reflcol = texCUBE(_Cube, IN.worldRefl);

                //float reflect_stren = (reflColor.r + reflColor.g + reflColor.b) / 3.0;

                float mask = saturate(1 - step(_ReflectMaskThreshold, dot(normal.xzy, float3(0, 1, 0))));

                blurNormal.xy *= _BlurNormalStrean;
                blurNormal = normalize(blurNormal);
                normal = normalize(lerp(blurNormal, normal, blurStren));

                o.Normal = normalize(normal.xyz);
#if _CullMode < 2
                if (IN.facing < 0)
                {
                    o.Normal = -normalize(o.Normal);
                }
#endif
                float3 worldNormal = WorldNormalVector (IN, o.Normal);
                float reflect_stren = saturate(pow(abs(dot(worldNormal, float3(0, 1, 0))), _ReflectPower));
                reflect_stren *= _ReflectStren * _RainStrength;
                //reflect_stren *= reflect_stren * 0.5f;

                //float3 worldY = float3(worldNormal.y, worldNormal.y, worldNormal.y);
                float3 colorMixed = grabColor.rgb + mask * _ReflectColor.rgb * reflect_stren;//float3(reflect_stren, reflect_stren, reflect_stren);
                //float3 colorMixed = grabColor.rgb + float3(reflect_stren, reflect_stren, reflect_stren);// *step(0.9f, normal.w) * 0.5f;

                float4 colorTex = tex2D(_ColorTex, IN.uv_HeightMap * _ColorTexScaleAndOffset.xy + _ColorTexScaleAndOffset.zw) * _ColorTexColorMultiplier;

                //float3 colorMixed = grabColor.rgb + pow(reflColor.rgb, 2) * step(0.9f, normal.w) * 0.5f;// grabColor.rgb + skyColor.rgb * normal.w * 0.5f; // lerp(grabColor.rgb, skyColor.rgb, normal.w);
                float3 albedo = lerp(float3(0, 0, 0), _BlurColor.rgb, _BlurColor.a * (1 - blurStren));
                albedo = lerp(albedo, _SmudgesColor.rgb, _SmudgesColor.a * smudgeStren);
                albedo = lerp(albedo, colorTex, colorTex.a);
                colorMixed *= saturate(1.0f - colorTex.a);

                o.Albedo = albedo;// float3(0.01, 0.01, 0.01); //skyColor.rgb;//float3(0.01, 0.01, 0.01);
                o.Emission = colorMixed * alpha;//skyColor.rgb;//grabColor.rgb;// *reflcol.rgb* _ReflectColor.rgb;
                //o.Normal = normalize(normal.xyz);
                o.Metallic = _Metallic;

                float smoothness = saturate(tex2D(_SmoothnessMap, IN.uv_HeightMap * _SmoothnessMapScaleAndOffset.xy + _SmoothnessMapScaleAndOffset.zw).r * _Smoothness);
                smoothness = lerp(smoothness, _RainSmoothness, rainStreak);
                smoothness = lerp(smoothness, _SmudgeSmoothness, smudgeStren);
                o.Smoothness = smoothness;
                o.Alpha = (1 - ((1 - alpha) * saturate(1.0f - colorTex.a)));
            }
            ENDCG

        }

            FallBack "Diffuse"
}
