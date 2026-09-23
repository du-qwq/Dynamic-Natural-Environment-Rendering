using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局草交互控制器。
/// 将少量 VegetationGrassInteractor 数据上传为 Shader Global，
/// 真正的大规模草变形全部在 GPU 顶点阶段完成。
/// 场景中通常只需要一个。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public class VegetationInteractionController : MonoBehaviour
{
    public const int ShaderMaxInteractors = 8;

    [Header("Global")]
    [Tooltip("是否启用草交互。关闭后不会执行草交互 Shader 计算。")]
    public bool enableGrassInteraction = true;

    [Range(1, ShaderMaxInteractors)]
    [Tooltip("每帧最多上传多少个 Interactor。建议 4；大量角色时再提高到 8。")]
    public int maxInteractors = 4;

    [Header("Grass Bend")]
    [Range(0f, 1f)]
    [Tooltip("草尖最大弯曲位移基础值，单位为世界空间米。")]
    public float bendStrength = 0.26f;

    [Range(0f, 0.3f)]
    [Tooltip("交互中心附近草尖向下压低的基础量，单位为世界空间米。")]
    public float flatten = 0.055f;

    [Range(0f, 1f)]
    [Tooltip("角色运动方向对最终弯曲方向的影响。0=纯径向推开，1=高速时更偏向运动方向。")]
    public float motionInfluence = 0.55f;

    [Range(0.1f, 4f)]
    [Tooltip("交互半径衰减曲线。越大，效果越集中在角色附近。")]
    public float falloffPower = 1.7f;

    [Range(0.1f, 6f)]
    [Tooltip("使用草 Mesh Vertex Color R 作为根部到草尖 Mask 时的幂。越大越锁住草根。")]
    public float tipPower = 1.8f;

    [Range(0.01f, 1f)]
    [Tooltip("多个 Interactor 叠加时，XZ 最终允许的最大草尖偏移。")]
    public float maxOffset = 0.30f;

    [Header("Selection")]
    [Tooltip("当 Interactor 数量超过 Max Interactors 时，用这个相机的位置作为同优先级对象的距离排序参考。为空时优先 Camera.main。")]
    public Camera targetCamera;

    [Header("Debug")]
    [SerializeField, Tooltip("当前实际上传给 Shader 的 Interactor 数量。")]
    private int uploadedInteractorCount;

    [SerializeField, Tooltip("Scene 调试：当前激活但尚未过滤的 Interactor 数量。")]
    private int activeInteractorCount;

    public int UploadedInteractorCount => uploadedInteractorCount;
    public int ActiveInteractorCount => activeInteractorCount;

    private static readonly int InteractionEnabledID = Shader.PropertyToID("_VegetationGrassInteractionEnabled");
    private static readonly int InteractorCountID = Shader.PropertyToID("_VegetationGrassInteractorCount");
    private static readonly int InteractorsID = Shader.PropertyToID("_VegetationGrassInteractors");
    private static readonly int InteractorMotionID = Shader.PropertyToID("_VegetationGrassInteractorMotion");
    private static readonly int InteractionParamsID = Shader.PropertyToID("_VegetationGrassInteractionParams");
    private static readonly int InteractionParams2ID = Shader.PropertyToID("_VegetationGrassInteractionParams2");

    // xyz = Center WS, w = Radius
    private readonly Vector4[] interactorData = new Vector4[ShaderMaxInteractors];

    // xy = Move Direction XZ, z = Motion01, w = Strength
    private readonly Vector4[] interactorMotionData = new Vector4[ShaderMaxInteractors];

    // 仅用于从大量 Interactor 中选出优先级最高 / 距离最近的少量对象；不产生 GC。
    private readonly VegetationGrassInteractor[] selected = new VegetationGrassInteractor[ShaderMaxInteractors];

    private void OnEnable()
    {
        ApplyGlobalInteraction();
    }

    private void LateUpdate()
    {
        ApplyGlobalInteraction();
    }

    private void OnValidate()
    {
        maxInteractors = Mathf.Clamp(maxInteractors, 1, ShaderMaxInteractors);
        bendStrength = Mathf.Max(0f, bendStrength);
        flatten = Mathf.Max(0f, flatten);
        motionInfluence = Mathf.Clamp01(motionInfluence);
        falloffPower = Mathf.Max(0.1f, falloffPower);
        tipPower = Mathf.Max(0.1f, tipPower);
        maxOffset = Mathf.Max(0.01f, maxOffset);

        ApplyGlobalInteraction();
    }

    private void OnDisable()
    {
        uploadedInteractorCount = 0;
        activeInteractorCount = 0;
        Shader.SetGlobalFloat(InteractionEnabledID, 0f);
        Shader.SetGlobalInt(InteractorCountID, 0);

#if UNITY_EDITOR
        ClearDebugUploadStates();
#endif
    }

    private void ApplyGlobalInteraction()
    {
        List<VegetationGrassInteractor> active = VegetationGrassInteractor.ActiveInteractors;
        activeInteractorCount = active != null ? active.Count : 0;

#if UNITY_EDITOR
        ClearDebugUploadStates();
#endif

        if (!enableGrassInteraction)
        {
            uploadedInteractorCount = 0;
            Shader.SetGlobalFloat(InteractionEnabledID, 0f);
            Shader.SetGlobalInt(InteractorCountID, 0);
            return;
        }

        Vector3 referencePosition = ResolveReferencePosition();
        int requestedCount = Mathf.Clamp(maxInteractors, 1, ShaderMaxInteractors);
        int count = SelectInteractors(referencePosition, requestedCount);

        for (int i = 0; i < count; i++)
        {
            VegetationGrassInteractor interactor = selected[i];
            Vector3 center = interactor.CenterWS;
            Vector3 velocity = interactor.VelocityWS;

            Vector2 horizontalVelocity = new Vector2(velocity.x, velocity.z);
            Vector2 moveDirection = horizontalVelocity.sqrMagnitude > 0.000001f
                ? horizontalVelocity.normalized
                : Vector2.zero;

            interactorData[i] = new Vector4(
                center.x,
                center.y,
                center.z,
                Mathf.Max(interactor.radius, 0.01f)
            );

            interactorMotionData[i] = new Vector4(
                moveDirection.x,
                moveDirection.y,
                interactor.Motion01,
                Mathf.Max(interactor.strength, 0f)
            );

#if UNITY_EDITOR
            interactor.SetDebugUploadState(true, i);
#endif
        }

        // 非必要，但清零未使用槽位有利于 Frame Debug / Shader 调试时观察。
        for (int i = count; i < ShaderMaxInteractors; i++)
        {
            interactorData[i] = Vector4.zero;
            interactorMotionData[i] = Vector4.zero;
            selected[i] = null;
        }

        uploadedInteractorCount = count;

        Shader.SetGlobalFloat(InteractionEnabledID, 1f);
        Shader.SetGlobalInt(InteractorCountID, count);
        Shader.SetGlobalVectorArray(InteractorsID, interactorData);
        Shader.SetGlobalVectorArray(InteractorMotionID, interactorMotionData);

        // x = Bend Strength, y = Flatten, z = Motion Influence, w = Falloff Power
        Shader.SetGlobalVector(
            InteractionParamsID,
            new Vector4(bendStrength, flatten, motionInfluence, falloffPower)
        );

        // x = Tip Power, y = Max XZ Offset
        Shader.SetGlobalVector(
            InteractionParams2ID,
            new Vector4(tipPower, maxOffset, 0f, 0f)
        );
    }

    private int SelectInteractors(Vector3 referencePosition, int requestedCount)
    {
        Array.Clear(selected, 0, selected.Length);

        List<VegetationGrassInteractor> active = VegetationGrassInteractor.ActiveInteractors;
        if (active == null || active.Count == 0) return 0;

        int selectedCount = 0;

        // Max 8，所以直接 O(N * 8) 查找，不排序、不创建临时 List，也没有每帧 GC。
        while (selectedCount < requestedCount)
        {
            VegetationGrassInteractor best = null;
            int bestPriority = int.MinValue;
            float bestDistanceSqr = float.PositiveInfinity;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                VegetationGrassInteractor candidate = active[i];

                if (candidate == null)
                {
                    active.RemoveAt(i);
                    continue;
                }

                if (!candidate.isActiveAndEnabled || candidate.strength <= 0f || candidate.radius <= 0f)
                    continue;

                if (IsAlreadySelected(candidate, selectedCount))
                    continue;

                int candidatePriority = candidate.priority;
                float candidateDistanceSqr = (candidate.CenterWS - referencePosition).sqrMagnitude;

                if (best == null ||
                    candidatePriority > bestPriority ||
                    (candidatePriority == bestPriority && candidateDistanceSqr < bestDistanceSqr))
                {
                    best = candidate;
                    bestPriority = candidatePriority;
                    bestDistanceSqr = candidateDistanceSqr;
                }
            }

            if (best == null) break;

            selected[selectedCount] = best;
            selectedCount++;
        }

        return selectedCount;
    }

    private bool IsAlreadySelected(VegetationGrassInteractor candidate, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (selected[i] == candidate) return true;
        }

        return false;
    }

    private Vector3 ResolveReferencePosition()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        return cam != null ? cam.transform.position : transform.position;
    }

#if UNITY_EDITOR
    private static void ClearDebugUploadStates()
    {
        List<VegetationGrassInteractor> active = VegetationGrassInteractor.ActiveInteractors;
        if (active == null) return;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            VegetationGrassInteractor interactor = active[i];
            if (interactor == null)
            {
                active.RemoveAt(i);
                continue;
            }

            interactor.SetDebugUploadState(false, -1);
        }
    }
#endif
}
