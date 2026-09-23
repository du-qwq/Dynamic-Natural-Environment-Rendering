Shader "Vegetation/IndirectTest"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.3, 0.8, 0.25, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Forward"

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            StructuredBuffer<float4x4> _VegetationMatrices;
            StructuredBuffer<uint> _VegetationVisibleIndices;

            int _VegetationUseVisibleIndices;

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            uint GetSourceInstanceIndex(uint instanceID)
            {
                if (_VegetationUseVisibleIndices != 0) return _VegetationVisibleIndices[instanceID];
                return instanceID;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                uint sourceIndex = GetSourceInstanceIndex(input.instanceID);
                float4x4 objectToWorld = _VegetationMatrices[sourceIndex];

                float3 positionWS = mul(objectToWorld, float4(input.positionOS, 1.0)).xyz;
                float3 normalWS = normalize(mul((float3x3)objectToWorld, input.normalOS));

                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.positionCS = TransformWorldToHClip(positionWS);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                Light mainLight = GetMainLight();
                float3 normalWS = normalize(input.normalWS);

                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float lighting = NdotL * mainLight.shadowAttenuation * mainLight.distanceAttenuation;

                float3 color = _BaseColor.rgb * (0.25 + lighting * mainLight.color);

                return half4(color, _BaseColor.a);
            }

            ENDHLSL
        }
    }
}