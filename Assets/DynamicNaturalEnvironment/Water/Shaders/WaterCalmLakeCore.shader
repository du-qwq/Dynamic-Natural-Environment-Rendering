Shader "Custom/Water/CalmLakeCore"
{
    Properties
    {
        _ShallowColor("浅水颜色", Color) = (0.12, 0.38, 0.40, 1)
        _DeepColor("深水颜色", Color) = (0.018, 0.12, 0.17, 1)
        _DepthColorRange("深浅颜色过渡距离", Range(0.5, 40)) = 14
        _ShallowAlpha("浅水透明度", Range(0, 1)) = 0.42
        _DeepAlpha("深水透明度", Range(0, 1)) = 0.90
        _AlphaDepthRange("透明度过渡距离", Range(0.5, 30)) = 8
        _ShoreFadeRange("岸边透明范围", Range(0.1, 8)) = 2.5
        _ShoreTransparency("岸边额外透明", Range(0, 0.8)) = 0.16

        _AbsorptionStrength("吸收强度", Range(0, 2)) = 0.12
        _ScatterColor("散射颜色", Color) = (0.035, 0.22, 0.21, 1)
        _ScatterStrength("散射强度", Range(0, 2)) = 0.18

        [NoScaleOffset] _NormalTex("水面法线贴图", 2D) = "bump" {}
        _NormalWorldTiling("法线世界平铺密度", Range(0.01, 0.5)) = 0.12
        _NormalStrength("法线强度", Range(0, 1.5)) = 0.28
        _FlowSpeed("水纹流速 XZ", Vector) = (0.018, 0.012, 0, 0)

        _SpecularColor("太阳高光颜色", Color) = (1, 0.96, 0.84, 1)
        _SpecularPower("太阳高光锐度", Range(8, 256)) = 96
        _SpecularStrength("太阳高光强度", Range(0, 5)) = 0.65

        _FresnelPower("菲涅尔指数", Range(1, 8)) = 5
        _FresnelStrength("菲涅尔强度", Range(0, 2)) = 1

        _PlanarReflectionStrength("平面倒影强度", Range(0, 1.5)) = 0.65
        _ReflectionMinimum("正视角最低倒影比例", Range(0, 0.5)) = 0.05
        _PlanarReflectionDistort("倒影水纹扰动", Range(0, 0.03)) = 0.0015
        _ReflectionTint("倒影颜色", Color) = (1, 1, 1, 1)

        _RefractionStrength("折射强度", Range(0, 0.03)) = 0.003
        _RefractionDepthRange("折射可见深度", Range(0.2, 15)) = 4
        _ShallowTransmission("浅水透射强度", Range(0, 1)) = 0.72
        _TransmissionTintStrength("浅水颜色染色", Range(0, 1)) = 0.18

        [HideInInspector] _PlanarReflectionTex("Planar Reflection Texture", 2D) = "black" {}
        [HideInInspector] _PlanarReflectionReady("Planar Reflection Ready", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 200

        Pass
        {
            Name "CalmLakeCore"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_NormalTex);
            SAMPLER(sampler_NormalTex);

            TEXTURE2D(_PlanarReflectionTex);
            SAMPLER(sampler_PlanarReflectionTex);
            float4 _PlanarReflectionTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthColorRange;
                float _ShallowAlpha;
                float _DeepAlpha;
                float _AlphaDepthRange;
                float _ShoreFadeRange;
                float _ShoreTransparency;

                float _AbsorptionStrength;
                half4 _ScatterColor;
                float _ScatterStrength;

                float _NormalWorldTiling;
                float _NormalStrength;
                float4 _FlowSpeed;

                half4 _SpecularColor;
                float _SpecularPower;
                float _SpecularStrength;

                float _FresnelPower;
                float _FresnelStrength;

                float _PlanarReflectionStrength;
                float _ReflectionMinimum;
                float _PlanarReflectionDistort;
                half4 _ReflectionTint;
                float _PlanarReflectionReady;

                float _RefractionStrength;
                float _RefractionDepthRange;
                float _ShallowTransmission;
                float _TransmissionTintStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 tangentWS : TEXCOORD2;
                half3 bitangentWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionHCS = positionInputs.positionCS;
                OUT.positionWS = positionInputs.positionWS;
                OUT.normalWS = normalInputs.normalWS;
                OUT.tangentWS = normalInputs.tangentWS;
                OUT.bitangentWS = normalInputs.bitangentWS;
                OUT.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);

                return OUT;
            }

            float3 DecodeNormalScaled(float4 packedNormal, float strength)
            {
                float3 normal = UnpackNormal(packedNormal);
                normal.xy *= strength;
                normal.z = sqrt(saturate(1.0 - dot(normal.xy, normal.xy)));
                return normalize(normal);
            }

            float3 SampleCalmNormalTS(float3 positionWS)
            {
                float2 baseUV = positionWS.xz * _NormalWorldTiling;
                float2 flow = _FlowSpeed.xy * _Time.y;

                float2 uvA = baseUV + flow;
                float2 uvB = baseUV * 1.37 + float2(-flow.y, flow.x) * 0.73;

                float3 normalA = DecodeNormalScaled(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uvA), _NormalStrength);
                float3 normalB = DecodeNormalScaled(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uvB), _NormalStrength * 0.72);

                return normalize(float3(normalA.xy + normalB.xy, normalA.z * normalB.z));
            }

            float GetWaterThickness(float2 screenUV, float3 positionWS)
            {
                #if UNITY_REVERSED_Z
                    float sceneDepthRaw = SampleSceneDepth(screenUV);
                #else
                    float sceneDepthRaw = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, SampleSceneDepth(screenUV));
                #endif

                float sceneEyeDepth = LinearEyeDepth(sceneDepthRaw, _ZBufferParams);
                float waterEyeDepth = -TransformWorldToView(positionWS).z;
                return max(0.0, sceneEyeDepth - waterEyeDepth);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 screenUV = GetNormalizedScreenSpaceUV(IN.positionHCS);
                float waterThickness = GetWaterThickness(screenUV, IN.positionWS);

                float depthT = 1.0 - exp(-waterThickness / max(_DepthColorRange, 0.001));
                float alphaT = 1.0 - exp(-waterThickness / max(_AlphaDepthRange, 0.001));

                float shoreMask = 1.0 - saturate(waterThickness / max(_ShoreFadeRange, 0.001));
                shoreMask = shoreMask * shoreMask * (3.0 - 2.0 * shoreMask);

                float3 normalTS = SampleCalmNormalTS(IN.positionWS);

                float3 worldNormal = normalize(
                    normalTS.x * normalize(IN.tangentWS) +
                    normalTS.y * normalize(IN.bitangentWS) +
                    normalTS.z * normalize(IN.normalWS)
                );

                float3 viewDir = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                float NdotV = saturate(dot(worldNormal, viewDir));

                float3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthT);

                float absorption = 1.0 - exp(-waterThickness * _AbsorptionStrength * 0.12);
                waterColor = lerp(waterColor, _DeepColor.rgb, absorption * 0.42);

                float scatterAmount = 1.0 - exp(-waterThickness * _ScatterStrength * 0.08);
                float3 scattering = _ScatterColor.rgb * scatterAmount * (0.14 + 0.16 * (1.0 - NdotV));

                float shallowVisibility = 1.0 - smoothstep(0.0, max(_RefractionDepthRange, 0.001), waterThickness);
                float2 refractionUV = screenUV + normalTS.xy * _RefractionStrength * shallowVisibility;
                refractionUV = saturate(refractionUV);

                float3 sceneColor = SampleSceneColor(refractionUV);
                float3 tintedSceneColor = lerp(sceneColor, sceneColor * _ShallowColor.rgb, _TransmissionTintStrength);
                float transmissionWeight = shallowVisibility * _ShallowTransmission;

                float3 waterBody = waterColor + scattering;
                waterBody = lerp(waterBody, tintedSceneColor, transmissionWeight);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float3 lightDir = normalize(mainLight.direction);
                float3 halfDir = normalize(lightDir + viewDir);
                float NdotH = saturate(dot(worldNormal, halfDir));

                float lightAttenuation = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                float specularTerm = pow(NdotH, _SpecularPower) * _SpecularStrength * lightAttenuation;
                float3 specular = specularTerm * _SpecularColor.rgb * mainLight.color;

                float fresnel = pow(1.0 - NdotV, _FresnelPower);
                fresnel = saturate(fresnel * _FresnelStrength);

                float2 reflectionUV = screenUV;

                #if UNITY_UV_STARTS_AT_TOP
                    if (_PlanarReflectionTex_TexelSize.y < 0.0) reflectionUV.y = 1.0 - reflectionUV.y;
                #endif

                reflectionUV += normalTS.xy * _PlanarReflectionDistort;
                reflectionUV = saturate(reflectionUV);

                float3 planarReflection = SAMPLE_TEXTURE2D(_PlanarReflectionTex, sampler_PlanarReflectionTex, reflectionUV).rgb;
                planarReflection *= _ReflectionTint.rgb;

                float reflectionWeight = saturate(_ReflectionMinimum + fresnel * _PlanarReflectionStrength);
                reflectionWeight *= saturate(_PlanarReflectionReady);

                float3 finalColor = lerp(waterBody, planarReflection, reflectionWeight);
                finalColor += specular;
                finalColor = MixFog(finalColor, IN.fogFactor);

                float alpha = lerp(_ShallowAlpha, _DeepAlpha, alphaT);
                alpha -= shoreMask * _ShoreTransparency;
                alpha = saturate(alpha + reflectionWeight * 0.05);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}