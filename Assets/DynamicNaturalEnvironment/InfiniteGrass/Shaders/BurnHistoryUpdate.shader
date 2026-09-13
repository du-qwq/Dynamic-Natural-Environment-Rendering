Shader "Hidden/InfiniteGrass/BurnHistoryUpdate"
{
    Properties
    {
        _CurrentBurnRT("Current Burn RT", 2D) = "black" {}
        _HistoryBurnRT("History Burn RT", 2D) = "black" {}
        _BurnSpeed("Burn Speed", Float) = 1
        _RegrowSpeed("Regrow Speed", Float) = 0
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

            TEXTURE2D(_CurrentBurnRT);
            SAMPLER(sampler_CurrentBurnRT);

            TEXTURE2D(_HistoryBurnRT);
            SAMPLER(sampler_HistoryBurnRT);

            float _BurnSpeed;
            float _RegrowSpeed;
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
                float currentBurn = SAMPLE_TEXTURE2D(_CurrentBurnRT, sampler_CurrentBurnRT, input.uv).r;
                float historyBurn = SAMPLE_TEXTURE2D(_HistoryBurnRT, sampler_HistoryBurnRT, input.uv).r;

                historyBurn = max(historyBurn - _RegrowSpeed * _DeltaTime, 0);
                historyBurn = saturate(historyBurn + currentBurn * _BurnSpeed * _DeltaTime);

                return half4(historyBurn, 0, 0, 1);
            }

            ENDHLSL
        }
    }
}
