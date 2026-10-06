Shader "Vegetation/Grass"
{
    Properties
    {
        [Header(Base)]
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (0.32,0.50,0.22,1)
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.42

        [Header(Shape)]
        _BladeHeightScale("Blade Height Scale", Range(0.5,3)) = 1
        _BladeBaseY("Blade Base Y", Float) = 0
        _HeightVariation("Height Variation", Range(0,0.6)) = 0.11

        [Header(Static Bend)]
        _BendStrength("Bend Strength", Range(0,2)) = 0.025
        _BendRandomness("Bend Randomness", Range(0,1)) = 0.40
        _BendPower("Bend Power", Range(0.5,8)) = 1.6
        _LeanStrength("Lean Strength", Range(0,1)) = 0.015

        [Header(Color Variation)]
        _VariationColor("Variation Color", Color) = (0.27,0.43,0.18,1)
        _VariationStrength("Instance Variation", Range(0,1)) = 0.06
        _ColorPatchStrength("Large Patch Strength", Range(0,1)) = 0.15
        _ColorPatchScale("Large Patch Scale", Range(0.005,0.2)) = 0.025
        _PatchDarkColor("Patch Dark Tint", Color) = (0.84,0.88,0.78,1)
        _PatchBrightColor("Patch Bright Tint", Color) = (1.04,1.06,0.96,1)

        [Header(Root Tip Gradient)]
        _RootColor("Root Tint", Color) = (0.20,0.31,0.12,1)
        _TipColor("Tip Tint", Color) = (0.50,0.68,0.34,1)
        _GradientStrength("Gradient Strength", Range(0,1)) = 0.18
        _GradientPower("Gradient Power", Range(0.1,8)) = 1.9

        [Header(Terrain Blend)]
        _TerrainColorBlend("Terrain Color Blend", Range(0,1)) = 0.02
        _TerrainBlendPower("Terrain Root Falloff", Range(0.1,8)) = 4.5

        [Header(Distance Visual)]
        _DistanceBlendStart("Distance Blend Start", Float) = 30
        _DistanceBlendEnd("Distance Blend End", Float) = 105
        _DistanceTerrainBlend("Distance Terrain Blend", Range(0,1)) = 0.65
        _DistanceVariationReduce("Distance Variation Reduce", Range(0,1)) = 0.75
        _DistanceGradientReduce("Distance Gradient Reduce", Range(0,1)) = 0.55
        _DistanceLightingReduce("Distance Lighting Detail Reduce", Range(0,1)) = 0.55
        _DistanceNormalUpBoost("Distance Normal Up Boost", Range(0,1)) = 0.10

        [Header(Base Lighting)]
        _AmbientStrength("Ambient Strength", Range(0,2)) = 0.46
        _LightWrap("Light Wrap", Range(0,1)) = 0.48
        _NormalUpBlend("Normal Up Blend", Range(0,1)) = 0.34
        _MinimumLight("Minimum Direct Light", Range(0,1)) = 0.14
        _ShadowStrength("Receive Shadow Strength", Range(0,1)) = 0.55

        [Header(Two Sided Lighting)]
        _BackfaceLightStrength("Backface Light Strength", Range(0,1)) = 0.92
        _TwoSidedUpStrength("Two Sided Up Strength", Range(0,1)) = 0.30

        [Header(Volume Lighting)]
        _SkyAmbientBlend("Sky Ambient Blend", Range(0,1)) = 0.42
        [HDR]_TopLightColor("Top Light Color", Color) = (0.78,0.86,0.66,1)
        _TopLightStrength("Top Light Strength", Range(0,1)) = 0.07
        _TopLightPower("Top Light Power", Range(0.1,8)) = 1.9
        _BottomAmbientReduce("Bottom Ambient Reduce", Range(0,1)) = 0.30
        _BottomDarkenStrength("Bottom Darken Strength", Range(0,0.5)) = 0.20
        _BottomDarkenPower("Bottom Darken Power", Range(0.1,8)) = 2.4

        [Header(Transmission)]
        [HDR]_TransmissionColor("Transmission Color", Color) = (0.78,1.00,0.48,1)
        _TransmissionStrength("Transmission Strength", Range(0,2)) = 0.38
        _TransmissionPower("Transmission Power", Range(0.5,16)) = 3.0
        _TransmissionTipStrength("Transmission Tip Strength", Range(0,1)) = 0.78
        _TransmissionShadowReduce("Transmission Shadow Reduce", Range(0,1)) = 0.35

        [Header(Directional Wind Highlight)]
        [HDR]_WindHighlightColor("Wind Highlight Color", Color) = (0.90,1.00,0.66,1)
        _WindHighlightStrength("Wind Highlight Strength", Range(0,3)) = 0.65
        _WindHighlightPower("Wind Highlight Power", Range(0.5,16)) = 4.0
        _WindHighlightBendMin("Wind Highlight Bend Min", Range(0,1)) = 0.15
        _WindHighlightBendMax("Wind Highlight Bend Max", Range(0,1)) = 0.75
        _WindHighlightTipPower("Wind Highlight Tip Power", Range(0.1,8)) = 1.7

        [Header(Gust Lighting)]
        _GustLightStart("Gust Light Start", Range(0,1)) = 0.15
        _GustLightEnd("Gust Light Full", Range(0,1)) = 0.72
        _GustHighlightBase("Non Gust Highlight", Range(0,1)) = 0.05
        _GustTransmissionBase("Non Gust Transmission", Range(0,1)) = 0.65
        _GustTransmissionPeak("Gust Transmission", Range(0,2)) = 1.25
        _GustLightPower("Gust Light Power", Range(0.25,4)) = 1.15

        [Header(Grazing Highlight)]
        [HDR]_GrazingColor("Grazing Color", Color) = (0.92,1.00,0.75,1)
        _GrazingStrength("Grazing Strength", Range(0,2)) = 0.15
        _GrazingPower("Grazing Power", Range(0.5,16)) = 4.0
        _GrazingSunPower("Grazing Sun Power", Range(0.5,8)) = 1.5

        [Header(Arc Wind)]
        _IdleSwayAngle("Idle Sway Angle", Range(0,30)) = 8
        _IdleSwayStrength("Idle Sway Strength", Range(0,2)) = 0.65
        _GustBendAngle("Gust Bend Angle", Range(0,85)) = 58
        _GustBendPower("Gust Bend Power", Range(0.25,4)) = 1.55
        _GustThreshold("Gust Threshold", Range(0,0.9)) = 0.25
        _WindArcResponse("Wind Strength Response", Range(0,10)) = 3.8
        _ArcHeightPower("Arc Height Power", Range(0.5,4)) = 1.35

        [Header(Grass Shadow Casting)]
        _GrassShadowDensity("Shadow Density", Range(0,1)) = 0.50
        _GrassShadowFadeStart("Shadow Fade Start", Float) = 15
        _GrassShadowFadeEnd("Shadow Fade End", Float) = 35
        _GrassShadowMinimumDensity("Far Shadow Minimum", Range(0,1)) = 0.08
        _GrassShadowTipReduce("Shadow Tip Reduce", Range(0,1)) = 0.35
        _GrassShadowTipPower("Shadow Tip Power", Range(0.1,8)) = 1.6

        [Header(Wind)]
        _WindStrengthMultiplier("Wind Strength Multiplier", Range(0,3)) = 1
        _WindSpeedMultiplier("Wind Speed Multiplier", Range(0,3)) = 1
        _WindFrequencyMultiplier("Wind Frequency Multiplier", Range(0,3)) = 1
        _WindRootHeight("Wind Root Height", Float) = 0
        _WindTopHeight("Wind Top Height", Float) = 1
        _WindMaskFromUV("Wind Mask From UV Y", Range(0,1)) = 1

        [Header(Wind Randomness)]
        _WindStrengthVariation("Strength Variation", Range(0,1)) = 0.08
        _WindSpeedVariation("Speed Variation", Range(0,1)) = 0.03
        _WindFrequencyVariation("Frequency Variation", Range(0,1)) = 0.025
        _WindPhaseRandomness("Phase Randomness", Range(0,1)) = 0.035
        _WindFlutterVariation("Flutter Variation", Range(0,1)) = 0.08
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "VegetationIndirectCommon.hlsl"
        #include "VegetationLOD.hlsl"
        #include "VegetationDepthNormals.hlsl"
        #include "VegetationReceiveShadows.hlsl"
        #include "VegetationAdditionalLights.hlsl"
        #include "VegetationTerrainCommon.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        float4 _BaseColor;
        float4 _VariationColor;
        float4 _PatchDarkColor;
        float4 _PatchBrightColor;
        float4 _RootColor;
        float4 _TipColor;
        float4 _TopLightColor;
        float4 _TransmissionColor;
        float4 _WindHighlightColor;
        float4 _GrazingColor;

        float _Cutoff;

        float _BladeHeightScale;
        float _BladeBaseY;
        float _HeightVariation;

        float _BendStrength;
        float _BendRandomness;
        float _BendPower;
        float _LeanStrength;

        float _VariationStrength;
        float _ColorPatchStrength;
        float _ColorPatchScale;

        float _GradientStrength;
        float _GradientPower;

        float _TerrainColorBlend;
        float _TerrainBlendPower;

        float _DistanceBlendStart;
        float _DistanceBlendEnd;
        float _DistanceTerrainBlend;
        float _DistanceVariationReduce;
        float _DistanceGradientReduce;
        float _DistanceLightingReduce;
        float _DistanceNormalUpBoost;

        float _AmbientStrength;
        float _LightWrap;
        float _NormalUpBlend;
        float _MinimumLight;
        float _ShadowStrength;

        float _BackfaceLightStrength;
        float _TwoSidedUpStrength;

        float _SkyAmbientBlend;
        float _TopLightStrength;
        float _TopLightPower;
        float _BottomAmbientReduce;
        float _BottomDarkenStrength;
        float _BottomDarkenPower;

        float _TransmissionStrength;
        float _TransmissionPower;
        float _TransmissionTipStrength;
        float _TransmissionShadowReduce;

        float _WindHighlightStrength;
        float _WindHighlightPower;
        float _WindHighlightBendMin;
        float _WindHighlightBendMax;
        float _WindHighlightTipPower;

        float _GustLightStart;
        float _GustLightEnd;
        float _GustHighlightBase;
        float _GustTransmissionBase;
        float _GustTransmissionPeak;
        float _GustLightPower;

        float _GrazingStrength;
        float _GrazingPower;
        float _GrazingSunPower;

        float _IdleSwayAngle;
        float _IdleSwayStrength;
        float _GustBendAngle;
        float _GustBendPower;
        float _GustThreshold;
        float _WindArcResponse;
        float _ArcHeightPower;

        float _GrassShadowDensity;
        float _GrassShadowFadeStart;
        float _GrassShadowFadeEnd;
        float _GrassShadowMinimumDensity;
        float _GrassShadowTipReduce;
        float _GrassShadowTipPower;

        float _WindStrengthMultiplier;
        float _WindSpeedMultiplier;
        float _WindFrequencyMultiplier;
        float _WindRootHeight;
        float _WindTopHeight;
        float _WindMaskFromUV;

        float _WindStrengthVariation;
        float _WindSpeedVariation;
        float _WindFrequencyVariation;
        float _WindPhaseRandomness;
        float _WindFlutterVariation;
        CBUFFER_END

        float GrassValueNoise(float2 position)
        {
            float2 cell = floor(position);
            float2 local = frac(position);
            float2 smoothLocal = local * local * (3.0 - 2.0 * local);
            float a = VegetationHash(cell);
            float b = VegetationHash(cell + float2(1,0));
            float c = VegetationHash(cell + float2(0,1));
            float d = VegetationHash(cell + float2(1,1));
            return lerp(lerp(a,b,smoothLocal.x),lerp(c,d,smoothLocal.x),smoothLocal.y);
        }

        float GetBladeHeightMask(float3 positionOS,float2 uv)
        {
            float range = max(_WindTopHeight - _WindRootHeight,0.0001);
            float mask = saturate((positionOS.y - _WindRootHeight) / range);
            return lerp(mask,saturate(uv.y),_WindMaskFromUV);
        }

        float GetInstanceHeightScale(float3 pivotWS)
        {
            float randomValue = VegetationHash(pivotWS.xz * 1.173 + float2(7.31,2.17));
            return lerp(1.0 - _HeightVariation,1.0 + _HeightVariation,randomValue);
        }

        float3 ScaleGrassPositionOS(float3 positionOS,float heightScale)
        {
            positionOS.y = _BladeBaseY + (positionOS.y - _BladeBaseY) * (_BladeHeightScale * heightScale);
            return positionOS;
        }

        float2 GetStaticBendDirection(float3 pivotWS)
        {
            float randomAngle = VegetationHash(pivotWS.xz * 0.417 + float2(3.17,8.91)) * 6.2831853;
            float2 randomDirection = float2(cos(randomAngle),sin(randomAngle));
            float fieldPhase = dot(pivotWS.xz,float2(0.037,0.053));
            float2 fieldDirection = normalize(float2(cos(fieldPhase),sin(fieldPhase)) + 0.0001);
            return normalize(lerp(fieldDirection,randomDirection,_BendRandomness));
        }

        float3 ApplyStaticGrassShape(float3 positionWS,float3 pivotWS,float heightMask)
        {
            if (_BendStrength <= 0.0001 && _LeanStrength <= 0.0001) return positionWS;

            float2 direction = GetStaticBendDirection(pivotWS);
            float bendMask = pow(saturate(heightMask),_BendPower);
            float randomStrength = lerp(0.75,1.25,VegetationHash(pivotWS.xz * 2.371 + float2(4.21,1.37)));

            positionWS.xz += direction * _BendStrength * randomStrength * bendMask;
            positionWS.xz += direction * _LeanStrength * randomStrength * heightMask;

            return positionWS;
        }

        float GetDistanceVisualBlend(float3 pivotWS)
        {
            float startDistance = max(_DistanceBlendStart,0.0);
            float endDistance = max(_DistanceBlendEnd,startDistance + 0.01);
            float t = saturate((distance(_WorldSpaceCameraPos,pivotWS) - startDistance) / (endDistance - startDistance));
            return t * t * (3.0 - 2.0 * t);
        }

        float GetGrassColorPatch(float3 pivotWS)
        {
            float scale = max(_ColorPatchScale,0.0001);
            float a = GrassValueNoise(pivotWS.xz * scale);
            float b = GrassValueNoise(pivotWS.xz * scale * 0.43 + float2(17.3,8.7));
            return saturate(a * 0.72 + b * 0.28);
        }

        void GetGrassWindParameters(float3 pivotWS,out float strengthMultiplier,out float speedMultiplier,out float frequencyMultiplier,out float phaseOffset,out float flutterPhaseOffset,out float flutterMultiplier)
        {
            float strengthRandom = VegetationHash(pivotWS.xz * 1.421 + float2(2.13,7.91)) * 2.0 - 1.0;
            float speedRandom = VegetationHash(pivotWS.xz * 2.173 + float2(8.31,1.73)) * 2.0 - 1.0;
            float frequencyRandom = VegetationHash(pivotWS.xz * 3.117 + float2(4.61,5.27)) * 2.0 - 1.0;
            float phaseRandom = VegetationHash(pivotWS.xz * 4.319 + float2(9.17,3.41)) * 2.0 - 1.0;
            float flutterPhaseRandom = VegetationHash(pivotWS.xz * 5.237 + float2(6.23,8.11)) * 2.0 - 1.0;
            float flutterRandom = VegetationHash(pivotWS.xz * 6.713 + float2(1.39,4.87)) * 2.0 - 1.0;

            strengthMultiplier = _WindStrengthMultiplier * max(0.05,1.0 + strengthRandom * _WindStrengthVariation);
            speedMultiplier = _WindSpeedMultiplier * max(0.05,1.0 + speedRandom * _WindSpeedVariation);
            frequencyMultiplier = _WindFrequencyMultiplier * max(0.05,1.0 + frequencyRandom * _WindFrequencyVariation);
            phaseOffset = phaseRandom * 6.2831853 * _WindPhaseRandomness;
            flutterPhaseOffset = flutterPhaseRandom * 6.2831853 * _WindPhaseRandomness;
            flutterMultiplier = max(0.05,1.0 + flutterRandom * _WindFlutterVariation);
        }

        VegetationWindSample EvaluateGrassWind(float3 positionWS,float3 pivotWS,out float arcStrength)
        {
            float strengthMultiplier;
            float speedMultiplier;
            float frequencyMultiplier;
            float phaseOffset;
            float flutterPhaseOffset;
            float flutterMultiplier;

            GetGrassWindParameters(pivotWS,strengthMultiplier,speedMultiplier,frequencyMultiplier,phaseOffset,flutterPhaseOffset,flutterMultiplier);

            float distanceFade = GetVegetationWindDistanceFade(pivotWS);
            arcStrength = abs(_VegetationWindDirectionStrength.w) * strengthMultiplier * distanceFade;

            return EvaluateVegetationWind(positionWS,pivotWS,1.0,strengthMultiplier,speedMultiplier,frequencyMultiplier,phaseOffset,flutterPhaseOffset,flutterMultiplier);
        }

        float3 RotateGrassVector(float3 value,float3 axis,float angle)
        {
            float s;
            float c;
            sincos(angle,s,c);
            return value * c + cross(axis,value) * s + axis * dot(axis,value) * (1.0 - c);
        }

        void EvaluateGrassArc(VegetationWindSample wind,float arcStrength,out float arcAngle,out float gustMask)
        {
            float strength01 = saturate(arcStrength * _WindArcResponse);

            float idleWave = clamp(wind.baseWave,-1.0,1.0);
            float idleAngle = radians(_IdleSwayAngle) * _IdleSwayStrength * idleWave * strength01;

            float thresholdRange = max(1.0 - _GustThreshold,0.0001);
            float rawGust = saturate((wind.gust - _GustThreshold) / thresholdRange);
            gustMask = pow(rawGust,max(_GustBendPower,0.01));

            float gustAngle = radians(_GustBendAngle) * gustMask * strength01;

            arcAngle = idleAngle + gustAngle;

            float minAngle = -radians(_IdleSwayAngle) * max(_IdleSwayStrength,0.0);
            float maxAngle = radians(_GustBendAngle + _IdleSwayAngle);

            arcAngle = clamp(arcAngle,minAngle,maxAngle);
        }

        void ApplyGrassArcWind(inout float3 positionWS,inout float3 normalWS,float3 bladeRootWS,float heightMask,VegetationWindSample wind,float arcStrength,out float arcAngle,out float gustMask)
        {
            arcAngle = 0.0;
            gustMask = 0.0;

            if (_VegetationWindEnabled < 0.5 || heightMask <= 0.0001) return;

            EvaluateGrassArc(wind,arcStrength,arcAngle,gustMask);

            float2 direction = normalize(wind.direction + float2(0.0001,0.0001));
            float3 bendAxis = normalize(float3(direction.y,0.0,-direction.x));

            float heightCurve = pow(saturate(heightMask),_ArcHeightPower);
            float vertexAngle = arcAngle * heightCurve;

            float3 relativePosition = positionWS - bladeRootWS;
            relativePosition = RotateGrassVector(relativePosition,bendAxis,vertexAngle);

            positionWS = bladeRootWS + relativePosition;
            normalWS = normalize(RotateGrassVector(normalWS,bendAxis,vertexAngle));

            float flutterMask = pow(saturate(heightMask),max(_VegetationWindFlutterParams.z,0.1));
            positionWS.xz += wind.sideDirection * wind.flutter * flutterMask;
        }

        void ApplyGrassForwardWind(inout float3 positionWS,inout float3 normalWS,float3 bladeRootWS,float3 pivotWS,float heightMask,out float2 windDirection,out float arcAngle,out float gustMask)
        {
            windDirection=float2(1.0,0.0);
            arcAngle=0.0;
            gustMask=0.0;
            if (_VegetationWindEnabled < 0.5 || heightMask <= 0.0001) return;
            float quality=GetVegetationForwardWindQuality(pivotWS);
            if (quality < 0.5) return;
            if (quality > 2.5)
            {
                float arcStrength;
                VegetationWindSample wind=EvaluateGrassWind(positionWS,pivotWS,arcStrength);
                windDirection=wind.direction;
                ApplyGrassArcWind(positionWS,normalWS,bladeRootWS,heightMask,wind,arcStrength,arcAngle,gustMask);
                return;
            }
            float2 direction=_VegetationWindDirectionStrength.xz;
            direction*=rsqrt(max(dot(direction,direction),0.0001));
            windDirection=direction;
            float distanceFade=GetVegetationWindDistanceFade(pivotWS);
            if (distanceFade <= 0.0001) return;
            float strength01=saturate(abs(_VegetationWindDirectionStrength.w)*_WindStrengthMultiplier*distanceFade*_WindArcResponse);
            float speed=_VegetationWindParams.x*_WindSpeedMultiplier;
            float frequency=_VegetationWindParams.y*_WindFrequencyMultiplier;
            float wave=sin(_Time.y*speed+dot(pivotWS.xz,direction)*frequency);
            float gustApprox=quality>1.5&&_VegetationWindGustEnabled>=0.5?saturate(wave*0.5+0.5)*saturate(_VegetationWindGustParams.x):0.0;
            gustMask=gustApprox;
            arcAngle=radians(_IdleSwayAngle)*_IdleSwayStrength*wave*strength01;
            if (quality>1.5) arcAngle+=radians(_GustBendAngle)*gustApprox*0.55*strength01;
            arcAngle=clamp(arcAngle,-radians(_IdleSwayAngle)*max(_IdleSwayStrength,0.0),radians(_GustBendAngle+_IdleSwayAngle));
            float3 bendAxis=normalize(float3(direction.y,0.0,-direction.x));
            float vertexAngle=arcAngle*saturate(heightMask)*saturate(heightMask);
            float3 relativePosition=positionWS-bladeRootWS;
            relativePosition=RotateGrassVector(relativePosition,bendAxis,vertexAngle);
            positionWS=bladeRootWS+relativePosition;
            normalWS=normalize(RotateGrassVector(normalWS,bendAxis,vertexAngle));
        }

        void ApplyGrassShadowWind(inout float3 positionWS,inout float3 normalWS,float3 bladeRootWS,float3 pivotWS,float heightMask)
        {
            if (_VegetationWindEnabled < 0.5 || heightMask <= 0.0001) return;
            float quality = GetVegetationShadowWindQuality(pivotWS);
            if (quality < 0.5) return;
            if (quality > 2.5)
            {
                float arcStrength;
                VegetationWindSample wind = EvaluateGrassWind(positionWS,pivotWS,arcStrength);
                float arcAngle;
                float gustMask;
                ApplyGrassArcWind(positionWS,normalWS,bladeRootWS,heightMask,wind,arcStrength,arcAngle,gustMask);
                return;
            }
            float2 direction = _VegetationWindDirectionStrength.xz;
            direction *= rsqrt(max(dot(direction,direction),0.0001));
            float distanceFade = GetVegetationWindDistanceFade(pivotWS);
            if (distanceFade <= 0.0001) return;
            float strength01 = saturate(abs(_VegetationWindDirectionStrength.w) * _WindStrengthMultiplier * distanceFade * _WindArcResponse);
            float speed = _VegetationWindParams.x * _WindSpeedMultiplier;
            float frequency = _VegetationWindParams.y * _WindFrequencyMultiplier;
            float wave = sin(_Time.y * speed + dot(pivotWS.xz,direction) * frequency);
            float gustApprox = quality > 1.5 && _VegetationWindGustEnabled >= 0.5 ? saturate(wave * 0.5 + 0.5) * saturate(_VegetationWindGustParams.x) : 0.0;
            float arcAngle = radians(_IdleSwayAngle) * _IdleSwayStrength * wave * strength01;
            if (quality > 1.5) arcAngle += radians(_GustBendAngle) * gustApprox * 0.55 * strength01;
            arcAngle = clamp(arcAngle,-radians(_IdleSwayAngle) * max(_IdleSwayStrength,0.0),radians(_GustBendAngle + _IdleSwayAngle));
            float3 bendAxis = normalize(float3(direction.y,0.0,-direction.x));
            float vertexAngle = arcAngle * saturate(heightMask) * saturate(heightMask);
            float3 relativePosition = positionWS - bladeRootWS;
            relativePosition = RotateGrassVector(relativePosition,bendAxis,vertexAngle);
            positionWS = bladeRootWS + relativePosition;
            normalWS = normalize(RotateGrassVector(normalWS,bendAxis,vertexAngle));
        }

        float3 ApplyTwoSidedGrassNormal(float3 normalWS,float faceSign)
        {
            normalWS = normalize(normalWS) * faceSign;
            normalWS.y = abs(normalWS.y);

            float3 upwardNormal = normalize(float3(normalWS.x,1.0,normalWS.z));
            return normalize(lerp(normalWS,upwardNormal,_TwoSidedUpStrength));
        }

        float GetArcBend01(float arcAngle)
        {
            float referenceAngle = max(radians(_GustBendAngle),0.001);
            return saturate(abs(arcAngle) / referenceAngle);
        }

        float GetGustLightGate(float gustMask)
        {
            float endValue = max(_GustLightEnd,_GustLightStart + 0.001);
            float gate = smoothstep(_GustLightStart,endValue,saturate(gustMask));
            return pow(saturate(gate),max(_GustLightPower,0.01));
        }

        float GetDirectionalWindHighlight(float3 normalWS,float3 viewDirWS,float3 lightDirWS,float2 windDirection,float arcAngle,float heightMask)
        {
            float bend01 = GetArcBend01(arcAngle);

            float bendRange = max(_WindHighlightBendMax - _WindHighlightBendMin,0.0001);
            float bendMask = saturate((bend01 - _WindHighlightBendMin) / bendRange);
            bendMask = bendMask * bendMask * (3.0 - 2.0 * bendMask);

            float bendSign = arcAngle >= 0.0 ? 1.0 : -1.0;

            float3 bendDirectionWS = normalize(float3(windDirection.x * bendSign,0.12,windDirection.y * bendSign));
            float3 halfVector = SafeNormalize(viewDirWS + lightDirWS);

            float normalSpecular = pow(saturate(dot(normalWS,halfVector)),_WindHighlightPower);

            float bendHalfAlignment = saturate(dot(bendDirectionWS,halfVector));
            bendHalfAlignment = pow(bendHalfAlignment,_WindHighlightPower * 0.65);

            float lightBendAlignment = saturate(dot(bendDirectionWS,lightDirWS) * 0.5 + 0.5);
            float viewBendAlignment = saturate(dot(bendDirectionWS,viewDirWS) * 0.5 + 0.5);

            float directionalAlignment = bendHalfAlignment;
            directionalAlignment *= lerp(0.50,1.0,lightBendAlignment);
            directionalAlignment *= lerp(0.60,1.0,viewBendAlignment);

            float tipMask = pow(saturate(heightMask),_WindHighlightTipPower);

            return saturate(max(normalSpecular,directionalAlignment) * bendMask * tipMask);
        }

        float GetDirectionalTransmission(float3 normalWS,float3 viewDirWS,float3 lightDirWS,float arcAngle,float heightMask)
        {
            float backLight = saturate(dot(viewDirWS,-lightDirWS));
            backLight = pow(backLight,_TransmissionPower);

            float normalBackLight = saturate(dot(-normalWS,lightDirWS));
            float grazing = 1.0 - saturate(abs(dot(normalWS,lightDirWS)));

            float bend01 = GetArcBend01(arcAngle);
            float windBoost = lerp(0.65,1.0,bend01);

            float tip = lerp(1.0 - _TransmissionTipStrength,1.0,heightMask);

            return backLight * lerp(0.4,1.0,max(normalBackLight,grazing)) * windBoost * tip;
        }

        float GetGrazingHighlight(float3 normalWS,float3 viewDirWS,float3 lightDirWS,float heightMask)
        {
            float viewGrazing = 1.0 - saturate(abs(dot(normalWS,viewDirWS)));
            viewGrazing = pow(viewGrazing,_GrazingPower);

            float sunFacing = saturate(dot(normalWS,lightDirWS) * 0.5 + 0.5);
            sunFacing = pow(sunFacing,_GrazingSunPower);

            return viewGrazing * sunFacing * heightMask;
        }

        float GetGrassShadowDensity(float distanceToCamera,float heightMask)
        {
            float fadeStart = max(_GrassShadowFadeStart,0.0);
            float fadeEnd = max(_GrassShadowFadeEnd,fadeStart + 0.01);
            float distanceFade = 1.0 - saturate((distanceToCamera - fadeStart) / (fadeEnd - fadeStart));

            float density = lerp(_GrassShadowMinimumDensity,_GrassShadowDensity,distanceFade);

            float tipMask = pow(saturate(heightMask),max(_GrassShadowTipPower,0.01));
            float tipRetention = lerp(1.0,1.0 - _GrassShadowTipReduce,tipMask);

            return saturate(density * tipRetention);
        }

        float GetGrassShadowRandom(float3 bladeRootWS)
        {
            float2 hashPosition = bladeRootWS.xz * 17.317 + float2(bladeRootWS.y * 3.173,bladeRootWS.y * 7.119);
            return VegetationHash(hashPosition + float2(4.713,9.281));
        }

        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

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
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fog

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 bladeRootXZ : TEXCOORD1;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation float lodDistance : TEXCOORD15;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                float2 terrainUV : TEXCOORD4;
                float instanceVariation : TEXCOORD5;
                float rootMask : TEXCOORD6;
                float heightMask : TEXCOORD7;
                float distanceBlend : TEXCOORD8;
                float2 windDirection : TEXCOORD9;
                float2 arcData : TEXCOORD10;
                float colorPatch : TEXCOORD11;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 pivotWS = GetVegetationPivotWS(input.instanceID);
                output.lodDistance = GetVegetationLODDistance(pivotWS);
                float heightScale = GetInstanceHeightScale(pivotWS);

                float3 originalPositionOS = input.positionOS;
                float3 scaledPositionOS = ScaleGrassPositionOS(input.positionOS,heightScale);

                float3 positionWS = TransformVegetationPositionToWorld(scaledPositionOS,input.instanceID);
                float3 normalWS = TransformVegetationNormalToWorld(input.normalOS,input.instanceID);

                float3 bladeRootOS = float3(input.bladeRootXZ.x,_BladeBaseY,input.bladeRootXZ.y);
                float3 bladeRootWS = TransformVegetationPositionToWorld(bladeRootOS,input.instanceID);

                float heightMask = GetBladeHeightMask(originalPositionOS,input.uv);
                float rootMask = pow(saturate(1.0 - heightMask),_TerrainBlendPower);

                positionWS = ApplyStaticGrassShape(positionWS,pivotWS,heightMask);

                float2 windDirection;
                float arcAngle;
                float gustMask;
                ApplyGrassForwardWind(positionWS,normalWS,bladeRootWS,pivotWS,heightMask,windDirection,arcAngle,gustMask);

                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv,_BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.terrainUV = GetVegetationTerrainUV(pivotWS);
                output.instanceVariation = VegetationHash(pivotWS.xz * 0.731 + float2(1.7,9.2));
                output.rootMask = rootMask;
                output.heightMask = heightMask;
                output.distanceBlend = GetDistanceVisualBlend(pivotWS);
                output.windDirection = windDirection;
                output.arcData = float2(arcAngle,gustMask);
                output.colorPatch = GetGrassColorPatch(pivotWS);

                return output;
            }

            half4 Frag(Varyings input,FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv);
                clip(baseSample.a * _BaseColor.a - _Cutoff);
                ApplyVegetationLODCrossFade(input.lodDistance, input.positionCS.xy);

                float heightMask = saturate(input.heightMask);
                float distanceBlend = saturate(input.distanceBlend);
                float lightingDetail = 1.0 - distanceBlend * _DistanceLightingReduce;

                float faceSign = IS_FRONT_VFACE(frontFace,1.0,-1.0);

                float3 normalWS = ApplyTwoSidedGrassNormal(input.normalWS,faceSign);

                float normalUpBlend = saturate(_NormalUpBlend + distanceBlend * _DistanceNormalUpBoost);
                normalWS = normalize(lerp(normalWS,float3(0,1,0),normalUpBlend));

                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                float variationStrength = _VariationStrength * (1.0 - distanceBlend * _DistanceVariationReduce);
                half3 baseTint = lerp(_BaseColor.rgb,_VariationColor.rgb,input.instanceVariation * variationStrength);

                float patchSigned = input.colorPatch * 2.0 - 1.0;
                float patchAmount = abs(patchSigned) * _ColorPatchStrength;
                half3 patchColor = patchSigned < 0.0 ? _PatchDarkColor.rgb : _PatchBrightColor.rgb;

                baseTint = lerp(baseTint,baseTint * patchColor,patchAmount);

                float gradientMask = pow(heightMask,_GradientPower);
                half3 gradientColor = lerp(_RootColor.rgb,_TipColor.rgb,gradientMask);
                float gradientStrength = _GradientStrength * (1.0 - distanceBlend * _DistanceGradientReduce);

                half3 albedo = baseSample.rgb * lerp(baseTint,gradientColor,gradientStrength);

                if (_VegetationTerrainColorEnabled > 0.5)
                {
                    half3 terrainColor = SampleVegetationTerrainColor(input.terrainUV);

                    float rootTerrainBlend = saturate(input.rootMask * _TerrainColorBlend);
                    float distanceTerrainBlend = saturate(distanceBlend * _DistanceTerrainBlend);
                    float combinedTerrainBlend = 1.0 - (1.0 - rootTerrainBlend) * (1.0 - distanceTerrainBlend);

                    albedo = lerp(albedo,terrainColor,saturate(combinedTerrainBlend));
                }

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float rawNdotL = dot(normalWS,mainLight.direction);
                float wrappedNdotL = saturate((rawNdotL + _LightWrap) / (1.0 + _LightWrap));
                wrappedNdotL = lerp(_MinimumLight,1.0,wrappedNdotL);

                if (faceSign < 0.0) wrappedNdotL *= _BackfaceLightStrength;

                float shadow = lerp(1.0,VegetationMainLightShadowAttenuation(mainLight.shadowAttenuation),_ShadowStrength);

                half3 directLighting = mainLight.color * wrappedNdotL * mainLight.distanceAttenuation * shadow;

                half3 localAmbient = SampleSH(normalWS);
                half3 skyAmbient = SampleSH(float3(0,1,0));
                half3 ambientLighting = lerp(localAmbient,skyAmbient,_SkyAmbientBlend) * _AmbientStrength;

                float topMask = pow(heightMask,_TopLightPower);
                float bottomMask = pow(saturate(1.0 - heightMask),_BottomDarkenPower);

                ambientLighting *= 1.0 - bottomMask * _BottomAmbientReduce;

                half3 color = albedo * (directLighting + ambientLighting);
                color *= 1.0 - bottomMask * _BottomDarkenStrength;

                half3 topLightTint = lerp(albedo,_TopLightColor.rgb,0.65);
                color += topLightTint * topMask * _TopLightStrength * lightingDetail;

                float arcAngle = input.arcData.x;
                float gustMask = saturate(input.arcData.y);
                float gustLightGate = GetGustLightGate(gustMask);

                float windHighlight = GetDirectionalWindHighlight(
                    normalWS,
                    viewDirWS,
                    mainLight.direction,
                    normalize(input.windDirection + 0.0001),
                    arcAngle,
                    heightMask
                );

                float highlightGate = lerp(_GustHighlightBase,1.0,gustLightGate);

                half3 windHighlightColor = _WindHighlightColor.rgb * mainLight.color;
                windHighlightColor *= windHighlight * _WindHighlightStrength * highlightGate;
                windHighlightColor *= mainLight.distanceAttenuation * shadow * lightingDetail;

                color += windHighlightColor;

                float transmission = GetDirectionalTransmission(
                    normalWS,
                    viewDirWS,
                    mainLight.direction,
                    arcAngle,
                    heightMask
                );

                float transmissionGate = lerp(_GustTransmissionBase,_GustTransmissionPeak,gustLightGate);
                transmission *= transmissionGate;

                if (faceSign < 0.0) transmission *= 1.15;

                half3 transmissionColor = albedo * _TransmissionColor.rgb * mainLight.color;
                transmissionColor *= transmission * _TransmissionStrength;
                transmissionColor *= mainLight.distanceAttenuation;
                transmissionColor *= lerp(1.0,shadow,_TransmissionShadowReduce);
                transmissionColor *= lightingDetail;

                color += transmissionColor;

                float grazing = GetGrazingHighlight(normalWS,viewDirWS,mainLight.direction,heightMask);

                half3 grazingColor = _GrazingColor.rgb * mainLight.color;
                grazingColor *= grazing * _GrazingStrength;
                grazingColor *= mainLight.distanceAttenuation * shadow * lightingDetail;

                color += grazingColor;

                [loop]
                for (int lightIndex = 0; lightIndex < GetVegetationAdditionalLightsCount(); ++lightIndex)
                {
                    Light light = GetVegetationAdditionalLight((uint)lightIndex, input.positionWS, unity_ProbesOcclusion);
                    float localNdotL = saturate((dot(normalWS, light.direction) + _LightWrap) / (1.0 + _LightWrap));
                    localNdotL = lerp(_MinimumLight, 1.0, localNdotL);
                    if (faceSign < 0.0) localNdotL *= _BackfaceLightStrength;
                    float localShadow = lerp(1.0, light.shadowAttenuation, _ShadowStrength);
                    float localAttenuation = light.distanceAttenuation * localShadow;
                    color += albedo * light.color * localNdotL * localAttenuation
                        * (1.0 - bottomMask * _BottomDarkenStrength);

                    float localWindHighlight = GetDirectionalWindHighlight(
                        normalWS, viewDirWS, light.direction,
                        normalize(input.windDirection + 0.0001), arcAngle, heightMask);
                    color += _WindHighlightColor.rgb * light.color * localWindHighlight
                        * _WindHighlightStrength * highlightGate * localAttenuation * lightingDetail;

                    float localTransmission = GetDirectionalTransmission(
                        normalWS, viewDirWS, light.direction, arcAngle, heightMask) * transmissionGate;
                    if (faceSign < 0.0) localTransmission *= 1.15;
                    color += albedo * _TransmissionColor.rgb * light.color * localTransmission
                        * _TransmissionStrength * light.distanceAttenuation
                        * lerp(1.0, localShadow, _TransmissionShadowReduce) * lightingDetail;

                    float localGrazing = GetGrazingHighlight(normalWS, viewDirWS, light.direction, heightMask);
                    color += _GrazingColor.rgb * light.color * localGrazing
                        * _GrazingStrength * localAttenuation * lightingDetail;
                }

                color = MixFog(color,input.fogFactor);

                return half4(color,1);
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

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 bladeRootXZ : TEXCOORD1;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation float lodDistance : TEXCOORD15;
                float2 uv : TEXCOORD0;
                float heightMask : TEXCOORD1;
                nointerpolation float shadowRandom : TEXCOORD2;
                nointerpolation float shadowDistance : TEXCOORD3;
                nointerpolation float shadowCutoff : TEXCOORD4;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;

                float3 pivotWS = GetVegetationPivotWS(input.instanceID);
                output.lodDistance = GetVegetationLODDistance(pivotWS);
                float heightScale = GetInstanceHeightScale(pivotWS);

                float3 originalPositionOS = input.positionOS;
                float3 scaledPositionOS = ScaleGrassPositionOS(input.positionOS,heightScale);

                float3 positionWS = TransformVegetationPositionToWorld(scaledPositionOS,input.instanceID);
                float3 normalWS = TransformVegetationNormalToWorld(input.normalOS,input.instanceID);

                float3 bladeRootOS = float3(input.bladeRootXZ.x,_BladeBaseY,input.bladeRootXZ.y);
                float3 bladeRootWS = TransformVegetationPositionToWorld(bladeRootOS,input.instanceID);

                float heightMask = GetBladeHeightMask(originalPositionOS,input.uv);

                positionWS = ApplyStaticGrassShape(positionWS,pivotWS,heightMask);

                ApplyGrassShadowWind(positionWS,normalWS,bladeRootWS,pivotWS,heightMask);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
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
                output.heightMask = heightMask;
                output.shadowRandom = GetGrassShadowRandom(bladeRootWS);
                output.shadowDistance = distance(bladeRootWS,_WorldSpaceCameraPos);
                output.shadowCutoff = GetVegetationShadowAlphaCutoff(_Cutoff,pivotWS);

                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                float shadowDensity = GetGrassShadowDensity(input.shadowDistance,input.heightMask);
                clip(shadowDensity - input.shadowRandom);

                half alpha = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).a * _BaseColor.a;
                clip(alpha - input.shadowCutoff);
                ApplyVegetationLODCrossFade(input.lodDistance, input.positionCS.xy);
                return 0;
            }

            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

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
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 bladeRootXZ : TEXCOORD1;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation float lodDistance : TEXCOORD15;
                float2 uv : TEXCOORD0;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;

                float3 pivotWS = GetVegetationPivotWS(input.instanceID);
                output.lodDistance = GetVegetationLODDistance(pivotWS);
                float heightScale = GetInstanceHeightScale(pivotWS);

                float3 originalPositionOS = input.positionOS;
                float3 scaledPositionOS = ScaleGrassPositionOS(input.positionOS,heightScale);

                float3 positionWS = TransformVegetationPositionToWorld(scaledPositionOS,input.instanceID);
                float3 normalWS = TransformVegetationNormalToWorld(input.normalOS,input.instanceID);

                float3 bladeRootOS = float3(input.bladeRootXZ.x,_BladeBaseY,input.bladeRootXZ.y);
                float3 bladeRootWS = TransformVegetationPositionToWorld(bladeRootOS,input.instanceID);

                float heightMask = GetBladeHeightMask(originalPositionOS,input.uv);

                positionWS = ApplyStaticGrassShape(positionWS,pivotWS,heightMask);

                float arcStrength;
                VegetationWindSample wind = EvaluateGrassWind(positionWS,pivotWS,arcStrength);

                float arcAngle;
                float gustMask;

                ApplyGrassArcWind(positionWS,normalWS,bladeRootWS,heightMask,wind,arcStrength,arcAngle,gustMask);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv,_BaseMap);

                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).a * _BaseColor.a;
                clip(alpha - _Cutoff);
                ApplyVegetationLODCrossFade(input.lodDistance, input.positionCS.xy);
                return 0;
            }

            ENDHLSL
        }
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode"="DepthNormalsOnly" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct DNAttributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 bladeRootXZ : TEXCOORD1;
                uint instanceID : SV_InstanceID;
            };
            struct DNVaryings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation float lodDistance : TEXCOORD15;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float distanceBlend : TEXCOORD2;
            };
            DNVaryings DepthNormalsVert(DNAttributes input)
            {
                DNVaryings output;
                float3 pivotWS = GetVegetationPivotWS(input.instanceID);
                output.lodDistance = GetVegetationLODDistance(pivotWS);
                float heightScale = GetInstanceHeightScale(pivotWS);
                float3 scaledPositionOS = ScaleGrassPositionOS(input.positionOS,heightScale);
                float3 positionWS = TransformVegetationPositionToWorld(scaledPositionOS,input.instanceID);
                float3 normalWS = TransformVegetationNormalToWorld(input.normalOS,input.instanceID);
                float3 bladeRootOS = float3(input.bladeRootXZ.x,_BladeBaseY,input.bladeRootXZ.y);
                float3 bladeRootWS = TransformVegetationPositionToWorld(bladeRootOS,input.instanceID);
                float heightMask = GetBladeHeightMask(input.positionOS,input.uv);
                positionWS = ApplyStaticGrassShape(positionWS,pivotWS,heightMask);
                float2 windDirection;
                float arcAngle, gustMask;
                ApplyGrassForwardWind(positionWS,normalWS,bladeRootWS,pivotWS,heightMask,windDirection,arcAngle,gustMask);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = normalWS;
                output.uv = TRANSFORM_TEX(input.uv,_BaseMap);
                output.distanceBlend = GetDistanceVisualBlend(pivotWS);
                return output;
            }
            half4 DepthNormalsFrag(DNVaryings input, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).a * _BaseColor.a;
                clip(alpha-_Cutoff);
                ApplyVegetationLODCrossFade(input.lodDistance,input.positionCS.xy);
                float faceSign = IS_FRONT_VFACE(frontFace,1.0,-1.0);
                float3 normalWS = ApplyTwoSidedGrassNormal(input.normalWS,faceSign);
                float normalUpBlend = saturate(_NormalUpBlend + saturate(input.distanceBlend)*_DistanceNormalUpBoost);
                normalWS = normalize(lerp(normalWS,float3(0,1,0),normalUpBlend));
                return EncodeVegetationDepthNormal(normalWS);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
