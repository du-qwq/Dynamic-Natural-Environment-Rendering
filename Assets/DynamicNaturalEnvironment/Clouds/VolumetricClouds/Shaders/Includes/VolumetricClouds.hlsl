#ifndef URP_VOLUMETRIC_CLOUDS_HLSL
#define URP_VOLUMETRIC_CLOUDS_HLSL

#include "./VolumetricCloudsDefs.hlsl"
#include "./VolumetricCloudsUtilities.hlsl"

// ============================================================
// 宏观云分布
// ============================================================

TEXTURE2D(_CloudMacroCoverageMap);
SAMPLER(sampler_CloudMacroCoverageMap);

half _CloudMacroEnabled;
half _CloudMacroUseMap;
half _CloudMacroCoverage;
float _CloudMacroScale;
half _CloudMacroSoftness;
float4 _CloudMacroOffset;
half _CloudMacroWindMultiplier;

// ============================================================
// 远距离云淡出
// ============================================================

half _CloudFarFadeEnabled;
float _CloudFarFadeStart;
float _CloudFarFadeEnd;
half _CloudFarRetention;
float _CloudMaxRenderDistance;

float4 _DNEAtmosphereCloudSunTint;

// 获取宏观云分布使用的世界XZ坐标。
float2 GetMacroCloudWorldXZ(float3 positionPS)
{
#ifndef _LOCAL_VOLUMETRIC_CLOUDS
    return positionPS.xz + _WorldSpaceCameraPos.xz;
#else
    return positionPS.xz;
#endif
}

// 计算宏观云覆盖率。
// 0 = 晴空。
// 1 = 完整云区。
half EvaluateMacroCloudCoverage(float3 positionPS)
{
    if (_CloudMacroEnabled <= 0.5h) return 1.0h;

    float safeScale = max(_CloudMacroScale, 1000.0);
    float2 worldXZ = GetMacroCloudWorldXZ(positionPS);
    float2 windOffset = _WindVector.xy * _CloudMacroWindMultiplier;
    float2 coverageUV = (worldXZ + windOffset) / safeScale + _CloudMacroOffset.xy;

    half rawCoverage;

    if (_CloudMacroUseMap > 0.5h)
    {
        rawCoverage = SAMPLE_TEXTURE2D_LOD(_CloudMacroCoverageMap, sampler_CloudMacroCoverageMap, coverageUV, 0.0).r;
    }
    else
    {
        float3 macroCoordinates = float3(coverageUV.x, 0.37, coverageUV.y);
        half4 macroNoise = SAMPLE_TEXTURE3D_LOD(_Worley128RGBA, s_trilinear_repeat_sampler, macroCoordinates, 1.5);
        rawCoverage = saturate(macroNoise.r * 0.82h + macroNoise.g * 0.18h);
    }

    half threshold = 1.0h - _CloudMacroCoverage;
    half edgeSoftness = max(_CloudMacroSoftness, 0.001h);

    return smoothstep(threshold - edgeSoftness, threshold + edgeSoftness, rawCoverage);
}

// 根据当前采样点距离摄像机的距离计算远处云衰减。
// 近处返回1，远处逐渐下降到 farRetention。
half EvaluateCloudFarFade(float distanceFromCamera)
{
    if (_CloudFarFadeEnabled <= 0.5h) return 1.0h;

    float fadeStart = max(_CloudFarFadeStart, 0.0);
    float fadeEnd = max(_CloudFarFadeEnd, fadeStart + 1.0);
    half fade01 = saturate((distanceFromCamera - fadeStart) / (fadeEnd - fadeStart));
    fade01 = fade01 * fade01 * (3.0h - 2.0h * fade01);

    return lerp(1.0h, _CloudFarRetention, fade01);
}

CloudRay BuildCloudsRay(float2 screenUV, float depth, half3 invViewDirWS, bool isOccluded)
{
    CloudRay ray;

#ifdef _LOCAL_VOLUMETRIC_CLOUDS
    ray.originWS = GetCameraPositionWS();
#else
    ray.originWS = float3(0.0, 0.0, 0.0);
#endif

    ray.direction = invViewDirWS;

#ifdef _LOCAL_VOLUMETRIC_CLOUDS
    float distance = LinearEyeDepth(depth, _ZBufferParams) * rcp(dot(ray.direction, -UNITY_MATRIX_V[2].xyz));
    ray.maxRayLength = lerp(MAX_SKYBOX_VOLUMETRIC_CLOUDS_DISTANCE, distance, isOccluded);
#else
    ray.maxRayLength = MAX_SKYBOX_VOLUMETRIC_CLOUDS_DISTANCE;
#endif

    // ------------------------------------------------------------
    // 最大云渲染距离。
    //
    // 原版允许一直追踪到约200km。
    // 当前环境镜头不需要这么远，因此直接限制Raymarch上限。
    // ------------------------------------------------------------

    if (_CloudFarFadeEnabled > 0.5h)
    {
        ray.maxRayLength = min(ray.maxRayLength, max(_CloudMaxRenderDistance, 1000.0));
    }

    ray.integrationNoise = GenerateRandomFloat(screenUV);

    return ray;
}

// 沿射线采样
VolumetricRayResult TraceVolumetricRay(CloudRay cloudRay)
{
    VolumetricRayResult volumetricRay;
    volumetricRay.scattering = 0.0;
    volumetricRay.ambient = 0.0;
    volumetricRay.transmittance = 1.0;
    volumetricRay.meanDistance = FLT_MAX;
    volumetricRay.invalidRay = true;

    RayMarchRange rayMarchRange;

    if (GetCloudVolumeIntersection(cloudRay.originWS, cloudRay.direction, rayMarchRange))
    {
        if (cloudRay.maxRayLength >= rayMarchRange.start)
        {
            volumetricRay.meanDistance = 0.0;

            float totalDistance = min(rayMarchRange.end, cloudRay.maxRayLength) - rayMarchRange.start;

            float stepS = min(totalDistance / (float)_NumPrimarySteps, _MaxStepSize);
            totalDistance = stepS * _NumPrimarySteps;

            float3 rayMarchStartPS = ConvertToPS(cloudRay.originWS) + rayMarchRange.start * cloudRay.direction;
            float3 rayMarchEndPS = rayMarchStartPS + totalDistance * cloudRay.direction;

            int currentIndex = 0;

            float meanDistanceDivider = 0.0;

            float currentDistance = cloudRay.integrationNoise;
            float3 currentPositionWS = cloudRay.originWS + (rayMarchRange.start + currentDistance) * cloudRay.direction;

            bool activeSampling = true;
            int sequentialEmptySamples = 0;

            while (currentIndex < (int)_NumPrimarySteps && currentDistance < totalDistance)
            {
                float sampleDistanceFromCamera = rayMarchRange.start + currentDistance;

                float densityAttenuationValue = DensityFadeValue(sampleDistanceFromCamera);
                float erosionMipOffset = ErosionMipOffset(sampleDistanceFromCamera);

                float3 currentPositionPS = ConvertToPS(currentPositionWS);

                // --------------------------------------------------------
                // 宏观天气分布。
                // 决定当前位置是大云区还是晴空区。
                // --------------------------------------------------------

                half macroCoverage = EvaluateMacroCloudCoverage(currentPositionPS);

                // --------------------------------------------------------
                // 远处云淡出。
                // 20~55km等区间内逐渐降低云密度。
                // --------------------------------------------------------

                half farFade = EvaluateCloudFarFade(sampleDistanceFromCamera);

                // 宏观分布已经判定为晴空，或远距离已经完全淡出时，
                // 不再执行完整云密度采样。
                half macroDensityMask = macroCoverage * farFade;

                if (activeSampling)
                {
                    if (macroDensityMask <= CLOUD_DENSITY_THRESHOLD)
                    {
                        sequentialEmptySamples++;

                        if (sequentialEmptySamples >= EMPTY_STEPS_BEFORE_LARGE_STEPS) activeSampling = false;

                        float relativeStepSize = lerp(cloudRay.integrationNoise, 1.0, saturate(currentIndex));
                        currentPositionWS += cloudRay.direction * stepS * relativeStepSize;
                        currentDistance += stepS * relativeStepSize;
                        currentIndex++;

                        continue;
                    }

                    CloudProperties properties;

                    EvaluateCloudProperties(currentPositionPS, 0.0, erosionMipOffset, false, false, properties);

                    properties.density *= macroDensityMask;
                    properties.density *= densityAttenuationValue;

                    if (properties.density > CLOUD_DENSITY_THRESHOLD)
                    {
                        half transmitanceXdensity = volumetricRay.transmittance * properties.density;

                        volumetricRay.meanDistance += sampleDistanceFromCamera * transmitanceXdensity;
                        meanDistanceDivider += transmitanceXdensity;

                        EvaluateCloud(
                            properties,
                            cloudRay.direction,
                            currentPositionPS,
                            stepS,
                            currentDistance / totalDistance,
                            volumetricRay
                        );

                        if (volumetricRay.transmittance < 0.003)
                        {
                            volumetricRay.transmittance = 0.0;
                            break;
                        }

                        sequentialEmptySamples = 0;
                    }
                    else
                    {
                        sequentialEmptySamples++;
                    }

                    if (sequentialEmptySamples == EMPTY_STEPS_BEFORE_LARGE_STEPS) activeSampling = false;

                    float relativeStepSize = lerp(cloudRay.integrationNoise, 1.0, saturate(currentIndex));

                    currentPositionWS += cloudRay.direction * stepS * relativeStepSize;
                    currentDistance += stepS * relativeStepSize;
                }
                else
                {
                    // 如果宏观层已经明确为晴空，直接大步跳过。
                    if (macroDensityMask <= CLOUD_DENSITY_THRESHOLD)
                    {
                        currentPositionWS += cloudRay.direction * stepS * 2.0;
                        currentDistance += stepS * 2.0;
                        currentIndex++;

                        continue;
                    }

                    CloudProperties properties;

                    EvaluateCloudProperties(currentPositionPS, 1.0, 0.0, true, false, properties);

                    properties.density *= macroDensityMask;
                    properties.density *= densityAttenuationValue;

                    if (properties.density < CLOUD_DENSITY_THRESHOLD)
                    {
                        currentPositionWS += cloudRay.direction * stepS * 2.0;
                        currentDistance += stepS * 2.0;
                    }
                    else
                    {
                        currentPositionWS -= cloudRay.direction * stepS;
                        currentDistance -= stepS;
                        currentIndex -= 1;

                        activeSampling = true;
                        sequentialEmptySamples = 0;
                    }
                }

                currentIndex++;
            }

            if (volumetricRay.meanDistance != 0.0 && meanDistanceDivider > 0.00001)
            {
                volumetricRay.invalidRay = false;

                volumetricRay.meanDistance /= meanDistanceDivider;
                volumetricRay.meanDistance = min(volumetricRay.meanDistance, cloudRay.maxRayLength);

                float3 currentPositionPS = ConvertToPS(cloudRay.originWS) + volumetricRay.meanDistance * cloudRay.direction;

                float relativeHeight = EvaluateNormalizedCloudHeight(currentPositionPS);

                Light sun = GetMainLight();

                #ifdef _PHYSICALLY_BASED_SUN
                half3 sunColor = _SunColor * EvaluateSunColorAttenuation(currentPositionPS, sun.direction, true) * _SunLightDimmer;
                #else
                half3 sunColor = sun.color * PI * _SunLightDimmer;
                #endif

                sunColor *= _DNEAtmosphereCloudSunTint.rgb;

#ifdef _CLOUDS_AMBIENT_PROBE
                half3 ambientTermTop = SAMPLE_TEXTURECUBE_LOD(
                    _VolumetricCloudsAmbientProbe,
                    sampler_VolumetricCloudsAmbientProbe,
                    half3(0.0, 1.0, 0.0),
                    4.0
                ).rgb;

                half3 ambientTermBottom = SAMPLE_TEXTURECUBE_LOD(
                    _VolumetricCloudsAmbientProbe,
                    sampler_VolumetricCloudsAmbientProbe,
                    half3(0.0, -1.0, 0.0),
                    4.0
                ).rgb;
#else
                half3 ambientTermTop = EvaluateVolumetricCloudsAmbientProbe(half3(0.0, 1.0, 0.0));
                half3 ambientTermBottom = EvaluateVolumetricCloudsAmbientProbe(half3(0.0, -1.0, 0.0));
#endif

                half3 ambient = max(
                    0,
                    lerp(ambientTermBottom, ambientTermTop, relativeHeight) * _AmbientProbeDimmer
                );

                volumetricRay.scattering = sunColor * volumetricRay.scattering;
                volumetricRay.scattering += ambient * volumetricRay.ambient;
            }
        }
    }

    return volumetricRay;
}

#endif