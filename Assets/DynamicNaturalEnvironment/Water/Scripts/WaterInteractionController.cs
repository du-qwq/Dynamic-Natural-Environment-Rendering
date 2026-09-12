using UnityEngine;

public class WaterInteractionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Material waterMaterial;
    [SerializeField] private Shader forceApplyShader;
    [SerializeField] private Shader displacementUpdateShader;
    [SerializeField] private Shader rippleNormalFromHeightShader;
    [SerializeField] private Transform forceTarget;
    [SerializeField] private Renderer waterRenderer;

    [Header("Water Area")]
    [SerializeField] private Vector2 fallbackWaterSize = new Vector2(20f, 20f);
    [SerializeField] private float cameraHeight = 15f;
    [SerializeField] private float nearPlane = 0.1f;
    [SerializeField] private float farPlane = 30f;

    [Header("RT Settings")]
    [SerializeField] private int textureSize = 512;

    [Header("Simulation")]
    [SerializeField] private float fixedDeltaTimeSim = 0.02f;
    [SerializeField] private float waveSpeed = 0.6f;
    [SerializeField] private float damping = 0.985f;
    [SerializeField] private float forceStrength = 10f;
    [SerializeField] private float forceRadius = 0.25f;

    [Header("Ripple Normal")]
    [SerializeField] private float rippleNormalStrength = 1.5f;
    

    private Material forceApplyMaterial;
    private Material displacementUpdateMaterial;
    private Material rippleNormalMaterial;

    private RenderTexture forceRT;
    private RenderTexture currentRT;
    private RenderTexture previousRT;
    private RenderTexture nextRT;
    private RenderTexture rippleNormalRT;

    private float simTimer;

    private static readonly int ForceTexID = Shader.PropertyToID("_ForceTex");
    private static readonly int CurrentTexID = Shader.PropertyToID("_CurrentTex");
    private static readonly int PreviousTexID = Shader.PropertyToID("_PreviousTex");

    private static readonly int RippleFieldTexID = Shader.PropertyToID("_RippleFieldTex");
    private static readonly int RippleNormalTexID = Shader.PropertyToID("_RippleNormalTex");

    private static readonly int WaterCenterID = Shader.PropertyToID("_WaterCenterWS");
    private static readonly int WaterSizeID = Shader.PropertyToID("_WaterSize");
    private static readonly int CameraHeightID = Shader.PropertyToID("_CameraHeight");
    private static readonly int ForceTargetPosID = Shader.PropertyToID("_ForceTargetPosWS");
    private static readonly int ForceRadiusID = Shader.PropertyToID("_ForceRadius");
    private static readonly int ForceStrengthID = Shader.PropertyToID("_ForceStrength");

    private static readonly int DeltaTimeID = Shader.PropertyToID("_DeltaTimeSim");
    private static readonly int WaveSpeedID = Shader.PropertyToID("_WaveSpeed");
    private static readonly int DampingID = Shader.PropertyToID("_Damping");
    private static readonly int GridSizeID = Shader.PropertyToID("_GridSize");

    private static readonly int RippleAreaCenterID = Shader.PropertyToID("_RippleAreaCenterWS");
    private static readonly int RippleAreaSizeID = Shader.PropertyToID("_RippleAreaSize");

    private static readonly int HeightTexelWorldSizeID = Shader.PropertyToID("_HeightTexelWorldSize");
    private static readonly int NormalStrengthID = Shader.PropertyToID("_NormalStrength");

    private void Start()
    {
        if (waterMaterial == null ||
            forceApplyShader == null ||
            displacementUpdateShader == null ||
            rippleNormalFromHeightShader == null)
        {
            Debug.LogError("WaterInteractionController: 引用没拖完整");
            enabled = false;
            return;
        }

        forceApplyMaterial = new Material(forceApplyShader);
        displacementUpdateMaterial = new Material(displacementUpdateShader);
        rippleNormalMaterial = new Material(rippleNormalFromHeightShader);

        forceRT = CreateHeightRT("ForceRT");
        currentRT = CreateHeightRT("CurrentRT");
        previousRT = CreateHeightRT("PreviousRT");
        nextRT = CreateHeightRT("NextRT");
        rippleNormalRT = CreateNormalRT("RippleNormalRT");

        ClearRT(forceRT);
        ClearRT(currentRT);
        ClearRT(previousRT);
        ClearRT(nextRT);
        ClearRT(rippleNormalRT);

        waterMaterial.SetTexture(RippleFieldTexID, currentRT);
        waterMaterial.SetTexture(RippleNormalTexID, rippleNormalRT);
    }

    private void Update()
    {
        if (forceApplyMaterial == null || displacementUpdateMaterial == null || rippleNormalMaterial == null)
            return;

        UpdateForceRT();

        simTimer += Time.deltaTime;
        while (simTimer >= fixedDeltaTimeSim)
        {
            simTimer -= fixedDeltaTimeSim;
            SimStep();
        }

        UpdateRippleNormalRT();

        GetWaterArea(out Vector3 center, out Vector2 size);

        waterMaterial.SetTexture(RippleFieldTexID, currentRT);
        waterMaterial.SetTexture(RippleNormalTexID, rippleNormalRT);
        waterMaterial.SetVector(RippleAreaCenterID, center);
        waterMaterial.SetVector(RippleAreaSizeID, size);
    }

    private RenderTexture CreateHeightRT(string rtName)
    {
        RenderTexture rt = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.RFloat);
        rt.name = rtName;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.filterMode = FilterMode.Bilinear;
        rt.Create();
        return rt;
    }

    private RenderTexture CreateNormalRT(string rtName)
    {
        RenderTexture rt = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGBHalf);
        rt.name = rtName;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.filterMode = FilterMode.Bilinear;
        rt.Create();
        return rt;
    }

    private void ClearRT(RenderTexture rt)
    {
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(false, true, Color.black);
        RenderTexture.active = prev;
    }

    private void GetWaterArea(out Vector3 center, out Vector2 size)
    {
        center = Vector3.zero;
        size = fallbackWaterSize;

        if (waterRenderer != null)
        {
            Bounds bounds = waterRenderer.bounds;
            center = bounds.center;
            size = new Vector2(bounds.size.x, bounds.size.z);
        }
    }

    private void UpdateForceRT()
    {
        GetWaterArea(out Vector3 center, out Vector2 size);

        forceApplyMaterial.SetVector(WaterCenterID, center);
        forceApplyMaterial.SetVector(WaterSizeID, size);
        forceApplyMaterial.SetFloat(CameraHeightID, cameraHeight);
        forceApplyMaterial.SetFloat(ForceRadiusID, forceRadius);
        forceApplyMaterial.SetFloat(ForceStrengthID, forceStrength);

        if (forceTarget != null)
            forceApplyMaterial.SetVector(ForceTargetPosID, forceTarget.position);
        else
            forceApplyMaterial.SetVector(ForceTargetPosID, new Vector4(99999, 99999, 99999, 1));

        Graphics.Blit(null, forceRT, forceApplyMaterial, 0);
    }

    private void SimStep()
    {
        GetWaterArea(out _, out Vector2 size);

        displacementUpdateMaterial.SetTexture(ForceTexID, forceRT);
        displacementUpdateMaterial.SetTexture(CurrentTexID, currentRT);
        displacementUpdateMaterial.SetTexture(PreviousTexID, previousRT);

        displacementUpdateMaterial.SetFloat(DeltaTimeID, fixedDeltaTimeSim);
        displacementUpdateMaterial.SetFloat(WaveSpeedID, waveSpeed);
        displacementUpdateMaterial.SetFloat(DampingID, damping);

        float dx = size.x / textureSize;//高度场每一个 texel，在世界里对应多宽
        displacementUpdateMaterial.SetFloat(GridSizeID, dx);

        Graphics.Blit(null, nextRT, displacementUpdateMaterial, 0);

        RenderTexture temp = previousRT;
        previousRT = currentRT;
        currentRT = nextRT;
        nextRT = temp;
    }

    private void UpdateRippleNormalRT()
    {
        GetWaterArea(out _, out Vector2 size);

        float dx = size.x / textureSize;
        float dz = size.y / textureSize;

        rippleNormalMaterial.SetVector(HeightTexelWorldSizeID, new Vector4(dx, dz, 0, 0));
        rippleNormalMaterial.SetFloat(NormalStrengthID, rippleNormalStrength);

        Graphics.Blit(currentRT, rippleNormalRT, rippleNormalMaterial, 0);
    }

    private void OnDrawGizmosSelected()
    {
        GetWaterArea(out Vector3 center, out Vector2 size);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(
            center,
            new Vector3(size.x, 0.05f, size.y)
        );
    }
}