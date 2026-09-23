#ifndef VEGETATION_INDIRECT_COMMON_INCLUDED
#define VEGETATION_INDIRECT_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

StructuredBuffer<float4x4> _VegetationMatrices;
StructuredBuffer<uint> _VegetationVisibleIndices;

int _VegetationUseVisibleIndices;

float _VegetationWindEnabled;
float _VegetationWindGustEnabled;
float _VegetationForwardWindLODEnabled;
float _VegetationForwardWindQuality;
float _VegetationForwardWindUseDistanceLOD;
float4 _VegetationForwardWindLODDistanceSq;
float _VegetationShadowWindQuality;
float _VegetationShadowWindLODEnabled;
float4 _VegetationShadowWindLODDistanceSq;
float _VegetationShadowAlphaCutoffLODEnabled;
float4 _VegetationShadowAlphaCutoffBias;
float4 _CameraPositionWS;

float4 _VegetationWindDirectionStrength;
float4 _VegetationWindParams;
float4 _VegetationWindCameraPosition;
float4 _VegetationWindGustParams;
float4 _VegetationWindFlutterParams;

struct VegetationWindSample
{
    float2 direction;
    float2 sideDirection;
    float distanceFade;
    float baseWave;
    float gust;
    float bend;
    float flutter;
};

uint GetVegetationSourceIndex(uint instanceID)
{
    return _VegetationUseVisibleIndices != 0 ? _VegetationVisibleIndices[instanceID] : instanceID;
}

float4x4 GetVegetationObjectToWorld(uint instanceID)
{
    return _VegetationMatrices[GetVegetationSourceIndex(instanceID)];
}

float3 TransformVegetationPositionToWorld(float3 positionOS, uint instanceID)
{
    return mul(GetVegetationObjectToWorld(instanceID), float4(positionOS, 1.0)).xyz;
}

float3 TransformVegetationNormalToWorld(float3 normalOS, uint instanceID)
{
    return normalize(mul((float3x3)GetVegetationObjectToWorld(instanceID), normalOS));
}

float3 GetVegetationPivotWS(uint instanceID)
{
    return mul(GetVegetationObjectToWorld(instanceID), float4(0, 0, 0, 1)).xyz;
}

float VegetationHash(float2 value)
{
    return frac(sin(dot(value, float2(12.9898, 78.233))) * 43758.5453);
}

float GetVegetationWindDistanceFade(float3 pivotWS)
{
    float distanceToCamera = distance(pivotWS, _VegetationWindCameraPosition.xyz);
    float fadeStart = _VegetationWindParams.z;
    float fadeEnd = max(_VegetationWindParams.w, fadeStart + 0.01);
    return 1.0 - saturate((distanceToCamera - fadeStart) / (fadeEnd - fadeStart));
}

float GetVegetationGust(float3 pivotWS, float2 direction)
{
    if (_VegetationWindGustEnabled < 0.5) return 0.0;

    float gustScale = max(_VegetationWindGustParams.y, 0.0001);
    float gustSpeed = _VegetationWindGustParams.z;
    float gustContrast = max(_VegetationWindGustParams.w, 0.1);

    float2 sideDirection = float2(-direction.y, direction.x);

    float forwardCoord = dot(pivotWS.xz, direction);
    float sideCoord = dot(pivotWS.xz, sideDirection);
    float time = _Time.y * gustSpeed;

    float waveA = sin(forwardCoord * gustScale - time);
    float waveB = sin(forwardCoord * gustScale * 0.47 + sideCoord * gustScale * 0.32 - time * 0.71);
    float waveC = sin(forwardCoord * gustScale * 0.21 - sideCoord * gustScale * 0.17 - time * 0.36);

    float gust = waveA * 0.56 + waveB * 0.29 + waveC * 0.15;
    gust = gust * 0.5 + 0.5;
    gust = pow(saturate(gust), gustContrast);

    return gust;
}

VegetationWindSample EvaluateVegetationWind(float3 positionWS, float3 pivotWS, float windMask, float strengthMultiplier, float speedMultiplier, float frequencyMultiplier, float phaseOffset, float flutterPhaseOffset, float flutterMultiplier)
{
    VegetationWindSample sample;

    sample.direction = float2(1.0, 0.0);
    sample.sideDirection = float2(0.0, 1.0);
    sample.distanceFade = 0.0;
    sample.baseWave = 0.0;
    sample.gust = 0.0;
    sample.bend = 0.0;
    sample.flutter = 0.0;

    float2 direction = _VegetationWindDirectionStrength.xz;
    float directionLength = max(length(direction), 0.0001);
    direction /= directionLength;

    sample.direction = direction;
    sample.sideDirection = float2(-direction.y, direction.x);

    if (_VegetationWindEnabled < 0.5 || windMask <= 0.0001) return sample;

    float distanceFade = GetVegetationWindDistanceFade(pivotWS);
    sample.distanceFade = distanceFade;

    if (distanceFade <= 0.0001) return sample;

    float strength = _VegetationWindDirectionStrength.w * strengthMultiplier * distanceFade;
    float speed = _VegetationWindParams.x * speedMultiplier;
    float frequency = _VegetationWindParams.y * frequencyMultiplier;

    float bendMask = pow(saturate(windMask), 1.35);

    float forwardCoord = dot(pivotWS.xz, direction);
    float sideCoord = dot(pivotWS.xz, sample.sideDirection);

    float phaseA = forwardCoord * frequency;
    float phaseB = forwardCoord * frequency * 0.41 + sideCoord * frequency * 0.23;

    float waveA = sin(_Time.y * speed + phaseA + phaseOffset);
    float waveB = sin(_Time.y * speed * 0.61 + phaseB + 1.73 + phaseOffset * 0.35);

    sample.baseWave = waveA * 0.72 + waveB * 0.28;
    sample.gust = GetVegetationGust(pivotWS, direction);

    float gustPush = sample.gust * _VegetationWindGustParams.x;
    float bendSignal = sample.baseWave * 0.22 + gustPush;

    sample.bend = bendSignal * strength * bendMask;

    float flutterStrength = _VegetationWindFlutterParams.x;
    float flutterSpeed = _VegetationWindFlutterParams.y;
    float flutterTipPower = _VegetationWindFlutterParams.z;

    float flutterMask = pow(saturate(windMask), flutterTipPower);
    float localPhase = dot(pivotWS.xz, float2(2.17, 1.73)) + flutterPhaseOffset;

    float flutterA = sin(_Time.y * flutterSpeed + localPhase);
    float flutterB = sin(_Time.y * flutterSpeed * 1.71 + localPhase * 1.37 + 2.31);

    float flutterSignal = flutterA * 0.68 + flutterB * 0.32;
    sample.flutter = flutterSignal * flutterStrength * flutterMultiplier * strength * flutterMask;

    return sample;
}

float3 ApplyVegetationWindSample(float3 positionWS, VegetationWindSample sample)
{
    positionWS.xz += sample.direction * sample.bend;
    positionWS.xz += sample.sideDirection * sample.flutter;
    return positionWS;
}

float GetVegetationForwardWindQuality(float3 pivotWS)
{
    if (_VegetationForwardWindLODEnabled < 0.5) return 3.0;
    if (_VegetationForwardWindUseDistanceLOD < 0.5) return clamp(round(_VegetationForwardWindQuality),0.0,3.0);
    float3 cameraDelta = pivotWS - _CameraPositionWS.xyz;
    float distanceSq = dot(cameraDelta,cameraDelta);
    if (distanceSq <= _VegetationForwardWindLODDistanceSq.x) return 3.0;
    if (distanceSq <= _VegetationForwardWindLODDistanceSq.y) return 2.0;
    return 1.0;
}

float GetVegetationShadowDistanceTier(float3 pivotWS)
{
    float3 cameraDelta = pivotWS - _CameraPositionWS.xyz;
    float distanceSq = dot(cameraDelta,cameraDelta);
    if (distanceSq <= _VegetationShadowWindLODDistanceSq.x) return 0.0;
    if (distanceSq <= _VegetationShadowWindLODDistanceSq.y) return 1.0;
    if (distanceSq <= _VegetationShadowWindLODDistanceSq.z) return 2.0;
    return 3.0;
}

float GetVegetationShadowWindQuality(float3 pivotWS)
{
    if (_VegetationShadowWindLODEnabled < 0.5) return clamp(round(_VegetationShadowWindQuality),0.0,3.0);
    return 3.0 - GetVegetationShadowDistanceTier(pivotWS);
}

float GetVegetationShadowAlphaCutoff(float baseCutoff, float3 pivotWS)
{
    if (_VegetationShadowAlphaCutoffLODEnabled < 0.5) return baseCutoff;
    float tier = GetVegetationShadowDistanceTier(pivotWS);
    float bias = tier < 0.5 ? 0.0 : (tier < 1.5 ? _VegetationShadowAlphaCutoffBias.x : (tier < 2.5 ? _VegetationShadowAlphaCutoffBias.y : _VegetationShadowAlphaCutoffBias.z));
    return saturate(baseCutoff + max(bias,0.0));
}

float3 ApplyVegetationShadowMainBend(float3 positionWS, float3 pivotWS, float windMask, float strengthMultiplier, float speedMultiplier, float frequencyMultiplier)
{
    if (_VegetationWindEnabled < 0.5 || windMask <= 0.0001) return positionWS;
    float2 direction = _VegetationWindDirectionStrength.xz;
    direction *= rsqrt(max(dot(direction,direction),0.0001));
    float distanceFade = GetVegetationWindDistanceFade(pivotWS);
    if (distanceFade <= 0.0001) return positionWS;
    float strength = _VegetationWindDirectionStrength.w * strengthMultiplier * distanceFade;
    float wave = sin(_Time.y * _VegetationWindParams.x * speedMultiplier + dot(pivotWS.xz,direction) * _VegetationWindParams.y * frequencyMultiplier);
    float mask = saturate(windMask);
    positionWS.xz += direction * wave * 0.18 * strength * mask * mask;
    return positionWS;
}

float3 ApplyVegetationShadowWind(float3 positionWS, float3 pivotWS, float windMask, float strengthMultiplier, float speedMultiplier, float frequencyMultiplier)
{
    if (_VegetationWindEnabled < 0.5 || windMask <= 0.0001) return positionWS;
    float2 direction = _VegetationWindDirectionStrength.xz;
    direction *= rsqrt(max(dot(direction,direction),0.0001));
    float distanceFade = GetVegetationWindDistanceFade(pivotWS);
    if (distanceFade <= 0.0001) return positionWS;
    float strength = _VegetationWindDirectionStrength.w * strengthMultiplier * distanceFade;
    float speed = _VegetationWindParams.x * speedMultiplier;
    float frequency = _VegetationWindParams.y * frequencyMultiplier;
    float wave = sin(_Time.y * speed + dot(pivotWS.xz,direction) * frequency);
    float gustApprox = _VegetationWindGustEnabled >= 0.5 ? saturate(wave * 0.5 + 0.5) * _VegetationWindGustParams.x * 0.65 : 0.0;
    float mask = saturate(windMask);
    float bendMask = mask * mask;
    positionWS.xz += direction * (wave * 0.22 + gustApprox) * strength * bendMask;
    return positionWS;
}

float3 ApplyVegetationWindAdvanced(float3 positionWS, float3 pivotWS, float windMask, float strengthMultiplier, float speedMultiplier, float frequencyMultiplier, float phaseOffset, float flutterPhaseOffset, float flutterMultiplier)
{
    VegetationWindSample sample = EvaluateVegetationWind(positionWS, pivotWS, windMask, strengthMultiplier, speedMultiplier, frequencyMultiplier, phaseOffset, flutterPhaseOffset, flutterMultiplier);
    return ApplyVegetationWindSample(positionWS, sample);
}

float3 ApplyVegetationWind(float3 positionWS, float3 pivotWS, float windMask, float strengthMultiplier, float speedMultiplier, float frequencyMultiplier)
{
    return ApplyVegetationWindAdvanced(positionWS, pivotWS, windMask, strengthMultiplier, speedMultiplier, frequencyMultiplier, 0.0, 0.0, 1.0);
}

float3 ApplyVegetationForwardWind(float3 positionWS, float3 pivotWS, float windMask, float strengthMultiplier, float speedMultiplier, float frequencyMultiplier)
{
    float quality = GetVegetationForwardWindQuality(pivotWS);
    if (quality < 0.5) return positionWS;
    if (quality > 2.5) return ApplyVegetationWind(positionWS,pivotWS,windMask,strengthMultiplier,speedMultiplier,frequencyMultiplier);
    if (quality > 1.5) return ApplyVegetationShadowWind(positionWS,pivotWS,windMask,strengthMultiplier,speedMultiplier,frequencyMultiplier);
    return ApplyVegetationShadowMainBend(positionWS,pivotWS,windMask,strengthMultiplier,speedMultiplier,frequencyMultiplier);
}

#endif