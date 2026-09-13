using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class AtmosphereSkyController : MonoBehaviour
{
    [Header("资源引用")]
    [InspectorName("天空材质")] public Material skyMaterial;
    [InspectorName("太阳方向光")] public Light sunLight;
    [InspectorName("目标摄像机")] public Camera targetCamera;

    [Header("行星")]
    [InspectorName("行星半径（米）")] public float planetRadius = 6360000.0f;
    [InspectorName("大气层高度（米）")] public float atmosphereHeight = 100000.0f;
    [InspectorName("海平面高度")] public float seaLevelY = 0.0f;

    [Header("瑞利散射")]
    [InspectorName("瑞利尺度高度（米）")] public float rayleighScaleHeight = 8500.0f;
    [InspectorName("瑞利强度"), Range(0.0f, 4.0f)] public float rayleighStrength = 1.0f;

    [Header("米氏散射")]
    [InspectorName("米氏尺度高度（米）")] public float mieScaleHeight = 1200.0f;
    [InspectorName("米氏强度"), Range(0.0f, 4.0f)] public float mieStrength = 1.0f;
    [InspectorName("米氏前向散射"), Range(-0.99f, 0.99f)] public float mieG = 0.76f;

    [Header("太阳")]
    [InspectorName("太阳强度"), Range(0.0f, 100.0f)] public float sunIntensity = 20.0f;
    [InspectorName("太阳圆盘强度"), Range(0.0f, 100.0f)] public float sunDiskIntensity = 20.0f;
    [InspectorName("太阳圆盘大小"), Range(0.05f, 2.0f)] public float sunDiskAngularRadius = 0.27f;
    [InspectorName("太阳圆盘柔和度"), Range(0.01f, 1.0f)] public float sunDiskSoftness = 0.15f;
    [InspectorName("太阳光晕强度"), Range(0.0f, 20.0f)] public float sunGlowIntensity = 2.0f;
    [InspectorName("太阳光晕大小"), Range(0.5f, 15.0f)] public float sunGlowSize = 2.0f;

    [Header("显示")]
    [InspectorName("天空曝光"), Range(0.1f, 8.0f)] public float exposure = 1.0f;

    [Header("体积云光照联动")]
    [InspectorName("大气影响体积云")]
    [Tooltip("只把大气计算出的太阳颜色传给体积云，不修改 Directional Light 本身。")]
    public bool affectVolumetricClouds = true;

    [InspectorName("云太阳染色强度")]
    [Tooltip("0 = 体积云完全使用 Directional Light 原色，1 = 完全使用大气透射后的太阳颜色。建议白天使用 0.2~0.5。")]
    [Range(0.0f, 1.0f)]
    public float cloudSunTintStrength = 0.35f;

    [InspectorName("云太阳采样步数")]
    [Tooltip("计算太阳穿过大气后的颜色。这里只在 CPU 每帧计算一次，24 已经足够。")]
    [Range(8, 48)]
    public int cloudSunSampleCount = 24;

    private static readonly int CloudSunTintID = Shader.PropertyToID("_DNEAtmosphereCloudSunTint");

    private void OnEnable()
    {
        ResolveReferences();
        Apply();
    }

    private void Update()
    {
        ResolveReferences();
        Apply();
    }

    private void OnValidate()
    {
        planetRadius = Mathf.Max(1000.0f, planetRadius);
        atmosphereHeight = Mathf.Max(1000.0f, atmosphereHeight);
        rayleighScaleHeight = Mathf.Max(1.0f, rayleighScaleHeight);
        mieScaleHeight = Mathf.Max(1.0f, mieScaleHeight);
        cloudSunSampleCount = Mathf.Clamp(cloudSunSampleCount, 8, 48);
        ResolveReferences();
        Apply();
    }

    private void OnDisable()
    {
        Shader.SetGlobalVector(CloudSunTintID, Vector4.one);
    }

    private void ResolveReferences()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (sunLight == null) sunLight = RenderSettings.sun;
    }

    private void Apply()
    {
        if (skyMaterial == null) return;

        RenderSettings.skybox = skyMaterial;
        if (sunLight != null) RenderSettings.sun = sunLight;

        Vector3 sunDirection = sunLight != null ? -sunLight.transform.forward : Vector3.up;
        sunDirection.Normalize();

        float cameraAltitude = 1.0f;
        if (targetCamera != null) cameraAltitude = Mathf.Max(targetCamera.transform.position.y - seaLevelY, 1.0f);

        skyMaterial.SetVector("_SunDirWS", sunDirection);
        skyMaterial.SetFloat("_CameraAltitude", cameraAltitude);
        skyMaterial.SetFloat("_PlanetRadius", planetRadius);
        skyMaterial.SetFloat("_AtmosphereHeight", atmosphereHeight);
        skyMaterial.SetFloat("_RayleighScaleHeight", rayleighScaleHeight);
        skyMaterial.SetFloat("_RayleighStrength", rayleighStrength);
        skyMaterial.SetFloat("_MieScaleHeight", mieScaleHeight);
        skyMaterial.SetFloat("_MieStrength", mieStrength);
        skyMaterial.SetFloat("_MieG", mieG);
        skyMaterial.SetFloat("_SunIntensity", sunIntensity);
        skyMaterial.SetFloat("_SunDiskIntensity", sunDiskIntensity);
        skyMaterial.SetFloat("_SunDiskAngularRadius", sunDiskAngularRadius);
        skyMaterial.SetFloat("_SunDiskSoftness", sunDiskSoftness);
        skyMaterial.SetFloat("_SunGlowIntensity", sunGlowIntensity);
        skyMaterial.SetFloat("_SunGlowSize", sunGlowSize);
        skyMaterial.SetFloat("_Exposure", exposure);

        UpdateCloudSunTint(sunDirection, cameraAltitude);
    }

    private void UpdateCloudSunTint(Vector3 sunDirection, float cameraAltitudeMeters)
    {
        if (!affectVolumetricClouds)
        {
            Shader.SetGlobalVector(CloudSunTintID, Vector4.one);
            return;
        }

        Vector3 transmittance = EvaluateSunTransmittance(sunDirection, cameraAltitudeMeters);
        float maximum = Mathf.Max(transmittance.x, Mathf.Max(transmittance.y, transmittance.z));

        Vector3 normalizedTint = maximum > 0.0001f ? transmittance / maximum : new Vector3(1.0f, 0.45f, 0.15f);
        Vector3 finalTint = Vector3.Lerp(Vector3.one, normalizedTint, cloudSunTintStrength);

        Shader.SetGlobalVector(CloudSunTintID, new Vector4(finalTint.x, finalTint.y, finalTint.z, 1.0f));
    }

    private Vector3 EvaluateSunTransmittance(Vector3 sunDirection, float cameraAltitudeMeters)
    {
        float planetRadiusKm = planetRadius * 0.001f;
        float atmosphereRadiusKm = planetRadiusKm + atmosphereHeight * 0.001f;
        float cameraAltitudeKm = Mathf.Max(cameraAltitudeMeters * 0.001f, 0.001f);

        Vector3 origin = new Vector3(0.0f, planetRadiusKm + cameraAltitudeKm, 0.0f);
        Vector3 direction = sunDirection.normalized;

        if (RaySphere(origin, direction, planetRadiusKm, out float planetNear, out _) && planetNear > 0.001f) return new Vector3(1.0f, 0.32f, 0.08f);

        if (!RaySphere(origin, direction, atmosphereRadiusKm, out _, out float atmosphereFar) || atmosphereFar <= 0.0f) return Vector3.one;

        int sampleCount = Mathf.Clamp(cloudSunSampleCount, 8, 48);
        float stepKm = atmosphereFar / sampleCount;

        Vector3 opticalDepth = Vector3.zero;
        Vector3 position = origin + direction * stepKm * 0.5f;

        for (int i = 0; i < sampleCount; i++)
        {
            float heightKm = Mathf.Max(position.magnitude - planetRadiusKm, 0.0f);
            opticalDepth += EvaluateExtinctionKm(heightKm) * stepKm;
            position += direction * stepKm;
        }

        return new Vector3(Mathf.Exp(-opticalDepth.x), Mathf.Exp(-opticalDepth.y), Mathf.Exp(-opticalDepth.z));
    }

    private Vector3 EvaluateExtinctionKm(float heightKm)
    {
        float rayleighScaleKm = rayleighScaleHeight * 0.001f;
        float mieScaleKm = mieScaleHeight * 0.001f;

        float rayleighDensity = Mathf.Exp(-heightKm / Mathf.Max(rayleighScaleKm, 0.001f));
        float mieDensity = Mathf.Exp(-heightKm / Mathf.Max(mieScaleKm, 0.001f));
        float ozoneDensity = Mathf.Max(0.0f, 1.0f - Mathf.Abs(heightKm - 25.0f) / 15.0f);

        Vector3 rayleigh = new Vector3(0.005802f, 0.013558f, 0.033100f) * rayleighDensity * rayleighStrength;
        Vector3 mieScattering = new Vector3(0.003996f, 0.003996f, 0.003996f) * mieDensity * mieStrength;
        Vector3 mieAbsorption = new Vector3(0.004400f, 0.004400f, 0.004400f) * mieDensity * mieStrength;
        Vector3 ozoneAbsorption = new Vector3(0.000650f, 0.001881f, 0.000085f) * ozoneDensity;

        return rayleigh + mieScattering + mieAbsorption + ozoneAbsorption;
    }

    private static bool RaySphere(Vector3 origin, Vector3 direction, float radius, out float nearDistance, out float farDistance)
    {
        float b = Vector3.Dot(origin, direction);
        float c = Vector3.Dot(origin, origin) - radius * radius;
        float discriminant = b * b - c;

        if (discriminant < 0.0f)
        {
            nearDistance = -1.0f;
            farDistance = -1.0f;
            return false;
        }

        float root = Mathf.Sqrt(discriminant);
        nearDistance = -b - root;
        farDistance = -b + root;
        return true;
    }
}