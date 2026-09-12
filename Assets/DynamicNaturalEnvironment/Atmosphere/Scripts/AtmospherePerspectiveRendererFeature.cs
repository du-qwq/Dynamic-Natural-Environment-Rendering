using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class AtmospherePerspectiveRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [InspectorName("启用")] public bool enable = true;
        [InspectorName("渲染时机")] public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        [Header("调试")]
        [InspectorName("调试模式"), Range(0, 3)]
        [Tooltip("0 = 正常效果\n1 = 强制洋红色（验证 Pass 是否执行）\n2 = 深度可视化\n3 = 空气透视遮罩可视化")]
        public int debugMode = 0;

        [Header("距离控制")]
        [InspectorName("近景开始距离"), Min(0f)] public float startDistance = 80f;
        [InspectorName("最远距离"), Min(1f)] public float maxDistance = 1800f;
        [InspectorName("距离衰减尺度"), Min(0.001f)] public float distanceScale = 350f;
        [InspectorName("空气透视强度"), Range(0f, 4f)] public float strength = 1f;

        [Header("天空颜色")]
        [InspectorName("地平线颜色")] public Color horizonColor = new Color(0.74f, 0.82f, 0.92f, 1f);
        [InspectorName("天顶颜色")] public Color zenithColor = new Color(0.32f, 0.55f, 0.92f, 1f);
        [InspectorName("太阳散射颜色")] public Color sunScatterColor = new Color(1f, 0.92f, 0.78f, 1f);
        [InspectorName("太阳散射强度"), Range(0f, 4f)] public float sunScatterStrength = 1f;
        [InspectorName("米氏前向性"), Range(0f, 0.95f)] public float mieG = 0.76f;

        [Header("高度影响")]
        [InspectorName("低处额外雾化强度"), Range(0f, 2f)] public float heightFogStrength = 0.35f;
        [InspectorName("最低高度")] public float minHeight = 0f;
        [InspectorName("最高高度")] public float maxHeight = 300f;
    }

    class AtmospherePerspectivePass : ScriptableRenderPass
    {
        static readonly int APParams0 = Shader.PropertyToID("_AP_Params0");
        static readonly int APParams1 = Shader.PropertyToID("_AP_Params1");
        static readonly int APHeightRange = Shader.PropertyToID("_AP_HeightRange");
        static readonly int APHorizonColor = Shader.PropertyToID("_AP_HorizonColor");
        static readonly int APZenithColor = Shader.PropertyToID("_AP_ZenithColor");
        static readonly int APSunScatterColor = Shader.PropertyToID("_AP_SunScatterColor");
        static readonly int APSunDirWS = Shader.PropertyToID("_AP_SunDirWS");
        static readonly int APDebugMode = Shader.PropertyToID("_AP_DebugMode");

        readonly Material material;
        readonly Settings settings;

        RTHandle source;
        RTHandle tempColor;

        public AtmospherePerspectivePass(Material material, Settings settings)
        {
            this.material = material;
            this.settings = settings;
            ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
        }

        public void Setup(RTHandle source)
        {
            this.source = source;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            RenderingUtils.ReAllocateIfNeeded(ref tempColor, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_AtmospherePerspectiveTemp");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!settings.enable || material == null || source == null) return;
            if (renderingData.cameraData.isPreviewCamera) return;
            if (renderingData.cameraData.cameraType == CameraType.Reflection) return;

            CommandBuffer cmd = CommandBufferPool.Get("Atmosphere Perspective");

            Light sun = RenderSettings.sun;
            Vector3 sunDir = sun != null ? -sun.transform.forward.normalized : Vector3.up;

            cmd.SetGlobalInt(APDebugMode, settings.debugMode);
            cmd.SetGlobalVector(APSunDirWS, new Vector4(sunDir.x, sunDir.y, sunDir.z, 0f));
            cmd.SetGlobalVector(APParams0, new Vector4(settings.startDistance, settings.maxDistance, settings.distanceScale, settings.strength));
            cmd.SetGlobalVector(APParams1, new Vector4(settings.sunScatterStrength, settings.mieG, settings.heightFogStrength, 0f));
            cmd.SetGlobalVector(APHeightRange, new Vector4(settings.minHeight, settings.maxHeight, 0f, 0f));
            cmd.SetGlobalColor(APHorizonColor, settings.horizonColor.linear);
            cmd.SetGlobalColor(APZenithColor, settings.zenithColor.linear);
            cmd.SetGlobalColor(APSunScatterColor, settings.sunScatterColor.linear);

            Blitter.BlitCameraTexture(cmd, source, tempColor);
            Blitter.BlitCameraTexture(cmd, tempColor, source, material, 0);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            tempColor?.Release();
        }
    }

    [SerializeField] private Settings settings = new Settings();
    [SerializeField] private Shader shader;

    private Material material;
    private AtmospherePerspectivePass pass;

    public override void Create()
    {
        if (shader == null) shader = Shader.Find("Hidden/DynamicNaturalEnvironment/AtmospherePerspective");

        if (shader == null)
        {
            Debug.LogError("找不到 AtmospherePerspective Shader。");
            return;
        }

        material = CoreUtils.CreateEngineMaterial(shader);
        pass = new AtmospherePerspectivePass(material, settings);
        pass.renderPassEvent = settings.renderPassEvent;
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (pass == null || material == null || !settings.enable) return;
        pass.renderPassEvent = settings.renderPassEvent;
        pass.Setup(renderer.cameraColorTargetHandle);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (pass == null || material == null || !settings.enable) return;
        pass.renderPassEvent = settings.renderPassEvent;
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Dispose();
        if (material != null) CoreUtils.Destroy(material);
    }
}