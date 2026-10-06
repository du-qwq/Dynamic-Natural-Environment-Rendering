Shader "ANGRYMESH/Stylized Pack/Grass VegetationIndirect"
{
    Properties
    {
        [Header(Base)][Toggle(_ENABLESMOOTHNESSWAVES_ON)] _EnableSmoothnessWaves("Enable Smoothness Waves", Float) = 1
        _BaseOpacityCutoff("Base Opacity Cutoff", Range(0,1)) = 0.3
        [HDR]_BaseAlbedoColor("Base Albedo Color", Color) = (0.5019608,0.5019608,0.5019608,0)
        _BaseAlbedoBrightness("Base Albedo Brightness", Range(0,5)) = 1
        _BaseAlbedoDesaturation("Base Albedo Desaturation", Range(0,1)) = 0
        _BaseSmoothnessIntensity("Base Smoothness Intensity", Range(0,1)) = 0.5
        _BaseSmoothnessWaves("Base Smoothness Waves", Range(0,1)) = 0.5
        _BaseNormalIntensity("Base Normal Intensity", Range(0,5)) = 0
        [NoScaleOffset]_BaseAlbedo("Base Albedo", 2D) = "gray" {}
        [NoScaleOffset]_BaseNormal("Base Normal", 2D) = "bump" {}

        [Header(Bottom Color)][Toggle(_ENABLEBOTTOMCOLOR_ON)] _EnableBottomColor("Enable Bottom Color", Float) = 1
        [Toggle(_ENABLEBOTTOMDITHER_ON)] _EnableBottomDither("Enable Bottom Dither", Float) = 0
        [HDR]_BottomColor("Bottom Color", Color) = (0.5019608,0.5019608,0.5019608,0)
        _BottomColorOffset("Bottom Color Offset", Range(0,5)) = 1
        _BottomColorContrast("Bottom Color Contrast", Range(0,5)) = 1
        _BottomDitherOffset("Bottom Dither Offset", Range(-1,1)) = 0
        _BottomDitherContrast("Bottom Dither Contrast", Range(1,10)) = 3

        [Header(Tint Color)][Toggle(_ENABLETINTVARIATIONCOLOR_ON)] _EnableTintVariationColor("Enable Tint Variation Color", Float) = 1
        [HDR]_TintColor("Tint Color", Color) = (0.5019608,0.5019608,0.5019608,0)
        _TintNoiseUVScale("Tint Noise UV Scale", Range(0,50)) = 5
        _TintNoiseIntensity("Tint Noise Intensity", Range(0,1)) = 1
        _TintNoiseContrast("Tint Noise Contrast", Range(0,10)) = 5
        [IntRange]_TintNoiseInvertMask("Tint Noise Invert Mask", Range(0,1)) = 0

        [Header(Wind)][Toggle(_ENABLEWIND_ON)] _EnableWind("Enable Wind", Float) = 1
        _WindGrassAmplitude("Wind Grass Amplitude", Range(0,1)) = 1
        _WindGrassSpeed("Wind Grass Speed", Range(0,1)) = 1
        _WindGrassScale("Wind Grass Scale", Range(0,1)) = 1
        _WindGrassTurbulence("Wind Grass Turbulence", Range(0,1)) = 1
        _WindGrassFlexibility("Wind Grass Flexibility", Range(0,1)) = 1

        [HideInInspector]_texcoord("",2D)="white"{}
    }

    SubShader
    {
        Tags
        {
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest"
            "RenderPipeline"="UniversalPipeline"
            "UniversalMaterialType"="Lit"
        }

        LOD 300
        Cull Off
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

        // 原 Stylized Pack 全局 Tint Noise。
        TEXTURE2D(ASP_GlobalTintNoiseTexture);
        SAMPLER(sampler_ASP_GlobalTintNoiseTexture);

        // 原 Stylized Pack Grass Waves。
        TEXTURE2D(ASPW_WindGrassWavesNoiseTexture);
        SAMPLER(sampler_ASPW_WindGrassWavesNoiseTexture);

        CBUFFER_START(UnityPerMaterial)

        half4 _BaseAlbedoColor;
        half4 _BottomColor;
        half4 _TintColor;

        half _BaseOpacityCutoff;
        half _BaseAlbedoBrightness;
        half _BaseAlbedoDesaturation;
        half _BaseSmoothnessIntensity;
        half _BaseSmoothnessWaves;
        half _BaseNormalIntensity;

        half _BottomColorOffset;
        half _BottomColorContrast;
        half _BottomDitherOffset;
        half _BottomDitherContrast;

        half _TintNoiseUVScale;
        half _TintNoiseIntensity;
        half _TintNoiseContrast;
        half _TintNoiseInvertMask;

        half _WindGrassAmplitude;
        half _WindGrassSpeed;
        half _WindGrassScale;
        half _WindGrassTurbulence;
        half _WindGrassFlexibility;

        CBUFFER_END

        // ============================================================
        // 原 Stylized Pack Global Values
        //
        // 这些参数仍然参与草的表面视觉计算。
        // 不应该因为改成 Vegetation Indirect 就删除。
        // ============================================================

        half3 ASPW_WindDirection;

        half ASPW_WindGrassSpeed;
        half ASPW_WindGrassAmplitude;
        half ASPW_WindGrassFlexibility;

        half ASPW_WindGrassWavesAmplitude;
        half ASPW_WindGrassWavesSpeed;
        half ASPW_WindGrassWavesScale;

        half ASPW_WindGrassTurbulence;
        half ASPW_WindToggle;

        half ASP_GlobalTintNoiseUVScale;
        half ASP_GlobalTintNoiseContrast;
        half ASP_GlobalTintNoiseToggle;
        half ASP_GlobalTintNoiseIntensity;

        // ============================================================
        // Grass Interaction - Global GPU Data
        //
        // 由 VegetationInteractionController 每帧通过 Shader Global 上传。
        // xyz / xy 数据全部使用世界空间；最多 8 个 Interactor。
        // ============================================================

        #define VEGETATION_MAX_GRASS_INTERACTORS 8

        float _VegetationGrassInteractionEnabled;
        int _VegetationGrassInteractorCount;

        // xyz = Center WS, w = Radius
        float4 _VegetationGrassInteractors[VEGETATION_MAX_GRASS_INTERACTORS];

        // xy = Movement Direction XZ, z = Motion01, w = Strength
        float4 _VegetationGrassInteractorMotion[VEGETATION_MAX_GRASS_INTERACTORS];

        // x = Bend Strength (m), y = Flatten (m), z = Motion Influence, w = Falloff Power
        float4 _VegetationGrassInteractionParams;

        // x = Tip Power, y = Max XZ Offset (m), zw = reserved
        float4 _VegetationGrassInteractionParams2;

        half3 OverlayBlend(half3 src, half3 dst)
        {
            return saturate(
                lerp(
                    2.0h * dst * src,
                    1.0h - 2.0h * (1.0h - dst) * (1.0h - src),
                    step(0.5h, dst)
                )
            );
        }

        float3 TransformVegetationTangentToWorld(float3 tangentOS, uint instanceID)
        {
            return normalize(
                mul(
                    (float3x3)GetVegetationObjectToWorld(instanceID),
                    tangentOS
                )
            );
        }

        float GetVegetationTransformSign(uint instanceID)
        {
            float3x3 m = (float3x3)GetVegetationObjectToWorld(instanceID);
            return determinant(m) < 0.0 ? -1.0 : 1.0;
        }

        // ============================================================
        // Vegetation Wind
        //
        // 注意：
        // 草真正的顶点运动使用 Vegetation Wind。
        //
        // ASPW 的参数仍然保留给 Smoothness Waves 等原 Surface 效果。
        // ============================================================

        float3 ApplyGrassWindWS(
            float3 positionWS,
            float3 pivotWS,
            half4 color
        )
        {
        #ifndef _ENABLEWIND_ON

            return positionWS;

        #else

            float windMask =
                saturate(color.r);

            float strengthMultiplier =
                max(
                    (float)_WindGrassAmplitude *
                    (float)_WindGrassFlexibility,
                    0.0
                );

            float speedMultiplier =
                max(
                    (float)_WindGrassSpeed,
                    0.01
                );

            float frequencyMultiplier =
                max(
                    (float)_WindGrassScale,
                    0.01
                );

            float flutterMultiplier =
                max(
                    (float)_WindGrassTurbulence,
                    0.0
                );

            VegetationWindSample wind =
                EvaluateVegetationWind(
                    positionWS,
                    pivotWS,
                    windMask,
                    strengthMultiplier,
                    speedMultiplier,
                    frequencyMultiplier,
                    0.0,
                    0.0,
                    flutterMultiplier
                );

            return ApplyVegetationWindSample(
                positionWS,
                wind
            );

        #endif
        }

        float3 ApplyGrassForwardWindWS(float3 positionWS,float3 pivotWS,half4 color)
        {
        #ifndef _ENABLEWIND_ON
            return positionWS;
        #else
            float quality=GetVegetationForwardWindQuality(pivotWS);
            if(quality<0.5)return positionWS;
            if(quality>2.5)return ApplyGrassWindWS(positionWS,pivotWS,color);
            float windMask=saturate((float)color.r);
            float strengthMultiplier=max((float)_WindGrassAmplitude*(float)_WindGrassFlexibility,0.0);
            float speedMultiplier=max((float)_WindGrassSpeed,0.01);
            float frequencyMultiplier=max((float)_WindGrassScale,0.01);
            if(quality>1.5)return ApplyVegetationShadowWind(positionWS,pivotWS,windMask,strengthMultiplier,speedMultiplier,frequencyMultiplier);
            return ApplyVegetationShadowMainBend(positionWS,pivotWS,windMask,strengthMultiplier,speedMultiplier,frequencyMultiplier);
        #endif
        }

        float3 ApplyGrassShadowWindWS(float3 positionWS,float3 pivotWS,half4 color)
        {
        #ifndef _ENABLEWIND_ON
            return positionWS;
        #else
            float quality=GetVegetationShadowWindQuality(pivotWS);
            if(quality<0.5)return positionWS;
            if(quality>2.5)return ApplyGrassWindWS(positionWS,pivotWS,color);
            float windMask=saturate((float)color.r);
            float strengthMultiplier=max((float)_WindGrassAmplitude*(float)_WindGrassFlexibility,0.0);
            float speedMultiplier=max((float)_WindGrassSpeed,0.01);
            float frequencyMultiplier=max((float)_WindGrassScale,0.01);
            if(quality>1.5)return ApplyVegetationShadowWind(positionWS,pivotWS,windMask,strengthMultiplier,speedMultiplier,frequencyMultiplier);
            return ApplyVegetationShadowMainBend(positionWS,pivotWS,windMask,strengthMultiplier,speedMultiplier,frequencyMultiplier);
        #endif
        }

        // ============================================================
        // Grass Interaction
        //
        // deformedPositionWS : 已经过风场后的待修改顶点位置。
        // basePositionWS     : 未经过风场的原始世界空间顶点位置，用于稳定计算交互距离。
        // pivotWS            : 当前实例 Pivot，用来统一同一草簇的弯曲方向。
        // color.r            : 沿用当前 Grass Wind Mask，作为根部 -> 草尖交互 Mask。
        // ============================================================
        float3 ApplyGrassInteractionWS(
            float3 deformedPositionWS,
            float3 basePositionWS,
            float3 pivotWS,
            half4 color
        )
        {
            if (_VegetationGrassInteractionEnabled < 0.5)
                return deformedPositionWS;

            int interactorCount = min(
                max(_VegetationGrassInteractorCount, 0),
                VEGETATION_MAX_GRASS_INTERACTORS
            );

            if (interactorCount <= 0)
                return deformedPositionWS;

            float bendStrength = max(_VegetationGrassInteractionParams.x, 0.0);
            float flattenAmount = max(_VegetationGrassInteractionParams.y, 0.0);
            float motionInfluence = saturate(_VegetationGrassInteractionParams.z);
            float falloffPower = max(_VegetationGrassInteractionParams.w, 0.01);

            float tipPower = max(_VegetationGrassInteractionParams2.x, 0.01);
            float maxOffset = max(_VegetationGrassInteractionParams2.y, 0.001);

            float tipMask = pow(
                saturate((float)color.r),
                tipPower
            );

            if (tipMask <= 0.0001)
                return deformedPositionWS;

            float2 totalOffsetXZ = float2(0.0, 0.0);
            float totalFlatten = 0.0;

            [loop]
            for (int i = 0; i < VEGETATION_MAX_GRASS_INTERACTORS; i++)
            {
                if (i >= interactorCount)
                    break;

                float4 interactor = _VegetationGrassInteractors[i];
                float4 motion = _VegetationGrassInteractorMotion[i];

                float radius = max(interactor.w, 0.001);
                float strength = max(motion.w, 0.0);

                if (strength <= 0.0001)
                    continue;

                // 用未受风影响的顶点位置算距离，避免交互范围跟着风场抖动。
                float2 vertexDelta = basePositionWS.xz - interactor.xz;
                float distanceToInteractor = length(vertexDelta);

                if (distanceToInteractor >= radius)
                    continue;

                float influence = saturate(
                    1.0 - distanceToInteractor / radius
                );

                influence = influence * influence * (3.0 - 2.0 * influence);
                influence = pow(influence, falloffPower);

                if (influence <= 0.0001)
                    continue;

                // 方向使用 Instance Pivot，避免同一片草叶左右顶点向相反方向分裂。
                float2 radialDelta = pivotWS.xz - interactor.xz;
                float radialLength = length(radialDelta);

                float2 movementDirection = motion.xy;
                float movementLength = length(movementDirection);

                if (movementLength > 0.0001)
                    movementDirection /= movementLength;

                float2 radialDirection;

                if (radialLength > 0.0001)
                {
                    radialDirection = radialDelta / radialLength;
                }
                else if (movementLength > 0.0001)
                {
                    radialDirection = movementDirection;
                }
                else
                {
                    radialDirection = float2(1.0, 0.0);
                }

                if (movementLength <= 0.0001)
                    movementDirection = radialDirection;

                float motionBlend = saturate(
                    motion.z * motionInfluence
                );

                float2 blendedDirection = lerp(
                    radialDirection,
                    movementDirection,
                    motionBlend
                );

                float blendedLength = length(blendedDirection);
                float2 bendDirection = blendedLength > 0.0001
                    ? blendedDirection / blendedLength
                    : radialDirection;

                float amount =
                    influence *
                    tipMask *
                    strength *
                    bendStrength;

                totalOffsetXZ += bendDirection * amount;

                // 多个 Interactor 重叠时 Flatten 取最大值，避免重复向下累计。
                totalFlatten = max(
                    totalFlatten,
                    influence * tipMask * strength * flattenAmount
                );
            }

            float offsetLength = length(totalOffsetXZ);
            if (offsetLength > maxOffset)
            {
                totalOffsetXZ *= maxOffset / offsetLength;
            }

            deformedPositionWS.xz += totalOffsetXZ;
            deformedPositionWS.y -= totalFlatten;

            return deformedPositionWS;
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

            DECLARE_LIGHTMAP_OR_SH(
                staticLightmapUV,
                vertexSH,
                5
            );

            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings Vert(Attributes IN)
        {
            Varyings OUT =
                (Varyings)0;

            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

            float3 pivotWS =
                GetVegetationPivotWS(
                    IN.instanceID
                );
            OUT.lodDistance = GetVegetationLODDistance(pivotWS);

            float3 positionWS =
                TransformVegetationPositionToWorld(
                    IN.positionOS.xyz,
                    IN.instanceID
                );

            float3 basePositionWS = positionWS;

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
                ApplyGrassForwardWindWS(
                    positionWS,
                    pivotWS,
                    IN.color
                );

            positionWS =
                ApplyGrassInteractionWS(
                    positionWS,
                    basePositionWS,
                    pivotWS,
                    IN.color
                );

            OUT.positionWS =
                positionWS;

            OUT.positionCS =
                TransformWorldToHClip(
                    positionWS
                );

            OUT.normalWS =
                normalWS;

            OUT.tangentWS =
                half4(
                    tangentWS,
                    IN.tangentOS.w *
                    GetVegetationTransformSign(
                        IN.instanceID
                    )
                );

            OUT.uv =
                IN.uv;

            OUT.color =
                IN.color;

            OUT.fogFactor =
                ComputeFogFactor(
                    OUT.positionCS.z
                );

            OUTPUT_LIGHTMAP_UV(
                IN.staticLightmapUV,
                unity_LightmapST,
                OUT.staticLightmapUV
            );

            OUTPUT_SH(
                OUT.normalWS,
                OUT.vertexSH
            );

            return OUT;
        }

        half Dither4x4(float2 pixel)
        {
            int x =
                (int)fmod(
                    pixel.x,
                    4.0
                );

            int y =
                (int)fmod(
                    pixel.y,
                    4.0
                );

            const half d[16] =
            {
                1, 9, 3, 11,
                13, 5, 15, 7,
                4, 12, 2, 10,
                16, 8, 14, 6
            };

            return
                d[y * 4 + x] /
                16.0h;
        }

        void EvaluateGrass(
            Varyings IN,
            out half3 albedo,
            out half3 normalTS,
            out half smoothness,
            out half alpha
        )
        {
            // ========================================================
            // Base
            // ========================================================

            half4 baseSample =
                SAMPLE_TEXTURE2D(
                    _BaseAlbedo,
                    sampler_BaseAlbedo,
                    IN.uv
                );

            half3 tex =
                baseSample.rgb;

            half lum =
                dot(
                    tex,
                    half3(
                        0.299h,
                        0.587h,
                        0.114h
                    )
                );

            tex =
                saturate(
                    lerp(
                        tex,
                        lum.xxx,
                        _BaseAlbedoDesaturation
                    )
                    *
                    _BaseAlbedoBrightness
                );

            half3 base =
                OverlayBlend(
                    tex,
                    _BaseAlbedoColor.rgb
                );

            albedo =
                base;

            // ========================================================
            // Tint Variation
            //
            // 完全恢复原 Stylized Pack 算法。
            // ========================================================

        #ifdef _ENABLETINTVARIATIONCOLOR_ON

            float2 noiseUV =
                IN.positionWS.xz
                *
                (
                    0.01h
                    *
                    ASP_GlobalTintNoiseUVScale
                    *
                    _TintNoiseUVScale
                );

            half noise =
                SAMPLE_TEXTURE2D(
                    ASP_GlobalTintNoiseTexture,
                    sampler_ASP_GlobalTintNoiseTexture,
                    noiseUV
                ).r;

            noise =
                lerp(
                    noise,
                    1.0h - noise,
                    _TintNoiseInvertMask
                );

            half tintMask =
                saturate(
                    noise
                    *
                    (
                        ASP_GlobalTintNoiseContrast
                        *
                        _TintNoiseContrast
                    )
                    *
                    IN.color.r
                );

            half3 tinted =
                lerp(
                    base,
                    OverlayBlend(
                        tex,
                        _TintColor.rgb
                    ),
                    tintMask
                );

            albedo =
                lerp(
                    base,
                    tinted,
                    ASP_GlobalTintNoiseToggle
                    *
                    _TintNoiseIntensity
                    *
                    ASP_GlobalTintNoiseIntensity
                );

        #endif

            // ========================================================
            // Bottom Color
            // ========================================================

        #ifdef _ENABLEBOTTOMCOLOR_ON

            half bottomMask =
                saturate(
                    _BottomColorOffset
                    *
                    pow(
                        abs(
                            1.0h -
                            IN.color.a
                        ),
                        _BottomColorContrast
                    )
                );

            albedo =
                lerp(
                    albedo,
                    OverlayBlend(
                        tex,
                        _BottomColor.rgb
                    ),
                    bottomMask
                );

        #endif

            // ========================================================
            // Normal
            // ========================================================

            normalTS =
                UnpackNormalScale(
                    SAMPLE_TEXTURE2D(
                        _BaseNormal,
                        sampler_BaseNormal,
                        IN.uv
                    ),
                    _BaseNormalIntensity
                );

            // ========================================================
            // Smoothness
            // ========================================================

            smoothness =
                _BaseSmoothnessIntensity;

            // --------------------------------------------------------
            // Smoothness Waves
            //
            // 这里恢复原 Grass Waves Texture，
            // 而不是使用 Vegetation Wind 生成出来的 windWave。
            // --------------------------------------------------------

        #ifdef _ENABLESMOOTHNESSWAVES_ON

            half3 directionWS =
                normalize(
                    ASPW_WindDirection
                    +
                    half3(
                        1e-4h,
                        0.0h,
                        0.0h
                    )
                );

            float2 waveUV =
                IN.positionWS.xz
                *
                (
                    ASPW_WindGrassWavesScale
                    *
                    0.1h
                )
                +
                (
                    -directionWS.xz
                )
                *
                _Time.y
                *
                (
                    ASPW_WindGrassWavesSpeed
                    *
                    0.1h
                );

            half wave =
                SAMPLE_TEXTURE2D(
                    ASPW_WindGrassWavesNoiseTexture,
                    sampler_ASPW_WindGrassWavesNoiseTexture,
                    waveUV
                ).r;

            smoothness =
                saturate(
                    lerp(
                        0.0h,
                        _BaseSmoothnessIntensity,
                        IN.color.r
                        *
                        (
                            wave
                            +
                            _BaseSmoothnessWaves
                        )
                    )
                );

        #endif

            // ========================================================
            // Alpha
            // ========================================================

            alpha =
                baseSample.a;

            // ========================================================
            // Bottom Dither
            // ========================================================

        #ifdef _ENABLEBOTTOMDITHER_ON

            half threshold =
                saturate(
                    (
                        IN.color.r -
                        _BottomDitherOffset
                    )
                    *
                    (
                        _BottomDitherContrast
                        *
                        2.0h
                    )
                );

            alpha *=
                step(
                    Dither4x4(
                        IN.positionCS.xy
                    ),
                    threshold *
                    1.00001h
                );

        #endif
        }

        half4 Frag(Varyings IN) : SV_Target
        {
            half3 albedo;
            half3 normalTS;

            half smoothness;
            half alpha;

            EvaluateGrass(
                IN,
                albedo,
                normalTS,
                smoothness,
                alpha
            );

            clip(
                alpha -
                _BaseOpacityCutoff
            );
            ApplyVegetationLODCrossFade(IN.lodDistance, IN.positionCS.xy);

            half3 bitangentWS =
                cross(
                    IN.normalWS,
                    IN.tangentWS.xyz
                )
                *
                IN.tangentWS.w;

            half3 normalWS =
                normalize(
                    IN.tangentWS.xyz * normalTS.x
                    +
                    bitangentWS * normalTS.y
                    +
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
                    0.0h,
                    0.0h,
                    smoothness,
                    1.0h,
                    0.0h,
                    alpha
                );

            color.rgb += VegetationUnshadowedMainLightPBRDelta(
                inputData, albedo, 0.0h, half3(0.0h, 0.0h, 0.0h),
                smoothness, 1.0h, alpha);

            color.rgb += EvaluateVegetationAdditionalLightsPBR(
                inputData,
                albedo,
                0.0h,
                half3(0.0h, 0.0h, 0.0h),
                smoothness,
                1.0h,
                alpha
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
            #pragma shader_feature_local _ENABLEBOTTOMCOLOR_ON
            #pragma shader_feature_local _ENABLETINTVARIATIONCOLOR_ON
            #pragma shader_feature_local _ENABLESMOOTHNESSWAVES_ON
            #pragma shader_feature_local _ENABLEBOTTOMDITHER_ON

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

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;

                half3 normalOS : NORMAL;

                float2 uv : TEXCOORD0;

                half4 color : COLOR;

                uint instanceID : SV_InstanceID;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation float lodDistance : TEXCOORD15;

                float2 uv : TEXCOORD0;
                nointerpolation float shadowCutoff : TEXCOORD1;
            };

            ShadowVaryings ShadowVert(
                ShadowAttributes IN
            )
            {
                ShadowVaryings OUT =
                    (ShadowVaryings)0;

                float3 pivotWS =
                    GetVegetationPivotWS(
                        IN.instanceID
                    );
                OUT.lodDistance = GetVegetationLODDistance(pivotWS);

                float3 positionWS =
                    TransformVegetationPositionToWorld(
                        IN.positionOS.xyz,
                        IN.instanceID
                    );

                float3 basePositionWS = positionWS;

                half3 normalWS =
                    TransformVegetationNormalToWorld(
                        IN.normalOS,
                        IN.instanceID
                    );

                positionWS = ApplyGrassShadowWindWS(positionWS,pivotWS,IN.color);
                positionWS = ApplyGrassInteractionWS(positionWS,basePositionWS,pivotWS,IN.color);

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

                OUT.positionCS =
                    TransformWorldToHClip(
                        positionWS
                    );

            #if !defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)

                #if UNITY_REVERSED_Z

                    OUT.positionCS.z =
                        min(
                            OUT.positionCS.z,
                            OUT.positionCS.w *
                            UNITY_NEAR_CLIP_VALUE
                        );

                #else

                    OUT.positionCS.z =
                        max(
                            OUT.positionCS.z,
                            OUT.positionCS.w *
                            UNITY_NEAR_CLIP_VALUE
                        );

                #endif

            #endif

                OUT.uv =
                    IN.uv;
                OUT.shadowCutoff = GetVegetationShadowAlphaCutoff(_BaseOpacityCutoff,pivotWS);

                return OUT;
            }

            half4 ShadowFrag(
                ShadowVaryings IN
            ) : SV_Target
            {
                clip(
                    SAMPLE_TEXTURE2D(
                        _BaseAlbedo,
                        sampler_BaseAlbedo,
                        IN.uv
                    ).a
                    -
                    IN.shadowCutoff
                );
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

            struct DepthAttributes
            {
                float4 positionOS : POSITION;

                float2 uv : TEXCOORD0;

                half4 color : COLOR;

                uint instanceID : SV_InstanceID;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation float lodDistance : TEXCOORD15;

                float2 uv : TEXCOORD0;
            };

            DepthVaryings DepthVert(
                DepthAttributes IN
            )
            {
                DepthVaryings OUT =
                    (DepthVaryings)0;

                float3 pivotWS =
                    GetVegetationPivotWS(
                        IN.instanceID
                    );
                OUT.lodDistance = GetVegetationLODDistance(pivotWS);

                float3 positionWS =
                    TransformVegetationPositionToWorld(
                        IN.positionOS.xyz,
                        IN.instanceID
                    );

                float3 basePositionWS = positionWS;

                positionWS =
                    ApplyGrassWindWS(
                        positionWS,
                        pivotWS,
                        IN.color
                    );

                positionWS =
                    ApplyGrassInteractionWS(
                        positionWS,
                        basePositionWS,
                        pivotWS,
                        IN.color
                    );

                OUT.positionCS =
                    TransformWorldToHClip(
                        positionWS
                    );

                OUT.uv =
                    IN.uv;

                return OUT;
            }

            half4 DepthFrag(
                DepthVaryings IN
            ) : SV_Target
            {
                clip(
                    SAMPLE_TEXTURE2D(
                        _BaseAlbedo,
                        sampler_BaseAlbedo,
                        IN.uv
                    ).a
                    -
                    _BaseOpacityCutoff
                );
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
            #pragma shader_feature_local _ENABLEBOTTOMCOLOR_ON
            #pragma shader_feature_local _ENABLETINTVARIATIONCOLOR_ON
            #pragma shader_feature_local _ENABLESMOOTHNESSWAVES_ON
            #pragma shader_feature_local _ENABLEBOTTOMDITHER_ON

            half4 DepthNormalsFrag(Varyings IN) : SV_Target
            {
                half3 albedo, normalTS;
                half smoothness, alpha;
                EvaluateGrass(IN, albedo, normalTS, smoothness, alpha);
                clip(alpha - _BaseOpacityCutoff);
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
