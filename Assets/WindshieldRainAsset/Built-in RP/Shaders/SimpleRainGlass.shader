Shader "WindshieldRainAsset/BuiltIn/SimpleRainGlass" {
    Properties{
        [Header(Surface Settings)]
        [Space]
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode("Cull Mode", Int) = 0
        _Tiling("Rain Tiling", float) = 1
        _Distortion("Rain Distortion", Range(0, 10)) = 0.1
        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0
        _DropsSmoothness("Drops Smoothness", Range(0, 1)) = 0.5
        _BlendOffset("Blend Offset", Range(0, 1)) = 0.5
        _AlphaThreshold("Alpha Threshold", Range(0, 10)) = 5

        [Header(Static Rain)]
        [Space]
        [Toggle(STATIC_RAIN_ON)] _EnableStaticRain("Enable Static Rain", float) = 1.0
        _StaticRainScale("Static Rain Scale", float) = 1.0
        _StaticRainTimeSpeed("Static Rain Time Speed", float) = 1.0

        [Header(Rain Settings)]
        [Space]
        _RainAmount("Rain Amount", Range(0, 1)) = 1
        _RainNormalStrength("Rain Normal Strength", float) = 1
        _RainTrailLength("Rain Trail Length", Range(0, 5)) = 0.5
        _RainTrailStrength("Rain Trail Strength", Range(0, 1)) = 0
        _RainTrailSlope("Rain Trail Slope", Range(0, 1)) = 0
        _RainAppearingSpeed("Rain Appearing Speed", float) = 1
        _RainAppearingSlope("Rain Appearing Slope", Range(0, 1)) = 1

        [Header(Rain Background Blur)]
        [Space]
        _TrailBlurReduction("Trail Blur Reduction", float) = 2
        _BackgroundStrength("Background Strength", Range(0, 1)) = 1
        _BlurStrength("Blur Strength", Range(0, 1)) = 0.5
        _BackgroundColor("Backgraound Color", Color) = (1, 1, 1, 0)
        _BackgroundNormalStrean("Backgraound Normal Strength", float) = 1
        _BackgroundNormalMap("Backgraound Normal Map", 2D) = "bump" {}
        
        [Header(Rain Reflection)]
        [Space]
        _ReflectMaskThreshold("Reflection Mask Threshold", Range(0.0, 1.0)) = 0.95
        _ReflectMaskSlope("Reflection Mask Slope", Range(0.0, 1.0)) = 0.1
        _ReflectColor("Reflection Color", Color) = (1,1,1,0.5)
        _ReflectPower("Reflection Power", float) = 10
        _MaxReflectStren("Max Reflection Strength", Range(0, 1)) = 1
        _ReflectStren("Reflection Strength", float) = 1
        _ReflectNormalStren("Reflection Normal Strength", float) = 1

        [Header(Debug)]
        [Space]
        [Toggle(DEBUG)]_Debug("Debug", int) = 0

        [HideInInspector]_ModTime("Mod Time", float) = 1
    }


        SubShader{
            Tags {"Queue" = "Transparent" "RenderType" = "Transparent"}

            // Use a GrabPass to capture the screen behind the object and pass it to the surface shader as a secondary UV
            GrabPass{ "_WindshieldGrabTexture" }

            Cull[_CullMode]

            CGPROGRAM
            #pragma target 3.0
            #pragma surface surf Standard vertex:vert fullforwardshadows alpha//StandardTranslucent alpha
            #include "UnityPBSLighting.cginc"
            #pragma shader_feature STATIC_RAIN_ON
            #pragma shader_feature DEBUG

            struct Input {
                float2 uv_BackgroundNormalMap;
                float4 screenPos;
                float3 worldPos;
                float3 worldNormal;
                float3 objectNormal;
                float3 worldRefl;
                float3 viewDir;
                float3 coords;
#if _CullMode < 2
                float facing : VFACE;
#endif
                INTERNAL_DATA
            };

            UNITY_DECLARE_TEXCUBE(_RealtimeCubemap);

            #include "SimpleRainDefinitions.hlsl"
            #include "../../Common/Shaders/SimpleRain.hlsl"
            #include "../../Common/Shaders/SampleBlur.hlsl"
            
            float _Metallic;
            float _Smoothness, _DropsSmoothness;
            float _Tiling;
            float _RainAmount;
            float _AlphaThreshold;

            float _Distortion;
            sampler2D _CameraDepthTexture;
            float4 _CameraDepthTexture_TexelSize;

            float _BackgroundStrength, _BlurStrength;
            float4 _BackgroundColor;

            float _ReflectMaskThreshold;
            float _ReflectMaskSlope;
            float4 _ReflectColor;
            float _ReflectPower;
            float _ReflectStren;
            float _MaxReflectStren;
            float _ReflectNormalStren;
            float _BlendOffset;
            float _TrailBlurReduction;
            
            sampler2D _BackgroundNormalMap;
            float4 _BlurNormalScaleAndOffset;
            float _BackgroundNormalStrean;

            float4x4 _TransformMatrix;
            //half4 _RealtimeCubemap_HDR;

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

            void vert (inout appdata_tan v, out Input o) {
                UNITY_INITIALIZE_OUTPUT(Input,o);
                o.objectNormal = normalize(mul(_TransformMatrix,  float4(v.normal, 0)).xyz);
                o.coords = mul(_TransformMatrix,  v.vertex).xyz * _Tiling;
            }

            void surf(Input IN, inout SurfaceOutputStandard o) {

                half3 blend = abs(IN.objectNormal);
                // make sure the weights sum up to 1 (divide by sum of x+y+z)
                blend = saturate(blend - _BlendOffset);
                blend /= dot(blend,1.0);
                // read the three texture projections, for x,y,z axes

#if DEBUG
                o.Albedo = float3(0, 0, 0);
                o.Emission = blend;
                o.Metallic = 0;
                o.Smoothness = 0;
                o.Alpha = 1;

#else

                const float3 up = float3(0,1,0);//mul(_TransformMatrix,  float4(0, 1, 0, 0)).xyz;
                const float3 forward = float3(0,0,1);//mul(_TransformMatrix,  float4(0, 0, 1, 0)).xyz;
                const float3 right = float3(1,0,0);//mul(_TransformMatrix,  float4(1, 0, 0, 0)).xyz;
                _DropSmoothnessMultiplier = 1.0;
                _TrailSmoothnessMultiplier = 1.0;
                float4 cx = sampleRainNormal(IN.coords.zy, -up, forward, _RainAmount);
                float4 cy = sampleRainNormal(IN.coords.xz, -forward, right, _RainAmount);
                float4 cz = sampleRainNormal(IN.coords.xy, -up, right, _RainAmount);
                // blend the textures based on weights
                float4 n = cx * blend.x + cy * blend.y + cz * blend.z;

                float3 norm = normalize(float3(n.x * _ReflectNormalStren, n.y * _ReflectNormalStren, n.z));


                //float3 tangent = normalize(cross(IN.worldNormal, float3(0, 1, 0)/*IN.worldTangent.xyz*/));// * (IN.tangent.w * unity_WorldTransformParams.w);
                //float3 binormal = normalize(cross(IN.worldNormal, tangent));
                //float3 worldNormal = normalize(norm.x * tangent + norm.y * -binormal + norm.z * IN.worldNormal);

                float2 uvOffset = _Distortion * norm.xy * 0.1;
                
                float2 uv = AlignWithGrabTexel((IN.screenPos.xy + uvOffset) / IN.screenPos.w);
                //float backgroundDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));
                //float surfaceDepth = UNITY_Z_0_FAR_FROM_CLIPSPACE(IN.screenPos.z);
                //float depthDifference = backgroundDepth - surfaceDepth;
                //uvOffset *= saturate(depthDifference);
                //uv = AlignWithGrabTexel((IN.screenPos.xy + uvOffset) / IN.screenPos.w);
                float4 grabColor = tex2D(_WindshieldGrabTexture, uv);

                float albedo = float3(0.01, 0.01, 0.01);
                float alpha = length(uvOffset);
                if (length(n.xy) < 0.0001) {
                    float3 blurNormal = normalize(UnpackNormal(tex2D(_BackgroundNormalMap, IN.uv_BackgroundNormalMap)));
                    float2 blurNormalOffset = blurNormal.xy * _BackgroundNormalStrean;
                    // float4 blurColor = float4(0, 0, 0, 0);
                    // float blurWeight = 0;
                    // float texelSize = 0.002;
                    // for (int x = -3; x <= 3; x++) {
                    //     for (int y = -3; y <= 3; y++) {
                    //         float2 offset = float2(x, y) * texelSize;
                    //         float weight = exp(-(x * x + y * y) / (_BlurRadius * _BlurRadius));
                    //         blurColor += tex2D(_GrabTexture, saturate(uv + offset + blurNormalOffset)) * weight;
                    //         blurWeight += weight;
                    //     }
                    // }
                    // blurColor = blurColor / blurWeight;
                    // blurColor.rgb *= (1 - _BlurColor.a); //lerp(blurColor.rgb, blurColor.rgb * _BlurColor.rgb, _BlurColor.a);
                    float4 blurColor;
                    {
                        PRESAMPLE_BLUR_TEXTURES(saturate(uv + blurNormalOffset))
                        GET_BLUR_COLOR_FROM_PRESAMPLE(blurColor, _BlurStrength)
                    }

                    float blurStren = 1 - saturate(saturate(n.w * _TrailBlurReduction + (1 - _BackgroundStrength)));
                    grabColor = lerp(grabColor, blurColor, blurStren * _RainAmount);
                    albedo = lerp(albedo, _BackgroundColor.rgb, blurStren * _BackgroundColor.a * _RainAmount);
                    alpha += blurStren * _RainAmount * (length(blurNormalOffset) + _BlurStrength);
                }
                alpha = saturate(alpha * _AlphaThreshold);

                float mask = saturate(smoothstep(_ReflectMaskThreshold - _ReflectMaskSlope, _ReflectMaskThreshold + _ReflectMaskSlope, length(n.xy)));

                o.Normal = norm;// normalize(normal.xyz);
#if _CullMode < 2
                if (IN.facing < 0)
                {
                    o.Normal = -normalize(o.Normal);
                }
#endif
                float3 worldNormal = WorldNormalVector (IN, o.Normal);
                //float3 viewDir = -normalize(_WorldSpaceCameraPos.xyz - IN.worldPos.xyz);
                float3 reflecDir = normalize(reflect(IN.viewDir, worldNormal));
                float reflect_stren = saturate(pow(dot(reflecDir, float3(0, 1, 0)), _ReflectPower));
                reflect_stren *= _ReflectStren;
                reflect_stren = min(_MaxReflectStren, reflect_stren);
                float3 colorMixed = grabColor.rgb + mask * _ReflectColor.rgb * reflect_stren;

                o.Albedo = albedo; //skyColor.rgb;//float3(0.01, 0.01, 0.01);

                //Unity_GlossyEnvironmentData glossIn = UnityGlossyEnvironmentSetup(_Smoothness, IN.viewDir, o.Normal, half3(0, 0, 0));
                //half3 env0 = Unity_GlossyEnvironment(UNITY_PASS_TEXCUBE(_RealtimeCubemap), _RealtimeCubemap_HDR, glossIn);

                //o.Emission = env0;


                o.Emission = alpha * colorMixed.rgb;//(float3(uvOffset.x, uvOffset.y, 1) + float3(1, 1, 1)) * 0.5f;// colorMixed.rgb;//float3(c.x, c.y, 1);//(bitangent.rgb + float3(1, 1, 1)) * 0.5;//grabColor.rgb;//IN.objectNormal.rgb;//grabColor.rgb;//skyColor.rgb;//grabColor.rgb;// *reflcol.rgb* _ReflectColor.rgb;
                o.Metallic = _Metallic;
                o.Smoothness = lerp(_Smoothness, _DropsSmoothness, saturate(n.w));
                o.Alpha = alpha;
#endif
            }
            ENDCG

        }

            FallBack "Diffuse"
}
