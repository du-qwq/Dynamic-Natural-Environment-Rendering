using UnityEngine;

public enum VegetationVolumeShape
{
    Box,
    Sphere,
    Capsule
}

[ExecuteAlways]
public class VegetationExclusionVolume : MonoBehaviour
{
    [Header("Volume")]
    public VegetationVolumeShape shape = VegetationVolumeShape.Box;

    [Tooltip("为空时影响所有程序化Layer；指定后只影响使用这个Species Group的Layer")]
    public VegetationSpeciesGroup affectedSpeciesGroup;

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

    public bool Contains(Vector3 worldPosition)
    {
        Vector3 localPosition = transform.InverseTransformPoint(worldPosition);

        switch (shape)
        {
            case VegetationVolumeShape.Box:
                return ContainsBox(localPosition);

            case VegetationVolumeShape.Sphere:
                return ContainsSphere(localPosition);

            case VegetationVolumeShape.Capsule:
                return ContainsCapsule(localPosition);
        }

        return false;
    }

    private bool ContainsBox(Vector3 localPosition)
    {
        Vector3 halfSize = new Vector3(
            Mathf.Max(0.001f, boxSize.x * 0.5f),
            Mathf.Max(0.001f, boxSize.y * 0.5f),
            Mathf.Max(0.001f, boxSize.z * 0.5f)
        );

        return
            Mathf.Abs(localPosition.x) <= halfSize.x &&
            Mathf.Abs(localPosition.y) <= halfSize.y &&
            Mathf.Abs(localPosition.z) <= halfSize.z;
    }

    private bool ContainsSphere(Vector3 localPosition)
    {
        float radius = Mathf.Max(0.001f, sphereRadius);
        return localPosition.sqrMagnitude <= radius * radius;
    }

    private bool ContainsCapsule(Vector3 localPosition)
    {
        float radius = Mathf.Max(0.001f, capsuleRadius);
        float height = Mathf.Max(capsuleHeight, radius * 2f);

        float halfLineLength = Mathf.Max(
            0f,
            height * 0.5f - radius
        );

        Vector3 a = new Vector3(
            0f,
            -halfLineLength,
            0f
        );

        Vector3 b = new Vector3(
            0f,
            halfLineLength,
            0f
        );

        Vector3 closest = ClosestPointOnSegment(
            localPosition,
            a,
            b
        );

        return
            (localPosition - closest).sqrMagnitude <=
            radius * radius;
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
            Vector3.Dot(point - a, ab) /
            denominator;

        t = Mathf.Clamp01(t);

        return a + ab * t;
    }

    private void OnValidate()
    {
        boxSize.x = Mathf.Max(0.01f, boxSize.x);
        boxSize.y = Mathf.Max(0.01f, boxSize.y);
        boxSize.z = Mathf.Max(0.01f, boxSize.z);

        sphereRadius = Mathf.Max(
            0.01f,
            sphereRadius
        );

        capsuleRadius = Mathf.Max(
            0.01f,
            capsuleRadius
        );

        capsuleHeight = Mathf.Max(
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
                0.20f,
                0.15f,
                0.75f
            )
        );
    }

    private void DrawVolumeGizmo(Color color)
    {
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Color oldColor = Gizmos.color;

        Gizmos.matrix =
            transform.localToWorldMatrix;

        Gizmos.color = color;

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

        Gizmos.matrix = oldMatrix;
        Gizmos.color = oldColor;
    }

    private void DrawCapsuleGizmo()
    {
        float radius = capsuleRadius;

        float height = Mathf.Max(
            capsuleHeight,
            radius * 2f
        );

        float halfLineLength =
            Mathf.Max(
                0f,
                height * 0.5f - radius
            );

        Vector3 top =
            Vector3.up * halfLineLength;

        Vector3 bottom =
            Vector3.down * halfLineLength;

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