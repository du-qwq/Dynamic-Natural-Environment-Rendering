#ifndef VEGETATION_ADDITIONAL_LIGHTS_INCLUDED
#define VEGETATION_ADDITIONAL_LIGHTS_INCLUDED

// Indirect draws have no per-object light index list. URP 14 ForwardLights
// packs non-main visible lights in camera-global order; use that index directly.
// Vegetation Light Layers are not supported without reliable rendering-layer data.
#define VEGETATION_MAX_ADDITIONAL_LIGHTS 8
int _VegetationAdditionalLightsCount;

int GetVegetationAdditionalLightsCount()
{
#if USE_FORWARD_PLUS
    // URP's clustered path is separate. Never add this camera-global loop there.
    return 0;
#else
    return min(max(_VegetationAdditionalLightsCount, 0), VEGETATION_MAX_ADDITIONAL_LIGHTS);
#endif
}

Light GetVegetationAdditionalLight(uint lightIndex, float3 positionWS, half4 shadowMask)
{
    Light light = GetAdditionalPerObjectLight((int)lightIndex, positionWS);

#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    half4 occlusionProbeChannels = _AdditionalLightsBuffer[lightIndex].occlusionProbeChannels;
#else
    half4 occlusionProbeChannels = _AdditionalLightsOcclusionProbes[lightIndex];
#endif

    light.shadowAttenuation = AdditionalLightShadow(
        (int)lightIndex,
        positionWS,
        light.direction,
        shadowMask,
        occlusionProbeChannels);
    light.shadowAttenuation = VegetationAdditionalLightShadowAttenuation(light.shadowAttenuation);

#if defined(_LIGHT_COOKIES)
    light.color *= SampleAdditionalLightCookie((int)lightIndex, positionWS);
#endif

    return light;
}

Light GetVegetationAdditionalLight(uint lightIndex, InputData inputData,
    half4 shadowMask, AmbientOcclusionFactor aoFactor)
{
    Light light = GetVegetationAdditionalLight(lightIndex, inputData.positionWS, shadowMask);

#if defined(_SCREEN_SPACE_OCCLUSION) && !defined(_SURFACE_TYPE_TRANSPARENT)
    if (IsLightingFeatureEnabled(DEBUGLIGHTINGFEATUREFLAGS_AMBIENT_OCCLUSION))
        light.color *= aoFactor.directAmbientOcclusion;
#endif

    return light;
}

half3 EvaluateVegetationAdditionalLightsPBR(
    InputData inputData,
    half3 albedo,
    half metallic,
    half3 specular,
    half smoothness,
    half occlusion,
    half alpha)
{
    int lightCount = GetVegetationAdditionalLightsCount();
    if (lightCount == 0)
        return half3(0.0h, 0.0h, 0.0h);

    half brdfAlpha = alpha;
    BRDFData brdfData;
    InitializeBRDFData(albedo, metallic, specular, smoothness, brdfAlpha, brdfData);

    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(
        inputData.normalizedScreenSpaceUV,
        occlusion);

    half3 additionalLighting = half3(0.0h, 0.0h, 0.0h);

    [loop]
    for (int lightIndex = 0; lightIndex < lightCount; ++lightIndex)
    {
        Light light = GetVegetationAdditionalLight(
            (uint)lightIndex,
            inputData,
            shadowMask,
            aoFactor);

        additionalLighting += LightingPhysicallyBased(
            brdfData,
            light,
            inputData.normalWS,
            inputData.viewDirectionWS);
    }

    return additionalLighting;
}

#endif
