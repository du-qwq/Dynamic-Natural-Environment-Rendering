using UnityEngine;

public class WaterRippleFieldController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Material waterMaterial;
    [SerializeField] private Shader rippleUpdateShader;
    [SerializeField] private Transform rippleTarget;

    [Header("Optional Emitters (better wake shape)")]
    [SerializeField] private Transform leftEmitter;
    [SerializeField] private Transform rightEmitter;

    [Header("Water Raycast")]
    [SerializeField] private LayerMask waterLayerMask;
    [SerializeField] private float rayStartHeight = 2f;
    [SerializeField] private float rayDistance = 10f;

    [Header("RT Settings")]
    [SerializeField] private int textureSize = 512;

    [Header("Auto Trigger")]
    [SerializeField] private bool autoTriggerByMovement = true;
    [SerializeField] private float minMoveDistance = 0.08f;
    [SerializeField] private float minTriggerInterval = 0.03f;
    [SerializeField] private float trailSpacing = 0.012f;

    [Header("Field Update")]
    [SerializeField] private float decay = 0.985f;
    [SerializeField] private float wakeLength = 0.06f;
    [SerializeField] private float wakeWidth = 0.018f;
    [SerializeField] private float wakeStrength = 0.75f;
    [SerializeField] private float wakeFoamStrength = 1.0f;

    private Material updateMaterial;
    private RenderTexture rtA;
    private RenderTexture rtB;
    private bool swapState;

    private const int MaxStamps = 32;
    private readonly Vector4[] impactUVs = new Vector4[MaxStamps];
    private readonly Vector4[] impactDirs = new Vector4[MaxStamps];
    private int nextStampIndex;

    private Vector3 lastTriggerPoint;
    private float lastTriggerTime;
    private bool hasTriggeredOnce;

    private bool hasLastMainUV;
    private Vector2 lastMainUV;

    private bool hasLastLeftUV;
    private Vector2 lastLeftUV;

    private bool hasLastRightUV;
    private Vector2 lastRightUV;

    private static readonly int RippleFieldTexID = Shader.PropertyToID("_RippleFieldTex");
    private static readonly int DecayID = Shader.PropertyToID("_Decay");
    private static readonly int WakeLengthID = Shader.PropertyToID("_WakeLength");
    private static readonly int WakeWidthID = Shader.PropertyToID("_WakeWidth");
    private static readonly int WakeStrengthID = Shader.PropertyToID("_WakeStrength");
    private static readonly int WakeFoamStrengthID = Shader.PropertyToID("_WakeFoamStrength");
    private static readonly int ImpactUVsID = Shader.PropertyToID("_ImpactUVs");
    private static readonly int ImpactDirsID = Shader.PropertyToID("_ImpactDirs");

    private void Start()
    {
        if (rippleUpdateShader == null || waterMaterial == null || rippleTarget == null)
            return;

        updateMaterial = new Material(rippleUpdateShader);

        rtA = CreateRT();
        rtB = CreateRT();

        ClearRT(rtA);
        ClearRT(rtB);

        waterMaterial.SetTexture(RippleFieldTexID, rtA);
    }

    private void Update()
    {
        if (updateMaterial == null || waterMaterial == null || rippleTarget == null)
            return;

        if (autoTriggerByMovement)
            TryAutoTrigger();

        UpdateRippleField();
    }

    private RenderTexture CreateRT()
    {
        RenderTexture rt = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGB32);
        rt.name = "WaterRippleFieldRT";
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

    private void TryAutoTrigger()
    {
        if (!TryGetWaterHit(rippleTarget.position, out Vector2 mainUV, out Vector3 hitPoint))
            return;

        if (!hasTriggeredOnce)
        {
            EmitTrailFromPoint(ref hasLastMainUV, ref lastMainUV, mainUV, 0f);

            if (leftEmitter != null &&
                TryGetWaterHit(leftEmitter.position, out Vector2 leftUV, out _))
            {
                EmitTrailFromPoint(ref hasLastLeftUV, ref lastLeftUV, leftUV, 0.85f);
            }

            if (rightEmitter != null &&
                TryGetWaterHit(rightEmitter.position, out Vector2 rightUV, out _))
            {
                EmitTrailFromPoint(ref hasLastRightUV, ref lastRightUV, rightUV, 0.85f);
            }

            lastTriggerPoint = hitPoint;
            lastTriggerTime = Time.time;
            hasTriggeredOnce = true;
            return;
        }

        float moveDistance = Vector3.Distance(hitPoint, lastTriggerPoint);
        float timeSinceLast = Time.time - lastTriggerTime;

        if (moveDistance < minMoveDistance || timeSinceLast < minTriggerInterval)
            return;

        EmitTrailFromPoint(ref hasLastMainUV, ref lastMainUV, mainUV, 0f);

        if (leftEmitter != null &&
            TryGetWaterHit(leftEmitter.position, out Vector2 leftUV2, out _))
        {
            EmitTrailFromPoint(ref hasLastLeftUV, ref lastLeftUV, leftUV2, 0.85f);
        }

        if (rightEmitter != null &&
            TryGetWaterHit(rightEmitter.position, out Vector2 rightUV2, out _))
        {
            EmitTrailFromPoint(ref hasLastRightUV, ref lastRightUV, rightUV2, 0.85f);
        }

        lastTriggerPoint = hitPoint;
        lastTriggerTime = Time.time;
        hasTriggeredOnce = true;
    }

    private void EmitTrailFromPoint(ref bool hasLastUV, ref Vector2 lastUV, Vector2 currentUV, float lateralBias)
    {
        if (!hasLastUV)
        {
            lastUV = currentUV;
            hasLastUV = true;

            PushStamp(currentUV, Vector2.up, 0.2f, lateralBias);
            return;
        }

        Vector2 delta = currentUV - lastUV;
        float dist = delta.magnitude;

        if (dist < 0.0001f)
            return;

        Vector2 dir = delta / dist;
        int steps = Mathf.Max(1, Mathf.CeilToInt(dist / Mathf.Max(trailSpacing, 0.0001f)));

        for (int i = 0; i < steps; i++)
        {
            float t = (i + 1f) / steps;
            Vector2 uv = Vector2.Lerp(lastUV, currentUV, t);
            float speed01 = Mathf.Clamp01(dist * 25f);
            PushStamp(uv, dir, speed01, lateralBias);
        }

        lastUV = currentUV;
    }

    private void PushStamp(Vector2 uv, Vector2 dir, float speed01, float lateralBias)
    {
        impactUVs[nextStampIndex] = new Vector4(uv.x, uv.y, 1f, 0f);
        impactDirs[nextStampIndex] = new Vector4(dir.x, dir.y, speed01, lateralBias);

        nextStampIndex++;
        if (nextStampIndex >= MaxStamps)
            nextStampIndex = 0;
    }

    private bool TryGetWaterHit(Vector3 worldPos, out Vector2 uv, out Vector3 hitPoint)
    {
        uv = Vector2.zero;
        hitPoint = Vector3.zero;

        Ray ray = new Ray(worldPos + Vector3.up * rayStartHeight, Vector3.down);

        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, waterLayerMask))
        {
            uv = hit.textureCoord;
            hitPoint = hit.point;
            return true;
        }

        return false;
    }

    private void UpdateRippleField()
    {
        RenderTexture source = swapState ? rtB : rtA;
        RenderTexture target = swapState ? rtA : rtB;

        updateMaterial.SetFloat(DecayID, decay);
        updateMaterial.SetFloat(WakeLengthID, wakeLength);
        updateMaterial.SetFloat(WakeWidthID, wakeWidth);
        updateMaterial.SetFloat(WakeStrengthID, wakeStrength);
        updateMaterial.SetFloat(WakeFoamStrengthID, wakeFoamStrength);
        updateMaterial.SetVectorArray(ImpactUVsID, impactUVs);
        updateMaterial.SetVectorArray(ImpactDirsID, impactDirs);

        Graphics.Blit(source, target, updateMaterial);

        waterMaterial.SetTexture(RippleFieldTexID, target);

        swapState = !swapState;

        for (int i = 0; i < MaxStamps; i++)
        {
            impactUVs[i].z = 0f;
        }
    }
}