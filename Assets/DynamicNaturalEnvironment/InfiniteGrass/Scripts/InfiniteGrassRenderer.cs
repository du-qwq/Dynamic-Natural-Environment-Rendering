using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class InfiniteGrassRenderer : MonoBehaviour
{
    [HideInInspector] public static InfiniteGrassRenderer instance;

    [Header("Base Grass Layer")]
    [Tooltip("Base 近距离草丛 Mesh")] public Mesh nearGrassMesh;
    [Tooltip("Base 远距离 Mesh，留空则使用近距离 Mesh")] public Mesh farGrassMesh;
    [Tooltip("Base 草材质")] public Material grassMaterial;
    [Min(0.05f)] public float spacing = 0.20f;
    [Range(0f, 1f)] public float baseDensityMultiplier = 1f;
    [Min(0.001f)] public float patchScale = 0.025f;
    [Range(0f, 0.95f)] public float patchStrength = 0.30f;
    [Range(0.05f, 1f)] public float farDensityMultiplier = 0.55f;

    [Header("Accent Grass Layer")]
    [Tooltip("Accent 近距离草丛 Mesh")] public Mesh accentNearGrassMesh;
    [Tooltip("Accent 远距离 Mesh，留空则使用近距离 Mesh")] public Mesh accentFarGrassMesh;
    [Tooltip("Accent 草材质")] public Material accentGrassMaterial;
    [Min(0.05f)] public float accentSpacing = 0.36f;
    [Range(0f, 1f)] public float accentDensityMultiplier = 0.45f;
    [Min(0.001f)] public float accentPatchScale = 0.022f;
    [Range(0f, 0.95f)] public float accentPatchStrength = 0.55f;
    [Range(0.05f, 1f)] public float accentFarDensityMultiplier = 0.30f;

    [Header("Distance")]
    [Min(1f)] public float drawDistance = 120f;
    [Min(0f)] public float fullDensityDistance = 45f;
    [Min(0f)] public float lodDistance = 45f;
    [Min(0.01f)] public float textureUpdateThreshold = 10f;

    [Header("Edge Distribution")]
    [Min(0.001f)] public float edgeNoiseScale = 0.02f;
    [Min(0f)] public float edgeNoiseStrength = 12f;

    [Header("Terrain Filtering")]
    public Terrain targetTerrain;
    [Min(0)] public int grassTerrainLayerIndex = 0;
    [Range(0f, 1f)] public float terrainLayerThreshold = 0.30f;
    public bool useSlopeFilter = true;
    [Range(0f, 90f)] public float maxSlope = 38f;
    [Range(0.1f, 30f)] public float slopeFade = 10f;

    [Header("Shadows")]
    public bool castNearShadows = true;
    public bool castFarShadows = false;
    public bool receiveShadows = true;

    [Header("Max Buffer Count (Millions)")]
    [Min(0.01f)] public float maxBufferCount = 1f;

    [Header("Debug")]
    public bool previewVisibleGrassCount;

    [HideInInspector] public ComputeBuffer nearArgsBuffer;
    [HideInInspector] public ComputeBuffer farArgsBuffer;
    [HideInInspector] public ComputeBuffer accentNearArgsBuffer;
    [HideInInspector] public ComputeBuffer accentFarArgsBuffer;

    [HideInInspector] public ComputeBuffer tBuffer;
    [HideInInspector] public ComputeBuffer farTBuffer;
    [HideInInspector] public ComputeBuffer accentTBuffer;
    [HideInInspector] public ComputeBuffer accentFarTBuffer;

    private Material nearGrassMaterial;
    private Material farGrassMaterial;
    private Material accentNearGrassMaterial;
    private Material accentFarGrassMaterial;

    private Material cachedBaseSourceMaterial;
    private Material cachedAccentSourceMaterial;

    private int cachedNearIndexCount = -1;
    private int cachedFarIndexCount = -1;
    private int cachedAccentNearIndexCount = -1;
    private int cachedAccentFarIndexCount = -1;

    public bool HasBaseLayer => nearGrassMesh && grassMaterial;
    public bool HasAccentLayer => accentNearGrassMesh && accentGrassMaterial;

    private void OnEnable()
    {
        instance = this;
    }

    private void OnDisable()
    {
        if (instance == this) instance = null;

        ReleaseBuffer(ref nearArgsBuffer);
        ReleaseBuffer(ref farArgsBuffer);
        ReleaseBuffer(ref accentNearArgsBuffer);
        ReleaseBuffer(ref accentFarArgsBuffer);

        ReleaseBuffer(ref tBuffer);
        ReleaseBuffer(ref farTBuffer);
        ReleaseBuffer(ref accentTBuffer);
        ReleaseBuffer(ref accentFarTBuffer);

        ReleaseRuntimeMaterials();

        cachedNearIndexCount = -1;
        cachedFarIndexCount = -1;
        cachedAccentNearIndexCount = -1;
        cachedAccentFarIndexCount = -1;
    }

    private void LateUpdate()
    {
        if (!Camera.main) return;

        Bounds bounds = CalculateCameraBounds(Camera.main);
        Vector2 centerPos = new Vector2(Mathf.Floor(Camera.main.transform.position.x / textureUpdateThreshold) * textureUpdateThreshold, Mathf.Floor(Camera.main.transform.position.z / textureUpdateThreshold) * textureUpdateThreshold);

        if (HasBaseLayer) DrawBaseLayer(bounds, centerPos);
        if (HasAccentLayer) DrawAccentLayer(bounds, centerPos);

        if (previewVisibleGrassCount) EnsureDebugBuffers();
    }

    private void DrawBaseLayer(Bounds bounds, Vector2 centerPos)
    {
        Mesh nearMesh = nearGrassMesh;
        Mesh farMesh = farGrassMesh ? farGrassMesh : nearGrassMesh;

        EnsureLayerMaterials(grassMaterial, ref nearGrassMaterial, ref farGrassMaterial, ref cachedBaseSourceMaterial, "Base");
        EnsureArgsBuffer(ref nearArgsBuffer, nearMesh, ref cachedNearIndexCount);
        EnsureArgsBuffer(ref farArgsBuffer, farMesh, ref cachedFarIndexCount);

        SetupGrassMaterial(nearGrassMaterial, grassMaterial, centerPos, 0, 0);
        SetupGrassMaterial(farGrassMaterial, grassMaterial, centerPos, 1, 0);

        if (nearArgsBuffer != null) Graphics.DrawMeshInstancedIndirect(nearMesh, 0, nearGrassMaterial, bounds, nearArgsBuffer, 0, null, castNearShadows ? ShadowCastingMode.On : ShadowCastingMode.Off, receiveShadows);
        if (farArgsBuffer != null) Graphics.DrawMeshInstancedIndirect(farMesh, 0, farGrassMaterial, bounds, farArgsBuffer, 0, null, castFarShadows ? ShadowCastingMode.On : ShadowCastingMode.Off, receiveShadows);
    }

    private void DrawAccentLayer(Bounds bounds, Vector2 centerPos)
    {
        Mesh nearMesh = accentNearGrassMesh;
        Mesh farMesh = accentFarGrassMesh ? accentFarGrassMesh : accentNearGrassMesh;

        EnsureLayerMaterials(accentGrassMaterial, ref accentNearGrassMaterial, ref accentFarGrassMaterial, ref cachedAccentSourceMaterial, "Accent");
        EnsureArgsBuffer(ref accentNearArgsBuffer, nearMesh, ref cachedAccentNearIndexCount);
        EnsureArgsBuffer(ref accentFarArgsBuffer, farMesh, ref cachedAccentFarIndexCount);

        SetupGrassMaterial(accentNearGrassMaterial, accentGrassMaterial, centerPos, 0, 1);
        SetupGrassMaterial(accentFarGrassMaterial, accentGrassMaterial, centerPos, 1, 1);

        if (accentNearArgsBuffer != null) Graphics.DrawMeshInstancedIndirect(nearMesh, 0, accentNearGrassMaterial, bounds, accentNearArgsBuffer, 0, null, castNearShadows ? ShadowCastingMode.On : ShadowCastingMode.Off, receiveShadows);
        if (accentFarArgsBuffer != null) Graphics.DrawMeshInstancedIndirect(farMesh, 0, accentFarGrassMaterial, bounds, accentFarArgsBuffer, 0, null, castFarShadows ? ShadowCastingMode.On : ShadowCastingMode.Off, receiveShadows);
    }

    private void SetupGrassMaterial(Material runtimeMaterial, Material sourceMaterial, Vector2 centerPos, int lodLevel, int layer)
    {
        if (!runtimeMaterial || !sourceMaterial) return;

        runtimeMaterial.CopyPropertiesFromMaterial(sourceMaterial);
        runtimeMaterial.enableInstancing = true;

        runtimeMaterial.SetVector("_CenterPos", centerPos);
        runtimeMaterial.SetFloat("_DrawDistance", drawDistance);
        runtimeMaterial.SetFloat("_TextureUpdateThreshold", textureUpdateThreshold);
        runtimeMaterial.SetInt("_GrassLODLevel", lodLevel);
        runtimeMaterial.SetInt("_GrassLayer", layer);

        SetupTerrainLayerData(runtimeMaterial);
    }

    private void SetupTerrainLayerData(Material material)
    {
        material.SetFloat("_UseTerrainBaseTex", 0f);

        if (!targetTerrain || !targetTerrain.terrainData) return;

        TerrainLayer[] layers = targetTerrain.terrainData.terrainLayers;
        if (grassTerrainLayerIndex < 0 || grassTerrainLayerIndex >= layers.Length) return;

        TerrainLayer layer = layers[grassTerrainLayerIndex];
        if (!layer || !layer.diffuseTexture) return;

        Vector2 tileSize = layer.tileSize;
        if (Mathf.Abs(tileSize.x) < 0.0001f || Mathf.Abs(tileSize.y) < 0.0001f) return;

        Vector2 tileOffset = layer.tileOffset;
        Vector3 terrainPos = targetTerrain.transform.position;

        material.SetTexture("_TerrainBaseTex", layer.diffuseTexture);
        material.SetVector("_TerrainPosition", new Vector4(terrainPos.x, terrainPos.y, terrainPos.z, 1f));
        material.SetVector("_TerrainLayerST", new Vector4(1f / tileSize.x, 1f / tileSize.y, tileOffset.x / tileSize.x, tileOffset.y / tileSize.y));
        material.SetFloat("_UseTerrainBaseTex", 1f);
    }

    private void EnsureLayerMaterials(Material source, ref Material nearMaterial, ref Material farMaterial, ref Material cachedSource, string suffix)
    {
        if (nearMaterial && farMaterial && cachedSource == source) return;

        ReleaseMaterial(ref nearMaterial);
        ReleaseMaterial(ref farMaterial);

        nearMaterial = new Material(source);
        farMaterial = new Material(source);

        nearMaterial.name = source.name + "_" + suffix + "_NearLOD";
        farMaterial.name = source.name + "_" + suffix + "_FarLOD";

        nearMaterial.enableInstancing = true;
        farMaterial.enableInstancing = true;

        cachedSource = source;
    }

    private void EnsureArgsBuffer(ref ComputeBuffer buffer, Mesh mesh, ref int cachedIndexCount)
    {
        if (!mesh) return;

        int indexCount = (int)mesh.GetIndexCount(0);
        if (buffer != null && cachedIndexCount == indexCount) return;

        ReleaseBuffer(ref buffer);

        buffer = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
        uint[] args = { mesh.GetIndexCount(0), 0, mesh.GetIndexStart(0), mesh.GetBaseVertex(0), 0 };
        buffer.SetData(args);

        cachedIndexCount = indexCount;
    }

    private void EnsureDebugBuffers()
    {
        if (tBuffer == null) tBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);
        if (farTBuffer == null) farTBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);
        if (accentTBuffer == null) accentTBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);
        if (accentFarTBuffer == null) accentFarTBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);
    }

    private void ReleaseRuntimeMaterials()
    {
        ReleaseMaterial(ref nearGrassMaterial);
        ReleaseMaterial(ref farGrassMaterial);
        ReleaseMaterial(ref accentNearGrassMaterial);
        ReleaseMaterial(ref accentFarGrassMaterial);

        cachedBaseSourceMaterial = null;
        cachedAccentSourceMaterial = null;
    }

    private void ReleaseMaterial(ref Material material)
    {
        if (!material) return;
        if (Application.isPlaying) Destroy(material);
        else DestroyImmediate(material);
        material = null;
    }

    private void ReleaseBuffer(ref ComputeBuffer buffer)
    {
        buffer?.Release();
        buffer = null;
    }

    private void OnGUI()
    {
        if (!previewVisibleGrassCount || tBuffer == null || farTBuffer == null || accentTBuffer == null || accentFarTBuffer == null) return;

        uint[] bNear = new uint[1];
        uint[] bFar = new uint[1];
        uint[] aNear = new uint[1];
        uint[] aFar = new uint[1];

        tBuffer.GetData(bNear);
        farTBuffer.GetData(bFar);
        accentTBuffer.GetData(aNear);
        accentFarTBuffer.GetData(aFar);

        GUIStyle style = new GUIStyle { fontSize = 21 };
        style.normal.textColor = Color.black;

        GUI.Label(new Rect(30, 30, 500, 30), $"Base Near : {bNear[0]}", style);
        GUI.Label(new Rect(30, 55, 500, 30), $"Base Far : {bFar[0]}", style);
        GUI.Label(new Rect(30, 80, 500, 30), $"Accent Near : {aNear[0]}", style);
        GUI.Label(new Rect(30, 105, 500, 30), $"Accent Far : {aFar[0]}", style);
        GUI.Label(new Rect(30, 130, 500, 30), $"Total : {bNear[0] + bFar[0] + aNear[0] + aFar[0]}", style);
    }

    private Bounds CalculateCameraBounds(Camera camera)
    {
        Vector3 ntl = camera.ViewportToWorldPoint(new Vector3(0, 1, camera.nearClipPlane));
        Vector3 ntr = camera.ViewportToWorldPoint(new Vector3(1, 1, camera.nearClipPlane));
        Vector3 nbl = camera.ViewportToWorldPoint(new Vector3(0, 0, camera.nearClipPlane));
        Vector3 nbr = camera.ViewportToWorldPoint(new Vector3(1, 0, camera.nearClipPlane));
        Vector3 ftl = camera.ViewportToWorldPoint(new Vector3(0, 1, drawDistance));
        Vector3 ftr = camera.ViewportToWorldPoint(new Vector3(1, 1, drawDistance));
        Vector3 fbl = camera.ViewportToWorldPoint(new Vector3(0, 0, drawDistance));
        Vector3 fbr = camera.ViewportToWorldPoint(new Vector3(1, 0, drawDistance));

        float[] xs = { ntl.x, ntr.x, nbl.x, nbr.x, ftl.x, ftr.x, fbl.x, fbr.x };
        float[] ys = { ntl.y, ntr.y, nbl.y, nbr.y, ftl.y, ftr.y, fbl.y, fbr.y };
        float[] zs = { ntl.z, ntr.z, nbl.z, nbr.z, ftl.z, ftr.z, fbl.z, fbr.z };

        Vector3 min = new Vector3(xs.Min(), ys.Min(), zs.Min());
        Vector3 max = new Vector3(xs.Max(), ys.Max(), zs.Max());

        Bounds bounds = new Bounds((min + max) * 0.5f, max - min);
        bounds.Expand(10f);
        return bounds;
    }

    private void OnValidate()
    {
        spacing = Mathf.Max(0.05f, spacing);
        accentSpacing = Mathf.Max(0.05f, accentSpacing);
        drawDistance = Mathf.Max(1f, drawDistance);
        fullDensityDistance = Mathf.Clamp(fullDensityDistance, 0f, drawDistance);
        lodDistance = Mathf.Clamp(lodDistance, 0f, drawDistance);
        textureUpdateThreshold = Mathf.Max(0.01f, textureUpdateThreshold);
        maxBufferCount = Mathf.Max(0.01f, maxBufferCount);
        patchScale = Mathf.Max(0.001f, patchScale);
        accentPatchScale = Mathf.Max(0.001f, accentPatchScale);
        edgeNoiseScale = Mathf.Max(0.001f, edgeNoiseScale);
        edgeNoiseStrength = Mathf.Max(0f, edgeNoiseStrength);
        grassTerrainLayerIndex = Mathf.Max(0, grassTerrainLayerIndex);
        terrainLayerThreshold = Mathf.Clamp01(terrainLayerThreshold);
        maxSlope = Mathf.Clamp(maxSlope, 0f, 90f);
        slopeFade = Mathf.Max(0.1f, slopeFade);
    }
}