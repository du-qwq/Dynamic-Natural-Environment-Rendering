#ifndef VEGETATION_RECEIVE_SHADOWS_INCLUDED
#define VEGETATION_RECEIVE_SHADOWS_INCLUDED

// Set per species by VegetationRenderer's MaterialPropertyBlock.
int _VegetationReceiveShadows;

half VegetationMainLightShadowAttenuation(half attenuation)
{
    return _VegetationReceiveShadows != 0 ? attenuation : 1.0h;
}

half VegetationAdditionalLightShadowAttenuation(half attenuation)
{
    return _VegetationReceiveShadows != 0 ? attenuation : 1.0h;
}

// URP's UniversalFragmentPBR obtains the main light internally. Restore only
// its realtime main-light shadow loss while retaining URP's PBR and GI path.
half3 VegetationUnshadowedMainLightPBRDelta(InputData inputData, half3 albedo,
    half metallic, half3 specular, half smoothness, half occlusion, half alpha)
{
    if (_VegetationReceiveShadows != 0) return half3(0.0h, 0.0h, 0.0h);

    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = albedo;
    surfaceData.metallic = metallic;
    surfaceData.specular = specular;
    surfaceData.smoothness = smoothness;
    surfaceData.occlusion = occlusion;
    surfaceData.alpha = alpha;
    surfaceData.clearCoatMask = 0.0h;
    surfaceData.clearCoatSmoothness = 1.0h;

    BRDFData brdfData;
    InitializeBRDFData(surfaceData, brdfData);
    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surfaceData);
    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);
    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);

#ifdef _LIGHT_LAYERS
    if (!IsMatchingLightLayer(mainLight.layerMask, GetMeshRenderingLayer()))
        return half3(0.0h, 0.0h, 0.0h);
#endif

    half shadowLoss = 1.0h - mainLight.shadowAttenuation;
    mainLight.shadowAttenuation = 1.0h;
    return LightingPhysicallyBased(brdfData, mainLight, inputData.normalWS,
        inputData.viewDirectionWS) * shadowLoss;
}

#endif
