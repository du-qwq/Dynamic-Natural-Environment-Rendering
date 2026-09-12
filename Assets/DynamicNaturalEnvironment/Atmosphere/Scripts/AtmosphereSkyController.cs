using UnityEngine;

[ExecuteAlways]
public class AtmosphereSkyController : MonoBehaviour
{
    [Header("资源引用")]
    [Tooltip("Stage 03A1 使用的 AtmosphereLUT.compute")]
    [SerializeField] private ComputeShader atmosphereCompute;

    [Tooltip("使用 Custom/Atmosphere/AtmosphereSky Shader 的天空材质")]
    [SerializeField] private Material skyMaterial;

    [Tooltip("场景中的太阳方向光")]
    [SerializeField] private Light sunLight;

    [Tooltip("用于计算观察高度的主摄像机")]
    [SerializeField] private Camera targetCamera;

    [Header("行星")]
    [Tooltip("行星半径，单位：米")]
    [SerializeField] private float planetRadius = 6360000f;

    [Tooltip("大气层高度，单位：米")]
    [SerializeField] private float atmosphereHeight = 100000f;

    [Tooltip("Unity 世界中对应海平面的 Y 高度")]
    [SerializeField] private float seaLevel = 0f;

    [Header("瑞利散射")]
    [Tooltip("瑞利密度随高度衰减的尺度高度")]
    [SerializeField] private float rayleighScaleHeight = 8500f;

    [Tooltip("蓝天散射强度倍率")]
    [SerializeField, Range(0f, 3f)] private float rayleighStrength = 1f;

    [Header("米氏散射")]
    [Tooltip("米氏粒子密度随高度衰减的尺度高度")]
    [SerializeField] private float mieScaleHeight = 1200f;

    [Tooltip("米氏散射强度。过高会让天空发灰")]
    [SerializeField, Range(0f, 3f)] private float mieStrength = 0.35f;

    [Tooltip("米氏吸收强度，用于抑制过度泛白")]
    [SerializeField, Range(0f, 3f)] private float mieAbsorptionStrength = 0.2f;

    [Tooltip("米氏前向散射参数。越接近 1，亮区越集中在太阳附近")]
    [SerializeField, Range(-0.95f, 0.95f)] private float mieG = 0.76f;

    [Header("臭氧")]
    [Tooltip("臭氧吸收强度倍率")]
    [SerializeField, Range(0f, 3f)] private float ozoneStrength = 1f;

    [Header("太阳")]
    [Tooltip("参与大气散射计算的太阳辐照强度")]
    [SerializeField, Range(0f, 50f)] private float sunIntensity = 20f;

    [Tooltip("天空中太阳圆盘的显示亮度")]
    [SerializeField, Range(0f, 100f)] private float sunDiskIntensity = 25f;

    [Tooltip("太阳视角半径，真实太阳约为 0.27 度")]
    [SerializeField, Range(0.1f, 1f)] private float sunAngularRadius = 0.27f;

    [Tooltip("太阳圆盘边缘柔和程度")]
    [SerializeField, Range(1f, 3f)] private float sunDiskSoftness = 1.35f;

    [Header("天空显示")]
    [Tooltip("最终天空曝光，只影响显示，不改变散射计算")]
    [SerializeField, Range(0.1f, 5f)] private float skyExposure = 1.2f;

    [Tooltip("启用后自动设置为当前场景全局 Skybox")]
    [SerializeField] private bool applyAsGlobalSkybox = true;

    private const int TransmittanceWidth = 256;
    private const int TransmittanceHeight = 64;
    private const int SkyViewWidth = 256;
    private const int SkyViewHeight = 128;

    private RenderTexture transmittanceLUT;
    private RenderTexture skyViewLUT;

    private int transmittanceKernel = -1;
    private int skyViewKernel = -1;

    private int lastAtmosphereHash;
    private Vector3 lastSunDirection;
    private float lastCameraHeight = float.MinValue;

    private bool forceTransmittanceUpdate = true;
    private bool forceSkyUpdate = true;
    private Material previousSkybox;

    private void OnEnable()
    {
        AutoAssign();

        if (applyAsGlobalSkybox && skyMaterial != null && RenderSettings.skybox != skyMaterial)
            previousSkybox = RenderSettings.skybox;

        EnsureResources();
        RefreshAtmosphere(true);
    }

    private void Update()
    {
        AutoAssign();

        if (atmosphereCompute == null || skyMaterial == null || sunLight == null || targetCamera == null) return;

        EnsureResources();
        RefreshAtmosphere(false);
    }

    private void OnValidate()
    {
        planetRadius = Mathf.Max(1000f, planetRadius);
        atmosphereHeight = Mathf.Max(1000f, atmosphereHeight);
        rayleighScaleHeight = Mathf.Max(100f, rayleighScaleHeight);
        mieScaleHeight = Mathf.Max(10f, mieScaleHeight);
        sunAngularRadius = Mathf.Max(0.01f, sunAngularRadius);
        sunDiskSoftness = Mathf.Max(1f, sunDiskSoftness);

        forceTransmittanceUpdate = true;
        forceSkyUpdate = true;
    }

    private void OnDisable()
    {
        if (applyAsGlobalSkybox && skyMaterial != null && RenderSettings.skybox == skyMaterial)
            RenderSettings.skybox = previousSkybox;

        ReleaseResources();
    }

    private void OnDestroy()
    {
        ReleaseResources();
    }

    private void AutoAssign()
    {
        if (targetCamera == null) targetCamera = Camera.main;

        if (sunLight == null)
        {
            if (RenderSettings.sun != null) sunLight = RenderSettings.sun;
            else
            {
                Light[] lights = FindObjectsOfType<Light>();
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i].type != LightType.Directional) continue;
                    sunLight = lights[i];
                    break;
                }
            }
        }
    }

    private void EnsureResources()
    {
        if (atmosphereCompute == null) return;

        if (transmittanceKernel < 0) transmittanceKernel = atmosphereCompute.FindKernel("GenerateTransmittance");
        if (skyViewKernel < 0) skyViewKernel = atmosphereCompute.FindKernel("GenerateSkyView");

        if (transmittanceLUT == null || !transmittanceLUT.IsCreated())
        {
            transmittanceLUT = CreateLUT(TransmittanceWidth, TransmittanceHeight, "Atmosphere_TransmittanceLUT");
            forceTransmittanceUpdate = true;
            forceSkyUpdate = true;
        }

        if (skyViewLUT == null || !skyViewLUT.IsCreated())
        {
            skyViewLUT = CreateLUT(SkyViewWidth, SkyViewHeight, "Atmosphere_SkyViewLUT");
            forceSkyUpdate = true;
        }
    }

    private RenderTexture CreateLUT(int width, int height, string textureName)
    {
        RenderTexture texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf)
        {
            name = textureName,
            enableRandomWrite = true,
            useMipMap = false,
            autoGenerateMips = false,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        texture.Create();
        return texture;
    }

    private void RefreshAtmosphere(bool forceAll)
    {
        if (transmittanceLUT == null || skyViewLUT == null) return;

        int atmosphereHash = CalculateAtmosphereHash();
        Vector3 sunDirection = GetSunDirection();
        float cameraHeight = GetCameraHeight();

        bool atmosphereChanged = forceAll || forceTransmittanceUpdate || atmosphereHash != lastAtmosphereHash;
        bool sunChanged = forceAll || Vector3.Dot(sunDirection, lastSunDirection) < 0.999999f;
        bool cameraChanged = forceAll || Mathf.Abs(cameraHeight - lastCameraHeight) > 0.05f;

        SetCommonComputeParameters(sunDirection, cameraHeight);

        if (atmosphereChanged)
        {
            GenerateTransmittance();
            lastAtmosphereHash = atmosphereHash;
            forceTransmittanceUpdate = false;
            forceSkyUpdate = true;
        }

        if (forceAll || forceSkyUpdate || atmosphereChanged || sunChanged || cameraChanged)
        {
            GenerateSkyView();
            lastSunDirection = sunDirection;
            lastCameraHeight = cameraHeight;
            forceSkyUpdate = false;
        }

        ApplySkyMaterial(sunDirection, cameraHeight);
    }

    private void SetCommonComputeParameters(Vector3 sunDirection, float cameraHeight)
    {
        atmosphereCompute.SetFloat("_PlanetRadius", planetRadius);
        atmosphereCompute.SetFloat("_AtmosphereHeight", atmosphereHeight);
        atmosphereCompute.SetFloat("_RayleighScaleHeight", rayleighScaleHeight);
        atmosphereCompute.SetFloat("_MieScaleHeight", mieScaleHeight);
        atmosphereCompute.SetFloat("_RayleighStrength", rayleighStrength);
        atmosphereCompute.SetFloat("_MieStrength", mieStrength);
        atmosphereCompute.SetFloat("_MieAbsorptionStrength", mieAbsorptionStrength);
        atmosphereCompute.SetFloat("_OzoneStrength", ozoneStrength);
        atmosphereCompute.SetFloat("_MieG", mieG);
        atmosphereCompute.SetFloat("_SunIntensity", sunIntensity);
        atmosphereCompute.SetFloat("_CameraHeight", cameraHeight);
        atmosphereCompute.SetVector("_SunDirWS", new Vector4(sunDirection.x, sunDirection.y, sunDirection.z, 0f));

        atmosphereCompute.SetInt("_TransmittanceWidth", TransmittanceWidth);
        atmosphereCompute.SetInt("_TransmittanceHeight", TransmittanceHeight);
        atmosphereCompute.SetInt("_SkyViewWidth", SkyViewWidth);
        atmosphereCompute.SetInt("_SkyViewHeight", SkyViewHeight);
    }

    private void GenerateTransmittance()
    {
        atmosphereCompute.SetTexture(transmittanceKernel, "_TransmittanceLUT", transmittanceLUT);

        int groupsX = Mathf.CeilToInt(TransmittanceWidth / 8f);
        int groupsY = Mathf.CeilToInt(TransmittanceHeight / 8f);

        atmosphereCompute.Dispatch(transmittanceKernel, groupsX, groupsY, 1);
    }

    private void GenerateSkyView()
    {
        atmosphereCompute.SetTexture(skyViewKernel, "_TransmittanceLUTRead", transmittanceLUT);
        atmosphereCompute.SetTexture(skyViewKernel, "_SkyViewLUT", skyViewLUT);

        int groupsX = Mathf.CeilToInt(SkyViewWidth / 8f);
        int groupsY = Mathf.CeilToInt(SkyViewHeight / 8f);

        atmosphereCompute.Dispatch(skyViewKernel, groupsX, groupsY, 1);
    }

    private void ApplySkyMaterial(Vector3 sunDirection, float cameraHeight)
    {
        skyMaterial.SetTexture("_TransmittanceLUT", transmittanceLUT);
        skyMaterial.SetTexture("_SkyViewLUT", skyViewLUT);
        skyMaterial.SetVector("_SunDirWS", new Vector4(sunDirection.x, sunDirection.y, sunDirection.z, 0f));
        skyMaterial.SetColor("_SunColor", sunLight.color);
        skyMaterial.SetFloat("_SunDiskIntensity", sunDiskIntensity);
        skyMaterial.SetFloat("_SunAngularRadiusDeg", sunAngularRadius);
        skyMaterial.SetFloat("_SunDiskSoftness", sunDiskSoftness);
        skyMaterial.SetFloat("_SkyExposure", skyExposure);
        skyMaterial.SetFloat("_CameraHeight", cameraHeight);
        skyMaterial.SetFloat("_AtmosphereHeight", atmosphereHeight);

        if (applyAsGlobalSkybox)
        {
            RenderSettings.skybox = skyMaterial;
            RenderSettings.sun = sunLight;
        }
    }

    private Vector3 GetSunDirection()
    {
        return sunLight != null ? -sunLight.transform.forward.normalized : Vector3.up;
    }

    private float GetCameraHeight()
    {
        if (targetCamera == null) return 0f;
        return Mathf.Clamp(targetCamera.transform.position.y - seaLevel, 0f, atmosphereHeight - 1f);
    }

    private int CalculateAtmosphereHash()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + planetRadius.GetHashCode();
            hash = hash * 31 + atmosphereHeight.GetHashCode();
            hash = hash * 31 + rayleighScaleHeight.GetHashCode();
            hash = hash * 31 + rayleighStrength.GetHashCode();
            hash = hash * 31 + mieScaleHeight.GetHashCode();
            hash = hash * 31 + mieStrength.GetHashCode();
            hash = hash * 31 + mieAbsorptionStrength.GetHashCode();
            hash = hash * 31 + mieG.GetHashCode();
            hash = hash * 31 + ozoneStrength.GetHashCode();
            hash = hash * 31 + sunIntensity.GetHashCode();
            return hash;
        }
    }

    private void ReleaseResources()
    {
        ReleaseTexture(ref transmittanceLUT);
        ReleaseTexture(ref skyViewLUT);
        transmittanceKernel = -1;
        skyViewKernel = -1;
    }

    private void ReleaseTexture(ref RenderTexture texture)
    {
        if (texture == null) return;

        texture.Release();

        if (Application.isPlaying) Destroy(texture);
        else DestroyImmediate(texture);

        texture = null;
    }
}