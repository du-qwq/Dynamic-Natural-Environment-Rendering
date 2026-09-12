using UnityEngine;

public class WaterRippleTest : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Material waterMaterial;
    [SerializeField] private Transform rippleTarget;

    [Header("Manual Trigger")]
    [SerializeField] private KeyCode triggerKey = KeyCode.Space;

    [Header("Auto Trigger")]
    [SerializeField] private bool autoTriggerByMovement = true;
    [SerializeField] private float minMoveDistance = 0.6f;
    [SerializeField] private float minTriggerInterval = 0.15f;

    [Header("Ripple Slots")]
    [SerializeField] private int maxRipples = 4;

    private static readonly int RippleOriginsWSID = Shader.PropertyToID("_RippleOriginsWS");
    private static readonly int RippleStartTimesID = Shader.PropertyToID("_RippleStartTimes");

    private Vector4[] rippleOriginsWS;
    private float[] rippleStartTimes;

    private Vector3 lastTriggerPosition;
    private float lastTriggerTime;
    private bool hasTriggeredOnce;
    private int nextRippleIndex;

    private void Start()
    {
        rippleOriginsWS = new Vector4[maxRipples];
        rippleStartTimes = new float[maxRipples];

        for (int i = 0; i < maxRipples; i++)
        {
            rippleOriginsWS[i] = Vector4.zero;
            rippleStartTimes[i] = -9999f;
        }

        if (rippleTarget != null)
        {
            lastTriggerPosition = rippleTarget.position;
        }

        ApplyRippleArrays();
    }

    private void Update()
    {
        if (waterMaterial == null || rippleTarget == null)
            return;

        if (Input.GetKeyDown(triggerKey))
        {
            TriggerRipple(rippleTarget.position);
            return;
        }

        if (!autoTriggerByMovement)
            return;

        TryAutoTrigger();
    }

    private void TryAutoTrigger()
    {
        Vector3 currentPos = rippleTarget.position;

        if (!hasTriggeredOnce)
        {
            TriggerRipple(currentPos);
            return;
        }

        float moveDistance = Vector3.Distance(currentPos, lastTriggerPosition);
        float timeSinceLast = Time.time - lastTriggerTime;

        if (moveDistance >= minMoveDistance && timeSinceLast >= minTriggerInterval)
        {
            TriggerRipple(currentPos);
        }
    }

    public void TriggerRipple(Vector3 worldPos)
    {
        rippleOriginsWS[nextRippleIndex] = new Vector4(worldPos.x, worldPos.y, worldPos.z, 0);
        rippleStartTimes[nextRippleIndex] = Time.time;

        nextRippleIndex++;
        if (nextRippleIndex >= maxRipples)
            nextRippleIndex = 0;

        lastTriggerPosition = worldPos;
        lastTriggerTime = Time.time;
        hasTriggeredOnce = true;

        ApplyRippleArrays();
    }

    private void ApplyRippleArrays()
    {
        waterMaterial.SetVectorArray(RippleOriginsWSID, rippleOriginsWS);
        waterMaterial.SetFloatArray(RippleStartTimesID, rippleStartTimes);
    }
}