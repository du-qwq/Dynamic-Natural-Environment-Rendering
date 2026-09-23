Shader "Vegetation/Bush"
{
    Properties
    {
        [Toggle(_HIDESIDES_ON)] _HideSides("Hide Sides", Float) = 0
        _HidePower("Hide Power", Float) = 2.5
        _Cutoff("Mask Clip Value", Float) = 0.5

        [Header(Main Maps)]
        [Space(10)]
        _MainColor("Main Color", Color) = (1,1,1,1)
        _Diffuse("Diffuse", 2D) = "white" {}

        [Header(Gradient Parameters)]
        [Space(10)]
        _GradientColor("Gradient Color", Color) = (1,1,1,1)
        _GradientFalloff("Gradient Falloff", Range(0,2)) = 2
        _GradientPosition("Gradient Position", Range(0,1)) = 0.5
        [Toggle(_INVERTGRADIENT_ON)] _InvertGradient("Invert Gradient", Float) = 0

        [Header(Color Variation)]
        [Space(10)]
        _ColorVariation("Color Variation", Color) = (1,0,0,1)
        _ColorVariationPower("Color Variation Power", Range(0,1)) = 1
        _ColorVariationNoise("Color Variation Noise", 2D) = "white" {}
        _NoiseScale("Noise Scale", Float) = 0.5

        [Header(Wind)]
        [Space(10)]
        [KeywordEnum(Bush,TreeLeaf)] _WindMaskMode("Wind Mask Mode", Float) = 0
        _WindMultiplier("Base Wind Multiplier", Range(0,3)) = 0.25
        _MicroWindMultiplier("Micro Wind Multiplier", Range(0,3)) = 0.15

        [KeywordEnum(R,G,B,A)] _BaseWindChannel("Bush Base Wind Channel", Float) = 2
        [KeywordEnum(R,G,B,A)] _MicroWindChannel("Bush Micro Wind Channel", Float) = 0

        _WindTrunkPosition("Bush Wind Trunk Position", Range(0.01,8)) = 1
        _WindTrunkContrast("Bush Wind Trunk Contrast", Range(0.1,10)) = 2
        _WindTreeBaseRigidity("Tree Leaf Base Rigidity", Range(0.1,5)) = 2.5

        [Toggle(_WINDDEBUGVIEW_ON)] _WindDebugView("Wind Debug View", Float) = 0
        [Toggle(_SEEVERTEXCOLOR_ON)] _SeeVertexColor("See Vertex Color", Float) = 0

        [Header(Translucency)]
        _Translucency("Strength", Range(0,10)) = 1
        _TransNormalDistortion("Normal Distortion", Range(0,1)) = 0.1
        _TransScattering("Scattering Falloff", Range(1,50)) = 2
        _TransDirect("Direct", Range(0,1)) = 1
        _TransAmbient("Ambient", Range(0,1)) = 0.2
        _TransShadow("Shadow", Range(0,1)) = 0.9

        [Header(Lighting)]
        _AmbientStrength("Ambient Strength", Range(0,2)) = 1
        _LightWrap("Light Wrap", Range(0,1)) = 0.15
        _ShadowStrength("Shadow Strength", Range(0,1)) = 1
        _ShadowFloor("Minimum Shadow Light", Range(0,1)) = 0.18
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Back
        ZWrite On

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/DynamicNaturalEnvironment/InfiniteGrass/Vegetation/Shaders/VegetationIndirectCommon.hlsl"

        TEXTURE2D(_Diffuse);
        SAMPLER(sampler_Diffuse);

        TEXTURE2D(_ColorVariationNoise);
        SAMPLER(sampler_ColorVariationNoise);

        CBUFFER_START(UnityPerMaterial)

        float4 _Diffuse_ST;

        float4 _MainColor;
        float4 _GradientColor;
        float4 _ColorVariation;

        float _GradientFalloff;
        float _GradientPosition;

        float _ColorVariationPower;
        float _NoiseScale;

        float _Cutoff;
        float _HidePower;

        float _WindMultiplier;
        float _MicroWindMultiplier;
        float _WindTrunkContrast;
        float _WindTrunkPosition;
        float _WindTreeBaseRigidity;

        float _Translucency;
        float _TransNormalDistortion;
        float _TransScattering;
        float _TransDirect;
        float _TransAmbient;
        float _TransShadow;

        float _AmbientStrength;
        float _LightWrap;
        float _ShadowStrength;
        float _ShadowFloor;

        CBUFFER_END

        float BaseWindMask(float4 colorV)
        {
            #if defined(_BASEWINDCHANNEL_R)
                return colorV.r;
            #elif defined(_BASEWINDCHANNEL_G)
                return colorV.g;
            #elif defined(_BASEWINDCHANNEL_A)
                return colorV.a;
            #else
                return colorV.b;
            #endif
        }

        float MicroWindMask(float4 colorV)
        {
            #if defined(_MICROWINDCHANNEL_G)
                return colorV.g;
            #elif defined(_MICROWINDCHANNEL_B)
                return colorV.b;
            #elif defined(_MICROWINDCHANNEL_A)
                return colorV.a;
            #else
                return colorV.r;
            #endif
        }

        float GetBushBaseWindMask(float4 colorV)
        {
            float rawMask = saturate(1.0 - BaseWindMask(colorV));
            float shapedMask = pow(rawMask,max(_WindTrunkPosition,0.01));
            return saturate(shapedMask * _WindTrunkContrast);
        }

        float GetTreeLeafBaseWindMask(float2 windUV)
        {
            return saturate(pow(abs(windUV.y),max(_WindTreeBaseRigidity,0.01)));
        }

        float GetTreeLeafMicroWindMask(float2 windUV,float4 colorV)
        {
            float leafMask = saturate(colorV.b);
            float branchMask = saturate(colorV.g * abs(windUV.y));
            return saturate(max(leafMask,branchMask));
        }

        float GetBaseWindMask(float2 windUV,float4 colorV)
        {
            #if defined(_WINDMASKMODE_TREELEAF)
                return GetTreeLeafBaseWindMask(windUV);
            #else
                return GetBushBaseWindMask(colorV);
            #endif
        }

        float GetMicroWindMask(float2 windUV,float4 colorV)
        {
            #if defined(_WINDMASKMODE_TREELEAF)
                return GetTreeLeafMicroWindMask(windUV,colorV);
            #else
                return saturate(MicroWindMask(colorV));
            #endif
        }

        float3 ApplyBushWind(float3 positionWS,float3 pivotWS,float3 normalWS,float2 windUV,float4 colorV)
        {
            float baseMask = GetBaseWindMask(windUV,colorV);

            positionWS = ApplyVegetationWind(
                positionWS,
                pivotWS,
                baseMask,
                _WindMultiplier,
                1.0,
                1.0
            );

            float microMask = GetMicroWindMask(windUV,colorV);
            float microPhase = dot(positionWS,float3(1.73,2.17,1.31));
            float microWaveA = sin(_Time.y * 2.7 + microPhase * 2.2);
            float microWaveB = sin(_Time.y * 4.1 + microPhase * 3.7 + 1.37);
            float microWave = microWaveA * 0.65 + microWaveB * 0.35;

            positionWS += normalWS * microWave * microMask * _MicroWindMultiplier * 0.025;
            return positionWS;
        }

        float3 ApplyBushForwardWind(float3 positionWS,float3 pivotWS,float3 normalWS,float2 windUV,float4 colorV)
        {
            float quality = GetVegetationForwardWindQuality(pivotWS);
            if (quality < 0.5) return positionWS;
            if (quality > 2.5) return ApplyBushWind(positionWS,pivotWS,normalWS,windUV,colorV);
            float baseMask = GetBaseWindMask(windUV,colorV);
            if (quality > 1.5) return ApplyVegetationShadowWind(positionWS,pivotWS,baseMask,_WindMultiplier,1.0,1.0);
            return ApplyVegetationShadowMainBend(positionWS,pivotWS,baseMask,_WindMultiplier,1.0,1.0);
        }

        float3 ApplyBushShadowWind(float3 positionWS,float3 pivotWS,float3 normalWS,float2 windUV,float4 colorV)
        {
            float quality = GetVegetationShadowWindQuality(pivotWS);
            if (quality < 0.5) return positionWS;
            if (quality > 2.5) return ApplyBushWind(positionWS,pivotWS,normalWS,windUV,colorV);
            float baseMask = GetBaseWindMask(windUV,colorV);
            if (quality > 1.5) return ApplyVegetationShadowWind(positionWS,pivotWS,baseMask,_WindMultiplier,1.0,1.0);
            return ApplyVegetationShadowMainBend(positionWS,pivotWS,baseMask,_WindMultiplier,1.0,1.0);
        }

        float3 GetLeafColor(float3 positionWS,float3 normalWS,half4 tex)
        {
            float ny = saturate(normalize(normalWS).y);

            #if defined(_INVERTGRADIENT_ON)
                ny = 1.0 - ny;
            #endif

            float gradient = saturate(
                (ny + lerp(-2.0,1.0,_GradientPosition)) /
                max(_GradientFalloff,0.0001)
            );

            float3 baseColor = lerp(
                _MainColor.rgb,
                _GradientColor.rgb,
                gradient
            );

            float2 noiseUV = positionWS.xz * (_NoiseScale / 100.0);

            float noise = SAMPLE_TEXTURE2D(
                _ColorVariationNoise,
                sampler_ColorVariationNoise,
                noiseUV
            ).r;

            float variationMask = saturate(
                _ColorVariationPower *
                noise *
                noise *
                noise
            );

            float3 variationColor = saturate(
                _ColorVariation.rgb /
                max(1.0 - baseColor,0.0001)
            );

            float3 variedColor = lerp(
                baseColor,
                variationColor,
                variationMask
            );

            return variedColor * tex.rgb;
        }

        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM

            #pragma target 4.5

            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #pragma shader_feature_local_vertex _WINDMASKMODE_BUSH _WINDMASKMODE_TREELEAF
            #pragma shader_feature_local_vertex _BASEWINDCHANNEL_R _BASEWINDCHANNEL_G _BASEWINDCHANNEL_B _BASEWINDCHANNEL_A
            #pragma shader_feature_local_vertex _MICROWINDCHANNEL_R _MICROWINDCHANNEL_G _MICROWINDCHANNEL_B _MICROWINDCHANNEL_A

            #pragma shader_feature_local_fragment _INVERTGRADIENT_ON
            #pragma shader_feature_local_fragment _HIDESIDES_ON
            #pragma shader_feature_local_fragment _WINDDEBUGVIEW_ON
            #pragma shader_feature_local_fragment _SEEVERTEXCOLOR_ON

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 windUV : TEXCOORD2;
                float4 color : COLOR;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 color : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                float2 windDebug : TEXCOORD5;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 pivotWS = GetVegetationPivotWS(input.instanceID);

                float3 positionWS = TransformVegetationPositionToWorld(
                    input.positionOS,
                    input.instanceID
                );

                float3 normalWS = TransformVegetationNormalToWorld(
                    input.normalOS,
                    input.instanceID
                );

                positionWS = ApplyBushForwardWind(
                    positionWS,
                    pivotWS,
                    normalWS,
                    input.windUV,
                    input.color
                );

                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.positionCS = TransformWorldToHClip(positionWS);

                output.uv = TRANSFORM_TEX(
                    input.uv,
                    _Diffuse
                );

                output.color = input.color;
                output.windDebug = float2(GetBaseWindMask(input.windUV,input.color),GetMicroWindMask(input.windUV,input.color));

                output.fogFactor = ComputeFogFactor(
                    output.positionCS.z
                );

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_Diffuse,sampler_Diffuse,input.uv);
                float alpha = tex.a;

                #if defined(_HIDESIDES_ON)
                    float3 hideViewDir = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                    float3 faceNormal = SafeNormalize(cross(ddy(input.positionWS),ddx(input.positionWS)));
                    alpha *= saturate(abs(dot(hideViewDir,faceNormal))*_HidePower);
                #endif

                clip(alpha - _Cutoff);

                float3 normalWS = normalize(input.normalWS);

                float3 leafColor = GetLeafColor(input.positionWS,normalWS,tex);

                #if defined(_WINDDEBUGVIEW_ON)
                    leafColor = float3(input.windDebug.x,input.windDebug.y,0);
                #endif

                #if defined(_SEEVERTEXCOLOR_ON)
                    leafColor = input.color.rgb;
                #endif

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float ndl = dot(normalWS,mainLight.direction);
                float wrappedNdotL = saturate((ndl + _LightWrap)/(1.0 + _LightWrap));

                float shadowAttenuation = max(mainLight.shadowAttenuation,_ShadowFloor);
                float shadow = lerp(1.0,shadowAttenuation,_ShadowStrength);

                float3 ambient = SampleSH(normalWS) * _AmbientStrength;

                float3 direct = mainLight.color * wrappedNdotL * mainLight.distanceAttenuation * shadow;

                float3 viewDir = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));

                float3 distortedLightDir = normalize(mainLight.direction + normalWS * _TransNormalDistortion);

                float back = pow(saturate(dot(viewDir,-distortedLightDir)),max(_TransScattering,1.0));

                float translucencyShadow = lerp(1.0,shadowAttenuation,_TransShadow);

                float3 translucency = mainLight.color * (back * _TransDirect + _TransAmbient) * _Translucency * translucencyShadow;

                float3 finalColor = leafColor * (ambient + direct);

                finalColor += leafColor * translucency;

                finalColor = MixFog(finalColor,input.fogFactor);

                return half4(finalColor,1);
            }

            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #pragma shader_feature_local_vertex _WINDMASKMODE_BUSH _WINDMASKMODE_TREELEAF
            #pragma shader_feature_local_vertex _BASEWINDCHANNEL_R _BASEWINDCHANNEL_G _BASEWINDCHANNEL_B _BASEWINDCHANNEL_A
            #pragma shader_feature_local_vertex _MICROWINDCHANNEL_R _MICROWINDCHANNEL_G _MICROWINDCHANNEL_B _MICROWINDCHANNEL_A

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 windUV : TEXCOORD2;
                float4 color : COLOR;
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

                float3 positionWS =
                    TransformVegetationPositionToWorld(
                        input.positionOS,
                        input.instanceID
                    );

                float3 normalWS =
                    TransformVegetationNormalToWorld(
                        input.normalOS,
                        input.instanceID
                    );

                positionWS = ApplyBushShadowWind(positionWS,pivotWS,normalWS,input.windUV,input.color);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS =
                        normalize(
                            _LightPosition -
                            positionWS
                        );
                #else
                    float3 lightDirectionWS =
                        _LightDirection;
                #endif

                float4 positionCS =
                    TransformWorldToHClip(
                        ApplyShadowBias(
                            positionWS,
                            normalWS,
                            lightDirectionWS
                        )
                    );

                #if UNITY_REVERSED_Z
                    positionCS.z =
                        min(
                            positionCS.z,
                            UNITY_NEAR_CLIP_VALUE
                        );
                #else
                    positionCS.z =
                        max(
                            positionCS.z,
                            UNITY_NEAR_CLIP_VALUE
                        );
                #endif

                output.positionCS = positionCS;

                output.uv = TRANSFORM_TEX(
                    input.uv,
                    _Diffuse
                );
                output.shadowCutoff = GetVegetationShadowAlphaCutoff(_Cutoff,pivotWS);

                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(
                    _Diffuse,
                    sampler_Diffuse,
                    input.uv
                ).a;

                clip(alpha - input.shadowCutoff);

                return 0;
            }

            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #pragma shader_feature_local_vertex _WINDMASKMODE_BUSH _WINDMASKMODE_TREELEAF
            #pragma shader_feature_local_vertex _BASEWINDCHANNEL_R _BASEWINDCHANNEL_G _BASEWINDCHANNEL_B _BASEWINDCHANNEL_A
            #pragma shader_feature_local_vertex _MICROWINDCHANNEL_R _MICROWINDCHANNEL_G _MICROWINDCHANNEL_B _MICROWINDCHANNEL_A

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 windUV : TEXCOORD2;
                float4 color : COLOR;
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

                float3 pivotWS =
                    GetVegetationPivotWS(
                        input.instanceID
                    );

                float3 positionWS =
                    TransformVegetationPositionToWorld(
                        input.positionOS,
                        input.instanceID
                    );

                float3 normalWS =
                    TransformVegetationNormalToWorld(
                        input.normalOS,
                        input.instanceID
                    );

                positionWS = ApplyBushWind(
                    positionWS,
                    pivotWS,
                    normalWS,
                    input.windUV,
                    input.color
                );

                output.positionCS =
                    TransformWorldToHClip(
                        positionWS
                    );

                output.uv = TRANSFORM_TEX(
                    input.uv,
                    _Diffuse
                );

                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(
                    _Diffuse,
                    sampler_Diffuse,
                    input.uv
                ).a;

                clip(alpha - _Cutoff);

                return 0;
            }

            ENDHLSL
        }
    }

    Fallback Off
}
