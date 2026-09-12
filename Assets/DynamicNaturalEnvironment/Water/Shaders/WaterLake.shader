Shader "Custom/WaterStylizedOcean"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 0.9)
        _DepthScale("Depth Scale", Float) = 1
        
        [NoScaleOffset] _NormalTex("Normal Tex", 2D) = "bump" {}
        _NormalTiling("Normal Tiling", Float) = 0.85
        _NormalScale("Normal Scale", Range(0, 2)) = 0.75
        _NormalRefract("Normal Refract", Range(0, 1)) = 0.025
        _WaveXSpeed("Wave X Speed", Float) = 0.05
        _WaveYSpeed("Wave Y Speed", Float) = 0.08
        
        _SpecularPower("Specular Power", Range(1, 256)) = 96
        _SpecularStrength("Specular Strength", Range(0, 5)) = 1.4
        
        _ShallowColor("Shallow Color", Color) = (0.045, 0.58, 0.56, 0.72)
        _DeepColor("Deep Color", Color) = (0.005, 0.12, 0.19, 1)

        _DepthColorRange("Depth Color Range", Float) = 4
        _AlphaDepthRange("Alpha Depth Range", Float) = 2.2
        _MinAlpha("Minimum Alpha", Range(0, 1)) = 0.55
        _AbsorptionColor("Absorption Color", Color) = (0.72, 0.30, 0.11, 1)
        _AbsorptionStrength("Absorption Strength", Range(0, 4)) = 0.72
        _MaxAlpha("Maximum Alpha", Range(0, 1)) = 0.96
        _ScatterColor("Scatter Color", Color) = (0.025, 0.42, 0.36, 1)
        _ScatterStrength("Scatter Strength", Range(0, 3)) = 0.85
        _ShoreScatterStrength("Shore Scatter Strength", Range(0, 3)) = 0.65
        _RefractionWeight("Refraction Weight", Range(0, 1)) = 0.32
        
        _FoamTex("Foam Tex", 2D) = "white" {}
        _FoamColor("Foam Color", Color) = (1, 1, 1, 1)

        _FoamRange("Foam Range", Float) = 0.9
        _FoamSoftness("Foam Softness", Float) = 0.75
        _FoamSpeed("Foam Speed", Float) = 0.15
        _FoamTiling("Foam Tiling", Float) = 1
        _FoamStrength("Foam Strength", Range(0, 3)) = 1.25
        _FoamDistortStrength("Foam Distort Strength", Range(0, 0.2)) = 0.04
        _FoamContrast("Foam Contrast", Range(0.5, 8)) = 3
        _FoamCutoff("Foam Cutoff", Range(0, 1)) = 0.45
        
        _RefractStrength("Refract Strength", Range(0, 0.2)) = 0.014
        
        _FresnelPower("Fresnel Power", Range(1, 8)) = 4
        _FresnelStrength("Fresnel Strength", Range(0, 2)) = 0.9
        _ReflectionTint("Reflection Tint", Color) = (0.72, 0.86, 0.96, 1)
        _ReflectionStrength("Reflection Strength", Range(0, 2)) = 1.1
        _EnvReflectionRoughness("Env Reflection Roughness", Range(0, 1)) = 0.2
        
        _PlanarReflectionTex("Planar Reflection Tex", 2D) = "black" {}
        _PlanarReflectionDistort("Planar Reflection Distort", Range(0, 0.1)) = 0.012
        _PlanarReflectionBlend("Planar Reflection Blend", Range(0, 1)) = 0.9
        _ReflectionFloor("Reflection Floor", Range(0, 0.5)) = 0.08
        
        _WaveA("Wave A (dirX, dirZ, steepness, wavelength)", Vector) = (1, 0, 0.18, 14)
        _WaveB("Wave B (dirX, dirZ, steepness, wavelength)", Vector) = (0.65, 0.75, 0.10, 7)

        _WaveSpeedA("Wave Speed A", Float) = 0.85
        _WaveSpeedB("Wave Speed B", Float) = 1.15
        
        _CrestFoamColor("Crest Foam Color", Color) = (1, 1, 1, 1)
        _CrestThreshold("Crest Threshold", Float) = 0.05
        _CrestSoftness("Crest Softness", Float) = 0.08
        _CrestSlopeStrength("Crest Slope Strength", Float) = 2
        _CrestFoamStrength("Crest Foam Strength", Range(0, 3)) = 0.7
        _CrestFoamTiling("Crest Foam Tiling", Float) = 2
        _CrestFoamSpeed("Crest Foam Speed", Float) = 0.2
        
        _CausticsTex("Caustics Tex", 2D) = "white" {}
        _CausticsColor("Caustics Color", Color) = (1, 1, 0.85, 1)
        _CausticsTiling("Caustics Tiling", Float) = 1.2
        _CausticsSpeed("Caustics Speed", Float) = 0.25
        _CausticsStrength("Caustics Strength", Range(0, 8)) = 1.5
        _CausticsDepthRange("Caustics Depth Range", Float) = 3.0
        _CausticsSharpness("Caustics Sharpness", Range(0.5, 4)) = 1.2
        
        _CausticsDispersion("Caustics Dispersion", Range(0, 0.02)) = 0.003
        _CausticsChromaticStrength("Caustics Chromatic Strength", Range(0, 1)) = 0.6
        
        _RippleColor("Ripple Color", Color) = (1, 1, 1, 1)
        _RippleColorStrength("Ripple Color Strength", Float) = 0.35
        
        _RippleFieldTex("Ripple Field Tex", 2D) = "black" {}
        _RippleFieldNormalStrength("Ripple Field Normal Strength", Float) = 1.2
        _RippleFieldColorStrength("Ripple Field Color Strength", Float) = 0.25
        _RippleFieldRefractStrength("Ripple Field Refract Strength", Float) = 0.03
        _RippleFieldFoamStrength("Ripple Field Foam Strength", Float) = 2.0
        _RippleFieldShoreFoamInfluence("Ripple Field Shore Foam Influence", Float) = 0.6
        _RippleFieldCrestFoamInfluence("Ripple Field Crest Foam Influence", Float) = 0.4
        _WakeFoamColorStrength("Wake Foam Color Strength", Float) = 1.2
        
        _RippleAreaCenterWS("Ripple Area Center WS", Vector) = (0,0,0,0)
        _RippleAreaSize("Ripple Area Size", Vector) = (20,20,0,0)
        _RippleNormalTex("Ripple Normal Tex", 2D) = "bump" {}
        _RippleNormalTexStrength("Ripple Normal Tex Strength", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"

            TEXTURE2D(_NormalTex);
            SAMPLER(sampler_NormalTex);

            TEXTURE2D(_FoamTex);
            SAMPLER(sampler_FoamTex);

            TEXTURE2D(_CausticsTex);
            SAMPLER(sampler_CausticsTex);

            TEXTURE2D(_RippleFieldTex);
            SAMPLER(sampler_RippleFieldTex);

            TEXTURE2D(_RippleNormalTex);
            SAMPLER(sampler_RippleNormalTex);

            TEXTURE2D(_PlanarReflectionTex);
            SAMPLER(sampler_PlanarReflectionTex);

            float4 _RippleFieldTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _DepthScale;

                float4 _NormalTex_ST;
                float _NormalTiling;
                float _NormalScale;
                float _NormalRefract;
                float _WaveXSpeed;
                float _WaveYSpeed;

                float _SpecularPower;
                float _SpecularStrength;

                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthColorRange;
                float _AlphaDepthRange;
                float _MinAlpha;
                float _MaxAlpha;
                half4 _AbsorptionColor;
                float _AbsorptionStrength;
                half4 _ScatterColor;
                float _ScatterStrength;
                float _ShoreScatterStrength;
                float _RefractionWeight;

                float4 _FoamTex_ST;
                half4 _FoamColor;
                float _FoamRange;
                float _FoamSoftness;
                float _FoamSpeed;
                float _FoamTiling;
                float _FoamStrength;
                float _FoamDistortStrength;
                float _FoamContrast;
                float _FoamCutoff;

                float _RefractStrength;

                float _FresnelPower;
                float _FresnelStrength;
                half4 _ReflectionTint;
                float _ReflectionStrength;
                float _EnvReflectionRoughness;

                float _PlanarReflectionDistort;
                float _PlanarReflectionBlend;
                float _ReflectionFloor;

                float4 _WaveA;
                float4 _WaveB;
                float _WaveSpeedA;
                float _WaveSpeedB;

                half4 _CrestFoamColor;
                float _CrestThreshold;
                float _CrestSoftness;
                float _CrestSlopeStrength;
                float _CrestFoamStrength;
                float _CrestFoamTiling;
                float _CrestFoamSpeed;

                float4 _CausticsTex_ST;
                half4 _CausticsColor;
                float _CausticsTiling;
                float _CausticsSpeed;
                float _CausticsStrength;
                float _CausticsDepthRange;
                float _CausticsSharpness;

                float _CausticsDispersion;
                float _CausticsChromaticStrength;

                half4 _RippleColor;
                float _RippleColorStrength;

                float4 _RippleFieldTex_ST;
                float _RippleFieldNormalStrength;
                float _RippleFieldColorStrength;
                float _RippleFieldRefractStrength;
                float _RippleFieldFoamStrength;
                float _RippleFieldShoreFoamInfluence;
                float _RippleFieldCrestFoamInfluence;
                float _WakeFoamColorStrength;
                float4 _RippleAreaCenterWS;
                float4 _RippleAreaSize;
                float _RippleNormalTexStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float2 uv          : TEXCOORD1;

                float3 tangentWS   : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
                float3 normalWS    : TEXCOORD4;
                float waveHeight   : TEXCOORD5;
            };

            #define TWO_PI 6.28318530718

            void ApplyGerstnerWave(
                float4 wave,
                float speed,
                float3 baseTangentWS,
                float3 baseBinormalWS,
                inout float3 positionWS,
                inout float3 tangentWS,
                inout float3 binormalWS
            )
            {
                float2 dir = normalize(wave.xy);
                float steepness = wave.z;
                float wavelength = max(wave.w, 0.001);

                float k = TWO_PI / wavelength;
                float a = steepness / k;

                float f = k * (dot(dir, positionWS.xz) - speed * _Time.y);

                float sinF = sin(f);
                float cosF = cos(f);

                positionWS.x += dir.x * (a * cosF);
                positionWS.y += a * sinF;
                positionWS.z += dir.y * (a * cosF);

                float du = dot(dir, normalize(baseTangentWS.xz));
                float dv = dot(dir, normalize(baseBinormalWS.xz));

                tangentWS += float3(
                    -dir.x * steepness * sinF * du,
                     steepness * cosF * du,
                    -dir.y * steepness * sinF * du
                );

                binormalWS += float3(
                    -dir.x * steepness * sinF * dv,
                     steepness * cosF * dv,
                    -dir.y * steepness * sinF * dv
                );
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float baseHeightWS = OUT.positionWS.y;

                float3 baseNormalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                float3 baseTangentWS = normalize(TransformObjectToWorldDir(IN.tangentOS.xyz));

                float tangentSign = IN.tangentOS.w;
                float3 baseBinormalWS = normalize(cross(baseNormalWS, baseTangentWS) * tangentSign);

                float3 tangentWS = baseTangentWS;
                float3 binormalWS = baseBinormalWS;

                ApplyGerstnerWave(_WaveA, _WaveSpeedA, baseTangentWS, baseBinormalWS, OUT.positionWS, tangentWS, binormalWS);
                ApplyGerstnerWave(_WaveB, _WaveSpeedB, baseTangentWS, baseBinormalWS, OUT.positionWS, tangentWS, binormalWS);

                // 大尺度湖面使用世界空间 XZ 作为表面纹理坐标，避免 Plane 缩放后 Normal/Foam 被拉伸。
                OUT.uv = OUT.positionWS.xz;

                float3 normalWS = normalize(cross(binormalWS, tangentWS));
                tangentWS = normalize(tangentWS);
                binormalWS = normalize(cross(normalWS, tangentWS));

                OUT.tangentWS = tangentWS;
                OUT.bitangentWS = binormalWS;
                OUT.normalWS = normalWS;

                OUT.waveHeight = OUT.positionWS.y - baseHeightWS;
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);

                return OUT;
            }

            float3 SampleFlowNormalTS(float2 uv)
            {
                float2 uv0 = uv * _NormalTiling;

                float2 flow1 = uv0 + float2(_WaveXSpeed * _Time.y, 0);
                float2 flow2 = float2(uv0.y, uv0.x) + float2(_WaveYSpeed * _Time.y, 0);

                float4 offsetSample =
                    (SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, flow1) +
                     SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, flow2)) * 0.5;

                float3 offsetNormal = UnpackNormal(offsetSample);
                float2 flowOffset = offsetNormal.xy * _NormalRefract;

                float3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uv0 + flowOffset));
                float3 n2 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uv0 - flowOffset));

                float3 normalTS = normalize(n1 + n2);

                normalTS.xy *= _NormalScale;
                normalTS.z = sqrt(1.0 - saturate(dot(normalTS.xy, normalTS.xy)));

                return normalTS;
            }

            float3 TransformTangentToWorldCustom(float3 normalTS, float3 tangentWS, float3 bitangentWS, float3 normalWS)
            {
                float3 worldNormal = normalize(
                    tangentWS   * normalTS.x +
                    bitangentWS * normalTS.y +
                    normalWS    * normalTS.z
                );

                return worldNormal;
            }

            // float SampleCaustics(float3 positionWS)
            // {
            //     float2 uv1 = positionWS.xz * _CausticsTiling;
            //     float2 uv2 = positionWS.xz * (_CausticsTiling * 1.31);
            //
            //     uv1 += float2(_CausticsSpeed * _Time.y, _CausticsSpeed * 0.35 * _Time.y);
            //     uv2 += float2(-_CausticsSpeed * 0.27 * _Time.y, _CausticsSpeed * 0.83 * _Time.y);
            //
            //     float c1 = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, uv1).r;
            //     float c2 = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, uv2).r;
            //
            //     float caustics = saturate(c1 + c2 - 0.65);
            //     caustics = pow(caustics, max(_CausticsSharpness, 0.0001));
            //
            //     return caustics;
            // }

            float SampleCausticsMono(float2 uv1, float2 uv2)
            {
                float c1 = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, uv1).r;
                float c2 = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, uv2).r;

                float caustics = saturate(c1 + c2 - 0.65);
                caustics = pow(caustics, max(_CausticsSharpness, 0.0001));

                return caustics;
            }

            float3 SampleCausticsRGB(float3 positionWS, float2 dispersionDir)
            {
                float2 uv1 = positionWS.xz * _CausticsTiling;
                float2 uv2 = positionWS.xz * (_CausticsTiling * 1.31);

                uv1 += float2(_CausticsSpeed * _Time.y, _CausticsSpeed * 0.35 * _Time.y);
                uv2 += float2(-_CausticsSpeed * 0.27 * _Time.y, _CausticsSpeed * 0.83 * _Time.y);

                float2 dir = normalize(dispersionDir + float2(0.0001, 0.0001));
                float2 offset = dir * _CausticsDispersion;

                //三个通道轻微错位
                float r = SampleCausticsMono(uv1 + offset, uv2 + offset);
                float g = SampleCausticsMono(uv1,          uv2);
                float b = SampleCausticsMono(uv1 - offset, uv2 - offset);

                float3 chroma = float3(r, g, b);

                //控制彩虹色强弱：0=完全灰度，1=完全RGB错位
                float luma = dot(chroma, float3(0.299, 0.587, 0.114));
                chroma = lerp(luma.xxx, chroma, _CausticsChromaticStrength);

                return chroma;
            }

            float SampleBrokenFoam(float2 baseUV, float tiling, float speed)
            {
                float t = _Time.y;

                float2 uv = baseUV * tiling;
                
                float2 distortUV = uv * 0.5 + float2(t * 0.03, -t * 0.02);
                float2 distort = UnpackNormal(
                    SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, distortUV)
                ).xy * _FoamDistortStrength;
                
                float2 uv1 = uv + distort + float2(t * speed, t * speed * 0.25);
                float2 uv2 = uv * 1.35 - distort + float2(-t * speed * 0.35, t * speed * 0.18);

                float f1 = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, uv1).r;
                float f2 = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, uv2).r;
                
                float foamTex = max(f1, f2 * 0.75);
                
                foamTex = lerp(foamTex, f1 * f2, 0.25);
                
                foamTex = saturate((foamTex - _FoamCutoff) / max(1.0 - _FoamCutoff, 0.0001));
                
                foamTex = pow(foamTex, 1.0 / max(_FoamContrast, 0.0001));

                return foamTex;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 screenUV = IN.positionHCS.xy / _ScaledScreenParams.xy;

                #if UNITY_REVERSED_Z
                    float sceneDepthRaw = SampleSceneDepth(screenUV);
                #else
                    float sceneDepthRaw = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(screenUV));
                #endif

                float sceneEyeDepth = LinearEyeDepth(sceneDepthRaw, _ZBufferParams);

                float3 positionVS = TransformWorldToView(IN.positionWS);
                float waterEyeDepth = -positionVS.z;

                float deltaDepth = max(0, sceneEyeDepth - waterEyeDepth);

                float depthLerp = saturate(deltaDepth / max(_DepthColorRange, 0.0001));
                float alphaDepthLerp = saturate(deltaDepth / max(_AlphaDepthRange, 0.0001));
                float alpha = lerp(_MinAlpha, _MaxAlpha, alphaDepthLerp) * _BaseColor.a;

                float3 baseTint = lerp(1.0, _BaseColor.rgb, 0.35);
                float3 waterDepthColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthLerp) * baseTint;
                float shallowMask = 1.0 - depthLerp;
                
                float3 normalTS = SampleFlowNormalTS(IN.uv);

                float3 worldNormal = TransformTangentToWorldCustom(
                    normalTS,
                    normalize(IN.tangentWS),
                    normalize(IN.bitangentWS),
                    normalize(IN.normalWS)
                );

                float2 rippleUV;
                rippleUV.x = (IN.positionWS.x - _RippleAreaCenterWS.x) / max(_RippleAreaSize.x, 0.0001) + 0.5;//把当前水面像素的世界坐标IN.positionWS.xz，映射到交互RT的UV坐标rippleUV
                rippleUV.y = (IN.positionWS.z - _RippleAreaCenterWS.z) / max(_RippleAreaSize.y, 0.0001) + 0.5;
                
                // rippleUV.y = 1.0 - rippleUV.y;

                float4 rippleSample = SAMPLE_TEXTURE2D(_RippleFieldTex, sampler_RippleFieldTex, rippleUV);
                float rippleField = rippleSample.r;//交互波纹高度场的主值
                float wakeFoamField = rippleSample.b;//额外存的一层尾迹信息

                float2 rippleTexel = _RippleFieldTex_TexelSize.xy;

                float rippleL = SAMPLE_TEXTURE2D(_RippleFieldTex, sampler_RippleFieldTex, rippleUV - float2(rippleTexel.x, 0)).r;
                float rippleR = SAMPLE_TEXTURE2D(_RippleFieldTex, sampler_RippleFieldTex, rippleUV + float2(rippleTexel.x, 0)).r;
                float rippleD = SAMPLE_TEXTURE2D(_RippleFieldTex, sampler_RippleFieldTex, rippleUV - float2(0, rippleTexel.y)).r;
                float rippleU = SAMPLE_TEXTURE2D(_RippleFieldTex, sampler_RippleFieldTex, rippleUV + float2(0, rippleTexel.y)).r;

                float3 rippleNormalWS = SAMPLE_TEXTURE2D(_RippleNormalTex, sampler_RippleNormalTex, rippleUV).xyz * 2.0 - 1.0;

                float2 rippleGrad = float2(rippleR - rippleL, rippleU - rippleD);

                float rippleFoamMask = saturate(length(rippleGrad) * _RippleFieldFoamStrength);

                float rippleMask = saturate(abs(rippleField) * 6.0 + length(rippleGrad) * 2.0);

                float3 rippleNormalAddWS = float3(
                    rippleNormalWS.x,
                    0.0,
                    rippleNormalWS.z
                ) * _RippleNormalTexStrength;

                worldNormal = normalize(worldNormal + rippleNormalAddWS * rippleMask);
                
                float2 rippleRefractOffset = rippleGrad * _RippleFieldRefractStrength;
                float2 refractOffset = normalTS.xy * _RefractStrength + rippleRefractOffset;
                float2 refractUV = saturate(screenUV + refractOffset);

                #if UNITY_REVERSED_Z
                    float refractDepthRaw = SampleSceneDepth(refractUV);
                #else
                    float refractDepthRaw = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(refractUV));
                #endif

                float refractEyeDepth = LinearEyeDepth(refractDepthRaw, _ZBufferParams);
                float refractionValid = step(waterEyeDepth + 0.01, refractEyeDepth);
                refractUV = lerp(screenUV, refractUV, refractionValid);

                float3 refractColor = SampleSceneColor(refractUV);

                float shallowCausticsMask = 1.0 - saturate(deltaDepth / max(_CausticsDepthRange, 0.0001));

                // 色散方向
                float2 dispersionDir = worldNormal.xz;
                
                // float2 dispersionDir = float2(1.0, 0.35);

                float3 causticsTex = SampleCausticsRGB(IN.positionWS, dispersionDir);
                float3 caustics = causticsTex * shallowCausticsMask * _CausticsStrength;
                
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float3 lightDir = normalize(mainLight.direction);
                float3 viewDir = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float3 halfDir = normalize(lightDir + viewDir);

                float NdotL = saturate(dot(worldNormal, lightDir));
                float NdotH = saturate(dot(worldNormal, halfDir));
                float lightAttenuation = mainLight.distanceAttenuation * mainLight.shadowAttenuation;

                float3 diffuse = waterDepthColor * lerp(0.32, 1.0, NdotL) * mainLight.color * lerp(0.55, 1.0, lightAttenuation);
                float3 specular = pow(NdotH, _SpecularPower) * _SpecularStrength * mainLight.color * lightAttenuation;
                
                float foamMask = 1.0 - saturate(deltaDepth / max(_FoamRange, 0.0001));
                foamMask = smoothstep(0.08, 0.92, foamMask);
                foamMask = pow(foamMask, max(_FoamSoftness, 0.0001));
                foamMask = saturate(
                    foamMask
                    + rippleFoamMask * _RippleFieldShoreFoamInfluence
                    + wakeFoamField
                );

                float2 foamBaseUV = TRANSFORM_TEX(IN.uv, _FoamTex);

                float foamTex = SampleBrokenFoam(
                    foamBaseUV,
                    _FoamTiling,
                    _FoamSpeed
                );

                float foam = foamTex * foamMask * _FoamStrength;
                float3 foamCol = foam * _FoamColor.rgb;

                float crestHeightMask = saturate(
                    (IN.waveHeight - _CrestThreshold) / max(_CrestSoftness, 0.0001)
                );

                float crestSlopeMask = pow(
                    saturate(1.0 - normalize(IN.normalWS).y),
                    _CrestSlopeStrength
                );

                float crestMask = saturate(crestHeightMask * crestSlopeMask);
                crestMask = saturate(
                    crestMask
                    + rippleFoamMask * _RippleFieldCrestFoamInfluence
                    + wakeFoamField * 0.5
                );

               float2 crestBaseUV = TRANSFORM_TEX(IN.uv, _FoamTex);

                float crestTex = SampleBrokenFoam(
                    crestBaseUV,
                    _CrestFoamTiling,
                    _CrestFoamSpeed
                );

                float crestFoam = crestTex * crestMask * _CrestFoamStrength;
                float3 crestFoamCol = crestFoam * _CrestFoamColor.rgb;

                float3 wakeFoamCol = wakeFoamField * _FoamColor.rgb * _WakeFoamColorStrength;
                
                float fresnel = pow(1.0 - saturate(dot(worldNormal, viewDir)), _FresnelPower);
                fresnel *= _FresnelStrength;
                fresnel = saturate(fresnel);
                
                float2 reflectionDistort = worldNormal.xz * _PlanarReflectionDistort;
                reflectionDistort += rippleGrad * _RippleFieldRefractStrength;
                float2 reflectionUV = screenUV + reflectionDistort;
                reflectionUV = saturate(reflectionUV);
                
                float3 planarReflection = SAMPLE_TEXTURE2D(
                    _PlanarReflectionTex,
                    sampler_PlanarReflectionTex,
                    reflectionUV
                ).rgb;

                float reflectionSignal = saturate(dot(planarReflection, float3(0.2126, 0.7152, 0.0722)) * 12.0);
                float3 reflectionSource = lerp(_ReflectionTint.rgb, planarReflection, _PlanarReflectionBlend * reflectionSignal);
                reflectionSource *= _ReflectionStrength;

                float3 extinction = max(_AbsorptionColor.rgb, 0.001) * _AbsorptionStrength;
                float3 transmittance = exp(-extinction * deltaDepth);
                float3 absorbedRefract = refractColor * transmittance;
                float3 depthScattering = waterDepthColor * (1.0 - transmittance);
                float3 shoreScattering = _ScatterColor.rgb * shallowMask * _ShoreScatterStrength;
                float forwardScatter = pow(saturate(dot(viewDir, -lightDir)), 4.0);
                float3 sunScattering = _ScatterColor.rgb * forwardScatter * _ScatterStrength * mainLight.color * lightAttenuation;
                float3 refractedWater = absorbedRefract + depthScattering + shoreScattering;
                float3 waterBodyBase = lerp(waterDepthColor + shoreScattering, refractedWater, _RefractionWeight);
                waterBodyBase += diffuse * 0.55 + sunScattering;
                waterBodyBase += caustics * _CausticsColor.rgb * shallowMask * 0.12;

                //斜视角以反射为主，俯视角仍保留少量环境反射
                float reflectionMask = saturate(_ReflectionFloor + fresnel * (1.0 - _ReflectionFloor));
                float3 waterBody = lerp(waterBodyBase, reflectionSource, reflectionMask);
                waterBody += specular;
                
                float3 rippleCol = rippleField * _RippleColor.rgb * _RippleFieldColorStrength;
                float3 finalCol = waterBody + foamCol + crestFoamCol + rippleCol + wakeFoamCol;
                float foamOpacity = saturate(foam + crestFoam + wakeFoamField * _WakeFoamColorStrength);
                float finalAlpha = saturate(alpha + foamOpacity * 0.35 * (1.0 - alpha));

                return half4(max(finalCol, 0.0), finalAlpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
