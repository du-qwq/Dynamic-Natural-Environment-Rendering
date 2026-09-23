Shader "Vegetation/Foliage"
{
    Properties
    {
        [Header(Base)]
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (0.45,0.65,0.30,1)
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.4

        [Header(Color Variation)]
        _VariationColor("Variation Color", Color) = (0.32,0.50,0.22,1)
        _VariationStrength("Variation Strength", Range(0,1)) = 0.15

        [Header(Terrain Blend)]
        _TerrainColorBlend("Terrain Root Blend", Range(0,1)) = 0.05
        _TerrainBlendPower("Terrain Root Falloff", Range(0.1,8)) = 3

        [Header(Lighting)]
        _AmbientStrength("Ambient Strength", Range(0,2)) = 0.65
        _LightWrap("Light Wrap", Range(0,1)) = 0.45
        _NormalUpBlend("Normal Up Blend", Range(0,1)) = 0.35
        _MinimumLight("Minimum Direct Light", Range(0,1)) = 0.18
        _ShadowStrength("Shadow Strength", Range(0,1)) = 0.55

        [Header(Translucency)]
        [HDR]_TranslucencyColor("Translucency Color", Color) = (0.8,1.0,0.55,1)
        _TranslucencyStrength("Translucency Strength", Range(0,1)) = 0.08
        _TranslucencyPower("Translucency Power", Range(0.5,16)) = 3

        [Header(Wind)]
        [Toggle]_UseWind("Use Wind", Float) = 1
        _WindStrengthMultiplier("Wind Strength Multiplier", Range(0,3)) = 0.5
        _WindSpeedMultiplier("Wind Speed Multiplier", Range(0,3)) = 1
        _WindFrequencyMultiplier("Wind Frequency Multiplier", Range(0,3)) = 1
        _WindRootHeight("Wind Root Height", Float) = 0
        _WindTopHeight("Wind Top Height", Float) = 1
        _WindMaskFromUV("Wind Mask From UV Y", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "VegetationIndirectCommon.hlsl"
        #include "VegetationTerrainCommon.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        float4 _BaseColor;
        float4 _VariationColor;
        float4 _TranslucencyColor;

        float _Cutoff;
        float _VariationStrength;

        float _TerrainColorBlend;
        float _TerrainBlendPower;

        float _AmbientStrength;
        float _LightWrap;
        float _NormalUpBlend;
        float _MinimumLight;
        float _ShadowStrength;

        float _TranslucencyStrength;
        float _TranslucencyPower;

        float _UseWind;
        float _WindStrengthMultiplier;
        float _WindSpeedMultiplier;
        float _WindFrequencyMultiplier;
        float _WindRootHeight;
        float _WindTopHeight;
        float _WindMaskFromUV;
        CBUFFER_END

        float GetFoliageHeightMask(float3 positionOS, float2 uv)
        {
            float heightRange = max(_WindTopHeight - _WindRootHeight,0.0001);
            float heightMask = saturate((positionOS.y - _WindRootHeight) / heightRange);
            return lerp(heightMask,saturate(uv.y),_WindMaskFromUV);
        }

        float3 ApplyFoliageWind(float3 positionWS, float3 pivotWS, float heightMask)
        {
            if (_UseWind < 0.5) return positionWS;
            return ApplyVegetationWind(positionWS,pivotWS,heightMask,_WindStrengthMultiplier,_WindSpeedMultiplier,_WindFrequencyMultiplier);
        }

        float3 ApplyFoliageForwardWind(float3 positionWS,float3 pivotWS,float heightMask)
        {
            if (_UseWind < 0.5) return positionWS;
            return ApplyVegetationForwardWind(positionWS,pivotWS,heightMask,_WindStrengthMultiplier,_WindSpeedMultiplier,_WindFrequencyMultiplier);
        }

        float3 ApplyFoliageShadowWind(float3 positionWS,float3 pivotWS,float heightMask)
        {
            if (_UseWind < 0.5) return positionWS;
            float quality = GetVegetationShadowWindQuality(pivotWS);
            if (quality < 0.5) return positionWS;
            if (quality > 2.5) return ApplyFoliageWind(positionWS,pivotWS,heightMask);
            if (quality > 1.5) return ApplyVegetationShadowWind(positionWS,pivotWS,heightMask,_WindStrengthMultiplier,_WindSpeedMultiplier,_WindFrequencyMultiplier);
            return ApplyVegetationShadowMainBend(positionWS,pivotWS,heightMask,_WindStrengthMultiplier,_WindSpeedMultiplier,_WindFrequencyMultiplier);
        }

        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float variation : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                float2 terrainUV : TEXCOORD5;
                float rootMask : TEXCOORD6;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 pivotWS = GetVegetationPivotWS(input.instanceID);
                float3 positionWS = TransformVegetationPositionToWorld(input.positionOS,input.instanceID);
                float3 normalWS = TransformVegetationNormalToWorld(input.normalOS,input.instanceID);

                float heightMask = GetFoliageHeightMask(input.positionOS,input.uv);
                float rootMask = pow(saturate(1.0 - heightMask),_TerrainBlendPower);

                positionWS = ApplyFoliageForwardWind(positionWS,pivotWS,heightMask);

                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv,_BaseMap);
                output.variation = VegetationHash(pivotWS.xz * 0.731 + float2(2.17,7.31));
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.terrainUV = GetVegetationTerrainUV(pivotWS);
                output.rootMask = rootMask;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv);
                clip(baseSample.a * _BaseColor.a - _Cutoff);

                float3 normalWS = normalize(input.normalWS);
                normalWS = normalize(lerp(normalWS,float3(0,1,0),_NormalUpBlend));

                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                float variationWeight = input.variation * _VariationStrength;
                half3 tint = lerp(_BaseColor.rgb,_VariationColor.rgb,variationWeight);
                half3 albedo = baseSample.rgb * tint;

                if (_VegetationTerrainColorEnabled > 0.5)
                {
                    half3 terrainColor = SampleVegetationTerrainColor(input.terrainUV);
                    float terrainBlend = saturate(input.rootMask * _TerrainColorBlend);
                    albedo = lerp(albedo,terrainColor,terrainBlend);
                }

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float rawNdotL = dot(normalWS,mainLight.direction);
                float wrappedNdotL = saturate((rawNdotL + _LightWrap) / (1.0 + _LightWrap));
                wrappedNdotL = lerp(_MinimumLight,1.0,wrappedNdotL);

                float shadow = lerp(1.0,mainLight.shadowAttenuation,_ShadowStrength);

                half3 directLighting = mainLight.color * wrappedNdotL * mainLight.distanceAttenuation * shadow;
                half3 ambientLighting = SampleSH(normalWS) * _AmbientStrength;

                half3 color = albedo * (directLighting + ambientLighting);

                float backView = pow(saturate(dot(viewDirWS,-mainLight.direction)),_TranslucencyPower);
                float grazing = 1.0 - saturate(abs(dot(normalWS,mainLight.direction)));

                half3 translucency = albedo * _TranslucencyColor.rgb * mainLight.color;
                translucency *= backView * lerp(0.4,1.0,grazing) * _TranslucencyStrength;
                translucency *= mainLight.distanceAttenuation;
                translucency *= lerp(1.0,shadow,0.5);

                color += translucency;
                color = MixFog(color,input.fogFactor);

                return half4(color,1);
            }

            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull Off
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

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                nointerpolation float shadowCutoff : TEXCOORD1;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;

                float3 pivotWS = GetVegetationPivotWS(input.instanceID);
                float3 positionWS = TransformVegetationPositionToWorld(input.positionOS,input.instanceID);
                float3 normalWS = TransformVegetationNormalToWorld(input.normalOS,input.instanceID);

                float heightMask = GetFoliageHeightMask(input.positionOS,input.uv);
                positionWS = ApplyFoliageShadowWind(positionWS,pivotWS,heightMask);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS,normalWS,lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv,_BaseMap);
                output.shadowCutoff = GetVegetationShadowAlphaCutoff(_Cutoff,pivotWS);

                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).a * _BaseColor.a;
                clip(alpha - input.shadowCutoff);
                return 0;
            }

            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;

                float3 pivotWS = GetVegetationPivotWS(input.instanceID);
                float3 positionWS = TransformVegetationPositionToWorld(input.positionOS,input.instanceID);

                float heightMask = GetFoliageHeightMask(input.positionOS,input.uv);
                positionWS = ApplyFoliageWind(positionWS,pivotWS,heightMask);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv,_BaseMap);

                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).a * _BaseColor.a;
                clip(alpha - _Cutoff);
                return 0;
            }

            ENDHLSL
        }
    }
}