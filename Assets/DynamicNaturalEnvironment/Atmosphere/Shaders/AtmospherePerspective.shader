Shader "Hidden/DynamicNaturalEnvironment/AtmospherePerspective"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" }

        ZWrite Off
        ZTest Always
        Cull Off
        Blend One Zero

        Pass
        {
            Name "AtmospherePerspective"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4 _AP_Params0;
            float4 _AP_Params1;
            float4 _AP_HeightRange;
            float4 _AP_HorizonColor;
            float4 _AP_ZenithColor;
            float4 _AP_SunScatterColor;
            float4 _AP_SunDirWS;
            int _AP_DebugMode;

            float RayleighPhase(float mu)
            {
                return 3.0 / (16.0 * PI) * (1.0 + mu * mu);
            }

            float MiePhase(float mu, float g)
            {
                g = clamp(g, 0.0, 0.95);
                float g2 = g * g;
                float value = max(1.0 + g2 - 2.0 * g * mu, 0.001);
                return (1.0 - g2) / (4.0 * PI * pow(value, 1.5));
            }

            float3 GetAtmosphereColor(float3 viewDir, float mu)
            {
                float upFactor = saturate(viewDir.y * 0.5 + 0.5);
                float3 skyColor = lerp(_AP_HorizonColor.rgb, _AP_ZenithColor.rgb, pow(upFactor, 0.4));

                float rayleigh = RayleighPhase(mu);
                float mie = MiePhase(mu, _AP_Params1.y);

                float3 color = skyColor * (0.85 + rayleigh * 0.75);
                color += _AP_SunScatterColor.rgb * mie * _AP_Params1.x * 0.12;

                return color;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float4 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                if (_AP_DebugMode == 1) return half4(1, 0, 1, 1);

                float rawDepth = SampleSceneDepth(uv);
                float linear01Depth = Linear01Depth(rawDepth, _ZBufferParams);

                if (_AP_DebugMode == 2) return half4(linear01Depth.xxx, 1);

                #if UNITY_REVERSED_Z
                    bool isSky = rawDepth <= 0.00001;
                #else
                    bool isSky = rawDepth >= 0.99999;
                #endif

                if (isSky)
                {
                    if (_AP_DebugMode == 3) return half4(0, 0, 0, 1);
                    return sourceColor;
                }

                float3 worldPos = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float3 cameraPos = _WorldSpaceCameraPos.xyz;
                float3 viewVector = worldPos - cameraPos;
                float distanceToCamera = length(viewVector);
                float3 viewDir = distanceToCamera > 0.0001 ? viewVector / distanceToCamera : float3(0, 0, 1);
                float3 sunDir = normalize(_AP_SunDirWS.xyz);

                float startDistance = _AP_Params0.x;
                float maxDistance = max(_AP_Params0.y, startDistance + 0.01);
                float distanceScale = max(_AP_Params0.z, 0.01);
                float strength = _AP_Params0.w;

                float distanceAfterStart = max(0.0, distanceToCamera - startDistance);
                float exponentialFog = 1.0 - exp(-distanceAfterStart / distanceScale);
                float rangeMask = saturate((distanceToCamera - startDistance) / (maxDistance - startDistance));
                float fogAmount = exponentialFog * rangeMask * strength;

                float minHeight = _AP_HeightRange.x;
                float maxHeight = max(_AP_HeightRange.y, minHeight + 0.01);
                float height01 = saturate((worldPos.y - minHeight) / (maxHeight - minHeight));
                float heightFactor = 1.0 + (1.0 - height01) * _AP_Params1.z;

                fogAmount = saturate(fogAmount * heightFactor);

                if (_AP_DebugMode == 3) return half4(fogAmount.xxx, 1);

                float mu = dot(viewDir, sunDir);
                float3 atmosphereColor = GetAtmosphereColor(viewDir, mu);
                float3 finalColor = lerp(sourceColor.rgb, atmosphereColor, fogAmount);

                return half4(finalColor, sourceColor.a);
            }
            ENDHLSL
        }
    }
}