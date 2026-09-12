Shader "Hidden/WaterForceApply"
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

            float3 _WaterCenterWS;
            float2 _WaterSize;
            float _CameraHeight;
            float3 _ForceTargetPosWS;
            float _ForceRadius;
            float _ForceStrength;

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
                float2 worldXZ;//当前这个RT像素，对应到世界空间中的水面XZ坐标
                worldXZ.x = _WaterCenterWS.x + (IN.uv.x - 0.5) * _WaterSize.x;//把 RT 的横向 UV，映射回世界空间里的水面X坐标
                worldXZ.y = _WaterCenterWS.z + (IN.uv.y - 0.5) * _WaterSize.y;

                float2 targetXZ = _ForceTargetPosWS.xz;
                float d = distance(worldXZ, targetXZ);//当前这个像素，离外力中心有多远

                float force = exp(-(d * d) / max(_ForceRadius * _ForceRadius, 1e-5));
                force *= _ForceStrength;

                return half4(force, 0, 0, 1);
            }
            ENDHLSL
        }
    }
}