Shader "DynamicNaturalEnvironment/StylizedGrass"
{
    Properties
    {
        [Header(Color)]
        _BottomColor("底部颜色", Color) = (0.12, 0.22, 0.04, 1)
        _TopColor("顶部颜色", Color) = (0.48, 0.62, 0.12, 1)
        _ColorVariation("随机颜色变化", Range(0, 0.3)) = 0.08
        _RootAO("根部明度", Range(0, 1)) = 0.55

        [Header(Lighting)]
        _NormalUp("法线向上修正", Range(0, 1)) = 0.75
        _AmbientStrength("环境光强度", Range(0, 1)) = 0.35

        [Header(Wind)]
        _WindDirection("风方向 XZ", Vector) = (1, 0, 0.35, 0)
        _WindStrength("风强度", Range(0, 0.2)) = 0.035
        _WindFrequency("风频率", Range(0.01, 5)) = 0.8
        _WindSpeed("风速度", Range(0, 5)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Cull Off
            ZWrite On

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BottomColor;
                float4 _TopColor;
                float4 _WindDirection;
                float _ColorVariation;
                float _RootAO;
                float _NormalUp;
                float _AmbientStrength;
                float _WindStrength;
                float _WindFrequency;
                float _WindSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 color : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float heightWeight = saturate(IN.uv.y);

                float2 windDir = normalize(_WindDirection.xz + float2(0.0001, 0.0001));
                float phase = dot(positionWS.xz, windDir) * _WindFrequency + _Time.y * _WindSpeed;
                float wind = sin(phase);

                positionWS.xz += windDir * wind * _WindStrength * heightWeight * heightWeight;

                OUT.positionWS = positionWS;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float heightWeight = saturate(IN.uv.y);

                float3 normalWS = normalize(IN.normalWS);
                normalWS = normalize(lerp(normalWS, float3(0, 1, 0), _NormalUp));

                float3 baseColor = lerp(_BottomColor.rgb, _TopColor.rgb, heightWeight);

                float randomValue = IN.color.r;
                float randomTint = 1.0 + (randomValue * 2.0 - 1.0) * _ColorVariation;
                baseColor *= randomTint;

                float rootAO = lerp(_RootAO, 1.0, heightWeight);
                baseColor *= rootAO;

                Light mainLight = GetMainLight();

                float NdotL = dot(normalWS, mainLight.direction);
                float halfLambert = saturate(NdotL * 0.5 + 0.5);

                float3 ambient = float3(_AmbientStrength, _AmbientStrength, _AmbientStrength);
                float3 direct = mainLight.color * halfLambert;

                float3 finalColor = baseColor * (ambient + direct);
                finalColor = MixFog(finalColor, IN.fogFactor);

                return half4(finalColor, 1.0);
            }

            ENDHLSL
        }
    }
}