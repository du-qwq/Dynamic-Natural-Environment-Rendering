using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public enum GrassDebugTextureType
{
    Height,
    Mask,
    Color,
    Slope,
    SlopeHistory,
    Wind,
    Burn,
    BurnHistory
}

public class GrassDataRendererFeature : ScriptableRendererFeature
{
    public static GrassDataRendererFeature activeInstance { get; private set; }//调试新增：编辑器窗口访问当前启用的草数据Renderer Feature

    public int DebugTextureSize => textureSize;//调试新增：当前数据RT分辨率
    public Texture GetDebugTexture(GrassDebugTextureType textureType) => grassDataPass?.GetDebugTexture(textureType);//调试新增：取得指定数据RT
    public void ResetSlopeHistory() => grassDataPass?.RequestResetSlopeHistory();//调试新增：下一帧清空压草历史
    public void ResetBurnHistory() => grassDataPass?.RequestResetBurnHistory();//调试新增：下一帧清空燃烧历史

    [SerializeField] private LayerMask heightMapLayer;
    [SerializeField] private Material heightMapMat;
    [SerializeField] private ComputeShader computeShader;
    [SerializeField] private int textureSize = 1024;

    [SerializeField] private Material slopeHistoryUpdateMat;
    [SerializeField] private float slopeRecoverSpeed = 0.25f;

    [SerializeField] private Material burnHistoryUpdateMat;//燃烧新增：燃烧历史更新材质
    [SerializeField] private float burnSpeed = 1f;//燃烧新增：燃烧累积速度
    [SerializeField] private float burnRegrowSpeed = 0f;//燃烧新增：草重新生长速度，0表示不会恢复

    GrassDataPass grassDataPass;

    public override void Create()
    {
        activeInstance = this;//调试新增：记录当前启用的Renderer Feature
        grassDataPass = new GrassDataPass(heightMapLayer, heightMapMat, computeShader, textureSize, slopeHistoryUpdateMat, slopeRecoverSpeed, burnHistoryUpdateMat, burnSpeed, burnRegrowSpeed);
        grassDataPass.renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(grassDataPass);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) grassDataPass?.Dispose();
        if (activeInstance == this) activeInstance = null;//调试新增：Feature销毁后清空静态引用
    }

    private class GrassDataPass : ScriptableRenderPass
    {
        private List<ShaderTagId> shaderTagsList = new List<ShaderTagId>();

        private RTHandle heightRT, heightDepthRT, maskRT, colorRT, slopeRT, slopeHistoryRT, slopeHistoryTempRT, windRT;
        private RTHandle burnRT, burnHistoryRT, burnHistoryTempRT;//燃烧新增：当前帧燃烧RT、历史燃烧RT、PingPong临时RT

        private LayerMask heightMapLayer;
        private Material heightMapMat;
        private ComputeShader computeShader;

        private int textureSize;
        private Material slopeHistoryUpdateMat;
        private float slopeRecoverSpeed;

        private Material burnHistoryUpdateMat;//燃烧新增：燃烧历史更新材质
        private float burnSpeed;//燃烧新增：燃烧累积速度
        private float burnRegrowSpeed;//燃烧新增：草重新生长速度

        private bool slopeHistoryInitialized = false;
        private bool burnHistoryInitialized = false;//燃烧新增：燃烧历史RT是否已经初始化
        private bool resetSlopeHistoryRequested = false;//调试新增：是否请求清空压草历史
        private bool resetBurnHistoryRequested = false;//调试新增：是否请求清空燃烧历史

        public GrassDataPass(LayerMask heightMapLayer, Material heightMapMat, ComputeShader computeShader, int textureSize, Material slopeHistoryUpdateMat, float slopeRecoverSpeed, Material burnHistoryUpdateMat, float burnSpeed, float burnRegrowSpeed)
        {
            this.heightMapLayer = heightMapLayer;
            this.heightMapMat = heightMapMat;
            this.computeShader = computeShader;
            this.textureSize = Mathf.Max(256, textureSize);
            this.slopeHistoryUpdateMat = slopeHistoryUpdateMat;
            this.slopeRecoverSpeed = slopeRecoverSpeed;
            this.burnHistoryUpdateMat = burnHistoryUpdateMat;//燃烧新增：保存燃烧历史更新材质
            this.burnSpeed = burnSpeed;//燃烧新增：保存燃烧累积速度
            this.burnRegrowSpeed = burnRegrowSpeed;//燃烧新增：保存草重新生长速度

            shaderTagsList.Add(new ShaderTagId("SRPDefaultUnlit"));
            shaderTagsList.Add(new ShaderTagId("UniversalForward"));
            shaderTagsList.Add(new ShaderTagId("UniversalForwardOnly"));
        }

        //调试新增：把RTHandle中的真实RenderTexture交给编辑器窗口预览
        public Texture GetDebugTexture(GrassDebugTextureType textureType)
        {
            switch (textureType)
            {
                case GrassDebugTextureType.Height: return heightRT != null ? heightRT.rt : null;
                case GrassDebugTextureType.Mask: return maskRT != null ? maskRT.rt : null;
                case GrassDebugTextureType.Color: return colorRT != null ? colorRT.rt : null;
                case GrassDebugTextureType.Slope: return slopeRT != null ? slopeRT.rt : null;
                case GrassDebugTextureType.SlopeHistory: return slopeHistoryRT != null ? slopeHistoryRT.rt : null;
                case GrassDebugTextureType.Wind: return windRT != null ? windRT.rt : null;
                case GrassDebugTextureType.Burn: return burnRT != null ? burnRT.rt : null;
                case GrassDebugTextureType.BurnHistory: return burnHistoryRT != null ? burnHistoryRT.rt : null;
                default: return null;
            }
        }

        public void RequestResetSlopeHistory()
        {
            resetSlopeHistoryRequested = true;
        }

        public void RequestResetBurnHistory()
        {
            resetBurnHistoryRequested = true;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderingUtils.ReAllocateIfNeeded(ref heightRT, new RenderTextureDescriptor(textureSize, textureSize, RenderTextureFormat.RGFloat, 0), FilterMode.Bilinear);
            RenderingUtils.ReAllocateIfNeeded(ref heightDepthRT, new RenderTextureDescriptor(textureSize, textureSize, RenderTextureFormat.RFloat, 32), FilterMode.Bilinear);
            RenderingUtils.ReAllocateIfNeeded(ref maskRT, new RenderTextureDescriptor(textureSize, textureSize, RenderTextureFormat.RFloat, 0), FilterMode.Bilinear);
            RenderingUtils.ReAllocateIfNeeded(ref colorRT, new RenderTextureDescriptor(textureSize, textureSize, RenderTextureFormat.ARGBFloat, 0), FilterMode.Bilinear);
            RenderingUtils.ReAllocateIfNeeded(ref slopeRT, new RenderTextureDescriptor(textureSize, textureSize, RenderTextureFormat.ARGBFloat, 0), FilterMode.Bilinear);
            RenderingUtils.ReAllocateIfNeeded(ref slopeHistoryRT, new RenderTextureDescriptor(textureSize, textureSize, RenderTextureFormat.ARGBFloat, 0), FilterMode.Bilinear);
            RenderingUtils.ReAllocateIfNeeded(ref slopeHistoryTempRT, new RenderTextureDescriptor(textureSize, textureSize, RenderTextureFormat.ARGBFloat, 0), FilterMode.Bilinear);
            RenderingUtils.ReAllocateIfNeeded(ref windRT, new RenderTextureDescriptor(textureSize, textureSize, RenderTextureFormat.ARGBHalf, 0), FilterMode.Bilinear);
            RenderingUtils.ReAllocateIfNeeded(ref burnRT, new RenderTextureDescriptor(textureSize, textureSize, RenderTextureFormat.RHalf, 0), FilterMode.Bilinear);//燃烧新增：当前帧燃烧输入RT
            RenderingUtils.ReAllocateIfNeeded(ref burnHistoryRT, new RenderTextureDescriptor(textureSize, textureSize, RenderTextureFormat.RHalf, 0), FilterMode.Bilinear);//燃烧新增：历史燃烧RT
            RenderingUtils.ReAllocateIfNeeded(ref burnHistoryTempRT, new RenderTextureDescriptor(textureSize, textureSize, RenderTextureFormat.RHalf, 0), FilterMode.Bilinear);//燃烧新增：历史燃烧临时RT

            ConfigureTarget(heightRT, heightDepthRT);
            ConfigureClear(ClearFlag.All, Color.black);
        }

        ComputeBuffer nearGrassPositionsBuffer;//LOD新增：近处草位置Buffer
        ComputeBuffer farGrassPositionsBuffer;//LOD新增：远处草位置Buffer
        int cachedCount = -1;

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (InfiniteGrassRenderer.instance == null || heightMapMat == null || computeShader == null || Camera.main == null) return;

            CommandBuffer cmd = CommandBufferPool.Get();

            float spacing = InfiniteGrassRenderer.instance.spacing;
            float fullDensityDistance = InfiniteGrassRenderer.instance.fullDensityDistance;
            float drawDistance = InfiniteGrassRenderer.instance.drawDistance;
            float maxBufferCount = InfiniteGrassRenderer.instance.maxBufferCount;
            float textureUpdateThreshold = InfiniteGrassRenderer.instance.textureUpdateThreshold;
            float lodDistance = InfiniteGrassRenderer.instance.lodDistance;//LOD新增：读取LOD分界距离

            Bounds cameraBounds = CalculateCameraBounds(Camera.main, drawDistance);

            Vector2 centerPos = new Vector2(Mathf.Floor(Camera.main.transform.position.x / textureUpdateThreshold) * textureUpdateThreshold, Mathf.Floor(Camera.main.transform.position.z / textureUpdateThreshold) * textureUpdateThreshold);

            Matrix4x4 viewMatrix = Matrix4x4.TRS(new Vector3(centerPos.x, cameraBounds.max.y, centerPos.y), Quaternion.LookRotation(-Vector3.up), new Vector3(1, 1, -1)).inverse;

            Matrix4x4 projMatrix = Matrix4x4.Ortho(-(drawDistance + textureUpdateThreshold), drawDistance + textureUpdateThreshold, -(drawDistance + textureUpdateThreshold), drawDistance + textureUpdateThreshold, 0, Mathf.Max(cameraBounds.size.y, 0.01f));

            cmd.SetViewProjectionMatrices(viewMatrix, projMatrix);

            // ================= HEIGHT =================
            using (new ProfilingScope(cmd, new ProfilingSampler("Height")))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var ds = CreateDrawingSettings(shaderTagsList, ref renderingData, renderingData.cameraData.defaultOpaqueSortFlags);

                heightMapMat.SetVector("_BoundsYMinMax", new Vector2(cameraBounds.min.y, cameraBounds.max.y));

                ds.overrideMaterial = heightMapMat;

                FilteringSettings fsHeight = new FilteringSettings(RenderQueueRange.all, heightMapLayer);
                context.DrawRenderers(renderingData.cullResults, ref ds, ref fsHeight);
            }

            // ================= MASK =================
            cmd.SetRenderTarget(maskRT);
            cmd.ClearRenderTarget(true, true, Color.clear);

            using (new ProfilingScope(cmd, new ProfilingSampler("Mask")))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var ds = CreateDrawingSettings(new ShaderTagId("GrassMask"), ref renderingData, SortingCriteria.CommonTransparent);

                FilteringSettings fsMask = new FilteringSettings(RenderQueueRange.all);
                context.DrawRenderers(renderingData.cullResults, ref ds, ref fsMask);
            }

            // ================= COLOR =================
            cmd.SetRenderTarget(colorRT);
            cmd.ClearRenderTarget(true, true, Color.clear);

            using (new ProfilingScope(cmd, new ProfilingSampler("Color")))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var ds = CreateDrawingSettings(new ShaderTagId("GrassColor"), ref renderingData, SortingCriteria.CommonTransparent);

                FilteringSettings fsColor = new FilteringSettings(RenderQueueRange.all);
                context.DrawRenderers(renderingData.cullResults, ref ds, ref fsColor);
            }

            // ================= SLOPE =================
            cmd.SetRenderTarget(slopeRT);
            cmd.ClearRenderTarget(true, true, new Color(0.5f, 0.5f, 0, 0));

            using (new ProfilingScope(cmd, new ProfilingSampler("Slope")))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var ds = CreateDrawingSettings(new ShaderTagId("GrassSlope"), ref renderingData, SortingCriteria.CommonTransparent);

                FilteringSettings fsSlope = new FilteringSettings(RenderQueueRange.all);
                context.DrawRenderers(renderingData.cullResults, ref ds, ref fsSlope);
            }

            // ================= WIND =================
            cmd.SetRenderTarget(windRT);
            cmd.ClearRenderTarget(true, true, new Color(0.5f, 0.5f, 0, 0));

            using (new ProfilingScope(cmd, new ProfilingSampler("Wind")))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var ds = CreateDrawingSettings(new ShaderTagId("GrassWind"), ref renderingData, SortingCriteria.CommonTransparent);

                FilteringSettings fsWind = new FilteringSettings(RenderQueueRange.all);
                context.DrawRenderers(renderingData.cullResults, ref ds, ref fsWind);
            }

            // ================= BURN =================
            cmd.SetRenderTarget(burnRT);
            cmd.ClearRenderTarget(true, true, Color.black);

            using (new ProfilingScope(cmd, new ProfilingSampler("Burn")))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var ds = CreateDrawingSettings(new ShaderTagId("GrassBurn"), ref renderingData, SortingCriteria.CommonTransparent);

                FilteringSettings fsBurn = new FilteringSettings(RenderQueueRange.all);
                context.DrawRenderers(renderingData.cullResults, ref ds, ref fsBurn);
            }

            if (!burnHistoryInitialized || resetBurnHistoryRequested)
            {
                cmd.SetRenderTarget(burnHistoryRT);
                cmd.ClearRenderTarget(true, true, Color.black);
                cmd.SetRenderTarget(burnHistoryTempRT);
                cmd.ClearRenderTarget(true, true, Color.black);
                burnHistoryInitialized = true;
                resetBurnHistoryRequested = false;//调试新增：完成一次清空后复位请求
            }

            if (burnHistoryUpdateMat != null)
            {
                burnHistoryUpdateMat.SetTexture("_CurrentBurnRT", burnRT);
                burnHistoryUpdateMat.SetTexture("_HistoryBurnRT", burnHistoryRT);
                burnHistoryUpdateMat.SetFloat("_BurnSpeed", Mathf.Max(0, burnSpeed));
                burnHistoryUpdateMat.SetFloat("_RegrowSpeed", Mathf.Max(0, burnRegrowSpeed));
                burnHistoryUpdateMat.SetFloat("_DeltaTime", Application.isPlaying ? Time.deltaTime : 0.016f);

                Blitter.BlitCameraTexture(cmd, burnHistoryRT, burnHistoryTempRT, burnHistoryUpdateMat, 0);

                var burnTemp = burnHistoryRT;
                burnHistoryRT = burnHistoryTempRT;
                burnHistoryTempRT = burnTemp;
            }

            // ================= HISTORY =================
            if (!slopeHistoryInitialized || resetSlopeHistoryRequested)
            {
                cmd.SetRenderTarget(slopeHistoryRT);
                cmd.ClearRenderTarget(true, true, new Color(0.5f, 0.5f, 0, 0));
                cmd.SetRenderTarget(slopeHistoryTempRT);
                cmd.ClearRenderTarget(true, true, new Color(0.5f, 0.5f, 0, 0));
                slopeHistoryInitialized = true;
                resetSlopeHistoryRequested = false;//调试新增：完成一次清空后复位请求
            }

            if (slopeHistoryUpdateMat != null)
            {
                slopeHistoryUpdateMat.SetTexture("_CurrentSlopeRT", slopeRT);
                slopeHistoryUpdateMat.SetTexture("_HistorySlopeRT", slopeHistoryRT);
                slopeHistoryUpdateMat.SetFloat("_RecoverSpeed", Mathf.Max(0, slopeRecoverSpeed));
                slopeHistoryUpdateMat.SetFloat("_DeltaTime", Time.deltaTime);

                Blitter.BlitCameraTexture(cmd, slopeHistoryRT, slopeHistoryTempRT, slopeHistoryUpdateMat, 0);

                var t = slopeHistoryRT;
                slopeHistoryRT = slopeHistoryTempRT;
                slopeHistoryTempRT = t;
            }

            cmd.SetGlobalTexture("_GrassHeightDebugRT", heightRT);//调试新增：草Shader显示高度图
            cmd.SetGlobalTexture("_GrassMaskDebugRT", maskRT);//调试新增：草Shader显示Mask图
            cmd.SetGlobalTexture("_GrassColorRT", colorRT);
            cmd.SetGlobalTexture("_GrassSlopeRT", slopeRT);
            cmd.SetGlobalTexture("_GrassSlopeHistoryRT", slopeHistoryRT);
            cmd.SetGlobalTexture("_GrassBurnRT", burnRT);//燃烧修复：给草Shader判断当前是否仍在燃烧
            cmd.SetGlobalTexture("_GrassBurnHistoryRT", burnHistoryRT);//燃烧新增：给草Shader使用的历史燃烧RT
            cmd.SetGlobalTexture("_GrassWindRT", windRT);

            cmd.SetViewProjectionMatrices(renderingData.cameraData.camera.worldToCameraMatrix, renderingData.cameraData.camera.projectionMatrix);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            // ================= COMPUTE =================
            Vector2Int gridSize = new Vector2Int(Mathf.CeilToInt(cameraBounds.size.x / spacing), Mathf.CeilToInt(cameraBounds.size.z / spacing));
            Vector2Int gridStart = new Vector2Int(Mathf.FloorToInt(cameraBounds.min.x / spacing), Mathf.FloorToInt(cameraBounds.min.z / spacing));

            int maxCount = Mathf.Max(1, Mathf.CeilToInt(1000000f * maxBufferCount));

            if (nearGrassPositionsBuffer == null || farGrassPositionsBuffer == null || cachedCount != maxCount)
            {
                nearGrassPositionsBuffer?.Release();
                farGrassPositionsBuffer?.Release();
                nearGrassPositionsBuffer = new ComputeBuffer(maxCount, sizeof(float) * 3, ComputeBufferType.Append);
                farGrassPositionsBuffer = new ComputeBuffer(maxCount, sizeof(float) * 3, ComputeBufferType.Append);
                cachedCount = maxCount;
            }

            computeShader.SetMatrix("_VPMatrix", Camera.main.projectionMatrix * Camera.main.worldToCameraMatrix);
            computeShader.SetFloat("_FullDensityDistance", fullDensityDistance);
            computeShader.SetFloat("_LODDistance", lodDistance);//LOD新增：把LOD分界距离传给ComputeShader
            computeShader.SetVector("_BoundsMin", cameraBounds.min);
            computeShader.SetVector("_BoundsMax", cameraBounds.max);
            computeShader.SetVector("_CameraPosition", Camera.main.transform.position);
            computeShader.SetVector("_CenterPos", centerPos);
            computeShader.SetFloat("_DrawDistance", drawDistance);
            computeShader.SetFloat("_TextureUpdateThreshold", textureUpdateThreshold);
            computeShader.SetFloat("_Spacing", spacing);
            computeShader.SetVector("_GridStartIndex", (Vector2)gridStart);
            computeShader.SetVector("_GridSize", (Vector2)gridSize);

            computeShader.SetBuffer(0, "_GrassPositionsNear", nearGrassPositionsBuffer);//LOD新增：绑定Near Buffer
            computeShader.SetBuffer(0, "_GrassPositionsFar", farGrassPositionsBuffer);//LOD新增：绑定Far Buffer
            computeShader.SetTexture(0, "_GrassHeightMapRT", heightRT);
            computeShader.SetTexture(0, "_GrassMaskMapRT", maskRT);

            nearGrassPositionsBuffer.SetCounterValue(0);
            farGrassPositionsBuffer.SetCounterValue(0);

            cmd.DispatchCompute(computeShader, 0, Mathf.CeilToInt(gridSize.x / 8f), Mathf.CeilToInt(gridSize.y / 8f), 1);

            cmd.SetGlobalBuffer("_GrassPositionsNear", nearGrassPositionsBuffer);//LOD新增：给GrassBladeShader使用的Near Buffer
            cmd.SetGlobalBuffer("_GrassPositionsFar", farGrassPositionsBuffer);//LOD新增：给GrassBladeShader使用的Far Buffer

            if (InfiniteGrassRenderer.instance.nearArgsBuffer != null) cmd.CopyCounterValue(nearGrassPositionsBuffer, InfiniteGrassRenderer.instance.nearArgsBuffer, 4);//LOD新增：复制近处草数量
            if (InfiniteGrassRenderer.instance.farArgsBuffer != null) cmd.CopyCounterValue(farGrassPositionsBuffer, InfiniteGrassRenderer.instance.farArgsBuffer, 4);//LOD新增：复制远处草数量

            if (InfiniteGrassRenderer.instance.tBuffer != null) cmd.CopyCounterValue(nearGrassPositionsBuffer, InfiniteGrassRenderer.instance.tBuffer, 0);//调试新增：始终保存Near计数，只有读取时才会产生GPU同步
            if (InfiniteGrassRenderer.instance.farTBuffer != null) cmd.CopyCounterValue(farGrassPositionsBuffer, InfiniteGrassRenderer.instance.farTBuffer, 0);//调试新增：始终保存Far计数，只有读取时才会产生GPU同步

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            heightRT?.Release();
            heightDepthRT?.Release();
            maskRT?.Release();
            colorRT?.Release();
            slopeRT?.Release();
            slopeHistoryRT?.Release();
            slopeHistoryTempRT?.Release();
            windRT?.Release();
            burnRT?.Release();//燃烧新增：释放当前帧燃烧RT
            burnHistoryRT?.Release();//燃烧新增：释放历史燃烧RT
            burnHistoryTempRT?.Release();//燃烧新增：释放历史燃烧临时RT
            nearGrassPositionsBuffer?.Release();
            farGrassPositionsBuffer?.Release();

            heightRT = null;
            heightDepthRT = null;
            maskRT = null;
            colorRT = null;
            slopeRT = null;
            slopeHistoryRT = null;
            slopeHistoryTempRT = null;
            windRT = null;
            burnRT = null;//燃烧新增：释放后置空
            burnHistoryRT = null;//燃烧新增：释放后置空
            burnHistoryTempRT = null;//燃烧新增：释放后置空
            nearGrassPositionsBuffer = null;
            farGrassPositionsBuffer = null;

            slopeHistoryInitialized = false;
            burnHistoryInitialized = false;//燃烧新增：重置燃烧历史RT初始化状态
            cachedCount = -1;
        }

        Bounds CalculateCameraBounds(Camera cam, float dist)
        {
            Vector3 a = cam.ViewportToWorldPoint(new Vector3(0, 1, cam.nearClipPlane));
            Vector3 b = cam.ViewportToWorldPoint(new Vector3(1, 0, dist));
            Vector3 c = cam.ViewportToWorldPoint(new Vector3(0, 0, dist));
            Vector3 d = cam.ViewportToWorldPoint(new Vector3(1, 1, dist));

            float maxX = Mathf.Max(a.x, b.x, c.x, d.x);
            float minX = Mathf.Min(a.x, b.x, c.x, d.x);
            float maxY = Mathf.Max(a.y, b.y, c.y, d.y);
            float minY = Mathf.Min(a.y, b.y, c.y, d.y);
            float maxZ = Mathf.Max(a.z, b.z, c.z, d.z);
            float minZ = Mathf.Min(a.z, b.z, c.z, d.z);

            Bounds bounds = new Bounds(new Vector3((maxX + minX) / 2, (maxY + minY) / 2, (maxZ + minZ) / 2), new Vector3(maxX - minX, maxY - minY, maxZ - minZ));

            bounds.Expand(1);

            return bounds;
        }
    }
}
