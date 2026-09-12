Shader "Custom/WaterCausticsOverlay"
{
    Properties
    {
        [NoScaleOffset] _CausticsTex("Caustics Tex", 2D) = "white" {}

        [Header(Water Area)]
        _WaterLevel("Water Level", Float) = 0
        _WaterAreaCenter("Water Area Center", Vector) = (0, 0, 0, 0)
        _WaterAreaSize("Water Area Size", Vector) = (20, 20, 0, 0)
        _WaterAreaFeather("Water Area Feather", Float) = 1

        [Header(Caustics)]
        _CausticsColor("Caustics Color", Color) = (1, 1, 1, 1)
        _CausticsTiling("Caustics Tiling", Float) = 0.25
        _CausticsSpeed("Caustics Speed", Float) = 0.08
        _CausticsStrength("Caustics Strength", Range(0, 5)) = 1.2

        _BlendDistance("Blend Distance", Float) = 4.0
        _SurfaceFade("Surface Fade", Float) = 0.15

        _CausticsSharpness("Caustics Sharpness", Range(0.5, 8)) = 2.5
        _CausticsThreshold("Caustics Threshold", Range(0, 1)) = 0.6

        [Header(Second Layer)]
        _SecondaryScale("Secondary Scale", Float) = 1.18
        _SecondaryWeight("Secondary Weight", Range(0, 2)) = 0.65

        [Header(Chromatic Dispersion)]
        _ChromaticOffset("Chromatic Offset", Range(0, 0.05)) = 0.002
        _ChromaticStrength("Chromatic Strength", Range(0, 1)) = 0.15

        [Header(Distortion)]
        _DistortStrength("Distort Strength", Range(0, 0.2)) = 0.005
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
        }

        ZWrite Off
        ZTest LEqual
        Cull Off
        
        Blend One One

        Pass
        {
            Name "Water Caustics Overlay"

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_CausticsTex);
            SAMPLER(sampler_CausticsTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _CausticsColor;

                float _WaterLevel;
                float4 _WaterAreaCenter;
                float4 _WaterAreaSize;
                float _WaterAreaFeather;

                float _CausticsTiling;
                float _CausticsSpeed;
                float _CausticsStrength;

                float _BlendDistance;
                float _SurfaceFade;

                float _CausticsSharpness;
                float _CausticsThreshold;

                float _SecondaryScale;
                float _SecondaryWeight;

                float _ChromaticOffset;
                float _ChromaticStrength;

                float _DistortStrength;

                float4x4 _MainLightMatrix;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 screenPos   : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.positionOS.xyz);

                OUT.positionHCS = vertexInput.positionCS;
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);

                return OUT;
            }

            float GetLuma(float3 col)
            {
                return dot(col, float3(0.299, 0.587, 0.114));
            }

            float SampleCausticsMono(float2 uv, float lodLevel)
            {
                float3 col = SAMPLE_TEXTURE2D_LOD(
                    _CausticsTex,
                    sampler_CausticsTex,
                    uv,
                    lodLevel
                ).rgb;

                return GetLuma(col);
            }

            float3 SampleCausticsRGB(float2 uv, float2 dir, float lodLevel)
            {
                dir = normalize(dir + float2(0.0001, 0.0001));

                float2 offset = dir * _ChromaticOffset;

                float r = SampleCausticsMono(uv + offset, lodLevel);
                float g = SampleCausticsMono(uv,          lodLevel);
                float b = SampleCausticsMono(uv - offset, lodLevel);

                float3 rgb = float3(r, g, b);

                float luma = GetLuma(rgb);
                float chromaMask = saturate(pow(luma, 1.5));

                rgb = lerp(luma.xxx, rgb, _ChromaticStrength * chromaMask);

                return rgb;
            }
            
            float GetWaterAreaMask(float3 worldPos)
            {
                float2 localPos = worldPos.xz - _WaterAreaCenter.xz;

                float2 halfSize = max(
                    _WaterAreaSize.xy * 0.5,
                    float2(0.0001, 0.0001)
                );
                
                float2 p = localPos / halfSize;
                
                float dist = length(p);

                float feather = max(_WaterAreaFeather, 0.0001);
                float feather01 = feather / max(min(halfSize.x, halfSize.y), 0.0001);

                float mask = 1.0 - smoothstep(
                    1.0 - feather01,
                    1.0,
                    dist
                );

                return mask;
            }

            float3 SampleCausticsPattern(float3 worldPos, float waterDepth)
            {
                float t = _Time.y;
                
                float3 lightSpacePos = mul(_MainLightMatrix, float4(worldPos, 1.0)).xyz;

                float2 baseUV = lightSpacePos.xz * _CausticsTiling;
                
                float lodLevel = saturate(
                    waterDepth / max(_BlendDistance, 0.0001)
                ) * 1.5;
                
                float2 noiseUV = worldPos.xz * (_CausticsTiling * 0.20)
                    + float2(t * 0.03, -t * 0.02);

                float noise = SampleCausticsMono(noiseUV, 0.0) - 0.5;
                float2 distort = float2(noise, -noise) * _DistortStrength;
                
                float2 uvA = baseUV
                    + distort
                    + float2(t * _CausticsSpeed, t * _CausticsSpeed * 0.25);
                
                float2 uvB = baseUV * _SecondaryScale
                    - distort * 0.5
                    + float2(-t * _CausticsSpeed * 0.30, t * _CausticsSpeed * 0.45);

                float2 dispersionDir = normalize(float2(1.0, 0.25) + distort);

                float3 A = SampleCausticsRGB(uvA,  dispersionDir,  lodLevel);
                float3 B = SampleCausticsRGB(uvB, -dispersionDir, lodLevel);
                
                float3 caustics = max(A, B * _SecondaryWeight);
                
                caustics = saturate(
                    (caustics - _CausticsThreshold) /
                    max(1.0 - _CausticsThreshold, 0.0001)
                );
                
                caustics = pow(caustics, max(_CausticsSharpness, 0.0001));

                return caustics;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;

                float rawDepth = SampleSceneDepth(screenUV);

                #if UNITY_REVERSED_Z
                    if (rawDepth <= 0.0001)
                        return half4(0, 0, 0, 1);

                    float deviceDepth = rawDepth;
                #else
                    if (rawDepth >= 0.9999)
                        return half4(0, 0, 0, 1);

                    float deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif
                
                float3 worldPos = ComputeWorldSpacePosition(
                    screenUV,
                    deviceDepth,
                    UNITY_MATRIX_I_VP
                );
                
                float waterDepth = _WaterLevel - worldPos.y;
                
                float underwaterMask = smoothstep(
                    0.0,
                    max(_SurfaceFade, 0.0001),
                    waterDepth
                );
                
                float depthFade = 1.0 - saturate(
                    waterDepth / max(_BlendDistance, 0.0001)
                );
                
                float areaMask = GetWaterAreaMask(worldPos);

                float mask = underwaterMask * depthFade * areaMask;

                if (mask <= 0.0001)
                    return half4(0, 0, 0, 1);

                Light mainLight = GetMainLight();

                float3 caustics = SampleCausticsPattern(worldPos, waterDepth);

                caustics *= _CausticsColor.rgb;
                caustics *= mainLight.color;
                caustics *= _CausticsStrength;
                caustics *= mask;
                
                return half4(caustics, 1.0);
            }

            ENDHLSL
        }
    }
}