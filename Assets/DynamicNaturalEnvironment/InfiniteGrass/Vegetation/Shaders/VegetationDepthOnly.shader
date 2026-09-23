Shader "Vegetation/DepthOnly"
{
    Properties
    {
        _MainTex("Albedo",2D)="white"{}
        _Cutoff("Alpha Cutoff",Range(0,1))=0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }

        ZWrite On
        ColorMask 0
        Cull Back


        Pass
        {
            Name "DepthOnly"

            Tags
            {
                "LightMode"="DepthOnly"
            }


            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "VegetationIndirectCommon.hlsl"


            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);


            CBUFFER_START(UnityPerMaterial)

            float4 _MainTex_ST;
            float _Cutoff;

            CBUFFER_END



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
                float2 uv:TEXCOORD0;
            };



            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 positionWS=
                    TransformVegetationPositionToWorld(
                        input.positionOS,
                        input.instanceID
                    );


                output.positionCS=
                    TransformWorldToHClip(positionWS);


                output.uv=
                    TRANSFORM_TEX(
                        input.uv,
                        _MainTex
                    );


                return output;
            }



            half4 Frag(Varyings input):SV_Target
            {

                half alpha=
                    SAMPLE_TEXTURE2D(
                        _MainTex,
                        sampler_MainTex,
                        input.uv
                    ).a;


                clip(alpha-_Cutoff);


                return 0;

            }


            ENDHLSL
        }
    }

    Fallback Off
}