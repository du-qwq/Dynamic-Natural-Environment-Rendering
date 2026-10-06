Shader "Vegetation/BushBillboard"
{
    Properties
    {
        _Cutoff("Mask Clip Value", Float) = 0.5

        [Header(Main Maps)]
        [Space(10)]
        _MainColor("Leaves Color", Color) = (1,1,1,1)
        _TrunkColor("Trunk Color", Color) = (0,0,0,1)
        _DetailColor("Detail Color", Color) = (1,1,1,1)
        _ColorID("Color ID", 2D) = "white" {}
        _Normal("Normal", 2D) = "bump" {}
        _NormalPower("Normal Power", Range(0,1)) = 1

        [Header(Gradient Parameters)]
        [Space(10)]
        _GradientColor("Gradient Color", Color) = (1,1,1,1)
        _GradientFalloff("Gradient Falloff", Range(0,2)) = 2
        _GradientPosition("Gradient Position", Range(0,1)) = 0.5
        [Toggle(_INVERTGRADIENT_ON)] _InvertGradient("Invert Gradient", Float) = 0

        [Header(Color Variation)]
        [Space(10)]
        _ColorVariation("Color Variation", Color) = (1,0,0,1)
        _ColorVariationPower("Color Variation Power", Range(0,1)) = 1
        _ColorVariationNoise("Color Variation Noise", 2D) = "white" {}
        _NoiseScale("Noise Scale", Float) = 0.5

        [Header(Wind)]
        [Space(10)]
        _WindMultiplier("Wind Multiplier", Range(0,3)) = 0.12
        [Toggle(_WINDDEBUGVIEW_ON)] _WindDebugView("Wind Debug View", Float) = 0

        [Header(Lighting)]
        _AmbientStrength("Ambient Strength", Range(0,2)) = 1
        _LightWrap("Light Wrap", Range(0,1)) = 0.15
        _ShadowStrength("Shadow Strength", Range(0,1)) = 1
        _ShadowFloor("Minimum Shadow Light", Range(0,1)) = 0.18
        _SmoothnessPower("Smoothness", Range(0,1)) = 0.15
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        //Cull Back
        Cull Off
        ZWrite On

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "VegetationIndirectCommon.hlsl"
        #include "VegetationLOD.hlsl"
        #include "VegetationDepthNormals.hlsl"
        #include "VegetationReceiveShadows.hlsl"
        #include "VegetationAdditionalLights.hlsl"

        TEXTURE2D(_ColorID);
        SAMPLER(sampler_ColorID);
        TEXTURE2D(_Normal);
        SAMPLER(sampler_Normal);
        TEXTURE2D(_ColorVariationNoise);
        SAMPLER(sampler_ColorVariationNoise);

        CBUFFER_START(UnityPerMaterial)
        float4 _ColorID_ST;
        float4 _Normal_ST;
        float4 _MainColor;
        float4 _TrunkColor;
        float4 _DetailColor;
        float4 _GradientColor;
        float4 _ColorVariation;
        float _NormalPower;
        float _Cutoff;
        float _GradientFalloff;
        float _GradientPosition;
        float _ColorVariationPower;
        float _NoiseScale;
        float _WindMultiplier;
        float _AmbientStrength;
        float _LightWrap;
        float _ShadowStrength;
        float _ShadowFloor;
        float _SmoothnessPower;
        float3 _CameraForwardWS;
        float3 _CameraRightWS;
        float3 _CameraUpWS;
        CBUFFER_END

        float3 TransformVegetationTangentToWorld(float3 tangentOS,uint instanceID)
        {
            float4x4 objectToWorld=GetVegetationObjectToWorld(instanceID);
            return normalize(mul((float3x3)objectToWorld,tangentOS));
        }

        float GetBillboardWindMask(float4 colorV)
        {
            return saturate(1.0-colorV.b);
        }

        float3 ApplyBillboardWind(float3 positionWS,float3 pivotWS,float4 colorV)
        {
            float windMask=GetBillboardWindMask(colorV);
            return ApplyVegetationWind(positionWS,pivotWS,windMask,_WindMultiplier,0.65,0.55);
        }

        float3 ApplyBillboardForwardWind(float3 positionWS,float3 pivotWS,float4 colorV)
        {
            float windMask=GetBillboardWindMask(colorV);
            return ApplyVegetationForwardWind(positionWS,pivotWS,windMask,_WindMultiplier,0.65,0.55);
        }

        float3 ApplyCameraBillboard(float3 positionOS,float3 pivotWS,uint instanceID)
        {
            float4x4 objectToWorld=GetVegetationObjectToWorld(instanceID);
            float3 originalWS=mul(objectToWorld,float4(positionOS,1)).xyz;

            float3 localOffset=originalWS-pivotWS;

            float3 forward=_CameraForwardWS;
            forward.y=0;
            forward=normalize(forward);

            float3 right=_CameraRightWS;
            right.y=0;
            right=normalize(right);

            // Imported billboard meshes are XY planes; keep X across the camera-facing plane.
            return pivotWS+right*localOffset.x+float3(0,localOffset.y,0)+forward*localOffset.z;
        }
        
        float3 GetBillboardColor(float3 positionWS,float3 normalWS,half4 tex)
        {
            float ny=saturate(normalize(normalWS).y);

            #if defined(_INVERTGRADIENT_ON)
                ny=1.0-ny;
            #endif

            float gradient=saturate((ny+lerp(-2.0,1.0,_GradientPosition))/max(_GradientFalloff,0.0001));
            float3 baseColor=lerp(_MainColor.rgb,_GradientColor.rgb,gradient);
            float noise=SAMPLE_TEXTURE2D(_ColorVariationNoise,sampler_ColorVariationNoise,positionWS.xz*(_NoiseScale/100.0)).r;
            float variation=saturate(_ColorVariationPower*noise*noise*noise);
            float3 variationColor=saturate(_ColorVariation.rgb/max(1.0-baseColor,0.0001));
            return lerp(baseColor,variationColor,variation)*tex.rgb;
        }

        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

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
            #pragma shader_feature_local_fragment _INVERTGRADIENT_ON
            #pragma shader_feature_local_fragment _WINDDEBUGVIEW_ON

            struct Attributes
            {
                float3 positionOS:POSITION;
                float3 normalOS:NORMAL;
                float4 tangentOS:TANGENT;
                float2 uv:TEXCOORD0;
                float4 color:COLOR;
                uint instanceID:SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                nointerpolation float lodDistance : TEXCOORD15;
                float3 positionWS:TEXCOORD0;
                float3 normalWS:TEXCOORD1;
                float4 tangentWS:TEXCOORD2;
                float2 uv:TEXCOORD3;
                float4 color:TEXCOORD4;
                float fogFactor:TEXCOORD5;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 pivotWS=GetVegetationPivotWS(input.instanceID);
                output.lodDistance = GetVegetationLODDistance(pivotWS);
                float3 positionWS=ApplyCameraBillboard(input.positionOS,pivotWS,input.instanceID);
                float3 normalWS=normalize(_CameraForwardWS);
                float3 tangentWS=normalize(cross(float3(0,1,0),normalWS));

                positionWS=ApplyBillboardForwardWind(positionWS,pivotWS,input.color);

                output.positionWS=positionWS;
                output.normalWS=normalWS;
                output.tangentWS=float4(tangentWS,input.tangentOS.w);
                output.positionCS=TransformWorldToHClip(positionWS);
                output.uv=input.uv;
                output.color=input.color;
                output.fogFactor=ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input):SV_Target
            {
                float2 uv=input.uv*_ColorID_ST.xy+_ColorID_ST.zw;
                half4 tex=SAMPLE_TEXTURE2D(_ColorID,sampler_ColorID,uv);
                clip(tex.a-_Cutoff);
                ApplyVegetationLODCrossFade(input.lodDistance, input.positionCS.xy);

                float3 N=normalize(input.normalWS);
                float3 T=normalize(input.tangentWS.xyz);
                float3 B=normalize(cross(N,T)*input.tangentWS.w);

                float2 normalUV=input.uv*_Normal_ST.xy+_Normal_ST.zw;
                half3 normalTS=UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal,sampler_Normal,normalUV),max(_NormalPower,0.001));
                N=normalize(mul(normalTS,float3x3(T,B,N)));

                float3 baseColor=GetBillboardColor(input.positionWS,N,tex);

                #if defined(_WINDDEBUGVIEW_ON)
                    float windMask=GetBillboardWindMask(input.color);
                    baseColor=windMask.xxx;
                #endif

                float4 shadowCoord=TransformWorldToShadowCoord(input.positionWS);
                Light mainLight=GetMainLight(shadowCoord);

                float ndl=dot(N,mainLight.direction);
                float wrappedNdotL=saturate((ndl+_LightWrap)/(1.0+_LightWrap));
                float shadowAttenuation=max(VegetationMainLightShadowAttenuation(mainLight.shadowAttenuation),_ShadowFloor);
                float shadow=lerp(1.0,shadowAttenuation,_ShadowStrength);

                float3 ambient=SampleSH(N)*_AmbientStrength;
                float3 direct=mainLight.color*wrappedNdotL*mainLight.distanceAttenuation*shadow;

                float3 viewDir=SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                float3 halfDir=SafeNormalize(viewDir+mainLight.direction);
                float specPower=lerp(8.0,128.0,saturate(_SmoothnessPower));
                float spec=pow(saturate(dot(N,halfDir)),specPower)*saturate(_SmoothnessPower);
                float3 color=baseColor*(ambient+direct)+mainLight.color*spec*mainLight.distanceAttenuation*shadow;

                [loop]
                for (int lightIndex = 0; lightIndex < GetVegetationAdditionalLightsCount(); ++lightIndex)
                {
                    Light light = GetVegetationAdditionalLight((uint)lightIndex, input.positionWS, unity_ProbesOcclusion);
                    float localNdotL = saturate((dot(N, light.direction) + _LightWrap) / (1.0 + _LightWrap));
                    float localShadow = lerp(1.0, max(light.shadowAttenuation, _ShadowFloor), _ShadowStrength);
                    float3 localHalfDir = SafeNormalize(viewDir + light.direction);
                    float localSpec = pow(saturate(dot(N, localHalfDir)), specPower) * saturate(_SmoothnessPower);
                    color += light.color * light.distanceAttenuation * localShadow
                        * (baseColor * localNdotL + localSpec);
                }

                color=MixFog(color,input.fogFactor);
                return half4(color,tex.a);
            }

            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float3 positionOS:POSITION;
                float3 normalOS:NORMAL;
                float2 uv:TEXCOORD0;
                float4 color:COLOR;
                uint instanceID:SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                nointerpolation float lodDistance : TEXCOORD15;
                float2 uv:TEXCOORD0;
                nointerpolation float shadowCutoff:TEXCOORD1;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                float3 pivotWS=GetVegetationPivotWS(input.instanceID);
                output.lodDistance = GetVegetationLODDistance(pivotWS);
                float3 positionWS=ApplyCameraBillboard(input.positionOS,pivotWS,input.instanceID);
                float3 normalWS=-normalize(_CameraForwardWS);
                positionWS=ApplyBillboardWind(positionWS,pivotWS,input.color);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS=normalize(_LightPosition-positionWS);
                #else
                    float3 lightDirectionWS=_LightDirection;
                #endif

                float4 positionCS=TransformWorldToHClip(ApplyShadowBias(positionWS,normalWS,lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z=min(positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z=max(positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS=positionCS;
                output.uv=input.uv;
                output.shadowCutoff=GetVegetationShadowAlphaCutoff(_Cutoff,pivotWS);
                return output;
            }

            half4 ShadowFrag(Varyings input):SV_Target
            {
                float2 uv=input.uv*_ColorID_ST.xy+_ColorID_ST.zw;
                half alpha=SAMPLE_TEXTURE2D(_ColorID,sampler_ColorID,uv).a;
                clip(alpha-input.shadowCutoff);
                ApplyVegetationLODCrossFade(input.lodDistance, input.positionCS.xy);
                return 0;
            }

            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct Attributes
            {
                float3 positionOS:POSITION;
                float2 uv:TEXCOORD0;
                float4 color:COLOR;
                uint instanceID:SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                nointerpolation float lodDistance : TEXCOORD15;
                float2 uv:TEXCOORD0;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                float3 pivotWS=GetVegetationPivotWS(input.instanceID);
                output.lodDistance = GetVegetationLODDistance(pivotWS);
                float3 positionWS=ApplyCameraBillboard(input.positionOS,pivotWS,input.instanceID);
                positionWS=ApplyBillboardWind(positionWS,pivotWS,input.color);
                output.positionCS=TransformWorldToHClip(positionWS);
                output.uv=input.uv;
                return output;
            }

            half4 DepthFrag(Varyings input):SV_Target
            {
                float2 uv=input.uv*_ColorID_ST.xy+_ColorID_ST.zw;
                half alpha=SAMPLE_TEXTURE2D(_ColorID,sampler_ColorID,uv).a;
                clip(alpha-_Cutoff);
                ApplyVegetationLODCrossFade(input.lodDistance, input.positionCS.xy);
                return 0;
            }

            ENDHLSL
        }
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode"="DepthNormalsOnly" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct DNAttributes
            {
                float3 positionOS : POSITION;
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
                float3 positionWS = ApplyCameraBillboard(input.positionOS,pivotWS,input.instanceID);
                positionWS = ApplyBillboardForwardWind(positionWS,pivotWS,input.color);
                float3 normalWS = normalize(_CameraForwardWS);
                float3 tangentWS = normalize(cross(float3(0,1,0),normalWS));
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = normalWS;
                output.tangentWS = float4(tangentWS,input.tangentOS.w);
                output.uv = input.uv;
                return output;
            }
            half4 DepthNormalsFrag(DNVaryings input) : SV_Target
            {
                float2 uv = input.uv*_ColorID_ST.xy+_ColorID_ST.zw;
                clip(SAMPLE_TEXTURE2D(_ColorID,sampler_ColorID,uv).a-_Cutoff);
                ApplyVegetationLODCrossFade(input.lodDistance,input.positionCS.xy);
                float3 normalWS = normalize(input.normalWS);
                float3 tangentWS = normalize(input.tangentWS.xyz);
                float3 bitangentWS = normalize(cross(normalWS,tangentWS)*input.tangentWS.w);
                float2 normalUV = input.uv*_Normal_ST.xy+_Normal_ST.zw;
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal,sampler_Normal,normalUV),
                    max(_NormalPower,0.001));
                normalWS = normalize(mul(normalTS,float3x3(tangentWS,bitangentWS,normalWS)));
                return EncodeVegetationDepthNormal(normalWS);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
