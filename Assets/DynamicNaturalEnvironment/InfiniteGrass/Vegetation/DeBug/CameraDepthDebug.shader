Shader "Hidden/Vegetation/CameraDepthDebug"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Overlay"
        }

        Pass
        {
            Name "CameraDepthDebug"

            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM

            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float _DebugMode;
            float _MaxDistance;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);

                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                output.uv = uv;

                #if UNITY_UV_STARTS_AT_TOP
                if (_ProjectionParams.x < 0.0)
                {
                    output.uv.y = 1.0 - output.uv.y;
                }
                #endif

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float rawDepth = SampleSceneDepth(input.uv);

                if (_DebugMode < 0.5)
                {
                    float geometry = abs(rawDepth - UNITY_RAW_FAR_CLIP_VALUE) > 1e-6 ? 1.0 : 0.0;
                    return half4(geometry, geometry, geometry, 1.0);
                }

                float depth = rawDepth;

                #if !UNITY_REVERSED_Z
                depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth);
                #endif

                float eyeDepth = LinearEyeDepth(depth, _ZBufferParams);
                float normalizedDepth = saturate(eyeDepth / max(_MaxDistance, 0.001));

                float value = 1.0 - normalizedDepth;

                return half4(value, value, value, 1.0);
            }

            ENDHLSL
        }
    }
}