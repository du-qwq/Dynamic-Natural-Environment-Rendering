using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 挂在 Player / NPC / 动物等需要推开草的对象上。
/// 本组件不碰 Vegetation Instance Buffer，也不生成草碰撞体；
/// 只负责向 VegetationInteractionController 提供交互中心、半径、强度和运动信息。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(900)]
public class VegetationGrassInteractor : MonoBehaviour
{
    private static readonly List<VegetationGrassInteractor> ActiveList = new List<VegetationGrassInteractor>(8);

    internal static List<VegetationGrassInteractor> ActiveInteractors => ActiveList;

    [Header("Interaction")]
    [Min(0.01f)]
    [Tooltip("XZ 平面中的草交互半径，单位为世界空间米。")]
    public float radius = 0.65f;

    [Range(0f, 2f)]
    [Tooltip("该 Interactor 对草的总影响倍率。1 为正常强度。")]
    public float strength = 1f;

    [Tooltip("交互中心相对于当前 Transform 的本地偏移。XZ 决定交互中心，Y 当前仅用于调试显示。")]
    public Vector3 centerOffset = new Vector3(0f, 0.15f, 0f);

    [Header("Motion")]
    [Min(0.01f)]
    [Tooltip("达到该水平速度后，Shader 中的运动方向影响视为 100%。")]
    public float fullMotionSpeed = 4f;

    [Min(0f)]
    [Tooltip("速度平滑速度。越大响应越快；0 表示不平滑。")]
    public float velocitySmoothing = 12f;

    [Header("Priority")]
    [Tooltip("当场景中的 Interactor 数量超过 Controller 的 Max Interactors 时，优先级更高的先被上传。")]
    public int priority = 100;

    [Header("Debug")]
    [Tooltip("在 Scene 视图中显示这个 Interactor 的调试信息。")]
    public bool showGizmos = true;

    [Tooltip("开启后只有选中该对象时才绘制；关闭后会一直显示。")]
    public bool drawOnlyWhenSelected = false;

    [Tooltip("显示交互半径圆和 50% 半径参考圆。")]
    public bool showRadiusGuide = true;

    [Tooltip("显示当前平滑后的水平运动方向与 Motion01。")]
    public bool showMotionDirection = true;

    [Tooltip("显示 GPU 上传槽位、Radius、Strength、Speed、Motion01。")]
    public bool showDebugLabel = true;

    [Range(0.25f, 2f)]
    [Tooltip("Scene 视图运动箭头长度倍率。")]
    public float debugArrowLength = 0.9f;

    private Vector3 previousCenterWS;
    private Vector3 smoothedVelocityWS;
    private bool hasPreviousCenter;

#if UNITY_EDITOR
    // 由 VegetationInteractionController 每帧更新，仅用于 Scene 调试。
    private bool debugUploadedToShader;
    private int debugUploadSlot = -1;
#endif

    public Vector3 CenterWS => transform.TransformPoint(centerOffset);
    public Vector3 VelocityWS => smoothedVelocityWS;

    public float HorizontalSpeed
    {
        get
        {
            Vector2 horizontal = new Vector2(smoothedVelocityWS.x, smoothedVelocityWS.z);
            return horizontal.magnitude;
        }
    }

    public float Motion01 => Mathf.Clamp01(HorizontalSpeed / Mathf.Max(fullMotionSpeed, 0.01f));

    private void OnEnable()
    {
        Register(this);
        ResetMotionTracking();
    }

    private void OnDisable()
    {
        Unregister(this);
        ResetMotionTracking();

#if UNITY_EDITOR
        SetDebugUploadState(false, -1);
#endif
    }

    private void OnDestroy()
    {
        Unregister(this);
    }

    private void LateUpdate()
    {
        SampleMotion();
    }

    private void OnValidate()
    {
        radius = Mathf.Max(0.01f, radius);
        strength = Mathf.Clamp(strength, 0f, 2f);
        fullMotionSpeed = Mathf.Max(0.01f, fullMotionSpeed);
        velocitySmoothing = Mathf.Max(0f, velocitySmoothing);
        debugArrowLength = Mathf.Clamp(debugArrowLength, 0.25f, 2f);
    }

    private void SampleMotion()
    {
        Vector3 currentCenter = CenterWS;

        if (!Application.isPlaying)
        {
            previousCenterWS = currentCenter;
            smoothedVelocityWS = Vector3.zero;
            hasPreviousCenter = true;
            return;
        }

        float dt = Time.deltaTime;
        if (!hasPreviousCenter || dt <= 0.000001f)
        {
            previousCenterWS = currentCenter;
            smoothedVelocityWS = Vector3.zero;
            hasPreviousCenter = true;
            return;
        }

        Vector3 rawVelocity = (currentCenter - previousCenterWS) / dt;
        previousCenterWS = currentCenter;

        // 草只关心水平运动，避免角色跳跃时把垂直速度当成“推草方向”。
        rawVelocity.y = 0f;

        // 防止瞬移 / Teleport 在一帧内产生极端速度。
        float maxTrackedSpeed = Mathf.Max(fullMotionSpeed * 4f, 1f);
        rawVelocity = Vector3.ClampMagnitude(rawVelocity, maxTrackedSpeed);

        if (velocitySmoothing <= 0f)
        {
            smoothedVelocityWS = rawVelocity;
            return;
        }

        float t = 1f - Mathf.Exp(-velocitySmoothing * dt);
        smoothedVelocityWS = Vector3.Lerp(smoothedVelocityWS, rawVelocity, t);
    }

    private void ResetMotionTracking()
    {
        previousCenterWS = CenterWS;
        smoothedVelocityWS = Vector3.zero;
        hasPreviousCenter = false;
    }

    private static void Register(VegetationGrassInteractor interactor)
    {
        if (interactor == null || ActiveList.Contains(interactor)) return;
        ActiveList.Add(interactor);
    }

    private static void Unregister(VegetationGrassInteractor interactor)
    {
        if (interactor == null) return;
        ActiveList.Remove(interactor);
    }

#if UNITY_EDITOR
    internal void SetDebugUploadState(bool uploaded, int slot)
    {
        debugUploadedToShader = uploaded;
        debugUploadSlot = uploaded ? slot : -1;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos || drawOnlyWhenSelected) return;
        DrawInteractionDebug(false);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || !drawOnlyWhenSelected) return;
        DrawInteractionDebug(true);
    }

    private void DrawInteractionDebug(bool selectedByUser)
    {
        Vector3 center = CenterWS;
        float safeRadius = Mathf.Max(radius, 0.01f);

        // 绿色 = 当前确实进入 Shader Global 数组；灰色 = 存在但本帧没有被 Controller 选中。
        Color uploadedColor = new Color(0.25f, 1f, 0.35f, 0.95f);
        Color notUploadedColor = new Color(0.55f, 0.55f, 0.55f, 0.70f);
        Color baseColor = debugUploadedToShader ? uploadedColor : notUploadedColor;

        if (selectedByUser)
            baseColor = Color.Lerp(baseColor, Color.white, 0.25f);

        if (showRadiusGuide)
        {
            UnityEditor.Handles.color = baseColor;
            UnityEditor.Handles.DrawWireDisc(center, Vector3.up, safeRadius);

            // 只是空间参考圆，不表示精确的 Shader Falloff 值。
            Color innerColor = baseColor;
            innerColor.a *= 0.45f;
            UnityEditor.Handles.color = innerColor;
            UnityEditor.Handles.DrawWireDisc(center, Vector3.up, safeRadius * 0.5f);
        }

        // 中心点。
        UnityEditor.Handles.color = baseColor;
        float centerSize = Mathf.Max(UnityEditor.HandleUtility.GetHandleSize(center) * 0.04f, 0.015f);
        UnityEditor.Handles.SphereHandleCap(0, center, Quaternion.identity, centerSize, EventType.Repaint);

        if (showMotionDirection)
        {
            Vector3 velocity = VelocityWS;
            velocity.y = 0f;

            float speed = velocity.magnitude;
            if (speed > 0.001f)
            {
                Vector3 direction = velocity / speed;
                float arrowLength = safeRadius * Mathf.Lerp(0.30f, 1.0f, Motion01) * debugArrowLength;
                Vector3 arrowStart = center + Vector3.up * 0.035f;
                Vector3 arrowEnd = arrowStart + direction * arrowLength;

                UnityEditor.Handles.color = Color.Lerp(new Color(1f, 0.65f, 0.1f, 0.95f), Color.red, Motion01);
                UnityEditor.Handles.DrawAAPolyLine(3f, arrowStart, arrowEnd);

                float coneSize = Mathf.Clamp(arrowLength * 0.16f, 0.035f, 0.14f);
                UnityEditor.Handles.ConeHandleCap(
                    0,
                    arrowEnd,
                    Quaternion.LookRotation(direction),
                    coneSize,
                    EventType.Repaint
                );
            }
        }

        if (showDebugLabel)
        {
            string uploadText = debugUploadedToShader
                ? $"GPU Slot {debugUploadSlot}"
                : "Not Uploaded";

            string label =
                $"{name}  [{uploadText}]\n" +
                $"Radius {safeRadius:0.00}   Strength {strength:0.00}\n" +
                $"Speed {HorizontalSpeed:0.00} m/s   Motion {Motion01:0.00}";

            Vector3 labelPos = center + Vector3.up * Mathf.Max(0.15f, safeRadius * 0.25f);

            GUIStyle style = new GUIStyle(UnityEditor.EditorStyles.helpBox)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };

            UnityEditor.Handles.Label(labelPos, label, style);
        }
    }
#endif
}
