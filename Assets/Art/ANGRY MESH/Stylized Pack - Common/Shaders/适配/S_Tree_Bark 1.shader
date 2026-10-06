Shader "ANGRYMESH/Stylized Pack/Tree Bark VegetationIndirect"
{
    Properties
    {
        [HDR][Header(Base)]_BaseAlbedoColor("Base Albedo Color", Color)=(0.5019608,0.5019608,0.5019608,0.5019608)
        _BaseAlbedoBrightness("Base Albedo Brightness",Range(0,5))=1
        _BaseAlbedoDesaturation("Base Albedo Desaturation",Range(0,1))=0
        _BaseMetallicIntensity("Base Metallic Intensity",Range(0,1))=0
        _BaseSmoothnessMin("Base Smoothness Min",Range(0,5))=0
        _BaseSmoothnessMax("Base Smoothness Max",Range(0,5))=1
        _BaseNormalIntensity("Base Normal Intensity",Range(0,5))=1
        _BaseBarkAOIntensity("Base Bark AO Intensity",Range(0,1))=0.5
        _BaseTreeAOIntensity("Base Tree AO Intensity",Range(0,1))=0
        [HDR]_BaseEmissiveColor("Base Emissive Color",Color)=(0,0,0,1)
        _BaseEmissiveIntensity("Base Emissive Intensity",Float)=2
        _BaseEmissiveMaskContrast("Base Emissive Mask Contrast",Float)=2
        [NoScaleOffset]_BaseAlbedo("Base Albedo",2D)="gray"{}
        [NoScaleOffset]_BaseNormal("Base Normal",2D)="bump"{}
        [NoScaleOffset]_BaseSMAE("Base SMAE",2D)="gray"{}

        [Header(Top Layer)][Toggle(_ENABLETOPLAYERBLEND_ON)] _EnableTopLayerBlend("Enable Top Layer Blend",Float)=1
        [HDR]_TopLayerAlbedoColor("Top Layer Albedo Color",Color)=(0.5019608,0.5019608,0.5019608,0.5019608)
        _TopLayerUVScale("Top Layer UV Scale",Range(0,50))=1
        _TopLayerSmoothnessMin("Top Layer Smoothness Min",Range(0,5))=0
        _TopLayerSmoothnessMax("Top Layer Smoothness Max",Range(0,5))=1
        _TopLayerNormalIntensity("Top Layer Normal Intensity",Range(0,5))=1
        _TopLayerNormalInfluence("Top Layer Normal Influence",Range(0,1))=0
        _TopLayerIntensity("Top Layer Intensity",Range(0,1))=1
        _TopLayerOffset("Top Layer Offset",Range(0,1))=0.5
        _TopLayerContrast("Top Layer Contrast",Range(0,30))=10
        _TopArrowDirectionOffset("Top Arrow Direction Offset",Range(0,2))=0
        _TopLayerBottomOffset("Top Layer Bottom Offset",Range(0,5))=1
        [NoScaleOffset]_TopLayerAlbedo("Top Layer Albedo",2D)="gray"{}
        [NoScaleOffset][Normal]_TopLayerNormal("Top Layer Normal",2D)="bump"{}
        [NoScaleOffset]_TopLayerSmoothness("Top Layer Smoothness",2D)="gray"{}

        [Header(Wind)][Toggle(_ENABLEWIND_ON)] _EnableWind("Enable Wind",Float)=1
        [Header(Wind (Vegetation Wind Controller))]_WindTreeFlexibility("Wind Tree Flexibility",Range(0,2))=1
        _WindTreeBaseRigidity("Wind Tree Base Rigidity",Range(0,5))=2.5
        _WindTreeSpeedMultiplier("Wind Tree Speed Multiplier",Range(0,2))=1
        _WindTreeFrequencyMultiplier("Wind Tree Frequency Multiplier",Range(0.05,3))=1
        [Toggle(_ENABLESTATICMESHSUPPORT_ON)] _EnableStaticMeshSupport("Enable Static Mesh Support",Float)=0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
            "UniversalMaterialType"="Lit"
        }

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

        TEXTURE2D(_BaseAlbedo);
        SAMPLER(sampler_BaseAlbedo);

        TEXTURE2D(_BaseNormal);
        SAMPLER(sampler_BaseNormal);

        TEXTURE2D(_BaseSMAE);
        SAMPLER(sampler_BaseSMAE);

        TEXTURE2D(_TopLayerAlbedo);
        SAMPLER(sampler_TopLayerAlbedo);

        TEXTURE2D(_TopLayerNormal);
        SAMPLER(sampler_TopLayerNormal);

        TEXTURE2D(_TopLayerSmoothness);
        SAMPLER(sampler_TopLayerSmoothness);

        CBUFFER_START(UnityPerMaterial)

        half4 _BaseAlbedoColor;
        half4 _TopLayerAlbedoColor;
        half4 _BaseEmissiveColor;

        half _BaseAlbedoBrightness;
        half _BaseAlbedoDesaturation;
        half _BaseMetallicIntensity;
        half _BaseSmoothnessMin;
        half _BaseSmoothnessMax;
        half _BaseNormalIntensity;
        half _BaseBarkAOIntensity;
        half _BaseTreeAOIntensity;
        half _BaseEmissiveIntensity;
        half _BaseEmissiveMaskContrast;

        half _TopLayerUVScale;
        half _TopLayerSmoothnessMin;
        half _TopLayerSmoothnessMax;
        half _TopLayerNormalIntensity;
        half _TopLayerNormalInfluence;
        half _TopLayerIntensity;
        half _TopLayerOffset;
        half _TopLayerContrast;
        half _TopArrowDirectionOffset;
        half _TopLayerBottomOffset;

        half _WindTreeFlexibility;
        half _WindTreeBaseRigidity;
        half _WindTreeSpeedMultiplier;
        half _WindTreeFrequencyMultiplier;

        CBUFFER_END

        // 原 Stylized Pack 全局参数。
        // Top Layer 的视觉逻辑必须继续使用它们，否则即使 Material 参数一致也会和原 Shader 不同。
        half3 ASPW_WindDirection;

        half ASPT_TopLayerOffset;
        half ASPT_TopLayerContrast;
        half ASPT_TopLayerIntensity;
        half ASPT_TopLayerArrowDirection;
        half ASPT_TopLayerBottomOffset;
        half ASPT_TopLayerHeightStart;
        half ASPT_TopLayerHeightFade;

        half3 OverlayBlend(half3 src, half3 dst)
        {
            return saturate(lerp(
                2.0h * dst * src,
                1.0h - 2.0h * (1.0h - dst) * (1.0h - src),
                step(0.5h, dst)
            ));
        }

        half3 BlendNormalRNMCompat(half3 n1, half3 n2)
        {
            half3 t = n1 + half3(0.0h, 0.0h, 1.0h);
            half3 u = n2 * half3(-1.0h, -1.0h, 1.0h);

            return normalize(t * dot(t, u) / max(t.z, 1e-4h) - u);
        }

        float3 TransformVegetationTangentToWorld(float3 tangentOS, uint instanceID)
        {
            return normalize(mul(
                (float3x3)GetVegetationObjectToWorld(instanceID),
                tangentOS
            ));
        }

        float GetVegetationTransformSign(uint instanceID)
        {
            float3x3 m = (float3x3)GetVegetationObjectToWorld(instanceID);
            return determinant(m) < 0.0 ? -1.0 : 1.0;
        }

        float GetTreeWindMask(float2 uv2)
        {
            return pow(
                saturate(abs(uv2.y)),
                max((float)_WindTreeBaseRigidity, 0.0001)
            );
        }

        float3 ApplyTreeWindWS(
            float3 positionWS,
            float3 pivotWS,
            float2 uv2
        )
        {
        #ifndef _ENABLEWIND_ON
            return positionWS;
        #else

            float windMask = GetTreeWindMask(uv2);

            float strengthMultiplier =
                max((float)_WindTreeFlexibility, 0.0);

            float speedMultiplier =
                max((float)_WindTreeSpeedMultiplier, 0.01);

            float frequencyMultiplier =
                max((float)_WindTreeFrequencyMultiplier, 0.01);

            return ApplyVegetationWind(
                positionWS,
                pivotWS,
                windMask,
                strengthMultiplier,
                speedMultiplier,
                frequencyMultiplier
            );

        #endif
        }

        float3 ApplyTreeForwardWindWS(float3 positionWS,float3 pivotWS,float2 uv2)
        {
        #ifndef _ENABLEWIND_ON
            return positionWS;
        #else
            float quality=GetVegetationForwardWindQuality(pivotWS);
            if(quality<0.5)return positionWS;
            if(quality>2.5)return ApplyTreeWindWS(positionWS,pivotWS,uv2);
            float windMask=GetTreeWindMask(uv2);
            float strengthMultiplier=max((float)_WindTreeFlexibility,0.0);
            float speedMultiplier=max((float)_WindTreeSpeedMultiplier,0.01);
            float frequencyMultiplier=max((float)_WindTreeFrequencyMultiplier,0.01);
            if(quality>1.5)return ApplyVegetationShadowWind(positionWS,pivotWS,windMask,strengthMultiplier,speedMultiplier,frequencyMultiplier);
            return ApplyVegetationShadowMainBend(positionWS,pivotWS,windMask,strengthMultiplier,speedMultiplier,frequencyMultiplier);
        #endif
        }

        float3 ApplyTreeShadowWindWS(float3 positionWS,float3 pivotWS,float2 uv2)
        {
        #ifndef _ENABLEWIND_ON
            return positionWS;
        #else
            float quality=GetVegetationShadowWindQuality(pivotWS);
            if(quality<0.5)return positionWS;
            if(quality>2.5)return ApplyTreeWindWS(positionWS,pivotWS,uv2);
            float windMask=GetTreeWindMask(uv2);
            float strengthMultiplier=max((float)_WindTreeFlexibility,0.0);
            float speedMultiplier=max((float)_WindTreeSpeedMultiplier,0.01);
            float frequencyMultiplier=max((float)_WindTreeFrequencyMultiplier,0.01);
            if(quality>1.5)return ApplyVegetationShadowWind(positionWS,pivotWS,windMask,strengthMultiplier,speedMultiplier,frequencyMultiplier);
            return ApplyVegetationShadowMainBend(positionWS,pivotWS,windMask,strengthMultiplier,speedMultiplier,frequencyMultiplier);
        #endif
        }

        struct Attributes
        {
            float4 positionOS : POSITION;

            half3 normalOS : NORMAL;
            half4 tangentOS : TANGENT;

            float2 uv : TEXCOORD0;
            float2 staticLightmapUV : TEXCOORD1;

            float2 uv2 : TEXCOORD2;
            float2 uv3 : TEXCOORD3;

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
            float2 uv2 : TEXCOORD4;
            float2 uv3 : TEXCOORD5;

            half4 color : COLOR;

            half fogFactor : TEXCOORD6;

            DECLARE_LIGHTMAP_OR_SH(
                staticLightmapUV,
                vertexSH,
                7
            );

            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings Vert(Attributes IN)
        {
            Varyings O = (Varyings)0;

            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(O);

            float3 pivotWS =
                GetVegetationPivotWS(IN.instanceID);
            O.lodDistance = GetVegetationLODDistance(pivotWS);

            float3 positionWS =
                TransformVegetationPositionToWorld(
                    IN.positionOS.xyz,
                    IN.instanceID
                );

            half3 normalWS =
                TransformVegetationNormalToWorld(
                    IN.normalOS,
                    IN.instanceID
                );

            half3 tangentWS =
                TransformVegetationTangentToWorld(
                    IN.tangentOS.xyz,
                    IN.instanceID
                );

            positionWS =
                ApplyTreeForwardWindWS(
                    positionWS,
                    pivotWS,
                    IN.uv2
                );

            O.positionWS = positionWS;
            O.positionCS = TransformWorldToHClip(positionWS);

            O.normalWS = normalWS;

            O.tangentWS = half4(
                tangentWS,
                IN.tangentOS.w *
                GetVegetationTransformSign(IN.instanceID)
            );

            O.uv = IN.uv;
            O.uv2 = IN.uv2;
            O.uv3 = IN.uv3;

            O.color = IN.color;

            O.fogFactor =
                ComputeFogFactor(O.positionCS.z);

            OUTPUT_LIGHTMAP_UV(
                IN.staticLightmapUV,
                unity_LightmapST,
                O.staticLightmapUV
            );

            OUTPUT_SH(
                O.normalWS,
                O.vertexSH
            );

            return O;
        }

        void EvaluateBark(
            Varyings IN,
            out half3 albedo,
            out half3 normalTS,
            out half3 emission,
            out half metallic,
            out half smoothness,
            out half occlusion
        )
        {
            // ---------------------------------------------------------
            // Base
            // ---------------------------------------------------------

            half treeMask = IN.color.b;

            half3 baseTex0 =
                SAMPLE_TEXTURE2D(
                    _BaseAlbedo,
                    sampler_BaseAlbedo,
                    IN.uv
                ).rgb;

            half3 baseTex1 =
                SAMPLE_TEXTURE2D(
                    _BaseAlbedo,
                    sampler_BaseAlbedo,
                    IN.uv3
                ).rgb;

            half3 baseTex =
                lerp(
                    baseTex0,
                    baseTex1,
                    treeMask
                );

            half lum =
                dot(
                    baseTex,
                    half3(
                        0.299h,
                        0.587h,
                        0.114h
                    )
                );

            baseTex =
                lerp(
                    baseTex,
                    lum.xxx,
                    _BaseAlbedoDesaturation
                ) *
                _BaseAlbedoBrightness;

            half3 baseAlb =
                OverlayBlend(
                    baseTex,
                    _BaseAlbedoColor.rgb
                );

            // ---------------------------------------------------------
            // SMAE
            // ---------------------------------------------------------

            half4 s0 =
                SAMPLE_TEXTURE2D(
                    _BaseSMAE,
                    sampler_BaseSMAE,
                    IN.uv
                );

            half4 s1 =
                SAMPLE_TEXTURE2D(
                    _BaseSMAE,
                    sampler_BaseSMAE,
                    IN.uv3
                );

            half4 smae =
                lerp(
                    s0,
                    s1,
                    treeMask
                );

            // 这里严格保持原 Shader。
            half baseAO =
                lerp(
                    1.0h,
                    smae.b,
                    _BaseBarkAOIntensity
                )
                *
                lerp(
                    1.0h,
                    IN.color.a,
                    _BaseTreeAOIntensity
                );

            // ---------------------------------------------------------
            // Base Normal
            // ---------------------------------------------------------

            half3 n0 =
                UnpackNormalScale(
                    SAMPLE_TEXTURE2D(
                        _BaseNormal,
                        sampler_BaseNormal,
                        IN.uv
                    ),
                    _BaseNormalIntensity
                );

            half3 n1 =
                UnpackNormalScale(
                    SAMPLE_TEXTURE2D(
                        _BaseNormal,
                        sampler_BaseNormal,
                        IN.uv3
                    ),
                    _BaseNormalIntensity
                );

            half3 baseN =
                normalize(
                    lerp(
                        n0,
                        n1,
                        treeMask
                    )
                );

            // ---------------------------------------------------------
            // World Normal
            // ---------------------------------------------------------

            half3 bitangentWS =
                cross(
                    IN.normalWS,
                    IN.tangentWS.xyz
                )
                *
                IN.tangentWS.w;

            half3 wN =
                normalize(
                    IN.tangentWS.xyz * baseN.x +
                    bitangentWS * baseN.y +
                    IN.normalWS * baseN.z
                );

            // ---------------------------------------------------------
            // Top Layer
            //
            // 这里全部恢复原 Shader 的 ASPT 全局参与方式。
            // ---------------------------------------------------------

            half topOffset =
                _TopLayerOffset *
                ASPT_TopLayerOffset;

            half topContrast =
                max(
                    _TopLayerContrast *
                    ASPT_TopLayerContrast,
                    1e-4h
                );

            half topIntensity =
                _TopLayerIntensity *
                ASPT_TopLayerIntensity;

            // 原 Shader 的方向遮罩依赖 ASPW_WindDirection。
            // 这里不换成 Vegetation Wind Direction，
            // 因为这是 Surface 外观逻辑，不是顶点风逻辑。
            half3 surfaceDirection =
                normalize(
                    ASPW_WindDirection +
                    half3(
                        1e-4h,
                        0.0h,
                        0.0h
                    )
                );

            half directionMask =
                (
                    1.0h -
                    dot(
                        wN,
                        surfaceDirection
                    )
                )
                *
                (
                    _TopArrowDirectionOffset *
                    ASPT_TopLayerArrowDirection
                )
                *
                (
                    (
                        3.0h -
                        (
                            1.0h -
                            dot(
                                wN,
                                half3(
                                    0.0h,
                                    1.0h,
                                    0.0h
                                )
                            )
                        )
                    )
                    *
                    (
                        1.0h -
                        IN.uv2.y
                    )
                    +
                    0.05h
                );

            half inv =
                1.0h -
                IN.uv2.y;

            half bottom =
                pow(
                    inv,
                    4.0h
                )
                *
                _TopLayerBottomOffset
                *
                ASPT_TopLayerBottomOffset;

            bottom =
                bottom *
                bottom
                +
                (
                    dot(
                        wN,
                        half3(
                            0.0h,
                            1.0h,
                            0.0h
                        )
                    )
                    -
                    1.0h
                );

            half directional =
                saturate(
                    pow(
                        abs(
                            clamp(
                                directionMask +
                                bottom,
                                0.0h,
                                5.0h
                            )
                            *
                            topOffset
                        ),
                        topContrast
                    )
                    *
                    topIntensity
                );

            half height =
                saturate(
                    (
                        IN.positionWS.y -
                        ASPT_TopLayerHeightStart
                    )
                    /
                    max(
                        abs(
                            ASPT_TopLayerHeightFade
                        ),
                        1e-4h
                    )
                );

            half mask =
                saturate(
                    (
                        saturate(
                            pow(
                                abs(
                                    saturate(wN.y) +
                                    topOffset
                                ),
                                topContrast
                            )
                        )
                        *
                        topIntensity
                        +
                        directional
                    )
                    *
                    height
                );

            // ---------------------------------------------------------
            // Base Surface
            // ---------------------------------------------------------

            half baseMetal =
                smae.g *
                _BaseMetallicIntensity;

            half baseSmooth =
                saturate(
                    lerp(
                        _BaseSmoothnessMin,
                        _BaseSmoothnessMax,
                        smae.r
                    )
                );

            half3 baseEm =
                _BaseEmissiveColor.rgb
                *
                pow(
                    abs(smae.a),
                    _BaseEmissiveMaskContrast
                )
                *
                _BaseEmissiveIntensity;

            albedo = baseAlb;
            normalTS = baseN;
            emission = baseEm;

            metallic = baseMetal;
            smoothness = baseSmooth;
            occlusion = baseAO;

            // ---------------------------------------------------------
            // Top Layer
            // ---------------------------------------------------------

        #ifdef _ENABLETOPLAYERBLEND_ON

            float2 tuv =
                IN.uv *
                _TopLayerUVScale;

            half3 topAlb =
                OverlayBlend(
                    SAMPLE_TEXTURE2D(
                        _TopLayerAlbedo,
                        sampler_TopLayerAlbedo,
                        tuv
                    ).rgb,
                    _TopLayerAlbedoColor.rgb
                )
                *
                baseAO;

            half3 topN =
                UnpackNormalScale(
                    SAMPLE_TEXTURE2D(
                        _TopLayerNormal,
                        sampler_TopLayerNormal,
                        tuv
                    ),
                    _TopLayerNormalIntensity
                );

            half3 combined =
                lerp(
                    BlendNormalRNMCompat(
                        topN,
                        baseN
                    ),
                    topN,
                    _TopLayerNormalInfluence
                );

            albedo =
                lerp(
                    baseAlb,
                    topAlb,
                    mask
                );

            normalTS =
                normalize(
                    lerp(
                        baseN,
                        combined,
                        mask
                    )
                );

            emission =
                lerp(
                    baseEm,
                    0.0h,
                    mask
                );

            metallic =
                lerp(
                    baseMetal,
                    0.0h,
                    mask
                );

            half topSmoothness =
                lerp(
                    _TopLayerSmoothnessMin,
                    _TopLayerSmoothnessMax,
                    SAMPLE_TEXTURE2D(
                        _TopLayerSmoothness,
                        sampler_TopLayerSmoothness,
                        tuv
                    ).r
                );

            smoothness =
                saturate(
                    lerp(
                        baseSmooth,
                        topSmoothness,
                        mask
                    )
                );

            occlusion =
                lerp(
                    baseAO,
                    1.0h,
                    mask
                );

        #endif
        }

        half4 Frag(Varyings IN) : SV_Target
        {
            ApplyVegetationLODCrossFade(IN.lodDistance, IN.positionCS.xy);
            half3 albedo;
            half3 normalTS;
            half3 emission;

            half metallic;
            half smoothness;
            half occlusion;

            EvaluateBark(
                IN,
                albedo,
                normalTS,
                emission,
                metallic,
                smoothness,
                occlusion
            );

            half3 bitangentWS =
                cross(
                    IN.normalWS,
                    IN.tangentWS.xyz
                )
                *
                IN.tangentWS.w;

            half3 normalWS =
                normalize(
                    IN.tangentWS.xyz * normalTS.x +
                    bitangentWS * normalTS.y +
                    IN.normalWS * normalTS.z
                );

            InputData inputData =
                (InputData)0;

            inputData.positionWS =
                IN.positionWS;

            inputData.normalWS =
                normalWS;

            inputData.viewDirectionWS =
                GetWorldSpaceNormalizeViewDir(
                    IN.positionWS
                );

            inputData.shadowCoord =
                TransformWorldToShadowCoord(
                    IN.positionWS
                );

            inputData.fogCoord =
                IN.fogFactor;

            inputData.vertexLighting =
                VertexLighting(
                    IN.positionWS,
                    normalWS
                );

            inputData.bakedGI =
                SAMPLE_GI(
                    IN.staticLightmapUV,
                    IN.vertexSH,
                    normalWS
                );

            inputData.normalizedScreenSpaceUV =
                GetNormalizedScreenSpaceUV(
                    IN.positionCS
                );

            inputData.shadowMask =
                SAMPLE_SHADOWMASK(
                    IN.staticLightmapUV
                );

            half4 color =
                UniversalFragmentPBR(
                    inputData,
                    albedo,
                    metallic,
                    0.0h,
                    smoothness,
                    occlusion,
                    emission,
                    1.0h
                );

            color.rgb += VegetationUnshadowedMainLightPBRDelta(
                inputData, albedo, metallic, half3(0.0h, 0.0h, 0.0h),
                smoothness, occlusion, 1.0h);

            color.rgb += EvaluateVegetationAdditionalLightsPBR(
                inputData,
                albedo,
                metallic,
                half3(0.0h, 0.0h, 0.0h),
                smoothness,
                occlusion,
                1.0h
            );

            color.rgb =
                MixFog(
                    color.rgb,
                    IN.fogFactor
                );

            return color;
        }

        ENDHLSL

        // ============================================================
        // Forward
        // ============================================================

        Pass
        {
            Name "ForwardLit"

            Tags
            {
                "LightMode"="UniversalForwardOnly"
            }

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

            #pragma shader_feature_local _ENABLEWIND_ON
            #pragma shader_feature_local _ENABLESTATICMESHSUPPORT_ON
            #pragma shader_feature_local _ENABLETOPLAYERBLEND_ON

            ENDHLSL
        }

        // ============================================================
        // Shadow Caster
        // ============================================================

        Pass
        {
            Name "ShadowCaster"

            Tags
            {
                "LightMode"="ShadowCaster"
            }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM

            #pragma target 4.5

            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #pragma shader_feature_local _ENABLEWIND_ON
            #pragma shader_feature_local _ENABLESTATICMESHSUPPORT_ON

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                float2 uv2 : TEXCOORD2;

                uint instanceID : SV_InstanceID;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation float lodDistance : TEXCOORD15;
            };

            ShadowVaryings ShadowVert(
                ShadowAttributes IN
            )
            {
                ShadowVaryings O =
                    (ShadowVaryings)0;

                float3 pivotWS =
                    GetVegetationPivotWS(
                        IN.instanceID
                    );
                O.lodDistance = GetVegetationLODDistance(pivotWS);

                float3 positionWS =
                    TransformVegetationPositionToWorld(
                        IN.positionOS.xyz,
                        IN.instanceID
                    );

                half3 normalWS =
                    TransformVegetationNormalToWorld(
                        IN.normalOS,
                        IN.instanceID
                    );

                positionWS = ApplyTreeShadowWindWS(positionWS,pivotWS,IN.uv2);

                float3 lightDirectionWS =
                    _LightDirection;

            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)

                lightDirectionWS =
                    normalize(
                        _LightPosition -
                        positionWS
                    );

            #endif

                positionWS =
                    ApplyShadowBias(
                        positionWS,
                        normalWS,
                        lightDirectionWS
                    );

                O.positionCS =
                    TransformWorldToHClip(
                        positionWS
                    );

            #if !defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)

                #if UNITY_REVERSED_Z

                    O.positionCS.z =
                        min(
                            O.positionCS.z,
                            O.positionCS.w *
                            UNITY_NEAR_CLIP_VALUE
                        );

                #else

                    O.positionCS.z =
                        max(
                            O.positionCS.z,
                            O.positionCS.w *
                            UNITY_NEAR_CLIP_VALUE
                        );

                #endif

            #endif

                return O;
            }

            half4 ShadowFrag(
                ShadowVaryings IN
            ) : SV_Target
            {
                ApplyVegetationLODCrossFade(IN.lodDistance, IN.positionCS.xy);
                return 0;
            }

            ENDHLSL
        }

        // ============================================================
        // Depth Only
        // ============================================================

        Pass
        {
            Name "DepthOnly"

            Tags
            {
                "LightMode"="DepthOnly"
            }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM

            #pragma target 4.5

            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #pragma shader_feature_local _ENABLEWIND_ON
            #pragma shader_feature_local _ENABLESTATICMESHSUPPORT_ON

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float2 uv2 : TEXCOORD2;

                uint instanceID : SV_InstanceID;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation float lodDistance : TEXCOORD15;
            };

            DepthVaryings DepthVert(
                DepthAttributes IN
            )
            {
                DepthVaryings O =
                    (DepthVaryings)0;

                float3 pivotWS =
                    GetVegetationPivotWS(
                        IN.instanceID
                    );
                O.lodDistance = GetVegetationLODDistance(pivotWS);

                float3 positionWS =
                    TransformVegetationPositionToWorld(
                        IN.positionOS.xyz,
                        IN.instanceID
                    );

                positionWS =
                    ApplyTreeWindWS(
                        positionWS,
                        pivotWS,
                        IN.uv2
                    );

                O.positionCS =
                    TransformWorldToHClip(
                        positionWS
                    );

                return O;
            }

            half4 DepthFrag(
                DepthVaryings IN
            ) : SV_Target
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
            #pragma shader_feature_local _ENABLEWIND_ON
            #pragma shader_feature_local _ENABLESTATICMESHSUPPORT_ON
            #pragma shader_feature_local _ENABLETOPLAYERBLEND_ON

            half4 DepthNormalsFrag(Varyings IN) : SV_Target
            {
                half3 albedo, normalTS, emission;
                half metallic, smoothness, occlusion;
                EvaluateBark(IN, albedo, normalTS, emission, metallic, smoothness, occlusion);
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
