using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class VegetationRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public bool renderSceneView = true;
        [Tooltip("Play模式下是否继续为SceneView执行Vegetation Forward。关闭可避免编辑器同时渲染Game和Scene两套植被。")]
        public bool renderSceneViewDuringPlay = false;
        public bool renderPreviewCamera = false;
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
        public bool enableAdditionalLights = true;
        [Range(0, 8)] public int maxVegetationAdditionalLights = 8;
        public bool enableDepthNormalsPass = true;
    }

    public Settings settings = new Settings();

    private VegetationForwardPass forwardPass;
    private VegetationDepthNormalsPass depthNormalsPass;

    public override void Create()
    {
        forwardPass = new VegetationForwardPass(settings);
        forwardPass.renderPassEvent = settings.renderPassEvent;
        depthNormalsPass = new VegetationDepthNormalsPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;

        if (camera == null) return;

        ResolveDiagnostics(out VegetationDiagnosticsMode diagnosticsMode, out int verboseInterval);

        string skipReason = null;
        if (renderingData.cameraData.renderType == CameraRenderType.Overlay) skipReason = "Overlay camera";
        else if (camera.cameraType == CameraType.Reflection) skipReason = "Reflection camera";
        else if (camera.cameraType == CameraType.Preview && !settings.renderPreviewCamera) skipReason = "Preview disabled";
        else if (camera.cameraType == CameraType.SceneView && !settings.renderSceneView) skipReason = "SceneView disabled";
        else if (camera.cameraType == CameraType.SceneView && Application.isPlaying && !settings.renderSceneViewDuringPlay) skipReason = "SceneView disabled during Play";

        bool enqueued = skipReason == null;
        VegetationDiagnostics.AddRenderPasses(
            camera,
            renderingData.cameraData,
            GetInstanceID(),
            name,
            enqueued,
            skipReason,
            diagnosticsMode,
            verboseInterval
        );

        if (!enqueued) return;

        forwardPass.renderPassEvent = settings.renderPassEvent;

        IReadOnlyList<VegetationRenderer> activeRenderers = VegetationRenderer.ActiveRenderers;
        for (int i = 0; settings.enableDepthNormalsPass && i < activeRenderers.Count; i++)
        {
            VegetationRenderer vegetationRenderer = activeRenderers[i];
            if (vegetationRenderer == null || !vegetationRenderer.isActiveAndEnabled) continue;
            if (!vegetationRenderer.ShouldRenderForwardForCamera(camera)) continue;
            renderer.EnqueuePass(depthNormalsPass);
            break;
        }
        renderer.EnqueuePass(forwardPass);
    }

    private static void ResolveDiagnostics(out VegetationDiagnosticsMode mode, out int verboseInterval)
    {
        mode = VegetationDiagnosticsMode.Off;
        verboseInterval = int.MaxValue;

        IReadOnlyList<VegetationRenderer> renderers = VegetationRenderer.ActiveRenderers;
        for (int i = 0; i < renderers.Count; i++)
        {
            VegetationRenderer vegetationRenderer = renderers[i];
            if (vegetationRenderer == null || !vegetationRenderer.isActiveAndEnabled) continue;
            if (vegetationRenderer.diagnosticsMode == VegetationDiagnosticsMode.Verbose)
                mode = VegetationDiagnosticsMode.Verbose;
            else if (vegetationRenderer.diagnosticsMode == VegetationDiagnosticsMode.Normal && mode == VegetationDiagnosticsMode.Off)
                mode = VegetationDiagnosticsMode.Normal;
            verboseInterval = Mathf.Min(verboseInterval, Mathf.Max(1, vegetationRenderer.diagnosticsVerboseInterval));
        }

        if (verboseInterval == int.MaxValue) verboseInterval = 120;
    }

    private sealed class VegetationDepthNormalsPass : ScriptableRenderPass
    {
        private static readonly FieldInfo normalsTextureField = typeof(UniversalRenderer).GetField("m_NormalsTexture", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo depthTextureField = typeof(UniversalRenderer).GetField("m_DepthTexture", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly PropertyInfo useDepthPrimingProperty = typeof(ScriptableRenderer).GetProperty("useDepthPriming", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly ProfilingSampler depthNormalsProfilingSampler = new ProfilingSampler("Vegetation DepthNormals");
        private bool targetReady;

        public VegetationDepthNormalsPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
            ConfigureInput(ScriptableRenderPassInput.Normal);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            targetReady = false;
            ScriptableRenderer renderer = renderingData.cameraData.renderer;
            if (!(renderer is UniversalRenderer) || normalsTextureField == null || depthTextureField == null) return;
            RTHandle normals = normalsTextureField.GetValue(renderer) as RTHandle;
            bool useDepthPriming = useDepthPrimingProperty != null && (bool)useDepthPrimingProperty.GetValue(renderer);
            RTHandle depth = useDepthPriming ? renderer.cameraDepthTargetHandle : depthTextureField.GetValue(renderer) as RTHandle;
            if (normals == null || depth == null) return;
            ConfigureTarget(normals, depth);
            ConfigureClear(ClearFlag.None, Color.clear);
            targetReady = true;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if (camera == null || !targetReady) return;

            CommandBuffer cmd = CommandBufferPool.Get("Vegetation DepthNormals");
            try
            {
                using (new ProfilingScope(cmd, depthNormalsProfilingSampler))
                {
                    IReadOnlyList<VegetationRenderer> renderers = VegetationRenderer.ActiveRenderers;
                    int additionalLightsCount = Mathf.Max(0, renderingData.lightData.additionalLightsCount);
                    for (int i = 0; i < renderers.Count; i++)
                    {
                        VegetationRenderer vegetationRenderer = renderers[i];
                        if (vegetationRenderer == null || !vegetationRenderer.isActiveAndEnabled) continue;
                        if (!vegetationRenderer.ShouldRenderForwardForCamera(camera)) continue;
                        vegetationRenderer.PrepareForCamera(cmd, camera, 0, additionalLightsCount);
                        vegetationRenderer.RenderDepthNormals(cmd, camera);
                    }
                }
                context.ExecuteCommandBuffer(cmd);
            }
            finally
            {
                CommandBufferPool.Release(cmd);
            }
        }
    }

    private sealed class VegetationForwardPass : ScriptableRenderPass
    {
        private static readonly FieldInfo renderingModeField = typeof(UniversalRenderer).GetField("m_RenderingMode", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly Settings settings;
        private readonly ProfilingSampler vegetationProfilingSampler = new ProfilingSampler("Vegetation Forward Total");
        private readonly ProfilingSampler vegetationDrawProfilingSampler = new ProfilingSampler("Vegetation Forward Draw");

        public VegetationForwardPass(Settings settings)
        {
            this.settings = settings;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ScriptableRenderer renderer = renderingData.cameraData.renderer;

            ConfigureTarget(
                renderer.cameraColorTargetHandle,
                renderer.cameraDepthTargetHandle
            );

            ConfigureClear(ClearFlag.None, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;

            if (camera == null) return;

            IReadOnlyList<VegetationRenderer> renderers = VegetationRenderer.ActiveRenderers;
            bool hasEligibleRenderer = false;
            ResolveDiagnostics(out VegetationDiagnosticsMode diagnosticsMode, out int verboseInterval);

            for (int i = 0; i < renderers.Count; i++)
            {
                VegetationRenderer vegetationRenderer = renderers[i];
                if (vegetationRenderer == null || !vegetationRenderer.isActiveAndEnabled) continue;
                if (!vegetationRenderer.ShouldRenderForwardForCamera(camera)) continue;

                hasEligibleRenderer = true;
            }

            long passID = VegetationDiagnostics.BeginExecute(
                camera,
                renderingData.cameraData,
                diagnosticsMode,
                verboseInterval
            );

            if (!hasEligibleRenderer)
            {
                VegetationDiagnostics.EndCamera(camera, passID, diagnosticsMode, verboseInterval);
                return;
            }

            int cameraAdditionalLights = Mathf.Max(0, renderingData.lightData.additionalLightsCount);
            bool isForward = renderingData.cameraData.renderer is UniversalRenderer universalRenderer
                && renderingModeField != null
                && (RenderingMode)renderingModeField.GetValue(universalRenderer) == RenderingMode.Forward;
            int additionalLightsCount = settings.enableAdditionalLights && isForward
                && renderingData.lightData.supportsAdditionalLights
                ? Mathf.Min(cameraAdditionalLights,
                    Mathf.Max(0, renderingData.lightData.maxPerObjectAdditionalLightsCount),
                    Mathf.Clamp(settings.maxVegetationAdditionalLights, 0, 8))
                : 0;
            VegetationDiagnostics.AdditionalLights(camera, cameraAdditionalLights, additionalLightsCount,
                settings.enableAdditionalLights, isForward, diagnosticsMode, verboseInterval);

            CommandBuffer cmd = CommandBufferPool.Get();

            try
            {
                using (new ProfilingScope(cmd, vegetationProfilingSampler))
                {
                    for (int i = 0; i < renderers.Count; i++)
                    {
                        VegetationRenderer vegetationRenderer = renderers[i];

                        if (vegetationRenderer == null) continue;
                        if (!vegetationRenderer.isActiveAndEnabled) continue;
                        if (!vegetationRenderer.ShouldRenderForwardForCamera(camera)) continue;

                        VegetationDiagnostics.Culling(camera, passID, diagnosticsMode, verboseInterval);
                        // Indirect draws have no Renderer from which URP can build a
                        // per-object light list. Pass a capped camera-visible count;
                        // vegetation shaders index URP's camera-global light data directly.
                        vegetationRenderer.PrepareForCamera(cmd, camera, passID, additionalLightsCount);
                        VegetationDiagnostics.CopyCounter(camera, passID, diagnosticsMode, verboseInterval);
                        VegetationDiagnostics.Draw(camera, passID, diagnosticsMode, verboseInterval);
                        using (new ProfilingScope(cmd, vegetationDrawProfilingSampler))
                        {
                            vegetationRenderer.RenderForward(cmd, camera, passID);
                        }
                    }
                }

                context.ExecuteCommandBuffer(cmd);
            }
            finally
            {
                CommandBufferPool.Release(cmd);
                VegetationDiagnostics.EndCamera(camera, passID, diagnosticsMode, verboseInterval);
            }
        }
    }
}
