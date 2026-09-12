Shader "Hidden/WaterDisplacementUpdate"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }

        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ForceTex);
            SAMPLER(sampler_ForceTex);

            TEXTURE2D(_CurrentTex);
            SAMPLER(sampler_CurrentTex);

            TEXTURE2D(_PreviousTex);
            SAMPLER(sampler_PreviousTex);

            float4 _CurrentTex_TexelSize;

            float _DeltaTimeSim;//模拟时间步长
            float _WaveSpeed;
            float _Damping;
            float _GridSize;//网格间距，高度场上一个像素，在世界空间里对应多大距离

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 texel = _CurrentTex_TexelSize.xy;

                float current = SAMPLE_TEXTURE2D(_CurrentTex, sampler_CurrentTex, IN.uv).r;
                float previous = SAMPLE_TEXTURE2D(_PreviousTex, sampler_PreviousTex, IN.uv).r;

                float left  = SAMPLE_TEXTURE2D(_CurrentTex, sampler_CurrentTex, IN.uv - float2(texel.x, 0)).r;
                float right = SAMPLE_TEXTURE2D(_CurrentTex, sampler_CurrentTex, IN.uv + float2(texel.x, 0)).r;
                float down  = SAMPLE_TEXTURE2D(_CurrentTex, sampler_CurrentTex, IN.uv - float2(0, texel.y)).r;
                float up    = SAMPLE_TEXTURE2D(_CurrentTex, sampler_CurrentTex, IN.uv + float2(0, texel.y)).r;

                float force = SAMPLE_TEXTURE2D(_ForceTex, sampler_ForceTex, IN.uv).r;

                float c2 = _WaveSpeed * _WaveSpeed;
                float dt2 = _DeltaTimeSim * _DeltaTimeSim;
                float dx2 = max(_GridSize * _GridSize, 1e-6);

                float laplacian = (left + right + up + down - 4.0 * current) / dx2;//当前点的离散拉普拉斯算子

                float next = 2.0 * current - previous + c2 * dt2 * laplacian;

                next += force * dt2;
                next *= _Damping;

                return half4(next, 0, 0, 1);
            }
            ENDHLSL
        }
    }
}