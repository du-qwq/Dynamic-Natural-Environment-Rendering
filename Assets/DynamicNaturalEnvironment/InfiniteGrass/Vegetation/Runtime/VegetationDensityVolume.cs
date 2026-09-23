using UnityEngine;

[ExecuteAlways]
public class VegetationDensityVolume : MonoBehaviour
{
    [Header("Volume")]
    public VegetationVolumeShape shape = VegetationVolumeShape.Box;

    [Tooltip("为空时影响所有程序化Layer；指定后只影响使用这个Species Group的Layer")]
    public VegetationSpeciesGroup affectedSpeciesGroup;

    [Header("Density")]
    [Range(0f, 1f)]
    [Tooltip("0 = 中心区域完全不生成，1 = 不改变原始密度")]
    public float densityMultiplier = 0.25f;

    [Range(0f, 1f)]
    [Tooltip("Volume边缘向原始密度渐变的区域比例。0表示硬边界")]
    public float edgeFalloff = 0.25f;

    [Header("Box")]
    public Vector3 boxSize = new Vector3(10f, 10f, 10f);

    [Header("Sphere")]
    [Min(0.01f)]
    public float sphereRadius = 5f;

    [Header("Capsule")]
    [Min(0.01f)]
    public float capsuleRadius = 3f;

    [Min(0.01f)]
    public float capsuleHeight = 10f;

    [Header("Debug")]
    public bool drawGizmo = true;

    public bool AffectsLayer(VegetationScatterLayer layer)
    {
        if (layer == null) return false;
        if (affectedSpeciesGroup == null) return true;
        return layer.speciesGroup == affectedSpeciesGroup;
    }

    public float EvaluateMultiplier(Vector3 worldPosition)
    {
        Vector3 localPosition =
            transform.InverseTransformPoint(
                worldPosition
            );

        float normalizedDepth;

        switch (shape)
        {
            case VegetationVolumeShape.Box:
                if (!TryGetBoxDepth(
                    localPosition,
                    out normalizedDepth))
                {
                    return 1f;
                }
                break;

            case VegetationVolumeShape.Sphere:
                if (!TryGetSphereDepth(
                    localPosition,
                    out normalizedDepth))
                {
                    return 1f;
                }
                break;

            case VegetationVolumeShape.Capsule:
                if (!TryGetCapsuleDepth(
                    localPosition,
                    out normalizedDepth))
                {
                    return 1f;
                }
                break;

            default:
                return 1f;
        }

        if (edgeFalloff <= 0.0001f)
        {
            return densityMultiplier;
        }

        float falloff = Mathf.Clamp01(
            normalizedDepth /
            Mathf.Max(
                edgeFalloff,
                0.0001f
            )
        );

        falloff =
            falloff *
            falloff *
            (3f - 2f * falloff);

        return Mathf.Lerp(
            1f,
            densityMultiplier,
            falloff
        );
    }

    private bool TryGetBoxDepth(
        Vector3 localPosition,
        out float normalizedDepth)
    {
        Vector3 halfSize = new Vector3(
            Mathf.Max(0.001f, boxSize.x * 0.5f),
            Mathf.Max(0.001f, boxSize.y * 0.5f),
            Mathf.Max(0.001f, boxSize.z * 0.5f)
        );

        float nx =
            Mathf.Abs(localPosition.x) /
            halfSize.x;

        float ny =
            Mathf.Abs(localPosition.y) /
            halfSize.y;

        float nz =
            Mathf.Abs(localPosition.z) /
            halfSize.z;

        float maxNormalized =
            Mathf.Max(
                nx,
                Mathf.Max(ny, nz)
            );

        if (maxNormalized > 1f)
        {
            normalizedDepth = 0f;
            return false;
        }

        normalizedDepth =
            1f - maxNormalized;

        return true;
    }

    private bool TryGetSphereDepth(
        Vector3 localPosition,
        out float normalizedDepth)
    {
        float radius =
            Mathf.Max(
                0.001f,
                sphereRadius
            );

        float distance =
            localPosition.magnitude;

        if (distance > radius)
        {
            normalizedDepth = 0f;
            return false;
        }

        normalizedDepth =
            1f -
            distance / radius;

        return true;
    }

    private bool TryGetCapsuleDepth(
        Vector3 localPosition,
        out float normalizedDepth)
    {
        float radius =
            Mathf.Max(
                0.001f,
                capsuleRadius
            );

        float height =
            Mathf.Max(
                capsuleHeight,
                radius * 2f
            );

        float halfLineLength =
            Mathf.Max(
                0f,
                height * 0.5f - radius
            );

        Vector3 a =
            Vector3.down *
            halfLineLength;

        Vector3 b =
            Vector3.up *
            halfLineLength;

        Vector3 closest =
            ClosestPointOnSegment(
                localPosition,
                a,
                b
            );

        float distance =
            Vector3.Distance(
                localPosition,
                closest
            );

        if (distance > radius)
        {
            normalizedDepth = 0f;
            return false;
        }

        normalizedDepth =
            1f -
            distance / radius;

        return true;
    }

    private static Vector3 ClosestPointOnSegment(
        Vector3 point,
        Vector3 a,
        Vector3 b)
    {
        Vector3 ab = b - a;

        float denominator =
            Vector3.Dot(ab, ab);

        if (denominator <= 0.000001f)
        {
            return a;
        }

        float t =
            Vector3.Dot(
                point - a,
                ab
            ) /
            denominator;

        t = Mathf.Clamp01(t);

        return a + ab * t;
    }

    private void OnValidate()
    {
        densityMultiplier =
            Mathf.Clamp01(
                densityMultiplier
            );

        edgeFalloff =
            Mathf.Clamp01(
                edgeFalloff
            );

        boxSize.x =
            Mathf.Max(
                0.01f,
                boxSize.x
            );

        boxSize.y =
            Mathf.Max(
                0.01f,
                boxSize.y
            );

        boxSize.z =
            Mathf.Max(
                0.01f,
                boxSize.z
            );

        sphereRadius =
            Mathf.Max(
                0.01f,
                sphereRadius
            );

        capsuleRadius =
            Mathf.Max(
                0.01f,
                capsuleRadius
            );

        capsuleHeight =
            Mathf.Max(
                capsuleRadius * 2f,
                capsuleHeight
            );
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmo)
        {
            return;
        }

        DrawVolumeGizmo(
            new Color(
                1f,
                0.75f,
                0.10f,
                0.75f
            )
        );
    }

    private void DrawVolumeGizmo(
        Color color)
    {
        Matrix4x4 oldMatrix =
            Gizmos.matrix;

        Color oldColor =
            Gizmos.color;

        Gizmos.matrix =
            transform.localToWorldMatrix;

        Gizmos.color =
            color;

        switch (shape)
        {
            case VegetationVolumeShape.Box:
                Gizmos.DrawWireCube(
                    Vector3.zero,
                    boxSize
                );
                break;

            case VegetationVolumeShape.Sphere:
                Gizmos.DrawWireSphere(
                    Vector3.zero,
                    sphereRadius
                );
                break;

            case VegetationVolumeShape.Capsule:
                DrawCapsuleGizmo();
                break;
        }

        Gizmos.matrix =
            oldMatrix;

        Gizmos.color =
            oldColor;
    }

    private void DrawCapsuleGizmo()
    {
        float radius =
            capsuleRadius;

        float height =
            Mathf.Max(
                capsuleHeight,
                radius * 2f
            );

        float halfLineLength =
            Mathf.Max(
                0f,
                height * 0.5f - radius
            );

        Vector3 top =
            Vector3.up *
            halfLineLength;

        Vector3 bottom =
            Vector3.down *
            halfLineLength;

        Gizmos.DrawWireSphere(
            top,
            radius
        );

        Gizmos.DrawWireSphere(
            bottom,
            radius
        );

        Gizmos.DrawLine(
            top + Vector3.right * radius,
            bottom + Vector3.right * radius
        );

        Gizmos.DrawLine(
            top - Vector3.right * radius,
            bottom - Vector3.right * radius
        );

        Gizmos.DrawLine(
            top + Vector3.forward * radius,
            bottom + Vector3.forward * radius
        );

        Gizmos.DrawLine(
            top - Vector3.forward * radius,
            bottom - Vector3.forward * radius
        );
    }
}