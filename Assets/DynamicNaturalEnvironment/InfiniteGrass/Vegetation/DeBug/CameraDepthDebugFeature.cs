using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CameraDepthDebugFeature : ScriptableRendererFeature
{
    public enum DebugMode
    {
        CloudOcclusionMask,
        LinearDepth
    }

    [System.Serializable]
    public class Settings
    {
        public bool enabled = true;
        public bool showInSceneView = false;
        public DebugMode mode = DebugMode.CloudOcclusionMask;
        [Min(1f)] public float maxDistance = 200f;
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
    }

    public Settings settings = new Settings();

    [SerializeField] private Shader debugShader;
    private CameraDepthDebugPass pass;
    private Material material;
    private bool missingShaderReported;

    public override void Create()
    {
        CoreUtils.Destroy(material);
        material = null;
        pass = null;
        missingShaderReported = false;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!isActive || !settings.enabled) return;

        Camera camera = renderingData.cameraData.camera;
        if (camera == null) return;
        if (camera.cameraType == CameraType.Preview) return;
        if (camera.cameraType == CameraType.Reflection) return;
        if (camera.cameraType == CameraType.SceneView && !settings.showInSceneView) return;
        if (!EnsurePass()) return;

        pass.renderPassEvent = settings.renderPassEvent;
        pass.Setup(settings);

        renderer.EnqueuePass(pass);
    }

    private bool EnsurePass()
    {
        if (pass != null && material != null) return true;

        Shader shader = debugShader != null ? debugShader : Shader.Find("Hidden/Vegetation/CameraDepthDebug");
        if (shader == null)
        {
            if (!missingShaderReported)
            {
                Debug.LogError("找不到 Shader: Hidden/Vegetation/CameraDepthDebug");
                missingShaderReported = true;
            }
            return false;
        }

        material = CoreUtils.CreateEngineMaterial(shader);
        pass = new CameraDepthDebugPass(material, settings);
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        material = null;
        pass = null;
    }

    private class CameraDepthDebugPass : ScriptableRenderPass
    {
        private readonly Material material;
        private Settings settings;

        private static readonly int DebugModeID = Shader.PropertyToID("_DebugMode");
        private static readonly int MaxDistanceID = Shader.PropertyToID("_MaxDistance");

        public CameraDepthDebugPass(Material material, Settings settings)
        {
            this.material = material;
            this.settings = settings;
        }

        public void Setup(Settings newSettings)
        {
            settings = newSettings;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureTarget(renderingData.cameraData.renderer.cameraColorTargetHandle);
            ConfigureClear(ClearFlag.None, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null) return;

            CommandBuffer cmd = CommandBufferPool.Get("Camera Depth Debug");

            material.SetFloat(DebugModeID, settings.mode == DebugMode.CloudOcclusionMask ? 0f : 1f);
            material.SetFloat(MaxDistanceID, Mathf.Max(settings.maxDistance, 1f));

            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
