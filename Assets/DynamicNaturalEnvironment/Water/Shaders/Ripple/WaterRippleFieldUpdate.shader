Shader "Hidden/WaterRippleFieldUpdate"
{
    Properties
    {
        _MainTex("Previous Field", 2D) = "black" {}
    }

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

            float _Decay;
            float _WakeLength;
            float _WakeWidth;
            float _WakeStrength;
            float _WakeFoamStrength;

            #define MAX_STAMPS 32
            float4 _ImpactUVs[MAX_STAMPS];
            float4 _ImpactDirs[MAX_STAMPS];

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

                float4 centerSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float current = centerSample.r;
                float previous = centerSample.g;
                float foamMemory = centerSample.b;

                float left  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(texel.x, 0)).r;
                float right = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(texel.x, 0)).r;
                float down  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(0, texel.y)).r;
                float up    = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0, texel.y)).r;

                float next = ((left + right + up + down) * 0.5 - previous);
                next *= _Decay;

                float stamp = 0.0;
                float newFoam = foamMemory * 0.965;

                [unroll]
                for (int i = 0; i < MAX_STAMPS; i++)
                {
                    if (_ImpactUVs[i].z > 0.5)
                    {
                        float2 impactUV = _ImpactUVs[i].xy;

                        float2 dir = _ImpactDirs[i].xy;
                        float dirLen = max(length(dir), 1e-5);
                        dir /= dirLen;

                        float speed01 = saturate(_ImpactDirs[i].z);
                        float lateralBias = _ImpactDirs[i].w;

                        float2 delta = IN.uv - impactUV;
                        float2 side = float2(-dir.y, dir.x);

                        float back = dot(delta, -dir);
                        float lateral = dot(delta, side);

                        float behindMask = step(0.0, back);

                        float bodyLen = max(_WakeLength * lerp(0.75, 1.35, speed01), 1e-5);
                        float bodyWid = max(_WakeWidth, 1e-5);

                        float wakeBody = exp(
                            -(back * back) / (bodyLen * bodyLen)
                            -(lateral * lateral) / (bodyWid * bodyWid)
                        );
                        wakeBody *= behindMask;

                        float sideOffset = bodyWid * lerp(0.55, 0.95, abs(lateralBias));
                        float sideBand = exp(
                            -(back * back) / ((bodyLen * 1.2) * (bodyLen * 1.2))
                            -((abs(lateral) - sideOffset) * (abs(lateral) - sideOffset))
                             / ((bodyWid * 0.35) * (bodyWid * 0.35) + 1e-5)
                        );
                        sideBand *= behindMask;

                        float frontDip = exp(
                            -(dot(delta, dir) * dot(delta, dir)) / ((bodyLen * 0.35) * (bodyLen * 0.35) + 1e-5)
                            -(lateral * lateral) / ((bodyWid * 0.8) * (bodyWid * 0.8) + 1e-5)
                        );

                        float wake = wakeBody * _WakeStrength * lerp(0.55, 1.0, speed01);
                        wake -= frontDip * _WakeStrength * 0.25;

                        stamp += wake;
                        newFoam = max(newFoam, sideBand * _WakeFoamStrength);
                    }
                }

                next += stamp;
                next = clamp(next, -1.0, 1.0);
                newFoam = saturate(newFoam);

                //R=当前高度,G=上一帧高度,B=尾迹泡沫记忆
                return half4(next, current, newFoam, 1);
            }
            ENDHLSL
        }
    }
}