using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[AddComponentMenu("Vegetation/Vegetation Collider Manager")]
public class VegetationColliderManager : MonoBehaviour
{
    private static readonly ProfilerMarker UpdateProfilerMarker = new ProfilerMarker("Vegetation.Collider.Update");

    [Header("数据")]
    [Tooltip("要读取实例数据的VegetationDatabase。通常与VegetationRenderer使用同一个Database。")]
    public VegetationDatabase database;

    [Tooltip("碰撞激活中心，通常指定Player根节点，而不是Camera。")]
    public Transform target;

    [Header("范围")]
    [Min(0.1f), Tooltip("目标进入该距离时，为支持碰撞的植被创建/启用Collider Proxy。")]
    public float activationRadius = 30f;

    [Min(0.1f), Tooltip("已激活的Collider超过该距离才回收。应略大于Activation Radius，避免边界反复开关。")]
    public float deactivationRadius = 35f;

    [Min(0.01f), Tooltip("目标至少移动这么远才重新扫描。Database数据发生变化时会忽略该限制并立即刷新。")]
    public float updateDistance = 2.5f;

    [Header("代理对象")]
    [Tooltip("碰撞代理所在Layer名称。建议创建VegetationCollision层。名称不存在时会回退到Default。")]
    public string colliderLayerName = "Default";

    [Tooltip("运行时是否在Hierarchy中隐藏Collider Proxy。调试碰撞范围时建议关闭。")]
    public bool hideProxyObjectsInHierarchy = false;

    [Header("调试")]
    public bool showDebugGizmos = true;

    [SerializeField] private int activeColliderCount;
    [SerializeField] private int pooledColliderCount;
    [SerializeField] private int scannedInstanceCount;
    [SerializeField] private int scannedChunkCount;

    public int ActiveColliderCount => activeColliderCount;
    public int PooledColliderCount => pooledColliderCount;
    public int ScannedInstanceCount => scannedInstanceCount;
    public int ScannedChunkCount => scannedChunkCount;

    private readonly Dictionary<int, ColliderProxy> activeProxies = new Dictionary<int, ColliderProxy>();
    private readonly Stack<ColliderProxy> proxyPool = new Stack<ColliderProxy>();
    private readonly List<int> releaseBuffer = new List<int>();
    private readonly HashSet<int> seenActiveIDs = new HashSet<int>();

    private Transform proxyRoot;
    private Vector3 lastRefreshPosition;
    private int lastDatabaseRevision = int.MinValue;
    private bool hasRefreshPosition;
    private bool warnedMissingTarget;
    private bool warnedInvalidLayer;

    private sealed class ColliderProxy
    {
        public GameObject gameObject;
        public Transform transform;
        public CapsuleCollider capsule;
        public BoxCollider box;
        public SphereCollider sphere;
        public int persistentID;
        public int speciesIndex;
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;

        hasRefreshPosition = false;
        lastDatabaseRevision = int.MinValue;
        warnedMissingTarget = false;
        EnsureProxyRoot();
    }

    private void Update()
    {
        using (UpdateProfilerMarker.Auto())
        {
        if (database == null)
        {
            ReleaseAllActive();
            return;
        }

        if (target == null)
        {
            ReleaseAllActive();

            if (!warnedMissingTarget)
            {
                Debug.LogWarning("VegetationColliderManager没有Target。请把Player根节点拖到Target，碰撞代理不会跟随Camera自动生成。", this);
                warnedMissingTarget = true;
            }

            return;
        }

        warnedMissingTarget = false;

        Vector3 targetPosition = target.position;
        bool databaseChanged = lastDatabaseRevision != database.DataRevision;
        bool movedEnough = !hasRefreshPosition || HorizontalDistanceSqr(lastRefreshPosition, targetPosition) >= updateDistance * updateDistance;

        if (!databaseChanged && !movedEnough) return;

        RefreshColliders(targetPosition);
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        ReleaseAllActive();
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying) return;

        ReleaseAllActive();

        while (proxyPool.Count > 0)
        {
            ColliderProxy proxy = proxyPool.Pop();
            if (proxy != null && proxy.gameObject != null) Destroy(proxy.gameObject);
        }

        if (proxyRoot != null) Destroy(proxyRoot.gameObject);
    }

    public void RefreshNow()
    {
        if (!Application.isPlaying || database == null || target == null) return;
        RefreshColliders(target.position);
    }

    private void RefreshColliders(Vector3 targetPosition)
    {
        EnsureProxyRoot();

        activationRadius = Mathf.Max(0.1f, activationRadius);
        deactivationRadius = Mathf.Max(activationRadius, deactivationRadius);
        updateDistance = Mathf.Max(0.01f, updateDistance);

        float activationRadiusSqr = activationRadius * activationRadius;
        float deactivationRadiusSqr = deactivationRadius * deactivationRadius;

        seenActiveIDs.Clear();
        scannedInstanceCount = 0;
        scannedChunkCount = 0;

        Vector3 range = new Vector3(deactivationRadius, 0f, deactivationRadius);
        Vector2Int minChunk = database.WorldToChunkCoordinate(targetPosition - range);
        Vector2Int maxChunk = database.WorldToChunkCoordinate(targetPosition + range);

        for (int x = minChunk.x; x <= maxChunk.x; x++)
        {
            for (int z = minChunk.y; z <= maxChunk.y; z++)
            {
                VegetationChunkData chunk = database.GetChunk(new Vector2Int(x, z));
                if (chunk == null || chunk.instances == null || chunk.instances.Count == 0) continue;

                scannedChunkCount++;

                for (int i = 0; i < chunk.instances.Count; i++)
                {
                    VegetationInstance instance = chunk.instances[i];
                    scannedInstanceCount++;

                    if (!TryGetCollisionSpecies(instance.speciesIndex, out VegetationSpecies species)) continue;

                    float distanceSqr = HorizontalDistanceSqr(instance.position, targetPosition);

                    if (activeProxies.TryGetValue(instance.persistentID, out ColliderProxy activeProxy))
                    {
                        if (distanceSqr <= deactivationRadiusSqr)
                        {
                            ConfigureProxy(activeProxy, instance, species);
                            seenActiveIDs.Add(instance.persistentID);
                        }

                        continue;
                    }

                    if (distanceSqr > activationRadiusSqr) continue;

                    ColliderProxy proxy = AcquireProxy();
                    ConfigureProxy(proxy, instance, species);
                    activeProxies.Add(instance.persistentID, proxy);
                    seenActiveIDs.Add(instance.persistentID);
                }
            }
        }

        releaseBuffer.Clear();

        foreach (KeyValuePair<int, ColliderProxy> pair in activeProxies)
        {
            if (!seenActiveIDs.Contains(pair.Key)) releaseBuffer.Add(pair.Key);
        }

        for (int i = 0; i < releaseBuffer.Count; i++) ReleaseProxy(releaseBuffer[i]);

        activeColliderCount = activeProxies.Count;
        pooledColliderCount = proxyPool.Count;
        lastRefreshPosition = targetPosition;
        lastDatabaseRevision = database.DataRevision;
        hasRefreshPosition = true;
    }

    private bool TryGetCollisionSpecies(int speciesIndex, out VegetationSpecies species)
    {
        species = null;

        if (database == null || database.species == null || speciesIndex < 0 || speciesIndex >= database.species.Count) return false;

        species = database.species[speciesIndex];
        return species != null && species.enableCollision;
    }

    private ColliderProxy AcquireProxy()
    {
        ColliderProxy proxy;

        if (proxyPool.Count > 0)
        {
            proxy = proxyPool.Pop();
        }
        else
        {
            proxy = CreateProxy();
        }

        if (proxy.gameObject != null)
        {
            proxy.gameObject.hideFlags = hideProxyObjectsInHierarchy ? HideFlags.HideInHierarchy : HideFlags.None;
            proxy.gameObject.SetActive(true);
        }

        return proxy;
    }

    private ColliderProxy CreateProxy()
    {
        EnsureProxyRoot();

        GameObject go = new GameObject("Vegetation Collider Proxy");
        go.transform.SetParent(proxyRoot, false);
        go.layer = ResolveColliderLayer();

        CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
        BoxCollider box = go.AddComponent<BoxCollider>();
        SphereCollider sphere = go.AddComponent<SphereCollider>();

        capsule.enabled = false;
        box.enabled = false;
        sphere.enabled = false;

        return new ColliderProxy
        {
            gameObject = go,
            transform = go.transform,
            capsule = capsule,
            box = box,
            sphere = sphere,
            persistentID = -1,
            speciesIndex = -1
        };
    }

    private void ConfigureProxy(ColliderProxy proxy, VegetationInstance instance, VegetationSpecies species)
    {
        if (proxy == null || proxy.gameObject == null || species == null) return;

        proxy.persistentID = instance.persistentID;
        proxy.speciesIndex = instance.speciesIndex;
        proxy.gameObject.name = "Vegetation Collider " + instance.persistentID + " - " + species.speciesName;
        proxy.gameObject.layer = ResolveColliderLayer();
        proxy.gameObject.hideFlags = hideProxyObjectsInHierarchy ? HideFlags.HideInHierarchy : HideFlags.None;

        proxy.transform.SetPositionAndRotation(instance.position, instance.rotation);
        proxy.transform.localScale = instance.scale;

        proxy.capsule.enabled = false;
        proxy.box.enabled = false;
        proxy.sphere.enabled = false;

        switch (species.colliderType)
        {
            case VegetationColliderType.Box:
                proxy.box.center = species.colliderCenter;
                proxy.box.size = new Vector3(
                    Mathf.Max(0.001f, species.boxColliderSize.x),
                    Mathf.Max(0.001f, species.boxColliderSize.y),
                    Mathf.Max(0.001f, species.boxColliderSize.z)
                );
                proxy.box.isTrigger = species.colliderIsTrigger;
                proxy.box.sharedMaterial = species.colliderMaterial;
                proxy.box.enabled = true;
                break;

            case VegetationColliderType.Sphere:
                proxy.sphere.center = species.colliderCenter;
                proxy.sphere.radius = Mathf.Max(0.001f, species.sphereRadius);
                proxy.sphere.isTrigger = species.colliderIsTrigger;
                proxy.sphere.sharedMaterial = species.colliderMaterial;
                proxy.sphere.enabled = true;
                break;

            default:
                float radius = Mathf.Max(0.001f, species.capsuleRadius);
                proxy.capsule.center = species.colliderCenter;
                proxy.capsule.direction = 1;
                proxy.capsule.radius = radius;
                proxy.capsule.height = Mathf.Max(radius * 2f, species.capsuleHeight);
                proxy.capsule.isTrigger = species.colliderIsTrigger;
                proxy.capsule.sharedMaterial = species.colliderMaterial;
                proxy.capsule.enabled = true;
                break;
        }
    }

    private void ReleaseProxy(int persistentID)
    {
        if (!activeProxies.TryGetValue(persistentID, out ColliderProxy proxy)) return;

        activeProxies.Remove(persistentID);

        if (proxy == null || proxy.gameObject == null) return;

        proxy.capsule.enabled = false;
        proxy.box.enabled = false;
        proxy.sphere.enabled = false;
        proxy.persistentID = -1;
        proxy.speciesIndex = -1;
        proxy.gameObject.SetActive(false);
        proxy.gameObject.name = "Vegetation Collider Proxy (Pooled)";
        proxyPool.Push(proxy);
    }

    private void ReleaseAllActive()
    {
        if (activeProxies.Count == 0)
        {
            activeColliderCount = 0;
            pooledColliderCount = proxyPool.Count;
            return;
        }

        releaseBuffer.Clear();

        foreach (int persistentID in activeProxies.Keys) releaseBuffer.Add(persistentID);
        for (int i = 0; i < releaseBuffer.Count; i++) ReleaseProxy(releaseBuffer[i]);

        activeColliderCount = 0;
        pooledColliderCount = proxyPool.Count;
        seenActiveIDs.Clear();
        hasRefreshPosition = false;
    }

    private void EnsureProxyRoot()
    {
        if (proxyRoot != null) return;

        GameObject root = new GameObject("Vegetation Collider Proxies - " + name);
        SceneManager.MoveGameObjectToScene(root, gameObject.scene);
        proxyRoot = root.transform;
        proxyRoot.position = Vector3.zero;
        proxyRoot.rotation = Quaternion.identity;
        proxyRoot.localScale = Vector3.one;
    }

    private int ResolveColliderLayer()
    {
        int layer = LayerMask.NameToLayer(colliderLayerName);

        if (layer >= 0) return layer;

        if (!warnedInvalidLayer)
        {
            Debug.LogWarning("VegetationColliderManager找不到Layer '" + colliderLayerName + "'，已回退到Default。", this);
            warnedInvalidLayer = true;
        }

        return 0;
    }

    private static float HorizontalDistanceSqr(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    private void OnValidate()
    {
        activationRadius = Mathf.Max(0.1f, activationRadius);
        deactivationRadius = Mathf.Max(activationRadius, deactivationRadius);
        updateDistance = Mathf.Max(0.01f, updateDistance);
        warnedInvalidLayer = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos || target == null) return;

        Gizmos.DrawWireSphere(target.position, activationRadius);
        Gizmos.DrawWireSphere(target.position, deactivationRadius);
    }
}
