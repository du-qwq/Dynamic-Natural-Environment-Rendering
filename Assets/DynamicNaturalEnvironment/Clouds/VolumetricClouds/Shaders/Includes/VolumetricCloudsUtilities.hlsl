#ifndef URP_VOLUMETRIC_CLOUDS_UTILITIES_HLSL
#define URP_VOLUMETRIC_CLOUDS_UTILITIES_HLSL

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/VolumeRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

#define NUM_MULTI_SCATTERING_OCTAVES 2
#define PHASE_FUNCTION_STRUCTURE half2
#define CLOUD_DETAIL_MIP_OFFSET 0.0
#define CLOUD_LUT_MIP_OFFSET 1.0
#define CLOUD_DENSITY_THRESHOLD 0.001
#define EMPTY_STEPS_BEFORE_LARGE_STEPS 8
#define FORWARD_ECCENTRICITY 0.7
#define BACKWARD_ECCENTRICITY 0.7
#define MIN_EROSION_DISTANCE 3000.0
#define MAX_EROSION_DISTANCE 100000.0
#define NOISE_TEXTURE_NORMALIZATION_FACTOR 100000.0
#define MAX_SKYBOX_VOLUMETRIC_CLOUDS_DISTANCE 200000.0
#define LIGHT_STEP_MAXIMAL_SIZE 1000.0

#define _PlanetCenterPosition _PlanetCenterRadius.xyz
#define ConvertToPS(positionWS) (positionWS - _PlanetCenterPosition)

//球谐函数计算环境光
half3 EvaluateVolumetricCloudsAmbientProbe(half3 normalWS)
{
    half3 result = SHEvalLinearL0L1(normalWS, clouds_SHAr, clouds_SHAg, clouds_SHAb);
    result += SHEvalLinearL2(normalWS, clouds_SHBr, clouds_SHBg, clouds_SHBb, clouds_SHC);
    return result;
}
//表示一条观察射线
struct CloudRay
{
    float3 originWS;
    half3 direction;
    float maxRayLength;
    float integrationNoise;//随机起始偏移，用于避免所有像素在相同距离开始采样，产生整齐条纹
};
//保存整条观察射线的结果
struct VolumetricRayResult
{
    half3 scattering;
    half ambient;
    half transmittance;
    float meanDistance;//当前射线上云的大致平均距离
    bool invalidRay;//表示射线有没有真正采样到有效云
};

struct RayMarchRange
{
    float start;
    float end;
};

struct CloudProperties
{
    half density;
    half ambientOcclusion;//当前点受到的环境光遮蔽
    float height;//当前点在云层中的归一化高度
    half sigmaT;//消光系数
};

struct CloudCoverageData
{
    half coverage;//云覆盖率
    half rainClouds;//降雨云程度，后面用于改变消光系数
    half cloudType;//云类型，例如层云、积云之间的混合
    half maxCloudHeight;
};
//根据场景亮度进行感知透射率修正
half EvaluateFinalTransmittance(half3 sceneColor, half transmittance)
{
    half luminance = Luminance(sceneColor * _PostExposure);//把RGB转换成单一亮度

    if (luminance > 0.0)
    {
        half resultLuminance = luminance * rcp(1.0 + luminance) * transmittance;
        resultLuminance = resultLuminance * rcp(max(1.0 - resultLuminance, 0.0001));

        half finalTransmittance = max(resultLuminance * rcp(luminance), pow(transmittance, 6.0));
        transmittance = lerp(transmittance, finalTransmittance, _ImprovedTransmittanceBlend);
    }

    return saturate(transmittance);
}

#if UNITY_VERSION < 202330
//无限远深度编码，将线性深度转换为倒数深度，可以在有限的0～1范围内表示非常远的云
float EncodeInfiniteDepth(float depth, float nearPlane)
{
    return saturate(nearPlane * rcp(depth));
}
//解码
float DecodeInfiniteDepth(float encodedDepth, float nearPlane)
{
    return nearPlane * rcp(max(encodedDepth, FLT_EPS));
}

#endif
//将云位置转换成深度
float ConvertCloudDepth(float3 positionWS)
{
    float4 positionCS = TransformWorldToHClip(positionWS);
    return positionCS.z * rcp(positionCS.w);
}
//生成随机数
float GenerateRandomFloat(float2 screenUV)
{
    float timeValue = unity_DeltaTime.y * _Time.y + _Seed;
    _Seed += 1.0;
    return GenerateHashedRandomFloat(uint3(screenUV * _ScreenSize.xy, timeValue));
}
//射线与球体求交
float2 IntersectSphere(float sphereRadius, float cosChi, float radialDistance, float reciprocalRadialDistance)
{
    float radiusRatio = sphereRadius * reciprocalRadialDistance;
    float discriminant = radiusRatio * radiusRatio - saturate(1.0 - cosChi * cosChi);

    if (discriminant < 0.0)
    {
        return discriminant.xx;
    }

    float root = sqrt(discriminant);
    return radialDistance * float2(-cosChi - root, -cosChi + root);
}
//重载
float2 IntersectSphere(float sphereRadius, float cosChi, float radialDistance)
{
    return IntersectSphere(sphereRadius, cosChi, radialDistance, rcp(radialDistance));
}
//地平线角度
float ComputeCosineOfHorizonAngle(float radialDistance)
{
    float sinHorizon = _EarthRadius * rcp(radialDistance);
    return -sqrt(saturate(1.0 - sinHorizon * sinHorizon));
}
//更通用的射线球体求交
int RaySphereIntersection(float3 rayOriginWS, float3 rayDirection, float radius, out float2 intersections)
{
    float3 rayOriginPS = rayOriginWS + float3(0.0, _EarthRadius, 0.0);

    float a = dot(rayDirection, rayDirection);
    float b = 2.0 * dot(rayDirection, rayOriginPS);
    float c = dot(rayOriginPS, rayOriginPS) - radius * radius;
    float discriminant = b * b - 4.0 * a * c;

    intersections = 0.0;

    if (discriminant < 0.0)
    {
        return 0;
    }

    float sqrtDiscriminant = sqrt(discriminant);
    float q = -0.5 * (b + FastSign(b) * sqrtDiscriminant);

    if (abs(q) < FLT_EPS)
    {
        float solution = -b * rcp(2.0 * a);
        intersections = solution.xx;
        return solution >= 0.0 ? 1 : 0;
    }

    intersections = float2(c * rcp(q), q * rcp(a));

    if (intersections.x > intersections.y)
    {
        intersections = intersections.yx;
    }

    int solutionCount = 2;

    if (intersections.x < 0.0)
    {
        intersections.x = intersections.y;
        solutionCount--;
    }

    if (intersections.y < 0.0)
    {
        solutionCount--;
    }

    return solutionCount;
}
//计算从云层中出去的距离
bool ExitCloudVolume(float3 originPS, half3 direction, float outerRadius, out float exitDistance)
{
    float radialDistance = length(originPS);
    float reciprocalRadialDistance = rcp(radialDistance);
    float cosChi = dot(originPS, direction) * reciprocalRadialDistance;

    exitDistance = IntersectSphere(outerRadius, cosChi, radialDistance, reciprocalRadialDistance).y;

    return cosChi >= ComputeCosineOfHorizonAngle(radialDistance);
}
//射线与云层球壳求交
bool IntersectCloudVolume(float3 originPS, half3 direction, float innerRadius, float outerRadius, out float entryDistance, out float exitDistance)
{
    float radialDistance = length(originPS);
    float reciprocalRadialDistance = rcp(radialDistance);
    float cosChi = dot(originPS, direction) * reciprocalRadialDistance;

    float2 innerIntersection = IntersectSphere(innerRadius, cosChi, radialDistance, reciprocalRadialDistance);
    float2 outerIntersection = IntersectSphere(outerRadius, cosChi, radialDistance, reciprocalRadialDistance);

    bool intersectsVolume;

    if (innerIntersection.x < 0.0 && innerIntersection.y >= 0.0)
    {
        entryDistance = innerIntersection.y;
        exitDistance = outerIntersection.y;
        intersectsVolume = cosChi >= ComputeCosineOfHorizonAngle(radialDistance);
    }
    else
    {
        entryDistance = max(outerIntersection.x, 0.0);
        exitDistance = innerIntersection.x >= 0.0 ? innerIntersection.x : outerIntersection.y;
        intersectsVolume = outerIntersection.y >= 0.0;
    }

    return intersectsVolume && exitDistance > entryDistance;
}
//统一获取云层交点
bool GetCloudVolumeIntersection(float3 originWS, half3 direction, out RayMarchRange rayMarchRange)
{
#ifdef _LOCAL_VOLUMETRIC_CLOUDS

    return IntersectCloudVolume(ConvertToPS(originWS), direction, _LowestCloudAltitude, _HighestCloudAltitude, rayMarchRange.start, rayMarchRange.end);

#else

    ZERO_INITIALIZE(RayMarchRange, rayMarchRange);

    float2 innerIntersections;
    float2 outerIntersections;

    int innerCount = RaySphereIntersection(originWS, direction, _LowestCloudAltitude, innerIntersections);
    int outerCount = RaySphereIntersection(originWS, direction, _HighestCloudAltitude, outerIntersections);

    if (outerCount == 0)
    {
        return false;
    }

    rayMarchRange.start = innerCount > 0 ? innerIntersections.x : outerIntersections.x;
    rayMarchRange.end = outerIntersections.x;

    if (rayMarchRange.end <= rayMarchRange.start)
    {
        rayMarchRange.end = outerIntersections.y;
    }

    return rayMarchRange.end > rayMarchRange.start;

#endif
}
//近距离密度淡入
half DensityFadeValue(float distanceToCamera)
{
    return saturate((distanceToCamera - _FadeInStart) * rcp(max(_FadeInDistance, 0.0001)));
}
//远处侵蚀噪声Mip
float ErosionMipOffset(float distanceToCamera)
{
    float distanceRatio = saturate((distanceToCamera - MIN_EROSION_DISTANCE) * rcp(MAX_EROSION_DISTANCE - MIN_EROSION_DISTANCE));
    return lerp(0.0, 4.0, distanceRatio);
}
//归一化云层高度
float EvaluateNormalizedCloudHeight(float3 positionPS)
{
    return saturate(RangeRemap(_LowestCloudAltitude, _HighestCloudAltitude, length(positionPS)));
}

float3 AnimateShapeNoisePosition(float3 positionPS)
{
    positionPS.y += positionPS.x / 3.0 + positionPS.z / 7.0;

    float3 horizontalDisplacement = float3(_WindVector.x, 0.0, _WindVector.y) * _MediumWindSpeed;
    float3 verticalDisplacement = float3(0.0, _VerticalShapeWindDisplacement, 0.0);

    return positionPS + horizontalDisplacement + verticalDisplacement;
}

float3 AnimateErosionNoisePosition(float3 positionPS)
{
    float3 horizontalDisplacement = float3(_WindVector.x, 0.0, _WindVector.y) * _SmallWindSpeed;
    float3 verticalDisplacement = float3(0.0, _VerticalErosionWindDisplacement, 0.0);

    return positionPS + horizontalDisplacement + verticalDisplacement;
}
//Weather Map数据
void GetCloudCoverageData(float3 positionPS, out CloudCoverageData data)
{
    half4 cloudMapData = half4(0.9, 0.0, 0.25, 1.0);

    data.coverage = cloudMapData.x;
    data.rainClouds = cloudMapData.y;
    data.cloudType = cloudMapData.z;
    data.maxCloudHeight = cloudMapData.w;
}
//密度重映射
half DensityRemap(half value, half oldMinimum, half oldMaximum, half newMinimum, half newMaximum)
{
    half normalizedValue = (value - oldMinimum) * rcp(max(oldMaximum - oldMinimum, 0.0001h));
    return normalizedValue * (newMaximum - newMinimum) + newMinimum;
}

half PowderEffect(half cloudDensity, half cosAngle, half intensity)
{
    half powder = 1.0h - exp(-cloudDensity * 4.0h);
    powder = saturate(powder * 2.0h);

    half backLighting = smoothstep(0.5h, -0.5h, cosAngle);
    half powderLighting = lerp(1.0h, powder, backLighting);

    return lerp(1.0h, powderLighting, intensity);
}

void EvaluateCloudProperties(float3 positionPS, float noiseMipOffset, float erosionMipOffset, bool cheapVersion, bool lightSampling, out CloudProperties properties)
{
    ZERO_INITIALIZE(CloudProperties, properties);

#ifndef _LOCAL_VOLUMETRIC_CLOUDS

    if (positionPS.y < _EarthRadius)
    {
        return;
    }

#endif

    properties.ambientOcclusion = 1.0h;
    properties.height = EvaluateNormalizedCloudHeight(positionPS);

#ifndef _LOCAL_VOLUMETRIC_CLOUDS

    positionPS.xz += _WorldSpaceCameraPos.xz;

#endif

    float3 shapePosition = AnimateShapeNoisePosition(positionPS);
    float3 shapeCoordinates = shapePosition.xzy * (_ShapeScale / NOISE_TEXTURE_NORMALIZATION_FACTOR);
    shapeCoordinates -= float3(_ShapeNoiseOffset.x, _ShapeNoiseOffset.y, _VerticalShapeNoiseOffset);
    shapeCoordinates += properties.height * float3(_WindDirection.x, _WindDirection.y, 0.0) * _AltitudeDistortion;

    half lowFrequencyNoise = SAMPLE_TEXTURE3D_LOD(_Worley128RGBA, s_trilinear_repeat_sampler, shapeCoordinates, noiseMipOffset).r;

    CloudCoverageData coverageData;
    GetCloudCoverageData(positionPS, coverageData);

    if (coverageData.coverage <= CLOUD_DENSITY_THRESHOLD || coverageData.maxCloudHeight < properties.height)
    {
        return;
    }

    half3 densityErosionAO = SAMPLE_TEXTURE2D_LOD(_CloudCurveTexture, s_linear_repeat_sampler, half2(0.0h, properties.height), 0.0).xyz;

    half shapeFactor = lerp(0.1h, 1.0h, _ShapeFactor) * densityErosionAO.y;
    half erosionFactor = _ErosionFactor * densityErosionAO.y;

#if defined(_CLOUDS_MICRO_EROSION)

    half microErosionFactor = _MicroErosionFactor * densityErosionAO.y;

#endif

    lowFrequencyNoise = lerp(1.0h, lowFrequencyNoise, shapeFactor);

    half densityThreshold = 1.0h - densityErosionAO.x * coverageData.coverage * (1.0h - shapeFactor);
    half cloudDensity = saturate(DensityRemap(lowFrequencyNoise, densityThreshold, 1.0h, 0.0h, 1.0h));
    cloudDensity *= coverageData.coverage * coverageData.coverage;

    properties.ambientOcclusion = densityErosionAO.z;
    properties.sigmaT = lerp(0.04h, 0.12h, coverageData.rainClouds);

    half ambientOcclusionBlend = saturate(1.0h - max(erosionFactor, shapeFactor) * 0.5h);
    properties.ambientOcclusion = lerp(1.0h, properties.ambientOcclusion, ambientOcclusionBlend);

    if (!cheapVersion)
    {
        float3 erosionCoordinates = AnimateErosionNoisePosition(positionPS);
        erosionCoordinates *= _ErosionScale / NOISE_TEXTURE_NORMALIZATION_FACTOR;

        half erosionNoise = 1.0h - SAMPLE_TEXTURE3D_LOD(_ErosionNoise, s_linear_repeat_sampler, erosionCoordinates, CLOUD_DETAIL_MIP_OFFSET + erosionMipOffset).r;
        erosionNoise = lerp(0.0h, erosionNoise, erosionFactor * 0.75h * coverageData.coverage);

        properties.ambientOcclusion = saturate(properties.ambientOcclusion - sqrt(max(erosionNoise * _ErosionOcclusion, 0.0h)));
        cloudDensity = DensityRemap(cloudDensity, erosionNoise, 1.0h, 0.0h, 1.0h);

#if defined(_CLOUDS_MICRO_EROSION)

        float3 microCoordinates = AnimateErosionNoisePosition(positionPS);
        microCoordinates *= _MicroErosionScale / NOISE_TEXTURE_NORMALIZATION_FACTOR;

        half microNoise = 1.0h - SAMPLE_TEXTURE3D_LOD(_ErosionNoise, s_linear_repeat_sampler, microCoordinates, CLOUD_DETAIL_MIP_OFFSET + erosionMipOffset).r;
        microNoise = lerp(0.0h, microNoise, microErosionFactor * 0.5h * coverageData.coverage);

        cloudDensity = DensityRemap(cloudDensity, microNoise, 1.0h, 0.0h, 1.0h);

#endif
    }

    if (lightSampling)
    {
        cloudDensity -= erosionFactor * 0.1h;

#if defined(_CLOUDS_MICRO_EROSION)

        cloudDensity -= microErosionFactor * 0.15h;

#endif
    }

    cloudDensity = max(cloudDensity, 0.0h);
    properties.density = cloudDensity * _DensityMultiplier;
}
//太阳方向透射率
half3 EvaluateSunTransmittance(float3 positionPS, half3 sunDirection, PHASE_FUNCTION_STRUCTURE phaseFunction)
{
    float totalLightDistance;
    half3 transmittance = 0.0h;

    if (!ExitCloudVolume(positionPS, sunDirection, _HighestCloudAltitude, totalLightDistance))
    {
        return transmittance;
    }

    totalLightDistance = clamp(totalLightDistance, 0.0, _NumLightSteps * LIGHT_STEP_MAXIMAL_SIZE);
    totalLightDistance += 5.0;

    float intervalSize = totalLightDistance * rcp(max((float)_NumLightSteps, 1.0));
    float opticalDepth = 0.0;

    for (int stepIndex = 0; stepIndex < (int)_NumLightSteps; stepIndex++)
    {
        float sampleDistance = intervalSize * (0.25 + stepIndex);
        float3 samplePositionPS = positionPS + sunDirection * sampleDistance;

        CloudProperties sampleProperties;
        EvaluateCloudProperties(samplePositionPS, 3.0 * stepIndex * rcp(max((float)_NumLightSteps, 1.0)), 0.0, true, true, sampleProperties);

        opticalDepth += sampleProperties.density * sampleProperties.sigmaT;
    }

    half3 extinction = intervalSize * opticalDepth * _ScatteringTint.xyz;

    for (int octaveIndex = 0; octaveIndex < NUM_MULTI_SCATTERING_OCTAVES; octaveIndex++)
    {
        half scatteringFactor = PositivePow(_MultiScattering, octaveIndex);
        transmittance += exp(-extinction * scatteringFactor) * phaseFunction[octaveIndex] * scatteringFactor;
    }

    return transmittance;
}
//用于近似上半路径的大气积分
float ChapmanUpperApprox(float scaledRadius, float cosTheta)
{
    float cosSquared = cosTheta * cosTheta;
    float numerator = 0.761643 * ((1.0 + 2.0 * scaledRadius) - cosSquared * scaledRadius);
    float denominator = cosTheta * scaledRadius + sqrt(scaledRadius * (1.47721 + 0.273828 * cosSquared * scaledRadius));

    return 0.5 * cosTheta + numerator * rcp(max(denominator, FLT_EPS));
}
//处理接近水平切线方向的情况
float ChapmanHorizontal(float scaledRadius)
{
    float inverseRoot = rsqrt(scaledRadius);
    float root = scaledRadius * inverseRoot;

    return 0.626657 * (inverseRoot + 2.0 * root);
}

#if defined(PHYSICALLY_BASED_SKY)

half _AirScaleHeight;
half _AerosolScaleHeight;
half _AirDensityFalloff;
half _AerosolDensityFalloff;

half3 _AirSeaLevelExtinction;
half _AerosolSeaLevelExtinction;

#define _PlanetaryRadius _EarthRadius

#else

#define _AirScaleHeight 8000.0
#define _AerosolScaleHeight 1200.0
#define _AirDensityFalloff (1.0 / _AirScaleHeight)
#define _AerosolDensityFalloff (1.0 / _AerosolScaleHeight)
#define _PlanetaryRadius _EarthRadius
#define _AirSeaLevelExtinction (half3(5.8, 13.5, 33.1) / 1000000.0)
#define _AerosolSeaLevelExtinction 0.00001

#endif

float3 ComputeAtmosphericOpticalDepth(float radialDistance, float cosTheta, bool aboveHorizon)
{
    float2 densityFalloff = float2(_AirDensityFalloff, _AerosolDensityFalloff);
    float2 scaleHeight = float2(_AirScaleHeight, _AerosolScaleHeight);

    float2 scaledRadius = densityFalloff * radialDistance;
    float2 scaledPlanetRadius = densityFalloff * _PlanetaryRadius;

    float sinTheta = sqrt(saturate(1.0 - cosTheta * cosTheta));

    float2 chapman;
    chapman.x = ChapmanUpperApprox(scaledRadius.x, abs(cosTheta)) * exp(scaledPlanetRadius.x - scaledRadius.x);
    chapman.y = ChapmanUpperApprox(scaledRadius.y, abs(cosTheta)) * exp(scaledPlanetRadius.y - scaledRadius.y);

    if (!aboveHorizon)
    {
        float sinGamma = radialDistance * rcp(_PlanetaryRadius) * sinTheta;
        float cosGamma = sqrt(saturate(1.0 - sinGamma * sinGamma));

        float2 planetChapman;
        planetChapman.x = ChapmanUpperApprox(scaledPlanetRadius.x, cosGamma);
        planetChapman.y = ChapmanUpperApprox(scaledPlanetRadius.y, cosGamma);

        chapman = planetChapman - chapman;
    }
    else if (cosTheta < 0.0)
    {
        float2 tangentRadius = scaledRadius * sinTheta;
        float2 tangentRescale = exp(scaledPlanetRadius - tangentRadius);

        float2 tangentChapman;
        tangentChapman.x = 2.0 * ChapmanHorizontal(tangentRadius.x);
        tangentChapman.y = 2.0 * ChapmanHorizontal(tangentRadius.y);

        chapman = tangentChapman * tangentRescale - chapman;
    }

    float2 opticalDepth = chapman * scaleHeight;

    return opticalDepth.x * _AirSeaLevelExtinction + opticalDepth.y * _AerosolSeaLevelExtinction;
}

half3 EvaluateSunColorAttenuation(float3 positionPS, half3 sunDirection, bool estimatePenumbra = false)
{
    float radialDistance = max(length(positionPS), _PlanetaryRadius);
    float cosTheta = dot(positionPS, sunDirection) * rcp(radialDistance);
    float cosHorizon = ComputeCosineOfHorizonAngle(radialDistance);

    if (cosTheta < cosHorizon)
    {
        return 0.0h;
    }

    float3 opticalDepth = ComputeAtmosphericOpticalDepth(radialDistance, cosTheta, true);
    half3 attenuation = TransmittanceFromOpticalDepth(opticalDepth);

    if (estimatePenumbra)
    {
        half penumbra = saturate((cosTheta - cosHorizon) * rcp(0.0019));
        attenuation *= penumbra;
    }

    return attenuation;
}

half3 EvaluateSunColor(float3 entryPositionPS, float3 exitPositionPS, half3 sunDirection, half3 sunColor, float relativeRayDistance)
{
    half3 entryColor = sunColor * EvaluateSunColorAttenuation(entryPositionPS, sunDirection, true);
    half3 exitColor = sunColor * EvaluateSunColorAttenuation(exitPositionPS, sunDirection, false);

    return lerp(entryColor, exitColor, relativeRayDistance);
}

void EvaluateCloud(CloudProperties cloudProperties, half3 rayDirection, float3 currentPositionPS, float stepSize, float relativeRayDistance, inout VolumetricRayResult volumetricRay)
{
    half extinction = cloudProperties.density * cloudProperties.sigmaT;
    half stepTransmittance = exp(-extinction * stepSize);

    Light mainLight = GetMainLight();
    half cosAngle = dot(rayDirection, mainLight.direction);

    PHASE_FUNCTION_STRUCTURE phaseFunction = 0.0h;

    for (int octaveIndex = 0; octaveIndex < NUM_MULTI_SCATTERING_OCTAVES; octaveIndex++)
    {
        half scatteringFactor = PositivePow(_MultiScattering, octaveIndex);
        half forwardPhase = HenyeyGreensteinPhaseFunction(FORWARD_ECCENTRICITY * scatteringFactor, cosAngle);
        half backwardPhase = HenyeyGreensteinPhaseFunction(-BACKWARD_ECCENTRICITY * scatteringFactor, cosAngle);

        phaseFunction[octaveIndex] = forwardPhase + backwardPhase;
    }

    half powder = PowderEffect(cloudProperties.density, cosAngle, _PowderEffectIntensity);
    half3 sunTransmittance = EvaluateSunTransmittance(currentPositionPS, mainLight.direction, phaseFunction);

    half3 sunLuminance = sunTransmittance * powder;
    half ambientLuminance = cloudProperties.ambientOcclusion;

    half scatteredEnergy = volumetricRay.transmittance - volumetricRay.transmittance * stepTransmittance;

    volumetricRay.scattering += sunLuminance * scatteredEnergy;
    volumetricRay.ambient += ambientLuminance * scatteredEnergy;
    volumetricRay.transmittance *= stepTransmittance;
}

#endif