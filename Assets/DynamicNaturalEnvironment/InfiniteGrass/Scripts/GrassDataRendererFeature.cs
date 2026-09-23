using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GrassDataRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private LayerMask heightMapLayer;
    [SerializeField] private Material heightMapMat;
    [SerializeField] private ComputeShader computeShader;
    [SerializeField] private int textureSize = 1024;

    private GrassDataPass pass;

    public override void Create()
    {
        pass = new GrassDataPass(heightMapLayer, heightMapMat, computeShader, textureSize);
        pass.renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        pass?.Setup();
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) pass?.Dispose();
    }

    private class GrassDataPass : ScriptableRenderPass
    {
        private readonly List<ShaderTagId> shaderTags = new List<ShaderTagId>();
        private readonly LayerMask heightMapLayer;
        private readonly Material heightMapMat;
        private readonly ComputeShader computeShader;
        private readonly int textureSize;

        private RTHandle heightRT;
        private RTHandle heightDepthRT;
        private RTHandle maskRT;

        private ComputeBuffer baseNearBuffer;
        private ComputeBuffer baseFarBuffer;
        private ComputeBuffer accentNearBuffer;
        private ComputeBuffer accentFarBuffer;

        private int cachedCount = -1;

        public GrassDataPass(LayerMask heightMapLayer, Material heightMapMat, ComputeShader computeShader, int textureSize)
        {
            this.heightMapLayer = heightMapLayer;
            this.heightMapMat = heightMapMat;
            this.computeShader = computeShader;
            this.textureSize = Mathf.Max(256, textureSize);

            shaderTags.Add(new ShaderTagId("SRPDefaultUnlit"));
            shaderTags.Add(new ShaderTagId("UniversalForward"));
            shaderTags.Add(new ShaderTagId("UniversalForwardOnly"));
        }

        public void Setup() { }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderingUtils.ReAllocateIfNeeded(ref heightRT, new RenderTextureDescriptor(textureSize, textureSize, RenderTextureFormat.RGFloat, 0), FilterMode.Bilinear);
            RenderingUtils.ReAllocateIfNeeded(ref heightDepthRT, new RenderTextureDescriptor(textureSize, textureSize, RenderTextureFormat.RFloat, 32), FilterMode.Bilinear);
            RenderingUtils.ReAllocateIfNeeded(ref maskRT, new RenderTextureDescriptor(textureSize, textureSize, RenderTextureFormat.RFloat, 0), FilterMode.Bilinear);

            ConfigureTarget(heightRT,heightDepthRT);
            ConfigureClear(ClearFlag.All,Color.black);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            InfiniteGrassRenderer grass = InfiniteGrassRenderer.instance;
            Camera mainCamera = Camera.main;

            if (!grass || !mainCamera || !heightMapMat || !computeShader) return;

            CommandBuffer cmd = CommandBufferPool.Get("Dual Layer Grass Data");

            Bounds cameraBounds = CalculateCameraBounds(mainCamera,grass.drawDistance);
            Vector2 centerPos = new Vector2(Mathf.Floor(mainCamera.transform.position.x / grass.textureUpdateThreshold) * grass.textureUpdateThreshold,Mathf.Floor(mainCamera.transform.position.z / grass.textureUpdateThreshold) * grass.textureUpdateThreshold);

            Matrix4x4 viewMatrix = Matrix4x4.TRS(new Vector3(centerPos.x,cameraBounds.max.y,centerPos.y),Quaternion.LookRotation(-Vector3.up),new Vector3(1,1,-1)).inverse;
            Matrix4x4 projMatrix = Matrix4x4.Ortho(-(grass.drawDistance + grass.textureUpdateThreshold),grass.drawDistance + grass.textureUpdateThreshold,-(grass.drawDistance + grass.textureUpdateThreshold),grass.drawDistance + grass.textureUpdateThreshold,0,Mathf.Max(cameraBounds.size.y,0.01f));

            cmd.SetViewProjectionMatrices(viewMatrix,projMatrix);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            var ds = CreateDrawingSettings(shaderTags,ref renderingData,renderingData.cameraData.defaultOpaqueSortFlags);
            heightMapMat.SetVector("_BoundsYMinMax",new Vector2(cameraBounds.min.y,cameraBounds.max.y));
            ds.overrideMaterial = heightMapMat;

            FilteringSettings heightFilter = new FilteringSettings(RenderQueueRange.all,heightMapLayer);
            context.DrawRenderers(renderingData.cullResults,ref ds,ref heightFilter);

            cmd.SetRenderTarget(maskRT);
            cmd.ClearRenderTarget(true,true,Color.clear);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            var maskDS = CreateDrawingSettings(new ShaderTagId("GrassMask"),ref renderingData,SortingCriteria.CommonTransparent);
            FilteringSettings maskFilter = new FilteringSettings(RenderQueueRange.all);
            context.DrawRenderers(renderingData.cullResults,ref maskDS,ref maskFilter);

            cmd.SetViewProjectionMatrices(renderingData.cameraData.camera.worldToCameraMatrix,renderingData.cameraData.camera.projectionMatrix);

            EnsureBuffers(grass);

            baseNearBuffer.SetCounterValue(0);
            baseFarBuffer.SetCounterValue(0);
            accentNearBuffer.SetCounterValue(0);
            accentFarBuffer.SetCounterValue(0);

            if (grass.HasBaseLayer)
            {
                DispatchLayer(cmd,grass,mainCamera,cameraBounds,centerPos,grass.spacing,grass.baseDensityMultiplier,grass.patchScale,grass.patchStrength,grass.farDensityMultiplier,17,baseNearBuffer,baseFarBuffer);
            }

            if (grass.HasAccentLayer)
            {
                DispatchLayer(cmd,grass,mainCamera,cameraBounds,centerPos,grass.accentSpacing,grass.accentDensityMultiplier,grass.accentPatchScale,grass.accentPatchStrength,grass.accentFarDensityMultiplier,911,accentNearBuffer,accentFarBuffer);
            }

            cmd.SetGlobalBuffer("_GrassPositionsBaseNear",baseNearBuffer);
            cmd.SetGlobalBuffer("_GrassPositionsBaseFar",baseFarBuffer);
            cmd.SetGlobalBuffer("_GrassPositionsAccentNear",accentNearBuffer);
            cmd.SetGlobalBuffer("_GrassPositionsAccentFar",accentFarBuffer);

            if (grass.nearArgsBuffer != null) cmd.CopyCounterValue(baseNearBuffer,grass.nearArgsBuffer,4);
            if (grass.farArgsBuffer != null) cmd.CopyCounterValue(baseFarBuffer,grass.farArgsBuffer,4);
            if (grass.accentNearArgsBuffer != null) cmd.CopyCounterValue(accentNearBuffer,grass.accentNearArgsBuffer,4);
            if (grass.accentFarArgsBuffer != null) cmd.CopyCounterValue(accentFarBuffer,grass.accentFarArgsBuffer,4);

            if (grass.previewVisibleGrassCount)
            {
                if (grass.tBuffer != null) cmd.CopyCounterValue(baseNearBuffer,grass.tBuffer,0);
                if (grass.farTBuffer != null) cmd.CopyCounterValue(baseFarBuffer,grass.farTBuffer,0);
                if (grass.accentTBuffer != null) cmd.CopyCounterValue(accentNearBuffer,grass.accentTBuffer,0);
                if (grass.accentFarTBuffer != null) cmd.CopyCounterValue(accentFarBuffer,grass.accentFarTBuffer,0);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private void DispatchLayer(CommandBuffer cmd, InfiniteGrassRenderer grass, Camera camera, Bounds bounds, Vector2 centerPos, float spacing, float densityMultiplier, float patchScale, float patchStrength, float farDensityMultiplier, int layerSeed, ComputeBuffer nearBuffer, ComputeBuffer farBuffer)
        {
            Vector2Int gridSize = new Vector2Int(Mathf.CeilToInt(bounds.size.x / spacing),Mathf.CeilToInt(bounds.size.z / spacing));
            Vector2Int gridStart = new Vector2Int(Mathf.FloorToInt(bounds.min.x / spacing),Mathf.FloorToInt(bounds.min.z / spacing));

            cmd.SetComputeMatrixParam(computeShader,"_VPMatrix",camera.projectionMatrix * camera.worldToCameraMatrix);
            cmd.SetComputeFloatParam(computeShader,"_FullDensityDistance",grass.fullDensityDistance);
            cmd.SetComputeFloatParam(computeShader,"_LODDistance",grass.lodDistance);
            cmd.SetComputeVectorParam(computeShader,"_BoundsMin",bounds.min);
            cmd.SetComputeVectorParam(computeShader,"_BoundsMax",bounds.max);
            cmd.SetComputeVectorParam(computeShader,"_CameraPosition",camera.transform.position);
            cmd.SetComputeVectorParam(computeShader,"_CenterPos",centerPos);
            cmd.SetComputeFloatParam(computeShader,"_DrawDistance",grass.drawDistance);
            cmd.SetComputeFloatParam(computeShader,"_TextureUpdateThreshold",grass.textureUpdateThreshold);
            cmd.SetComputeFloatParam(computeShader,"_Spacing",spacing);
            cmd.SetComputeVectorParam(computeShader,"_GridStartIndex",new Vector4(gridStart.x,gridStart.y,0,0));
            cmd.SetComputeVectorParam(computeShader,"_GridSize",new Vector4(gridSize.x,gridSize.y,0,0));

            cmd.SetComputeFloatParam(computeShader,"_PatchScale",patchScale);
            cmd.SetComputeFloatParam(computeShader,"_PatchStrength",patchStrength);
            cmd.SetComputeFloatParam(computeShader,"_FarDensityMultiplier",farDensityMultiplier);
            cmd.SetComputeFloatParam(computeShader,"_DensityMultiplier",densityMultiplier);
            cmd.SetComputeFloatParam(computeShader,"_EdgeNoiseScale",grass.edgeNoiseScale);
            cmd.SetComputeFloatParam(computeShader,"_EdgeNoiseStrength",grass.edgeNoiseStrength);
            cmd.SetComputeIntParam(computeShader,"_LayerSeed",layerSeed);

            cmd.SetComputeIntParam(computeShader,"_UseSlopeFilter",grass.useSlopeFilter ? 1 : 0);
            cmd.SetComputeFloatParam(computeShader,"_MaxSlope",grass.maxSlope);
            cmd.SetComputeFloatParam(computeShader,"_SlopeFade",grass.slopeFade);
            cmd.SetComputeFloatParam(computeShader,"_HeightMapSize",textureSize);

            SetupTerrainFilter(cmd,grass);

            cmd.SetComputeBufferParam(computeShader,0,"_GrassPositionsNear",nearBuffer);
            cmd.SetComputeBufferParam(computeShader,0,"_GrassPositionsFar",farBuffer);
            cmd.SetComputeTextureParam(computeShader,0,"_GrassHeightMapRT",heightRT);
            cmd.SetComputeTextureParam(computeShader,0,"_GrassMaskMapRT",maskRT);

            cmd.DispatchCompute(computeShader,0,Mathf.CeilToInt(gridSize.x / 8f),Mathf.CeilToInt(gridSize.y / 8f),1);
        }

        private void SetupTerrainFilter(CommandBuffer cmd, InfiniteGrassRenderer grass)
        {
            cmd.SetComputeIntParam(computeShader,"_UseTerrainLayerMask",0);

            if (!grass.targetTerrain || !grass.targetTerrain.terrainData) return;

            TerrainData data = grass.targetTerrain.terrainData;
            int textureIndex = grass.grassTerrainLayerIndex / 4;
            int channel = grass.grassTerrainLayerIndex % 4;

            Texture2D[] controlTextures = data.alphamapTextures;
            if (textureIndex < 0 || textureIndex >= controlTextures.Length) return;

            cmd.SetComputeTextureParam(computeShader,0,"_TerrainControl",controlTextures[textureIndex]);
            cmd.SetComputeVectorParam(computeShader,"_TerrainPosition",grass.targetTerrain.transform.position);
            cmd.SetComputeVectorParam(computeShader,"_TerrainSize",data.size);
            cmd.SetComputeIntParam(computeShader,"_TerrainLayerChannel",channel);
            cmd.SetComputeFloatParam(computeShader,"_TerrainLayerThreshold",grass.terrainLayerThreshold);
            cmd.SetComputeIntParam(computeShader,"_UseTerrainLayerMask",1);
        }

        private void EnsureBuffers(InfiniteGrassRenderer grass)
        {
            int maxCount = Mathf.Max(1,Mathf.CeilToInt(1000000f * grass.maxBufferCount));
            if (cachedCount == maxCount && baseNearBuffer != null && baseFarBuffer != null && accentNearBuffer != null && accentFarBuffer != null) return;

            ReleaseBuffers();

            baseNearBuffer = new ComputeBuffer(maxCount,sizeof(float) * 3,ComputeBufferType.Append);
            baseFarBuffer = new ComputeBuffer(maxCount,sizeof(float) * 3,ComputeBufferType.Append);
            accentNearBuffer = new ComputeBuffer(maxCount,sizeof(float) * 3,ComputeBufferType.Append);
            accentFarBuffer = new ComputeBuffer(maxCount,sizeof(float) * 3,ComputeBufferType.Append);

            cachedCount = maxCount;
        }

        private Bounds CalculateCameraBounds(Camera cam, float distance)
        {
            Vector3 a = cam.ViewportToWorldPoint(new Vector3(0,1,cam.nearClipPlane));
            Vector3 b = cam.ViewportToWorldPoint(new Vector3(1,0,distance));
            Vector3 c = cam.ViewportToWorldPoint(new Vector3(0,0,distance));
            Vector3 d = cam.ViewportToWorldPoint(new Vector3(1,1,distance));

            float maxX = Mathf.Max(a.x,b.x,c.x,d.x);
            float minX = Mathf.Min(a.x,b.x,c.x,d.x);
            float maxY = Mathf.Max(a.y,b.y,c.y,d.y);
            float minY = Mathf.Min(a.y,b.y,c.y,d.y);
            float maxZ = Mathf.Max(a.z,b.z,c.z,d.z);
            float minZ = Mathf.Min(a.z,b.z,c.z,d.z);

            Bounds bounds = new Bounds(new Vector3((maxX + minX) * 0.5f,(maxY + minY) * 0.5f,(maxZ + minZ) * 0.5f),new Vector3(maxX - minX,maxY - minY,maxZ - minZ));
            bounds.Expand(1f);
            return bounds;
        }

        private void ReleaseBuffers()
        {
            baseNearBuffer?.Release();
            baseFarBuffer?.Release();
            accentNearBuffer?.Release();
            accentFarBuffer?.Release();

            baseNearBuffer = null;
            baseFarBuffer = null;
            accentNearBuffer = null;
            accentFarBuffer = null;
            cachedCount = -1;
        }

        public void Dispose()
        {
            heightRT?.Release();
            heightDepthRT?.Release();
            maskRT?.Release();

            heightRT = null;
            heightDepthRT = null;
            maskRT = null;

            ReleaseBuffers();
        }
    }
}