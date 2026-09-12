Shader "Custom/Atmosphere/AtmosphereSky"
{
    Properties
    {
        _SkyExposure("天空曝光", Range(0.1, 5)) = 1.2
        _SunDiskIntensity("太阳圆盘亮度", Range(0, 100)) = 25
        _SunAngularRadiusDeg("太阳角半径", Range(0.1, 1)) = 0.27
        _SunDiskSoftness("太阳边缘柔和度", Range(1, 3)) = 1.35

        [HideInInspector] _SkyViewLUT("Sky View LUT", 2D) = "black" {}
        [HideInInspector] _TransmittanceLUT("Transmittance LUT", 2D) = "white" {}
        [HideInInspector] _SunDirWS("Sun Direction", Vector) = (0, 1, 0, 0)
        [HideInInspector] _SunColor("Sun Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _CameraHeight("Camera Height", Float) = 0
        [HideInInspector] _AtmosphereHeight("Atmosphere Height", Float) = 100000
    }

    SubShader
    {
        Tags
        {
            "Queue"="Background"
            "RenderType"="Background"
            "PreviewType"="Skybox"
            "RenderPipeline"="UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "AtmosphereSky"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_SkyViewLUT);
            SAMPLER(sampler_SkyViewLUT);

            TEXTURE2D(_TransmittanceLUT);
            SAMPLER(sampler_TransmittanceLUT);

            CBUFFER_START(UnityPerMaterial)
                float _SkyExposure;
                float _SunDiskIntensity;
                float _SunAngularRadiusDeg;
                float _SunDiskSoftness;
                float4 _SunDirWS;
                float4 _SunColor;
                float _CameraHeight;
                float _AtmosphereHeight;
            CBUFFER_END
            

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 skyDirection : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.skyDirection = IN.positionOS.xyz;
                return OUT;
            }

            float2 DirectionToSkyUV(float3 direction)
            {
                direction = normalize(direction);

                float azimuth = atan2(direction.z, direction.x);
                float elevation = asin(clamp(direction.y, -1.0, 1.0));

                float u = azimuth / (2.0 * PI) + 0.5;
                float v = elevation / PI + 0.5;

                return float2(frac(u), saturate(v));
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 viewDir = normalize(IN.skyDirection);
                float3 sunDir = normalize(_SunDirWS.xyz);

                float2 skyUV = DirectionToSkyUV(viewDir);
                float3 skyColor = SAMPLE_TEXTURE2D_LOD(_SkyViewLUT, sampler_SkyViewLUT, skyUV, 0).rgb;

                float height01 = saturate(_CameraHeight / max(_AtmosphereHeight, 1.0));
                float sunMu = clamp(sunDir.y, -1.0, 1.0);
                float sunTransmittanceU = sunMu * 0.5 + 0.5;

                float3 sunTransmittance = SAMPLE_TEXTURE2D_LOD(
                    _TransmittanceLUT,
                    sampler_TransmittanceLUT,
                    float2(sunTransmittanceU, height01),
                    0
                ).rgb;

                float sunDot = saturate(dot(viewDir, sunDir));

                float radiusRad = radians(_SunAngularRadiusDeg);
                float innerThreshold = cos(radiusRad);
                float outerThreshold = cos(radiusRad * _SunDiskSoftness);

                float sunDisk = smoothstep(outerThreshold, innerThreshold, sunDot);

                float3 sunColor = _SunColor.rgb * sunTransmittance * sunDisk * _SunDiskIntensity;

                float3 hdrColor = max(skyColor + sunColor, 0.0);
                float3 finalColor = 1.0 - exp(-hdrColor * _SkyExposure);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}