Shader "Hidden/WaterRippleNormalFromHeight"
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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _MainTex_TexelSize;

            float4 _HeightTexelWorldSize;//x=每个像素在世界空间的宽度, y=每个像素在世界空间的长度
            float _NormalStrength;

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
                float2 texel = _MainTex_TexelSize.xy;

                float hL = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(texel.x, 0)).r;
                float hR = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(texel.x, 0)).r;
                float hD = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(0, texel.y)).r;
                float hU = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0, texel.y)).r;

                float dx = max(_HeightTexelWorldSize.x, 1e-5);
                float dz = max(_HeightTexelWorldSize.y, 1e-5);

                float dHdX = (hR - hL) / (2.0 * dx);
                float dHdZ = (hU - hD) / (2.0 * dz);

                float3 normalWS = normalize(float3(-dHdX * _NormalStrength, 1.0, -dHdZ * _NormalStrength));

                return half4(normalWS * 0.5 + 0.5, 1.0);
            }
            ENDHLSL
        }
    }
}