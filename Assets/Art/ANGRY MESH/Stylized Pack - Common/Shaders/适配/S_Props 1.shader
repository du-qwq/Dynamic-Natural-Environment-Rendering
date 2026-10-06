Shader "ANGRYMESH/Stylized Pack/Props VegetationIndirect"
{
    Properties
    {
        [HDR][Header(Base)]_BaseAlbedoColor("Base Albedo Color", Color) = (0.5019608,0.5019608,0.5019608,0.5019608)
        _BaseAlbedoBrightness("Base Albedo Brightness", Range(0,5)) = 1
        _BaseAlbedoDesaturation("Base Albedo Desaturation", Range(0,1)) = 0
        _BaseUVScale("Base UV Scale", Range(0,50)) = 1
        _BaseMetallicIntensity("Base Metallic Intensity", Range(0,1)) = 0
        _BaseSmoothnessMin("Base Smoothness Min", Range(0,5)) = 0
        _BaseSmoothnessMax("Base Smoothness Max", Range(0,5)) = 1
        _BaseNormalIntensity("Base Normal Intensity", Range(0,5)) = 1
        _BaseAOIntensity("Base AO Intensity", Range(0,1)) = 0.5
        [HDR]_BaseEmissiveColor("Base Emissive Color", Color) = (0,0,0,1)
        _BaseEmissiveIntensity("Base Emissive Intensity", Float) = 2
        _BaseEmissiveMaskContrast("Base Emissive Mask Contrast", Float) = 2
        [NoScaleOffset]_BaseAlbedo("Base Albedo", 2D) = "gray" {}
        [NoScaleOffset][Normal]_BaseNormal("Base Normal", 2D) = "bump" {}
        [NoScaleOffset]_BaseSMAE("Base SMAE", 2D) = "gray" {}

        [Header(Top Layer)][Toggle(_ENABLETOPLAYERBLEND_ON)] _EnableTopLayerBlend("Enable Top Layer Blend", Float) = 1
        [HDR]_TopLayerAlbedoColor("Top Layer Albedo Color", Color) = (0.5019608,0.5019608,0.5019608,0.5019608)
        _TopLayerUVScale("Top Layer UV Scale", Range(0,50)) = 5
        _TopLayerSmoothnessMin("Top Layer Smoothness Min", Range(0,5)) = 0
        _TopLayerSmoothnessMax("Top Layer Smoothness Max", Range(0,5)) = 1
        _TopLayerNormalIntensity("Top Layer Normal Intensity", Range(0,5)) = 1
        _TopLayerNormalInfluence("Top Layer Normal Influence", Range(0,1)) = 0
        _TopLayerIntensity("Top Layer Intensity", Range(0,1)) = 1
        _TopLayerOffset("Top Layer Offset", Range(0,1)) = 0.5
        _TopLayerContrast("Top Layer Contrast", Range(0,30)) = 10
        _TopLayerVPaintMaskIntensity("Top Layer VPaint Mask Intensity", Range(0,1)) = 0
        [NoScaleOffset]_TopLayerAlbedo("Top Layer Albedo", 2D) = "gray" {}
        [NoScaleOffset][Normal]_TopLayerNormal("Top Layer Normal", 2D) = "bump" {}
        [NoScaleOffset]_TopLayerSmoothness("Top Layer Smoothness", 2D) = "gray" {}

        [Header(Top Layer Noise)][Toggle(_ENABLETOPLAYERNOISE_ON)] _EnableTopLayerNoise("Enable Top Layer Noise", Float) = 0
        _TopLayerNoiseUVScale("Top Layer Noise UV Scale", Range(0,50)) = 5
        _TopLayerNoiseContrast("Top Layer Noise Contrast", Range(0,20)) = 1
        [NoScaleOffset]_TopLayerNoise("Top Layer Noise", 2D) = "black" {}

        [Header(Detail)][Toggle(_ENABLEDETAIL_ON)] _EnableDetail("Enable Detail", Float) = 1
        _DetailUVScale("Detail UV Scale", Range(0,50)) = 2
        _DetailAlbedoIntensity("Detail Albedo Intensity", Range(0,1)) = 1
        _DetailAlbedoPower("Detail Albedo Power", Range(0,10)) = 1
        _DetailNormalIntensity("Detail Normal Intensity", Range(0,5)) = 1
        [NoScaleOffset]_DetailAlbedo("Detail Albedo", 2D) = "gray" {}
        [NoScaleOffset][Normal]_DetailNormal("Detail Normal", 2D) = "bump" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Lit" }
        LOD 300
        Cull Back
        ZWrite On
        ZTest LEqual

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
        #include "Assets/DynamicNaturalEnvironment/InfiniteGrass/Vegetation/Shaders/VegetationIndirectCommon.hlsl"
        #include "Assets/DynamicNaturalEnvironment/InfiniteGrass/Vegetation/Shaders/VegetationLOD.hlsl"
        #include "Assets/DynamicNaturalEnvironment/InfiniteGrass/Vegetation/Shaders/VegetationDepthNormals.hlsl"
        #include "Assets/DynamicNaturalEnvironment/InfiniteGrass/Vegetation/Shaders/VegetationReceiveShadows.hlsl"
        #include "Assets/DynamicNaturalEnvironment/InfiniteGrass/Vegetation/Shaders/VegetationAdditionalLights.hlsl"

        TEXTURE2D(_BaseAlbedo); SAMPLER(sampler_BaseAlbedo);
        TEXTURE2D(_BaseNormal); SAMPLER(sampler_BaseNormal);
        TEXTURE2D(_BaseSMAE); SAMPLER(sampler_BaseSMAE);
        TEXTURE2D(_TopLayerAlbedo); SAMPLER(sampler_TopLayerAlbedo);
        TEXTURE2D(_TopLayerNormal); SAMPLER(sampler_TopLayerNormal);
        TEXTURE2D(_TopLayerSmoothness); SAMPLER(sampler_TopLayerSmoothness);
        TEXTURE2D(_TopLayerNoise); SAMPLER(sampler_TopLayerNoise);
        TEXTURE2D(_DetailAlbedo); SAMPLER(sampler_DetailAlbedo);
        TEXTURE2D(_DetailNormal); SAMPLER(sampler_DetailNormal);

        CBUFFER_START(UnityPerMaterial)
        half4 _BaseAlbedoColor, _BaseEmissiveColor, _TopLayerAlbedoColor;
        half _BaseAlbedoBrightness, _BaseAlbedoDesaturation, _BaseUVScale, _BaseMetallicIntensity, _BaseSmoothnessMin, _BaseSmoothnessMax, _BaseNormalIntensity, _BaseAOIntensity;
        half _BaseEmissiveIntensity, _BaseEmissiveMaskContrast;
        half _TopLayerUVScale, _TopLayerSmoothnessMin, _TopLayerSmoothnessMax, _TopLayerNormalIntensity, _TopLayerNormalInfluence, _TopLayerIntensity, _TopLayerOffset, _TopLayerContrast, _TopLayerVPaintMaskIntensity;
        half _TopLayerNoiseUVScale, _TopLayerNoiseContrast;
        half _DetailUVScale, _DetailAlbedoIntensity, _DetailAlbedoPower, _DetailNormalIntensity;
        CBUFFER_END

        half ASPP_TopLayerOffset, ASPP_TopLayerContrast, ASPP_TopLayerIntensity, ASPT_TopLayerHeightStart, ASPT_TopLayerHeightFade;

        half3 OverlayBlend(half3 src, half3 dst)
        {
            return saturate(lerp(2.0h * dst * src, 1.0h - 2.0h * (1.0h - dst) * (1.0h - src), step(0.5h, dst)));
        }

        half3 BlendNormalRNMCompat(half3 n1, half3 n2)
        {
            half3 t = n1 + half3(0,0,1);
            half3 u = n2 * half3(-1,-1,1);
            return normalize(t * dot(t,u) / max(t.z,1e-4h) - u);
        }

        float3 TransformVegetationTangentToWorld(float3 tangentOS, uint instanceID)
        {
            return normalize(mul((float3x3)GetVegetationObjectToWorld(instanceID), tangentOS));
        }

        float GetVegetationTransformSign(uint instanceID)
        {
            float3x3 m = (float3x3)GetVegetationObjectToWorld(instanceID);
            return determinant(m) < 0.0 ? -1.0 : 1.0;
        }

        struct Attributes
        {
            float4 positionOS : POSITION;
            half3 normalOS : NORMAL;
            half4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            float2 staticLightmapUV : TEXCOORD1;
            half4 color : COLOR;
            uint instanceID : SV_InstanceID;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            nointerpolation float lodDistance : TEXCOORD15;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half4 tangentWS : TEXCOORD2;
            float2 uv : TEXCOORD3;
            half4 color : COLOR;
            half fogFactor : TEXCOORD4;
            DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 5);
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings Vert(Attributes IN)
        {
            Varyings OUT = (Varyings)0;
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

            float3 positionWS = TransformVegetationPositionToWorld(IN.positionOS.xyz, IN.instanceID);
            OUT.lodDistance = GetVegetationLODDistance(GetVegetationPivotWS(IN.instanceID));
            half3 normalWS = TransformVegetationNormalToWorld(IN.normalOS, IN.instanceID);
            half3 tangentWS = TransformVegetationTangentToWorld(IN.tangentOS.xyz, IN.instanceID);

            OUT.positionCS = TransformWorldToHClip(positionWS);
            OUT.positionWS = positionWS;
            OUT.normalWS = normalWS;
            OUT.tangentWS = half4(tangentWS, IN.tangentOS.w * GetVegetationTransformSign(IN.instanceID));
            OUT.uv = IN.uv;
            OUT.color = IN.color;
            OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);

            OUTPUT_LIGHTMAP_UV(IN.staticLightmapUV, unity_LightmapST, OUT.staticLightmapUV);
            OUTPUT_SH(OUT.normalWS, OUT.vertexSH);

            return OUT;
        }

        void EvaluateSurface(Varyings IN, out half3 albedo, out half3 normalTS, out half3 emission, out half metallic, out half smoothness, out half occlusion)
        {
            float2 baseUV = IN.uv * _BaseUVScale;

            half3 baseTex = SAMPLE_TEXTURE2D(_BaseAlbedo, sampler_BaseAlbedo, baseUV).rgb;
            half lum = dot(baseTex, half3(0.299,0.587,0.114));
            baseTex = lerp(baseTex, lum.xxx, _BaseAlbedoDesaturation) * _BaseAlbedoBrightness;
            half3 baseAlbedo = OverlayBlend(baseTex, _BaseAlbedoColor.rgb);

            half3 baseNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(_BaseNormal, sampler_BaseNormal, baseUV), _BaseNormalIntensity);

            float2 detailUV = IN.uv * _DetailUVScale;
            half3 detailAlbedo = baseAlbedo;
            half3 detailNormal = baseNormal;

        #ifdef _ENABLEDETAIL_ON
            half3 d = pow(abs(SAMPLE_TEXTURE2D(_DetailAlbedo, sampler_DetailAlbedo, detailUV).rgb * 2.0h), _DetailAlbedoPower.xxx);
            detailAlbedo = lerp(baseAlbedo, OverlayBlend(baseAlbedo, d), _DetailAlbedoIntensity);
            detailNormal = BlendNormalRNMCompat(UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormal, sampler_DetailNormal, detailUV), _DetailNormalIntensity), baseNormal);
        #endif

            half3 bitangentWS = cross(IN.normalWS, IN.tangentWS.xyz) * IN.tangentWS.w;
            half3 baseWorldN = normalize(IN.tangentWS.xyz * baseNormal.x + bitangentWS * baseNormal.y + IN.normalWS * baseNormal.z);

            half heightFade = saturate((IN.positionWS.y - ASPT_TopLayerHeightStart) / max(abs(ASPT_TopLayerHeightFade), 1e-4h));
            half mask = saturate(pow(abs(saturate(baseWorldN.y) + _TopLayerOffset * ASPP_TopLayerOffset), max(_TopLayerContrast * ASPP_TopLayerContrast, 1e-4h)) * (_TopLayerIntensity * ASPP_TopLayerIntensity)) * heightFade;

        #ifdef _ENABLETOPLAYERNOISE_ON
            half noise = 1.0h - SAMPLE_TEXTURE2D(_TopLayerNoise, sampler_TopLayerNoise, IN.uv * 0.1h * _TopLayerNoiseUVScale).r;
            mask *= saturate(pow(abs(noise), _TopLayerNoiseContrast));
        #endif

            mask *= lerp(1.0h, IN.color.r, _TopLayerVPaintMaskIntensity);

            half4 smae = SAMPLE_TEXTURE2D(_BaseSMAE, sampler_BaseSMAE, baseUV);
            half baseMetal = smae.g * _BaseMetallicIntensity;
            half baseSmooth = saturate(lerp(_BaseSmoothnessMin, _BaseSmoothnessMax, smae.r));
            half baseAO = lerp(1.0h, smae.b, _BaseAOIntensity);
            half3 baseEmissive = _BaseEmissiveColor.rgb * pow(abs(smae.a), _BaseEmissiveMaskContrast) * _BaseEmissiveIntensity;

            albedo = detailAlbedo;
            normalTS = detailNormal;
            emission = baseEmissive;
            metallic = baseMetal;
            smoothness = baseSmooth;
            occlusion = baseAO;

        #ifdef _ENABLETOPLAYERBLEND_ON
            float2 topUV = IN.uv * _TopLayerUVScale;

            half3 topAlb = OverlayBlend(SAMPLE_TEXTURE2D(_TopLayerAlbedo, sampler_TopLayerAlbedo, topUV).rgb, _TopLayerAlbedoColor.rgb);
            half3 topN = UnpackNormalScale(SAMPLE_TEXTURE2D(_TopLayerNormal, sampler_TopLayerNormal, topUV), _TopLayerNormalIntensity);
            half3 topCombined = lerp(BlendNormalRNMCompat(topN, baseNormal), topN, _TopLayerNormalInfluence);

            albedo = lerp(detailAlbedo, topAlb, mask);
            normalTS = normalize(lerp(detailNormal, topCombined, mask));
            emission = lerp(baseEmissive, 0, mask);
            metallic = lerp(baseMetal, 0, mask);

            half topSmooth = lerp(_TopLayerSmoothnessMin, _TopLayerSmoothnessMax, SAMPLE_TEXTURE2D(_TopLayerSmoothness, sampler_TopLayerSmoothness, topUV).r);
            smoothness = saturate(lerp(baseSmooth, topSmooth, mask));
            occlusion = lerp(baseAO, 1.0h, mask);
        #endif
        }

        half4 Frag(Varyings IN) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
            ApplyVegetationLODCrossFade(IN.lodDistance, IN.positionCS.xy);

            half3 albedo;
            half3 normalTS;
            half3 emission;
            half metallic;
            half smoothness;
            half occlusion;

            EvaluateSurface(IN, albedo, normalTS, emission, metallic, smoothness, occlusion);

            half3 bitangentWS = cross(IN.normalWS, IN.tangentWS.xyz) * IN.tangentWS.w;
            half3 normalWS = normalize(IN.tangentWS.xyz * normalTS.x + bitangentWS * normalTS.y + IN.normalWS * normalTS.z);

            InputData inputData = (InputData)0;
            inputData.positionWS = IN.positionWS;
            inputData.normalWS = normalWS;
            inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
            inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
            inputData.fogCoord = IN.fogFactor;
            inputData.vertexLighting = VertexLighting(IN.positionWS, normalWS);
            inputData.bakedGI = SAMPLE_GI(IN.staticLightmapUV, IN.vertexSH, normalWS);
            inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
            inputData.shadowMask = SAMPLE_SHADOWMASK(IN.staticLightmapUV);

            half4 c = UniversalFragmentPBR(inputData, albedo, metallic, half3(0,0,0), smoothness, occlusion, emission, 1.0h);
            c.rgb += VegetationUnshadowedMainLightPBRDelta(inputData, albedo, metallic,
                half3(0.0h, 0.0h, 0.0h), smoothness, occlusion, 1.0h);
            c.rgb += EvaluateVegetationAdditionalLightsPBR(inputData, albedo, metallic, half3(0,0,0), smoothness, occlusion, 1.0h);
            c.rgb = MixFog(c.rgb, IN.fogFactor);

            return c;
        }

        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_fog
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #pragma shader_feature_local _ENABLETOPLAYERBLEND_ON
            #pragma shader_feature_local _ENABLEDETAIL_ON
            #pragma shader_feature_local _ENABLETOPLAYERNOISE_ON

            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation float lodDistance : TEXCOORD15;
            };

            ShadowVaryings ShadowVert(ShadowAttributes IN)
            {
                ShadowVaryings OUT = (ShadowVaryings)0;
                OUT.lodDistance = GetVegetationLODDistance(GetVegetationPivotWS(IN.instanceID));

                float3 positionWS = TransformVegetationPositionToWorld(IN.positionOS.xyz, IN.instanceID);
                half3 normalWS = TransformVegetationNormalToWorld(IN.normalOS, IN.instanceID);

                float3 lightDirWS = _LightDirection;

            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                lightDirWS = normalize(_LightPosition - positionWS);
            #endif

                positionWS = ApplyShadowBias(positionWS, normalWS, lightDirWS);
                OUT.positionCS = TransformWorldToHClip(positionWS);

            #if !defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                #if UNITY_REVERSED_Z
                    OUT.positionCS.z = min(OUT.positionCS.z, OUT.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    OUT.positionCS.z = max(OUT.positionCS.z, OUT.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
            #endif

                return OUT;
            }

            half4 ShadowFrag(ShadowVaryings IN) : SV_Target
            {
                ApplyVegetationLODCrossFade(IN.lodDistance, IN.positionCS.xy);
                return 0;
            }

            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                uint instanceID : SV_InstanceID;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation float lodDistance : TEXCOORD15;
            };

            DepthVaryings DepthVert(DepthAttributes IN)
            {
                DepthVaryings OUT = (DepthVaryings)0;
                OUT.lodDistance = GetVegetationLODDistance(GetVegetationPivotWS(IN.instanceID));
                float3 positionWS = TransformVegetationPositionToWorld(IN.positionOS.xyz, IN.instanceID);
                OUT.positionCS = TransformWorldToHClip(positionWS);
                return OUT;
            }

            half4 DepthFrag(DepthVaryings IN) : SV_Target
            {
                ApplyVegetationLODCrossFade(IN.lodDistance, IN.positionCS.xy);
                return 0;
            }

            ENDHLSL
        }
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma shader_feature_local _ENABLETOPLAYERBLEND_ON
            #pragma shader_feature_local _ENABLEDETAIL_ON
            #pragma shader_feature_local _ENABLETOPLAYERNOISE_ON

            half4 DepthNormalsFrag(Varyings IN) : SV_Target
            {
                half3 albedo, normalTS, emission;
                half metallic, smoothness, occlusion;
                EvaluateSurface(IN, albedo, normalTS, emission, metallic, smoothness, occlusion);
                ApplyVegetationLODCrossFade(IN.lodDistance, IN.positionCS.xy);
                half3 bitangentWS = cross(IN.normalWS, IN.tangentWS.xyz) * IN.tangentWS.w;
                half3 normalWS = normalize(IN.tangentWS.xyz * normalTS.x +
                    bitangentWS * normalTS.y + IN.normalWS * normalTS.z);
                return EncodeVegetationDepthNormal(normalWS);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
