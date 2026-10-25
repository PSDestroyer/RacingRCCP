Shader "WindshieldRainAsset/BuiltIn/SimpleRainStandard" {
    Properties{
        [Header(Surface Settings)]
        [Space]
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode("Cull Mode", Int) = 0
        _MainTex("Main Texture", 2D) = "white" {}
        _Color("Color", Color) = (1, 1, 1, 1)
        _Tiling("Rain Tiling", float) = 1
        _Distortion("Rain Distortion", Range(0, 10)) = 0.1
        _Metallic("Metallic", Range(0, 1)) = 0
        _WetSmoothness("Wet Smoothness", Range(0, 1)) = 1
        _DrySmoothness("Dry Smoothness", Range(0, 1)) = 0
        _DropsSmoothness("Drops Smoothness", Range(0, 1)) = 0.5
        _BlendOffset("Blend Offset", Range(0, 1)) = 0.5

        [Header(Static Rain)]
        [Space]
        [Toggle(STATIC_RAIN_ON)] _EnableStaticRain("Enable Static Rain", float) = 1.0
        _StaticRainScale("Static Rain Scale", float) = 1.0
        _StaticRainTimeSpeed("Static Rain Time Speed", float) = 1.0

        [Header(Wetness)]
        [Space]
        _WetnessMask("Wetness Mask", 2D) = "white" {}

        [Header(Rain Settings)]
        [Space]
        _RainAmount("Rain Amount", Range(0, 1)) = 1
        _RainNormalStrength("Rain Normal Strength", float) = 1
        _RainTrailLength("Rain Trail Length", Range(0, 5)) = 0.5
        _RainTrailStrength("Rain Trail Strength", Range(0, 1)) = 0
        _RainTrailSlope("Rain Trail Slope", Range(0, 1)) = 0
        _RainAppearingSpeed("Rain Appearing Speed", float) = 1
        _RainAppearingSlope("Rain Appearing Slope", Range(0, 1)) = 1
        _DropSmoothnessMultiplier("Drop Smoothness Multiplier", float) = 1
        _TrailSmoothnessMultiplier("Trail Smoothness Multiplier", float) = 1

        [Header(Rain Detail)]
        [Space]
        _DetailRainNormalStrength("Detail Rain Normal Strength", float) = 1
        _DetailRainNormalMap("Detail Rain Normal Map", 2D) = "bump" {}
        
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
            Tags {"Queue" = "Geometry" "RenderType" = "Opaque"}
            
            Cull[_CullMode]

            CGPROGRAM
            #pragma target 3.0
            #pragma surface surf Standard vertex:vert fullforwardshadows//StandardTranslucent alpha
            #include "UnityPBSLighting.cginc"
            #pragma shader_feature STATIC_RAIN_ON
            #pragma shader_feature DEBUG

            struct Input {
                float2 uv_MainTex;
                float4 screenPos;
                float3 worldPos;
                float3 worldNormal;
                float3 objectNormal;
                float3 coords;
                INTERNAL_DATA
            };

            #include "SimpleRainDefinitions.hlsl"
            #include "../../Common/Shaders/SimpleRain.hlsl"

            
            float _Metallic;
            float _WetSmoothness, _DrySmoothness, _DropsSmoothness;
            
            float _Tiling;

            float _Distortion;
            float _RainAmount;

            float _ReflectMaskThreshold;
            float _ReflectMaskSlope;
            float4 _ReflectColor;
            float _ReflectPower;
            float _ReflectStren;
            float _MaxReflectStren;
            float _ReflectNormalStren;
            float _BlendOffset;

            sampler2D _WetnessMask;
            sampler2D _MainTex;
            float4 _Color;

            float _DetailRainNormalStrength;
            sampler2D _DetailRainNormalMap;
            float4 _DetailRainNormalMap_ST;
        
            float4x4 _TransformMatrix;

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


#if DEBUG
                o.Albedo = float3(0, 0, 0);
                o.Emission = blend;
                o.Metallic = 0;
                o.Smoothness = 0;
                o.Alpha = 1;

#else

                float rain_amount = _RainAmount * tex2D(_WetnessMask, IN.uv_MainTex);
                // read the three texture projections, for x,y,z axes
                const float3 up = float3(0,1,0);//mul(_TransformMatrix,  float4(0, 1, 0, 0)).xyz;
                const float3 forward = float3(0,0,1);//mul(_TransformMatrix,  float4(0, 0, 1, 0)).xyz;
                const float3 right = float3(1,0,0);//mul(_TransformMatrix,  float4(1, 0, 0, 0)).xyz;
                float4 cx = sampleRainNormal(IN.coords.zy, -up, forward, rain_amount);
                float4 cy = sampleRainNormal(IN.coords.xz, -forward, right, rain_amount);
                float4 cz = sampleRainNormal(IN.coords.xy, -up, right, rain_amount);
                // blend the textures based on weights
                float4 n = cx * blend.x + cy * blend.y + cz * blend.z;

                float3 norm = float3(0, 0, 1);
                if (length(n) > 0.001) {
                    norm = normalize(float3(n.x * _ReflectNormalStren, n.y * _ReflectNormalStren, n.z));
                }

                //float3 tangent = normalize(cross(IN.worldNormal, float3(0, 1, 0)/*IN.worldTangent.xyz*/));// * (IN.tangent.w * unity_WorldTransformParams.w);
                //float3 binormal = normalize(cross(IN.worldNormal, tangent));
                //float3 worldNormal = normalize(norm.x * tangent + norm.y * -binormal + norm.z * IN.worldNormal);

                
                if (length(n.xy) < 0.0001) {
                    float3 detailRainNorm_x = normalize(UnpackNormal(tex2D(_DetailRainNormalMap, IN.coords.zy * _DetailRainNormalMap_ST.xy + _DetailRainNormalMap_ST.zw)));
                    float3 detailRainNorm_y = normalize(UnpackNormal(tex2D(_DetailRainNormalMap, IN.coords.xz * _DetailRainNormalMap_ST.xy + _DetailRainNormalMap_ST.zw)));
                    float3 detailRainNorm_z = normalize(UnpackNormal(tex2D(_DetailRainNormalMap, IN.coords.xy * _DetailRainNormalMap_ST.xy + _DetailRainNormalMap_ST.zw)));
                    float3 detailRainNorm = normalize(detailRainNorm_x * blend.x + detailRainNorm_y * blend.y + detailRainNorm_z * blend.z);
                    
                    detailRainNorm.xy *= _DetailRainNormalStrength;
                    detailRainNorm = normalize(detailRainNorm);
                    norm = lerp(norm, detailRainNorm, (1 - saturate(n.w)) * rain_amount);
                    norm = normalize(norm);
                }
                float2 uvOffset = _Distortion * norm.xy;
                float4 grabColor = tex2D(_MainTex, IN.uv_MainTex  + uvOffset) * _Color;

                float mask = saturate(smoothstep(_ReflectMaskThreshold - _ReflectMaskSlope, _ReflectMaskThreshold + _ReflectMaskSlope, length(n.xy)));

                o.Normal = norm;// normalize(normal.xyz);
                float reflect_stren = 0;
                if (mask > 0) {
                    float3 worldNormal = WorldNormalVector (IN, o.Normal);
                    float3 viewDir = -normalize(_WorldSpaceCameraPos.xyz - IN.worldPos.xyz);
                    float3 reflecDir = normalize(reflect(viewDir, worldNormal));
                    float3 sunDir = float3(0, 1, 0);//normalize(_WorldSpaceLightPos0.xyz);
                    reflect_stren = saturate(pow(saturate(dot(reflecDir, sunDir)), _ReflectPower));
                    reflect_stren *= _ReflectStren;
                    reflect_stren = min(_MaxReflectStren, reflect_stren);
                }
                //float3 colorMixed = grabColor.rgb + mask * _ReflectColor.rgb * reflect_stren;

                o.Albedo = grabColor.rgb; //skyColor.rgb;//float3(0.01, 0.01, 0.01);
                o.Emission = mask * _ReflectColor.rgb * reflect_stren;// colorMixed.rgb;//float3(c.x, c.y, 1);//(bitangent.rgb + float3(1, 1, 1)) * 0.5;//grabColor.rgb;//IN.objectNormal.rgb;//grabColor.rgb;//skyColor.rgb;//grabColor.rgb;// *reflcol.rgb* _ReflectColor.rgb;
                o.Metallic = _Metallic;
                o.Smoothness = lerp(_DrySmoothness, lerp(_WetSmoothness, _DropsSmoothness, saturate(n.w)), rain_amount);
                o.Alpha = 1;
#endif
            }
            ENDCG

        }

            FallBack "Diffuse"
}
