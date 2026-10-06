using System;
using UnityEngine;
using UnityEngine.Serialization;

public enum VegetationType
{
    Grass,
    Flower,
    Bush,
    Tree,
    Rock
}

public enum VegetationGroundPlacementMode
{
    MeshBounds,
    Pivot
}

public enum VegetationColliderType
{
    Capsule,
    Box,
    Sphere
}

[Serializable]
public class VegetationLODAsset
{
    [Tooltip("该LOD使用的Mesh")]
    public Mesh mesh;

    [Tooltip("材质顺序必须与Mesh的SubMesh顺序一致")]
    public Material[] materials = Array.Empty<Material>();

    public bool IsValid => mesh != null && materials != null && materials.Length > 0;
}

[CreateAssetMenu(fileName = "VS_NewVegetation", menuName = "Vegetation/Species")]
public class VegetationSpecies : ScriptableObject
{
    [Header("基本信息")]
    public string speciesName = "New Vegetation";

    [Tooltip("植被类型")]
    public VegetationType vegetationType = VegetationType.Grass;

    [Header("LOD资源")]
    public VegetationLODAsset lod0 = new VegetationLODAsset();
    public VegetationLODAsset lod1 = new VegetationLODAsset();
    public VegetationLODAsset lod2 = new VegetationLODAsset();
    public VegetationLODAsset lod3 = new VegetationLODAsset();

    [Header("LOD距离")]
    [Min(0f), Tooltip("LOD0结束距离。草在该距离内保持100%密度")]
    public float lod0Distance = 30f;

    [Min(0f), Tooltip("LOD1结束距离。草在LOD0 Distance到该距离之间使用Mid Density")]
    public float lod1Distance = 80f;

    [Min(0f), Tooltip("LOD2结束距离。超过该距离后使用LOD3")]
    public float lod2Distance = 140f;

    [Min(0f), Tooltip("超过该距离后完全剔除")]
    public float cullDistance = 200f;

    [Header("Mesh LOD Cross Fade")]
    [Tooltip("在相邻有效 Mesh LOD 的距离边界使用 Dither Crossfade。草密度 LOD 不受影响。")]
    public bool enableLODCrossFade = true;

    [Min(0f), Tooltip("Mesh LOD 过渡带宽度，单位为世界空间米。0 表示硬切。")]
    public float lodCrossFadeWidth = 8f;

    [Header("草密度LOD")]
    [Range(0f, 1f), Tooltip("草在中距离保留的实例比例")]
    public float grassMidDensity = 0.6f;

    [Range(0f, 1f), Tooltip("草在远距离保留的实例比例")]
    public float grassFarDensity = 0.3f;

    [Header("生成设置")]
    public Vector2 scaleRange = new Vector2(0.8f, 1.2f);

    [Tooltip("贴地方式。MeshBounds会根据Mesh底部自动修正Pivot高度，Pivot则直接把Pivot放到地面。")]
    public VegetationGroundPlacementMode groundPlacementMode = VegetationGroundPlacementMode.MeshBounds;

    [Tooltip("贴地后的额外偏移。正值抬高，负值下沉，单位为世界空间米。")]
    public float groundOffset = 0f;

    [Tooltip("是否随机绕Y轴旋转")]
    public bool randomYRotation = true;

    [Tooltip("是否根据地面法线倾斜")]
    public bool alignToTerrainNormal;

    [Min(0f), Tooltip("实例之间允许的最小距离")]
    public float minSpacing = 0.2f;

    [Header("碰撞")]
    [Tooltip("是否为该Species在玩家附近生成动态碰撞代理。草、花等通常保持关闭。")]
    public bool enableCollision = false;

    [Tooltip("碰撞体类型。树干通常使用Capsule，岩石通常使用Box。")]
    public VegetationColliderType colliderType = VegetationColliderType.Capsule;

    [Tooltip("碰撞体中心，使用Species模型本地空间坐标。实例的位置、旋转、缩放会自动应用到碰撞代理。")]
    public Vector3 colliderCenter = new Vector3(0f, 1f, 0f);

    [Min(0.001f), Tooltip("CapsuleCollider半径。仅Collider Type为Capsule时使用。")]
    public float capsuleRadius = 0.35f;

    [Min(0.001f), Tooltip("CapsuleCollider高度。仅Collider Type为Capsule时使用。高度不会小于直径。")]
    public float capsuleHeight = 2f;

    [Tooltip("BoxCollider尺寸。仅Collider Type为Box时使用。")]
    public Vector3 boxColliderSize = new Vector3(0.7f, 2f, 0.7f);

    [Min(0.001f), Tooltip("SphereCollider半径。仅Collider Type为Sphere时使用。")]
    public float sphereRadius = 0.5f;

    [Tooltip("是否作为Trigger使用。普通树木/岩石阻挡通常关闭。")]
    public bool colliderIsTrigger = false;

    [Tooltip("可选Physic Material。为空时使用Unity默认物理材质。")]
    public PhysicMaterial colliderMaterial;

    [Header("渲染")]
    public bool castShadows = true;
    public bool receiveShadows = true;

    [Header("阴影优化")]
    [Min(0f), Tooltip("阴影最远剔除距离。0表示跟随Forward Cull Distance，建议树木先设为80~140，草设为25~40。")]
    public float shadowCullDistance = 0f;

    [Range(0.1f, 2f), Tooltip("阴影LOD距离相对Forward LOD距离的倍率。1表示完全跟随Forward；小于1会让阴影更早进入低LOD。")]
    public float shadowLODScale = 1f;

    [Header("剔除边界")]
    [Min(0f), Tooltip("在世界空间XZ方向扩展Bounds，用于覆盖风摆、草弯曲等Shader顶点位移")]
    public float horizontalBoundsPadding = 0.5f;

    [Min(0f), Tooltip("在世界空间Y方向扩展Bounds，用于覆盖高度变化和垂直方向顶点位移")]
    public float verticalBoundsPadding = 0.25f;

    [SerializeField, HideInInspector, FormerlySerializedAs("lod0Mesh")]
    private Mesh legacyLod0Mesh;

    [SerializeField, HideInInspector, FormerlySerializedAs("lod1Mesh")]
    private Mesh legacyLod1Mesh;

    [SerializeField, HideInInspector, FormerlySerializedAs("lod2Mesh")]
    private Mesh legacyLod2Mesh;

    [SerializeField, HideInInspector, FormerlySerializedAs("material")]
    private Material legacyMaterial;

    public Mesh lod0Mesh => lod0 != null ? lod0.mesh : null;
    public Mesh lod1Mesh => lod1 != null ? lod1.mesh : null;
    public Mesh lod2Mesh => lod2 != null ? lod2.mesh : null;
    public Mesh lod3Mesh => lod3 != null ? lod3.mesh : null;
    public Material material => lod0 != null && lod0.materials != null && lod0.materials.Length > 0 ? lod0.materials[0] : null;

    private void OnEnable()
    {
        MigrateLegacyData();
    }

    private void OnValidate()
    {
        MigrateLegacyData();

        scaleRange.x = Mathf.Max(0.001f, scaleRange.x);
        scaleRange.y = Mathf.Max(scaleRange.x, scaleRange.y);

        lod0Distance = Mathf.Max(0f, lod0Distance);
        lod1Distance = Mathf.Max(lod0Distance, lod1Distance);

        if (vegetationType == VegetationType.Grass)
        {
            cullDistance = Mathf.Max(lod1Distance, cullDistance);
        }
        else
        {
            lod2Distance = Mathf.Max(lod1Distance, lod2Distance);
            cullDistance = Mathf.Max(lod2Distance, cullDistance);
        }

        capsuleRadius = Mathf.Max(0.001f, capsuleRadius);
        capsuleHeight = Mathf.Max(capsuleRadius * 2f, capsuleHeight);
        boxColliderSize.x = Mathf.Max(0.001f, boxColliderSize.x);
        boxColliderSize.y = Mathf.Max(0.001f, boxColliderSize.y);
        boxColliderSize.z = Mathf.Max(0.001f, boxColliderSize.z);
        sphereRadius = Mathf.Max(0.001f, sphereRadius);

        shadowCullDistance = Mathf.Max(0f, shadowCullDistance);
        lodCrossFadeWidth = Mathf.Max(0f, lodCrossFadeWidth);
        shadowLODScale = shadowLODScale <= 0f ? 1f : Mathf.Clamp(shadowLODScale, 0.1f, 2f);
        horizontalBoundsPadding = Mathf.Max(0f, horizontalBoundsPadding);
        verticalBoundsPadding = Mathf.Max(0f, verticalBoundsPadding);
    }

    public float GetShadowCullDistance()
    {
        return shadowCullDistance > 0f ? shadowCullDistance : cullDistance;
    }

    public float GetGroundPlacementBottomY()
    {
        Mesh mesh = GetGroundPlacementMesh();
        return mesh != null ? mesh.bounds.min.y : 0f;
    }

    public Mesh GetGroundPlacementMesh()
    {
        if (lod0 != null && lod0.mesh != null) return lod0.mesh;
        if (lod1 != null && lod1.mesh != null) return lod1.mesh;
        if (lod2 != null && lod2.mesh != null) return lod2.mesh;
        if (lod3 != null && lod3.mesh != null) return lod3.mesh;
        return null;
    }

    public Bounds GetLocalMeshBounds()
    {
        bool hasBounds = false;
        Bounds bounds = default;

        EncapsulateLODBounds(lod0, ref bounds, ref hasBounds);
        EncapsulateLODBounds(lod1, ref bounds, ref hasBounds);
        EncapsulateLODBounds(lod2, ref bounds, ref hasBounds);
        EncapsulateLODBounds(lod3, ref bounds, ref hasBounds);

        if (!hasBounds) bounds = new Bounds(Vector3.zero, Vector3.one * 0.02f);

        return bounds;
    }

    private static void EncapsulateLODBounds(VegetationLODAsset lod, ref Bounds bounds, ref bool hasBounds)
    {
        if (lod == null || !lod.IsValid || lod.mesh == null) return;

        if (!hasBounds)
        {
            bounds = lod.mesh.bounds;
            hasBounds = true;
            return;
        }

        bounds.Encapsulate(lod.mesh.bounds);
    }

    private void MigrateLegacyData()
    {
        if (lod0 == null) lod0 = new VegetationLODAsset();
        if (lod1 == null) lod1 = new VegetationLODAsset();
        if (lod2 == null) lod2 = new VegetationLODAsset();
        if (lod3 == null) lod3 = new VegetationLODAsset();

        if (lod0.mesh == null && legacyLod0Mesh != null) lod0.mesh = legacyLod0Mesh;
        if (lod1.mesh == null && legacyLod1Mesh != null) lod1.mesh = legacyLod1Mesh;
        if (lod2.mesh == null && legacyLod2Mesh != null) lod2.mesh = legacyLod2Mesh;

        if (legacyMaterial == null) return;

        if (lod0.mesh != null && (lod0.materials == null || lod0.materials.Length == 0)) lod0.materials = new[] { legacyMaterial };
        if (lod1.mesh != null && (lod1.materials == null || lod1.materials.Length == 0)) lod1.materials = new[] { legacyMaterial };
        if (lod2.mesh != null && (lod2.materials == null || lod2.materials.Length == 0)) lod2.materials = new[] { legacyMaterial };
    }
}
