Shader "DynamicNaturalEnvironment/StylizedGrassIndirect"
{
    Properties
    {
        [Header(Grass Color)]
        _BottomColor("底部颜色", Color) = (0.10,0.16,0.04,1)
        _TopColor("顶部颜色", Color) = (0.42,0.52,0.12,1)
        _ColorVariation("颜色随机", Range(0,0.3)) = 0.05
        _RootAO("根部明度", Range(0,1)) = 0.72

        [Header(Terrain Blend)]
        [NoScaleOffset]_TerrainBaseTex("Terrain 草地贴图", 2D) = "white" {}
        _TerrainTint("Terrain 融合颜色", Color) = (1,1,1,1)
        _TerrainFallbackColor("Terrain 备用颜色", Color) = (0.20,0.27,0.08,1)
        _RootTerrainBlend("根部 Terrain 融合", Range(0,1)) = 0.5
        _FarTerrainBlend("远处 Terrain 融合", Range(0,1)) = 0.68
        _TerrainSampleMip("Terrain 采样模糊", Range(0,6)) = 2

        [Header(Instance)]
        _ScaleMin("最小缩放", Range(0.1,2)) = 0.65
        _ScaleMax("最大缩放", Range(0.1,2)) = 0.95

        [Header(Blade Shape)]
        _BladeHeight("叶片高度倍率", Range(0.1,2)) = 1
        _BladeWidth("叶片宽度倍率", Range(0.2,2)) = 1

        [Header(Translucency)]
        _TranslucencyColor("透光颜色", Color) = (1.0,0.92,0.55,1)
        _TranslucencyStrength("透光强度", Range(0,2)) = 0.35
        _BackLightPower("背光集中度", Range(0.5,8)) = 2.2
        _TipTranslucency("草尖透光权重", Range(0,1)) = 0.75

        [Header(Visual LOD)]
        _VisualLODStart("视觉 LOD 开始距离", Float) = 30
        _VisualLODEnd("视觉 LOD 完成距离", Float) = 100
        _FarScale("远处尺寸倍率", Range(0.5,1)) = 0.84
        _FarWindMultiplier("远处风力倍率", Range(0,1)) = 0.5

        [Header(Lighting)]
        _NormalUp("近处法线向上修正", Range(0,1)) = 0.85
        _FarNormalUp("远处法线向上修正", Range(0,1)) = 0.97
        _AmbientStrength("环境光", Range(0,2)) = 0.55
        _ShadowFloor("阴影最低亮度", Range(0,1)) = 0.38

        [Header(Wind Direction)]
        _WindDirection("风方向 XZ", Vector) = (1,0.25,0,0)

        [Header(Base Wind)]
        _BaseWindStrength("基础风强度", Range(0,0.2)) = 0.018
        _BaseWindSpeed("基础风速度", Range(0,5)) = 0.8

        [Header(Gust)]
        _GustStrength("阵风强度", Range(0,0.3)) = 0.07
        _GustScale("阵风尺度", Range(0.001,0.2)) = 0.035
        _GustSpeed("阵风推进速度", Range(0,10)) = 1.5
        _GustSharpness("阵风集中程度", Range(0.1,8)) = 2.2
        _GustWarp("阵风弯曲", Range(0,5)) = 1.2

        [Header(Micro Wind)]
        _MicroWindStrength("微风强度", Range(0,0.1)) = 0.012
        _MicroWindScale("微风尺度", Range(0.01,2)) = 0.22
        _MicroWindSpeed("微风速度", Range(0,10)) = 2.2

        [Header(Bending)]
        _WindBendPower("草尖风力权重", Range(0.5,5)) = 2.0
        _CrossWindStrength("横向扰动", Range(0,1)) = 0.18
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_TerrainBaseTex);
            SAMPLER(sampler_TerrainBaseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BottomColor;
                float4 _TopColor;
                float4 _TerrainTint;
                float4 _TerrainFallbackColor;
                float4 _WindDirection;
                float4 _TerrainPosition;
                float4 _TerrainLayerST;

                float _ColorVariation;
                float _RootAO;

                float _RootTerrainBlend;
                float _FarTerrainBlend;
                float _TerrainSampleMip;
                float _UseTerrainBaseTex;

                float _ScaleMin;
                float _ScaleMax;
                float _BladeHeight;
                float _BladeWidth;

                float _VisualLODStart;
                float _VisualLODEnd;
                float _FarScale;
                float _FarWindMultiplier;

                float _NormalUp;
                float _FarNormalUp;
                float _AmbientStrength;
                float _ShadowFloor;
                float4 _TranslucencyColor;
                float _TranslucencyStrength;
                float _BackLightPower;
                float _TipTranslucency;

                float _BaseWindStrength;
                float _BaseWindSpeed;

                float _GustStrength;
                float _GustScale;
                float _GustSpeed;
                float _GustSharpness;
                float _GustWarp;

                float _MicroWindStrength;
                float _MicroWindScale;
                float _MicroWindSpeed;

                float _WindBendPower;
                float _CrossWindStrength;

                float2 _CenterPos;
                float _DrawDistance;
                float _TextureUpdateThreshold;
                int _GrassLODLevel;
                int _GrassLayer;
            CBUFFER_END

            StructuredBuffer<float3> _GrassPositionsBaseNear;
            StructuredBuffer<float3> _GrassPositionsBaseFar;
            StructuredBuffer<float3> _GrassPositionsAccentNear;
            StructuredBuffer<float3> _GrassPositionsAccentFar;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 color : COLOR0;
                float fogFactor : TEXCOORD0;
            };

            float Random12(float2 p)
            {
                return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453123);
            }

            float3 GetGrassPivot(uint instanceID)
            {
                if (_GrassLayer == 0) return _GrassLODLevel == 0 ? _GrassPositionsBaseNear[instanceID] : _GrassPositionsBaseFar[instanceID];
                return _GrassLODLevel == 0 ? _GrassPositionsAccentNear[instanceID] : _GrassPositionsAccentFar[instanceID];
            }

            void RotateY(inout float3 v,float angle)
            {
                float s,c;
                sincos(angle,s,c);
                float x = v.x * c - v.z * s;
                float z = v.x * s + v.z * c;
                v.x = x;
                v.z = z;
            }

            float3 SampleTerrainColor(float3 pivot)
            {
                float3 terrainColor = _TerrainFallbackColor.rgb;

                if (_UseTerrainBaseTex > 0.5)
                {
                    float2 terrainUV = (pivot.xz - _TerrainPosition.xz) * _TerrainLayerST.xy + _TerrainLayerST.zw;
                    terrainColor = SAMPLE_TEXTURE2D_LOD(_TerrainBaseTex,sampler_TerrainBaseTex,terrainUV,_TerrainSampleMip).rgb;
                }

                return terrainColor * _TerrainTint.rgb;
            }

            float2 GetWindOffset(float2 positionXZ,float instanceRandom,float heightWeight,float visualLOD)
            {
                float2 windDir = _WindDirection.xy;
                float windLength = length(windDir);
                windDir = windLength > 0.0001 ? windDir / windLength : float2(1,0);

                float2 crossDir = float2(-windDir.y,windDir.x);
                float alongWind = dot(positionXZ,windDir);
                float acrossWind = dot(positionXZ,crossDir);

                float baseWave = sin(alongWind * 0.12 + _Time.y * _BaseWindSpeed + instanceRandom * 2.0);
                float baseAmount = _BaseWindStrength * (0.75 + baseWave * 0.25);

                float warpedCoordinate = alongWind * _GustScale - _Time.y * _GustSpeed;
                warpedCoordinate += sin(acrossWind * _GustScale * 0.65 + _Time.y * 0.18) * _GustWarp;

                float gustWave = sin(warpedCoordinate) * 0.5 + 0.5;
                float gustAmount = pow(saturate(gustWave),_GustSharpness) * _GustStrength;

                float microPhase = dot(positionXZ,float2(0.73,1.17)) * _MicroWindScale + _Time.y * _MicroWindSpeed + instanceRandom * 6.283185;
                float microWave = sin(microPhase);
                float microAmount = microWave * _MicroWindStrength;

                float crossWave = sin(acrossWind * 0.11 + alongWind * 0.04 + _Time.y * 0.7 + instanceRandom * 3.0);
                float crossAmount = (gustAmount + abs(microAmount)) * crossWave * _CrossWindStrength;

                float distanceWind = lerp(1.0,_FarWindMultiplier,visualLOD);
                float bendWeight = pow(saturate(heightWeight),_WindBendPower);

                float2 offset = windDir * (baseAmount + gustAmount + microAmount);
                offset += crossDir * crossAmount;

                return offset * bendWeight * distanceWind;
            }

            Varyings vert(Attributes IN,uint instanceID : SV_InstanceID)
            {
                Varyings OUT;

                float3 pivot = GetGrassPivot(instanceID);
                float distanceFromCamera = distance(_WorldSpaceCameraPos,pivot);
                float visualLOD = smoothstep(_VisualLODStart,max(_VisualLODStart + 0.01,_VisualLODEnd),distanceFromCamera);

                float instanceRandom = Random12(pivot.xz);
                float rotationRandom = Random12(pivot.zx + 37.17);
                float scaleRandom = Random12(pivot.xz + 91.73);

                float angle = rotationRandom * 6.28318530718;
                float scale = lerp(_ScaleMin,_ScaleMax,scaleRandom);
                scale *= lerp(1.0,_FarScale,visualLOD);

                float3 localPosition = IN.positionOS.xyz;
                localPosition.x *= scale * _BladeWidth;
                localPosition.z *= scale * _BladeWidth;
                localPosition.y *= scale * _BladeHeight;
                float3 localNormal = IN.normalOS;

                RotateY(localPosition,angle);
                RotateY(localNormal,angle);

                float heightWeight = saturate(IN.uv.y);

                float3 positionWS = pivot + localPosition;
                positionWS.xz += GetWindOffset(pivot.xz,instanceRandom,heightWeight,visualLOD);

                float normalUp = lerp(_NormalUp,_FarNormalUp,visualLOD);
                float3 normalWS = normalize(lerp(normalize(localNormal),float3(0,1,0),normalUp));

                float3 grassColor = lerp(_BottomColor.rgb,_TopColor.rgb,heightWeight);

                float bladeRandom = saturate(IN.color.r);
                float colorRandom = lerp(instanceRandom,bladeRandom,0.35);
                float variationStrength = _ColorVariation * lerp(1.0,0.25,visualLOD);

                grassColor *= 1.0 + (colorRandom * 2.0 - 1.0) * variationStrength;
                grassColor *= lerp(_RootAO,1.0,heightWeight);

                float3 terrainColor = SampleTerrainColor(pivot);

                float rootBlend = (1.0 - heightWeight) * _RootTerrainBlend;
                float distanceBlend = visualLOD * _FarTerrainBlend;
                float terrainBlend = saturate(max(rootBlend,distanceBlend));

                float3 baseColor = lerp(grassColor,terrainColor,terrainBlend);

                float3 viewDirWS = normalize(_WorldSpaceCameraPos - positionWS);

                float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half halfLambert = saturate(dot(normalWS,mainLight.direction) * 0.5 + 0.5);
                half shadow = lerp(_ShadowFloor,1.0,mainLight.shadowAttenuation);
                half3 ambient = max(SampleSH(normalWS),0) * _AmbientStrength;
                half3 direct = mainLight.color * lerp(0.55,1.0,halfLambert) * mainLight.distanceAttenuation * shadow;

                half backScatter = pow(saturate(dot(viewDirWS,-mainLight.direction)),_BackLightPower);
                half backLightMask = saturate(1.0 - halfLambert);
                half tipMask = lerp(1.0,heightWeight,_TipTranslucency);
                half translucency = backScatter * backLightMask * tipMask * _TranslucencyStrength * shadow;
                half3 transmission = baseColor * _TranslucencyColor.rgb * mainLight.color * translucency;

                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.color = baseColor * (ambient + direct) + transmission;
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return half4(MixFog(IN.color,IN.fogFactor),1);
            }

            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            CBUFFER_START(UnityPerMaterial)
                float4 _BottomColor;
                float4 _TopColor;
                float4 _TerrainTint;
                float4 _TerrainFallbackColor;
                float4 _WindDirection;
                float4 _TerrainPosition;
                float4 _TerrainLayerST;

                float _ColorVariation;
                float _RootAO;

                float _RootTerrainBlend;
                float _FarTerrainBlend;
                float _TerrainSampleMip;
                float _UseTerrainBaseTex;

                float _ScaleMin;
                float _ScaleMax;
                float _BladeHeight;
                float _BladeWidth;

                float _VisualLODStart;
                float _VisualLODEnd;
                float _FarScale;
                float _FarWindMultiplier;

                float _NormalUp;
                float _FarNormalUp;
                float _AmbientStrength;
                float _ShadowFloor;
                float4 _TranslucencyColor;
                float _TranslucencyStrength;
                float _BackLightPower;
                float _TipTranslucency;

                float _BaseWindStrength;
                float _BaseWindSpeed;

                float _GustStrength;
                float _GustScale;
                float _GustSpeed;
                float _GustSharpness;
                float _GustWarp;

                float _MicroWindStrength;
                float _MicroWindScale;
                float _MicroWindSpeed;

                float _WindBendPower;
                float _CrossWindStrength;

                float2 _CenterPos;
                float _DrawDistance;
                float _TextureUpdateThreshold;
                int _GrassLODLevel;
                int _GrassLayer;
            CBUFFER_END

            StructuredBuffer<float3> _GrassPositionsBaseNear;
            StructuredBuffer<float3> _GrassPositionsBaseFar;
            StructuredBuffer<float3> _GrassPositionsAccentNear;
            StructuredBuffer<float3> _GrassPositionsAccentFar;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            float ShadowRandom12(float2 p)
            {
                return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453123);
            }

            float3 ShadowGetGrassPivot(uint instanceID)
            {
                if (_GrassLayer == 0) return _GrassLODLevel == 0 ? _GrassPositionsBaseNear[instanceID] : _GrassPositionsBaseFar[instanceID];
                return _GrassLODLevel == 0 ? _GrassPositionsAccentNear[instanceID] : _GrassPositionsAccentFar[instanceID];
            }

            void ShadowRotateY(inout float3 v,float angle)
            {
                float s,c;
                sincos(angle,s,c);
                float x = v.x * c - v.z * s;
                float z = v.x * s + v.z * c;
                v.x = x;
                v.z = z;
            }

            float2 ShadowGetWindOffset(float2 positionXZ,float instanceRandom,float heightWeight)
            {
                float2 windDir = _WindDirection.xy;
                float windLength = length(windDir);
                windDir = windLength > 0.0001 ? windDir / windLength : float2(1,0);

                float2 crossDir = float2(-windDir.y,windDir.x);
                float alongWind = dot(positionXZ,windDir);
                float acrossWind = dot(positionXZ,crossDir);

                float baseWave = sin(alongWind * 0.12 + _Time.y * _BaseWindSpeed + instanceRandom * 2.0);
                float baseAmount = _BaseWindStrength * (0.75 + baseWave * 0.25);

                float warpedCoordinate = alongWind * _GustScale - _Time.y * _GustSpeed;
                warpedCoordinate += sin(acrossWind * _GustScale * 0.65 + _Time.y * 0.18) * _GustWarp;

                float gustWave = sin(warpedCoordinate) * 0.5 + 0.5;
                float gustAmount = pow(saturate(gustWave),_GustSharpness) * _GustStrength;

                float microPhase = dot(positionXZ,float2(0.73,1.17)) * _MicroWindScale + _Time.y * _MicroWindSpeed + instanceRandom * 6.283185;
                float microAmount = sin(microPhase) * _MicroWindStrength;

                float crossWave = sin(acrossWind * 0.11 + alongWind * 0.04 + _Time.y * 0.7 + instanceRandom * 3.0);
                float crossAmount = (gustAmount + abs(microAmount)) * crossWave * _CrossWindStrength;

                float bendWeight = pow(saturate(heightWeight),_WindBendPower);

                float2 offset = windDir * (baseAmount + gustAmount + microAmount);
                offset += crossDir * crossAmount;

                return offset * bendWeight;
            }

            ShadowVaryings ShadowVert(ShadowAttributes IN,uint instanceID : SV_InstanceID)
            {
                ShadowVaryings OUT;

                float3 pivot = ShadowGetGrassPivot(instanceID);

                float instanceRandom = ShadowRandom12(pivot.xz);
                float rotationRandom = ShadowRandom12(pivot.zx + 37.17);
                float scaleRandom = ShadowRandom12(pivot.xz + 91.73);

                float angle = rotationRandom * 6.28318530718;
                float scale = lerp(_ScaleMin,_ScaleMax,scaleRandom);

                if (_GrassLODLevel == 1) scale *= _FarScale;

                float3 localPosition = IN.positionOS.xyz;
                localPosition.x *= scale * _BladeWidth;
                localPosition.z *= scale * _BladeWidth;
                localPosition.y *= scale * _BladeHeight;
                float3 localNormal = IN.normalOS;

                ShadowRotateY(localPosition,angle);
                ShadowRotateY(localNormal,angle);

                float heightWeight = saturate(IN.uv.y);

                float3 positionWS = pivot + localPosition;
                positionWS.xz += ShadowGetWindOffset(pivot.xz,instanceRandom,heightWeight);

                float3 normalWS = normalize(localNormal);
                float3 lightDirectionWS = _LightDirection;

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    lightDirectionWS = normalize(_LightPosition - positionWS);
                #endif

                positionWS = ApplyShadowBias(positionWS,normalWS,lightDirectionWS);

                float4 positionCS = TransformWorldToHClip(positionWS);

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #endif

                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 ShadowFrag(ShadowVaryings IN) : SV_Target
            {
                return 0;
            }

            ENDHLSL
        }
    }
}