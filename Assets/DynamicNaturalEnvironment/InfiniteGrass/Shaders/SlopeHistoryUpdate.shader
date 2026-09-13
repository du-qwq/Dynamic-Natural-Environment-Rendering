Shader "Hidden/InfiniteGrass/SlopeHistoryUpdate"
{
    Properties
    {
        _CurrentSlopeRT("Current Slope RT", 2D) = "black" {}
        _HistorySlopeRT("History Slope RT", 2D) = "black" {}
        _RecoverSpeed("Recover Speed", Float) = 0.25
        _DeltaTime("Delta Time", Float) = 0.016
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_CurrentSlopeRT);
            SAMPLER(sampler_CurrentSlopeRT);

            TEXTURE2D(_HistorySlopeRT);
            SAMPLER(sampler_HistorySlopeRT);

            float _RecoverSpeed;
            float _DeltaTime;

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.uv = uv;
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);

                #if UNITY_UV_STARTS_AT_TOP
                    output.positionCS.y *= -1.0;
                #endif

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float4 currentSlope = SAMPLE_TEXTURE2D(_CurrentSlopeRT, sampler_CurrentSlopeRT, input.uv);
                float4 historySlope = SAMPLE_TEXTURE2D(_HistorySlopeRT, sampler_HistorySlopeRT, input.uv);

                // 历史压草强度随时间恢复
                historySlope.a = max(historySlope.a - _RecoverSpeed * _DeltaTime, 0.0);

                // 如果历史强度已经恢复到0，就把方向重置成中性方向
                if (historySlope.a <= 0.001)
                {
                    historySlope.rg = float2(0.5, 0.5);
                    historySlope.b = 0;
                    historySlope.a = 0;
                }

                // 当前帧新踩到的地方覆盖历史
                if (currentSlope.a > historySlope.a)
                {
                    return currentSlope;
                }

                return historySlope;
            }
            ENDHLSL
        }
    }
}