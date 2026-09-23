#ifndef VEGETATION_ADDITIONAL_LIGHTS_INCLUDED
#define VEGETATION_ADDITIONAL_LIGHTS_INCLUDED

// DrawMeshInstancedIndirect has no Renderer, so URP cannot build a per-object
// additional-light index list for it. The renderer feature supplies the camera's
// explicit additional-light count and this code indexes URP's camera-global light
// data directly. This deliberately bypasses unity_LightData/unity_LightIndices.
int _VegetationAdditionalLightsCount;

Light GetVegetationAdditionalLight(
    uint lightIndex,
    InputData inputData,
    half4 shadowMask,
    AmbientOcclusionFactor aoFactor)
{
    Light light = GetAdditionalPerObjectLight((int)lightIndex, inputData.positionWS);

#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    half4 occlusionProbeChannels = _AdditionalLightsBuffer[lightIndex].occlusionProbeChannels;
#else
    half4 occlusionProbeChannels = _AdditionalLightsOcclusionProbes[lightIndex];
#endif

    light.shadowAttenuation = AdditionalLightShadow(
        (int)lightIndex,
        inputData.positionWS,
        light.direction,
        shadowMask,
        occlusionProbeChannels);

#if defined(_LIGHT_COOKIES)
    light.color *= SampleAdditionalLightCookie((int)lightIndex, inputData.positionWS);
#endif

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
    int lightCount = max(_VegetationAdditionalLightsCount, 0);
    if (lightCount == 0)
        return half3(0.0h, 0.0h, 0.0h);

    half brdfAlpha = alpha;
    BRDFData brdfData;
    InitializeBRDFData(albedo, metallic, specular, smoothness, brdfAlpha, brdfData);

    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(
        inputData.normalizedScreenSpaceUV,
        occlusion);

    uint meshRenderingLayers = GetMeshRenderingLayer();
    half3 additionalLighting = half3(0.0h, 0.0h, 0.0h);

    [loop]
    for (int lightIndex = 0; lightIndex < lightCount; ++lightIndex)
    {
        Light light = GetVegetationAdditionalLight(
            (uint)lightIndex,
            inputData,
            shadowMask,
            aoFactor);

#ifdef _LIGHT_LAYERS
        if (!IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
            continue;
#endif

        additionalLighting += LightingPhysicallyBased(
            brdfData,
            light,
            inputData.normalWS,
            inputData.viewDirectionWS);
    }

    return additionalLighting;
}

#endif
