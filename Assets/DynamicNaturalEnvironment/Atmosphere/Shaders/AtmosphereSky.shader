Shader "Custom/SkyboxSingleScattering"
{
    Properties
    {
        _SunIntensity("太阳强度", Float) = 20
        _Exposure("天空曝光", Float) = 1

        _PlanetRadius("行星半径", Float) = 6360000
        _AtmosphereHeight("大气层高度", Float) = 100000
        _CameraAltitude("摄像机海拔", Float) = 1

        _RayleighScaleHeight("瑞利尺度高度", Float) = 8500
        _RayleighStrength("瑞利强度", Float) = 1

        _MieScaleHeight("米氏尺度高度", Float) = 1200
        _MieStrength("米氏强度", Float) = 1
        _MieG("米氏前向散射", Range(-0.99,0.99)) = 0.76

        _SunDiskColor("太阳颜色", Color) = (1,0.95,0.82,1)
        _SunDiskIntensity("太阳圆盘强度", Float) = 20
        _SunDiskAngularRadius("太阳圆盘大小", Float) = 0.27
        _SunDiskSoftness("太阳圆盘柔和度", Float) = 0.15
        _SunGlowIntensity("太阳光晕强度", Float) = 2
        _SunGlowSize("太阳光晕大小", Float) = 2
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
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define PI 3.14159265359
            #define VIEW_SAMPLE_COUNT 16
            #define LIGHT_SAMPLE_COUNT 12

            CBUFFER_START(UnityPerMaterial)
                float _SunIntensity;
                float _Exposure;

                float _PlanetRadius;
                float _AtmosphereHeight;
                float _CameraAltitude;

                float _RayleighScaleHeight;
                float _RayleighStrength;

                float _MieScaleHeight;
                float _MieStrength;
                float _MieG;

                float3 _SunDirWS;

                float4 _SunDiskColor;
                float _SunDiskIntensity;
                float _SunDiskAngularRadius;
                float _SunDiskSoftness;
                float _SunGlowIntensity;
                float _SunGlowSize;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 directionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.directionWS = normalize(TransformObjectToWorldDir(input.positionOS));
                return output;
            }

            float2 RaySphere(float3 origin, float3 direction, float radius)
            {
                float b = dot(origin, direction);
                float c = dot(origin, origin) - radius * radius;
                float discriminant = b * b - c;

                if (discriminant < 0.0) return float2(-1.0, -1.0);

                float s = sqrt(discriminant);
                return float2(-b - s, -b + s);
            }

            float GetHeightKm(float3 positionKm, float planetRadiusKm)
            {
                return max(length(positionKm) - planetRadiusKm, 0.0);
            }

            float RayleighDensity(float heightKm)
            {
                float scaleHeightKm = max(_RayleighScaleHeight * 0.001, 0.001);
                return exp(-heightKm / scaleHeightKm);
            }

            float MieDensity(float heightKm)
            {
                float scaleHeightKm = max(_MieScaleHeight * 0.001, 0.001);
                return exp(-heightKm / scaleHeightKm);
            }

            float OzoneDensity(float heightKm)
            {
                return saturate(1.0 - abs(heightKm - 25.0) / 15.0);
            }

            float3 RayleighScatteringKm(float heightKm)
            {
                float3 coefficient = float3(0.005802, 0.013558, 0.033100);
                return coefficient * RayleighDensity(heightKm) * _RayleighStrength;
            }

            float3 MieScatteringKm(float heightKm)
            {
                float3 coefficient = float3(0.003996, 0.003996, 0.003996);
                return coefficient * MieDensity(heightKm) * _MieStrength;
            }

            float3 MieAbsorptionKm(float heightKm)
            {
                float3 coefficient = float3(0.004400, 0.004400, 0.004400);
                return coefficient * MieDensity(heightKm) * _MieStrength;
            }

            float3 OzoneAbsorptionKm(float heightKm)
            {
                float3 coefficient = float3(0.000650, 0.001881, 0.000085);
                return coefficient * OzoneDensity(heightKm);
            }

            float3 ExtinctionKm(float heightKm)
            {
                return RayleighScatteringKm(heightKm) + MieScatteringKm(heightKm) + MieAbsorptionKm(heightKm) + OzoneAbsorptionKm(heightKm);
            }

            float RayleighPhase(float cosTheta)
            {
                return 3.0 / (16.0 * PI) * (1.0 + cosTheta * cosTheta);
            }

            float MiePhase(float cosTheta)
            {
                float g = _MieG;
                float g2 = g * g;
                float denominator = max(1.0 + g2 - 2.0 * g * cosTheta, 0.0001);
                return 3.0 / (8.0 * PI) * ((1.0 - g2) * (1.0 + cosTheta * cosTheta)) / ((2.0 + g2) * pow(denominator, 1.5));
            }

            float3 ScatteringKm(float heightKm, float cosTheta)
            {
                float3 rayleigh = RayleighScatteringKm(heightKm) * RayleighPhase(cosTheta);
                float3 mie = MieScatteringKm(heightKm) * MiePhase(cosTheta);
                return rayleigh + mie;
            }

            float3 IntegrateTransmittance(float3 startKm, float3 endKm, float planetRadiusKm)
            {
                float3 delta = endKm - startKm;
                float distanceKm = length(delta);

                if (distanceKm <= 0.0001) return 1.0;

                float3 direction = delta / distanceKm;
                float stepKm = distanceKm / LIGHT_SAMPLE_COUNT;
                float3 opticalDepth = 0.0;
                float3 positionKm = startKm + direction * stepKm * 0.5;

                [loop]
                for (int i = 0; i < LIGHT_SAMPLE_COUNT; i++)
                {
                    float heightKm = GetHeightKm(positionKm, planetRadiusKm);
                    opticalDepth += ExtinctionKm(heightKm) * stepKm;
                    positionKm += direction * stepKm;
                }

                return exp(-opticalDepth);
            }

            float3 SunTransmittance(float3 positionKm, float3 sunDirection, float planetRadiusKm, float atmosphereRadiusKm)
            {
                float2 planetHit = RaySphere(positionKm, sunDirection, planetRadiusKm);
                if (planetHit.x > 0.001) return 0.0;

                float2 atmosphereHit = RaySphere(positionKm, sunDirection, atmosphereRadiusKm);
                if (atmosphereHit.y <= 0.0) return 0.0;

                float3 endKm = positionKm + sunDirection * atmosphereHit.y;
                return IntegrateTransmittance(positionKm, endKm, planetRadiusKm);
            }

            float SunDiskMask(float3 rayDirection, float3 sunDirection)
            {
                float cosTheta = dot(rayDirection, sunDirection);
                float radius = radians(_SunDiskAngularRadius);
                float softness = radians(_SunDiskSoftness);
                return smoothstep(cos(radius + softness), cos(radius), cosTheta);
            }

            float SunGlowMask(float3 rayDirection, float3 sunDirection)
            {
                float cosTheta = dot(rayDirection, sunDirection);
                float inner = cos(radians(_SunDiskAngularRadius));
                float outer = cos(radians(_SunGlowSize));
                return smoothstep(outer, inner, cosTheta);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 rayDirection = normalize(input.directionWS);
                float3 sunDirection = normalize(_SunDirWS);

                float planetRadiusKm = _PlanetRadius * 0.001;
                float atmosphereRadiusKm = planetRadiusKm + _AtmosphereHeight * 0.001;
                float cameraAltitudeKm = max(_CameraAltitude * 0.001, 0.001);

                float3 cameraPositionKm = float3(0.0, planetRadiusKm + cameraAltitudeKm, 0.0);

                float2 atmosphereHit = RaySphere(cameraPositionKm, rayDirection, atmosphereRadiusKm);
                if (atmosphereHit.y <= 0.0) return half4(0.02, 0.04, 0.08, 1.0);

                float rayStart = max(atmosphereHit.x, 0.0);
                float rayEnd = atmosphereHit.y;

                float2 planetHit = RaySphere(cameraPositionKm, rayDirection, planetRadiusKm);
                if (planetHit.x > 0.001) rayEnd = min(rayEnd, planetHit.x);

                float rayLengthKm = rayEnd - rayStart;
                if (rayLengthKm <= 0.0001) return half4(0.02, 0.025, 0.03, 1.0);

                float stepKm = rayLengthKm / VIEW_SAMPLE_COUNT;
                float cosTheta = dot(rayDirection, sunDirection);

                float3 accumulatedColor = 0.0;
                float3 viewOpticalDepth = 0.0;
                float3 positionKm = cameraPositionKm + rayDirection * (rayStart + stepKm * 0.5);

                [loop]
                for (int i = 0; i < VIEW_SAMPLE_COUNT; i++)
                {
                    float heightKm = GetHeightKm(positionKm, planetRadiusKm);
                    float3 extinction = ExtinctionKm(heightKm);

                    viewOpticalDepth += extinction * stepKm;

                    float3 viewTransmittance = exp(-viewOpticalDepth);
                    float3 sunTransmittance = SunTransmittance(positionKm, sunDirection, planetRadiusKm, atmosphereRadiusKm);
                    float3 scattering = ScatteringKm(heightKm, cosTheta);

                    accumulatedColor += sunTransmittance * scattering * viewTransmittance * stepKm * _SunIntensity;
                    positionKm += rayDirection * stepKm;
                }

                float3 cameraSunTransmittance = SunTransmittance(cameraPositionKm, sunDirection, planetRadiusKm, atmosphereRadiusKm);

                float sunDisk = SunDiskMask(rayDirection, sunDirection);
                float sunGlow = SunGlowMask(rayDirection, sunDirection);

                accumulatedColor += _SunDiskColor.rgb * cameraSunTransmittance * _SunDiskIntensity * sunDisk;
                accumulatedColor += _SunDiskColor.rgb * cameraSunTransmittance * _SunGlowIntensity * sunGlow;

                float horizon = 1.0 - saturate(abs(rayDirection.y));
                accumulatedColor += float3(0.006, 0.012, 0.020) * horizon;

                float3 color = 1.0 - exp(-max(accumulatedColor, 0.0) * _Exposure);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}