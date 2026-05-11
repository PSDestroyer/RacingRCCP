Shader "Shader Forge/Character" {
    Properties {
        _Color01 ("Color01", Color) = (1,1,1,1)
        _Color02 ("Color02", Color) = (1.014706,1.014706,1.014706,1)
        _Color03 ("Color03", Color) = (1,1,1,1)
        _Albedo ("Albedo", 2D) = "white" {}
        [Normal] _Normal ("Normal", 2D) = "bump" {}
        _ColorMask ("ColorMask", 2D) = "white" {}
        _WhiteMask ("WhiteMask", 2D) = "white" {}
        _ORM ("ORM", 2D) = "white" {}
        _Roughness ("Roughness", Color) = (0.5,0.5,0.5,1)
        _Emissive ("Emissive", 2D) = "black" {}
    }

    SubShader {
        Tags {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
        }

        Pass {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _ENVIRONMENT_REFLECTIONS_OFF
            #pragma multi_compile_fragment _ _SPECULARHIGHLIGHTS_OFF
            #pragma multi_compile_fragment _ _RECEIVE_SHADOWS_OFF
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color01;
                float4 _Color02;
                float4 _Color03;
                float4 _Albedo_ST;
                float4 _Normal_ST;
                float4 _ColorMask_ST;
                float4 _WhiteMask_ST;
                float4 _ORM_ST;
                float4 _Roughness;
                float4 _Emissive_ST;
            CBUFFER_END

            TEXTURE2D(_Albedo); SAMPLER(sampler_Albedo);
            TEXTURE2D(_Normal); SAMPLER(sampler_Normal);
            TEXTURE2D(_ColorMask); SAMPLER(sampler_ColorMask);
            TEXTURE2D(_WhiteMask); SAMPLER(sampler_WhiteMask);
            TEXTURE2D(_ORM); SAMPLER(sampler_ORM);
            TEXTURE2D(_Emissive); SAMPLER(sampler_Emissive);

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                half3 vertexLighting : TEXCOORD5;
                half fogFactor : TEXCOORD6;
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 7);
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            struct CharacterSurfaceData {
                half3 albedo;
                half3 normalTS;
                half metallic;
                half occlusion;
                half smoothness;
                half3 emission;
            };

            half ComputeSmoothness(half ormRoughness, half roughnessControl) {
                half gloss = 1.0h - saturate(
                    roughnessControl > 0.5h
                        ? (1.0h - (1.0h - 2.0h * (roughnessControl - 0.5h)) * (1.0h - ormRoughness))
                        : (2.0h * roughnessControl * ormRoughness)
                );

                return saturate(gloss);
            }

            CharacterSurfaceData InitializeCharacterSurface(float2 uv) {
                CharacterSurfaceData surface;

                half4 albedoTex = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, TRANSFORM_TEX(uv, _Albedo));
                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_Normal, sampler_Normal, TRANSFORM_TEX(uv, _Normal)));
                half4 colorMask = SAMPLE_TEXTURE2D(_ColorMask, sampler_ColorMask, TRANSFORM_TEX(uv, _ColorMask));
                half3 whiteMask = SAMPLE_TEXTURE2D(_WhiteMask, sampler_WhiteMask, TRANSFORM_TEX(uv, _WhiteMask)).rgb;
                half4 orm = SAMPLE_TEXTURE2D(_ORM, sampler_ORM, TRANSFORM_TEX(uv, _ORM));
                half3 emissive = SAMPLE_TEXTURE2D(_Emissive, sampler_Emissive, TRANSFORM_TEX(uv, _Emissive)).rgb;

                half3 tinted = saturate(min(albedoTex.rgb, (_Color01.rgb * colorMask.r) + (_Color02.rgb * colorMask.g) + (_Color03.rgb * colorMask.b)));

                surface.albedo = lerp(albedoTex.rgb, tinted, whiteMask);
                surface.normalTS = normalTS;
                surface.metallic = saturate(orm.b);
                surface.occlusion = saturate(orm.r);
                surface.smoothness = ComputeSmoothness(orm.g, _Roughness.r);
                surface.emission = emissive;

                return surface;
            }

            Varyings vert(Attributes input) {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = input.uv;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = half4(normalInputs.tangentWS.xyz, input.tangentOS.w);
                output.vertexLighting = VertexLighting(positionInputs.positionWS, normalInputs.normalWS);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                output.shadowCoord = GetShadowCoord(positionInputs);
                OUTPUT_LIGHTMAP_UV(input.uv, unity_LightmapST, output.staticLightmapUV);
                OUTPUT_SH(output.normalWS, output.vertexSH);

                return output;
            }

            half4 frag(Varyings input) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                CharacterSurfaceData character = InitializeCharacterSurface(input.uv);
                half3 bitangentWS = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = TransformTangentToWorld(character.normalTS, tangentToWorld);
                inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = input.shadowCoord;
                inputData.fogCoord = input.fogFactor;
                inputData.vertexLighting = input.vertexLighting;
                inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = character.albedo;
                surfaceData.metallic = character.metallic;
                surfaceData.specular = half3(0, 0, 0);
                surfaceData.smoothness = character.smoothness;
                surfaceData.normalTS = character.normalTS;
                surfaceData.occlusion = character.occlusion;
                surfaceData.emission = character.emission;
                surfaceData.alpha = 1.0h;
                surfaceData.clearCoatMask = 0.0h;
                surfaceData.clearCoatSmoothness = 0.0h;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1.0h;
                return color;
            }
            ENDHLSL
        }

        Pass {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
