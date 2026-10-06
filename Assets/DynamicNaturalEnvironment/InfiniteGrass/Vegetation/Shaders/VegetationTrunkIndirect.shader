Shader "Custom/VegetationTrunk"
{
    Properties
    {
        [Header(Main Maps)][Space(10)]
        _Color("Main Color", Color) = (1,1,1,1)
        _MainTex("Albedo", 2D) = "white" {}
        _BumpMap("Normal", 2D) = "bump" {}
        _NormalPower("Normal Power", Range(0,1)) = 1
        _MetallicROcclusionGSmoothnessA("Metallic (R) Occlusion (G) Smoothness (A)", 2D) = "white" {}
        _MetallicPower("Metallic Power", Range(0,1)) = 0.5
        _SmoothnessPower("Smoothness Power", Range(0,1)) = 0.5
        _OcclusionPower("Occlusion Power", Range(0,1)) = 1

        [Space(10)][Header(Deposit Maps)][Space(10)]
        _2ndColor("Color", Color) = (1,1,1,1)
        _DetailAlbedoMap("Albedo", 2D) = "white" {}
        _DetailNormalMap("Normal", 2D) = "bump" {}
        _2ndNormalPower("Normal Power", Range(0,1)) = 1
        [Toggle]_BlendNormals("Blend Normals", Float) = 1
        _DetailMetallicGlossMap("Metallic (R) Occlusion (G) Smoothness (A)", 2D) = "black" {}
        _LayerMetallicPower("Layer Metallic Power", Range(0,1)) = 0.5
        _LayerSmoothnessPower("Layer Smoothness Power", Range(0,1)) = 0.5
        _LayerOcclusionPower("Layer Occlusion Power", Range(0,1)) = 1
        _LayerMask("Layer Mask (R)", 2D) = "white" {}
        [Toggle(_INVERTMASK_ON)] _InvertMask("Invert Mask", Float) = 0

        [Space(10)][Header(Layer)][Space(10)]
        [Toggle]_UseVertexColor("Use Vertex Color", Float) = 1
        [KeywordEnum(R,G,B,A)] _LayerChannel("Layer Channel", Float) = 1
        _LayerPower("Layer Power", Range(0,1)) = 0.5
        _LayerThreshold("Layer Threshold", Range(0,50)) = 50
        _LayerPosition("Layer Position", Float) = 0
        _LayerContrast("Layer Contrast", Float) = 0

        [Space(10)][Header(Wind)][Space(10)]
        [KeywordEnum(R,G,B,A)] _BaseWindChannel("Base Wind Channel", Float) = 2
        _WindMultiplier("Wind Multiplier", Float) = 0
        _WindTrunkPosition("Wind Trunk Position", Float) = 1
        _WindTrunkContrast("Wind Trunk Contrast", Float) = 2

        [Space(10)][Header(Lighting)][Space(10)]
        _ShadowFloor("Minimum Shadow Light", Range(0,1)) = 0.18

        [Space(10)][Header(Debug)][Space(10)]
        [Toggle(_SEEVERTEXCOLOR_ON)] _SeeVertexColor("See Vertex Color", Float) = 0
        [KeywordEnum(RGBA,R,G,B,A)] _VertexColorChannel("Vertex Color Channel", Float) = 0
        [Toggle(_WINDDEBUGVIEW_ON)] _WindDebugView("Wind Debug View", Float) = 0

        [HideInInspector] _texcoord("", 2D) = "white" {}
        [HideInInspector] __dirty("", Int) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Back
        ZWrite On
        ZTest LEqual

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "VegetationIndirectCommon.hlsl"
        #include "VegetationLOD.hlsl"
        #include "VegetationDepthNormals.hlsl"
        #include "VegetationReceiveShadows.hlsl"
        #include "VegetationAdditionalLights.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        TEXTURE2D(_BumpMap);
        SAMPLER(sampler_BumpMap);

        TEXTURE2D(_MetallicROcclusionGSmoothnessA);
        SAMPLER(sampler_MetallicROcclusionGSmoothnessA);

        TEXTURE2D(_DetailAlbedoMap);
        SAMPLER(sampler_DetailAlbedoMap);

        TEXTURE2D(_DetailNormalMap);
        SAMPLER(sampler_DetailNormalMap);

        TEXTURE2D(_DetailMetallicGlossMap);
        SAMPLER(sampler_DetailMetallicGlossMap);

        TEXTURE2D(_LayerMask);
        SAMPLER(sampler_LayerMask);

        CBUFFER_START(UnityPerMaterial)
        float4 _Color;
        float4 _MainTex_ST;
        float4 _BumpMap_ST;

        float _NormalPower;
        float _MetallicPower;
        float _SmoothnessPower;
        float _OcclusionPower;

        float4 _2ndColor;
        float _2ndNormalPower;
        float _BlendNormals;
        float _LayerMetallicPower;
        float _LayerSmoothnessPower;
        float _LayerOcclusionPower;

        float _UseVertexColor;
        float _LayerPower;
        float _LayerThreshold;
        float _LayerPosition;
        float _LayerContrast;

        float _WindMultiplier;
        float _WindTrunkPosition;
        float _WindTrunkContrast;
        float _ShadowFloor;
        CBUFFER_END

        float GetBaseWindChannel(float4 vertexColor)
        {
            #if defined(_BASEWINDCHANNEL_R)
                return vertexColor.r;
            #elif defined(_BASEWINDCHANNEL_G)
                return vertexColor.g;
            #elif defined(_BASEWINDCHANNEL_A)
                return vertexColor.a;
            #else
                return vertexColor.b;
            #endif
        }

        float GetTrunkWindMask(float4 vertexColor)
        {
            float rawMask = saturate(1.0 - GetBaseWindChannel(vertexColor));
            float shapedMask = pow(rawMask, max(_WindTrunkPosition, 0.01));
            return saturate(shapedMask * _WindTrunkContrast);
        }

        float3 TransformVegetationTangentToWorld(float3 tangentOS, uint instanceID)
        {
            float3x3 objectToWorld = (float3x3)GetVegetationObjectToWorld(instanceID);
            return normalize(mul(objectToWorld, tangentOS));
        }

        float GetVegetationTransformSign(uint instanceID)
        {
            float3x3 m = (float3x3)GetVegetationObjectToWorld(instanceID);
            return determinant(m) < 0.0 ? -1.0 : 1.0;
        }

        float3 ApplyTrunkWind(float3 positionWS, float3 pivotWS, float4 vertexColor)
        {
            float windMask = GetTrunkWindMask(vertexColor);
            return ApplyVegetationWind(positionWS, pivotWS, windMask, _WindMultiplier, 1.0, 1.0);
        }

        float3 ApplyTrunkForwardWind(float3 positionWS,float3 pivotWS,float4 vertexColor)
        {
            float windMask=GetTrunkWindMask(vertexColor);
            return ApplyVegetationForwardWind(positionWS,pivotWS,windMask,_WindMultiplier,1.0,1.0);
        }

        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
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

            #pragma shader_feature_local_vertex _BASEWINDCHANNEL_R _BASEWINDCHANNEL_G _BASEWINDCHANNEL_B _BASEWINDCHANNEL_A
            #pragma shader_feature_local_fragment _SEEVERTEXCOLOR_ON
            #pragma shader_feature_local_fragment _WINDDEBUGVIEW_ON

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation float lodDistance : TEXCOORD15;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float4 color : TEXCOORD4;
                float fogFactor : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 pivotWS = GetVegetationPivotWS(input.instanceID);
                output.lodDistance = GetVegetationLODDistance(pivotWS);
                float3 positionWS = TransformVegetationPositionToWorld(input.positionOS, input.instanceID);
                float3 normalWS = TransformVegetationNormalToWorld(input.normalOS, input.instanceID);
                float3 tangentWS = TransformVegetationTangentToWorld(input.tangentOS.xyz, input.instanceID);

                positionWS = ApplyTrunkForwardWind(positionWS,pivotWS,input.color);

                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.tangentWS = float4(tangentWS, input.tangentOS.w * GetVegetationTransformSign(input.instanceID));
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                ApplyVegetationLODCrossFade(input.lodDistance, input.positionCS.xy);
                half4 baseSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;

                float3 normalWS = normalize(input.normalWS);
                float3 tangentWS = normalize(input.tangentWS.xyz);
                float3 bitangentWS = normalize(cross(normalWS, tangentWS) * input.tangentWS.w);

                float2 normalUV = input.uv * _BumpMap_ST.xy + _BumpMap_ST.zw;
                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, normalUV),
                    max(_NormalPower, 0.001)
                );

                normalWS = normalize(mul(normalTS, float3x3(tangentWS, bitangentWS, normalWS)));

                half4 packed = SAMPLE_TEXTURE2D(
                    _MetallicROcclusionGSmoothnessA,
                    sampler_MetallicROcclusionGSmoothnessA,
                    input.uv
                );

                float metallic = saturate(packed.r * _MetallicPower);
                float occlusion = lerp(1.0, packed.g, saturate(_OcclusionPower));
                float smoothness = saturate(packed.a * _SmoothnessPower);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float3 viewDirWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float shadowAttenuation = max(VegetationMainLightShadowAttenuation(mainLight.shadowAttenuation), _ShadowFloor);

                float3 ambient = SampleSH(normalWS) * occlusion;
                float3 direct = mainLight.color * NdotL * mainLight.distanceAttenuation * shadowAttenuation;

                float3 halfVector = SafeNormalize(viewDirWS + mainLight.direction);
                float specularPower = lerp(8.0, 128.0, smoothness);
                float specularTerm = pow(saturate(dot(normalWS, halfVector)), specularPower) * smoothness;

                float3 specularColor = lerp(half3(0.04,0.04,0.04), baseSample.rgb, metallic);
                float3 color = baseSample.rgb * (ambient + direct);
                color += mainLight.color * specularColor * specularTerm * mainLight.distanceAttenuation * shadowAttenuation;

                [loop]
                for (int lightIndex = 0; lightIndex < GetVegetationAdditionalLightsCount(); ++lightIndex)
                {
                    Light light = GetVegetationAdditionalLight((uint)lightIndex, input.positionWS, unity_ProbesOcclusion);
                    float localNdotL = saturate(dot(normalWS, light.direction));
                    float localShadow = max(light.shadowAttenuation, _ShadowFloor);
                    float localAttenuation = light.distanceAttenuation * localShadow;
                    float3 localHalfVector = SafeNormalize(viewDirWS + light.direction);
                    float localSpecular = pow(saturate(dot(normalWS, localHalfVector)), specularPower) * smoothness;
                    color += light.color * localAttenuation
                        * (baseSample.rgb * localNdotL + specularColor * localSpecular);
                }

                #if defined(_WINDDEBUGVIEW_ON)
                    float windMask = GetTrunkWindMask(input.color);
                    color = float3(windMask, 0.0, 0.0);
                #endif

                #if defined(_SEEVERTEXCOLOR_ON)
                    color = input.color.rgb;
                #endif

                color = MixFog(color, input.fogFactor);

                return half4(color, baseSample.a);
            }

            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local_vertex _BASEWINDCHANNEL_R _BASEWINDCHANNEL_G _BASEWINDCHANNEL_B _BASEWINDCHANNEL_A

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation float lodDistance : TEXCOORD15;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;

                float3 pivotWS = GetVegetationPivotWS(input.instanceID);
                output.lodDistance = GetVegetationLODDistance(pivotWS);
                float3 positionWS = TransformVegetationPositionToWorld(input.positionOS, input.instanceID);
                float3 normalWS = TransformVegetationNormalToWorld(input.normalOS, input.instanceID);

                positionWS = ApplyTrunkWind(positionWS, pivotWS, input.color);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS)
                );

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                ApplyVegetationLODCrossFade(input.lodDistance, input.positionCS.xy);
                return 0;
            }

            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local_vertex _BASEWINDCHANNEL_R _BASEWINDCHANNEL_G _BASEWINDCHANNEL_B _BASEWINDCHANNEL_A

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation float lodDistance : TEXCOORD15;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;

                float3 pivotWS = GetVegetationPivotWS(input.instanceID);
                output.lodDistance = GetVegetationLODDistance(pivotWS);
                float3 positionWS = TransformVegetationPositionToWorld(input.positionOS, input.instanceID);

                positionWS = ApplyTrunkWind(positionWS, pivotWS, input.color);

                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                ApplyVegetationLODCrossFade(input.lodDistance, input.positionCS.xy);
                return 0;
            }

            ENDHLSL
        }
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode"="DepthNormalsOnly" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma shader_feature_local_vertex _BASEWINDCHANNEL_R _BASEWINDCHANNEL_G _BASEWINDCHANNEL_B _BASEWINDCHANNEL_A

            struct DNAttributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                uint instanceID : SV_InstanceID;
            };
            struct DNVaryings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation float lodDistance : TEXCOORD15;
                float3 normalWS : TEXCOORD0;
                float4 tangentWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };
            DNVaryings DepthNormalsVert(DNAttributes input)
            {
                DNVaryings output;
                float3 pivotWS = GetVegetationPivotWS(input.instanceID);
                output.lodDistance = GetVegetationLODDistance(pivotWS);
                float3 positionWS = TransformVegetationPositionToWorld(input.positionOS,input.instanceID);
                positionWS = ApplyTrunkForwardWind(positionWS,pivotWS,input.color);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformVegetationNormalToWorld(input.normalOS,input.instanceID);
                float3 tangentWS = TransformVegetationTangentToWorld(input.tangentOS.xyz,input.instanceID);
                output.tangentWS = float4(tangentWS,input.tangentOS.w*GetVegetationTransformSign(input.instanceID));
                output.uv = TRANSFORM_TEX(input.uv,_MainTex);
                return output;
            }
            half4 DepthNormalsFrag(DNVaryings input) : SV_Target
            {
                ApplyVegetationLODCrossFade(input.lodDistance,input.positionCS.xy);
                float3 normalWS = normalize(input.normalWS);
                float3 tangentWS = normalize(input.tangentWS.xyz);
                float3 bitangentWS = normalize(cross(normalWS,tangentWS)*input.tangentWS.w);
                float2 normalUV = input.uv * _BumpMap_ST.xy + _BumpMap_ST.zw;
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,normalUV),
                    max(_NormalPower,0.001));
                normalWS = normalize(mul(normalTS,float3x3(tangentWS,bitangentWS,normalWS)));
                return EncodeVegetationDepthNormal(normalWS);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
