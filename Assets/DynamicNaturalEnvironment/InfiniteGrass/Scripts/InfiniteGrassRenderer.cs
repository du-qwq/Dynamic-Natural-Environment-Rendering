using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using static Unity.Burst.Intrinsics.X86.Avx;

public enum GrassDebugMode
{
    None = 0,
    LOD = 1,
    Mask = 2,
    Slope = 3,
    Wind = 4,
    Burn = 5,
    Height = 6,
    Color = 7
}

[ExecuteAlways]
public class InfiniteGrassRenderer : MonoBehaviour
{
    public Texture2D densityTexture;
    [HideInInspector] public static InfiniteGrassRenderer instance;

    [Header("Internal")]
    public Material grassMaterial;
    public ComputeBuffer nearArgsBuffer;//LOD新增：近处草间接绘制参数Buffer
    public ComputeBuffer farArgsBuffer;//LOD新增：远处草间接绘制参数Buffer
    public ComputeBuffer tBuffer;
    public ComputeBuffer farTBuffer;//LOD新增：远处草Debug计数Buffer

    [Header("Grass Properties")]
    public float spacing = 0.5f;//每根草的间距决定每个草的疏密程度
    public float drawDistance = 300;//草的最大绘制距离
    public float fullDensityDistance = 50;//满密度距离，ComputeShader里会用它做远处密度衰减
    public int grassMeshSubdivision = 5;//草叶分段数，保留旧参数，LOD开启后由下面两个细分参数控制
    public float textureUpdateThreshold = 10.0f;

    [Header("Grass LOD")]
    public float lodDistance = 80f;//LOD新增：小于这个距离使用近处高细分草，大于这个距离使用远处低细分草
    public int nearGrassMeshSubdivision = 5;//LOD新增：近处草叶分段数
    public int farGrassMeshSubdivision = 1;//LOD新增：远处草叶分段数

    [Header("Max Buffer Count (Millions)")]
    public float maxBufferCount = 2;//最大草数量

    [Header("Debug (Enabling this will make the performance drop a lot)")]
    public bool previewVisibleGrassCount = false;//是否显示当前可见草数量

    [Header("Debug Visualization")]
    public GrassDebugMode debugMode = GrassDebugMode.None;//调试显示模式，None为正常草地效果

    private readonly uint[] nearDebugCountData = new uint[1];//编辑器调试窗口读取近处草数量时复用，避免反复创建数组
    private readonly uint[] farDebugCountData = new uint[1];//编辑器调试窗口读取远处草数量时复用，避免反复创建数组

    private Mesh cachedGrassMesh;
    private Mesh cachedNearGrassMesh;//LOD新增：缓存近处草Mesh
    private Mesh cachedFarGrassMesh;//LOD新增：缓存远处草Mesh

    private Material nearGrassMaterial;//LOD新增：近处草材质实例
    private Material farGrassMaterial;//LOD新增：远处草材质实例
    private Material cachedSourceGrassMaterial;//LOD新增：记录材质源，材质改变时重建实例

    //记录当前argsBuffer对应的Mesh index数量，只有草Mesh变化时才重建argsBuffer
    private int cachedIndexCount = -1;

    //记录当前argsBuffer对应的最大草数量，只有maxBufferCount变化时才重建argsBuffer
    private float cachedMaxBufferCount = -1;

    private int cachedNearIndexCount = -1;//LOD新增：记录近处草Mesh index数量
    private int cachedFarIndexCount = -1;//LOD新增：记录远处草Mesh index数量
    private int oldNearSubdivision = -1;//LOD新增：记录近处草上一次细分数
    private int oldFarSubdivision = -1;//LOD新增：记录远处草上一次细分数

    private void OnEnable()
    {
        instance = this;
    }

    private void OnDisable()
    {
        instance = null;

        nearArgsBuffer?.Release();
        farArgsBuffer?.Release();
        tBuffer?.Release();
        farTBuffer?.Release();

        //Release 后置空，避免后面误用已经释放的Buffer
        nearArgsBuffer = null;
        farArgsBuffer = null;
        tBuffer = null;
        farTBuffer = null;

        ReleaseLODMaterials();//LOD新增：释放运行时创建的两个材质实例

        //重置缓存标记，下次启用时重新创建Buffer
        cachedIndexCount = -1;
        cachedMaxBufferCount = -1;
        cachedNearIndexCount = -1;
        cachedFarIndexCount = -1;
    }

    void LateUpdate()
    {
        if (spacing <= 0 || grassMaterial == null || Camera.main == null) return;

        Bounds cameraBounds = CalculateCameraBounds(Camera.main);
        Vector2 centerPos = new Vector2(Mathf.Floor(Camera.main.transform.position.x / textureUpdateThreshold) * textureUpdateThreshold, Mathf.Floor(Camera.main.transform.position.z / textureUpdateThreshold) * textureUpdateThreshold);

        EnsureLODMaterials();//LOD新增：确保近处和远处材质实例存在

        nearGrassMaterial.CopyPropertiesFromMaterial(grassMaterial);//同步Grass Width、Grass Height、风和颜色等材质参数到近处LOD材质
        farGrassMaterial.CopyPropertiesFromMaterial(grassMaterial);//同步Grass Width、Grass Height、风和颜色等材质参数到远处LOD材质

        //不要每帧Release/New，只在需要时创建或重建argsBuffer
        EnsureArgsBuffer();

        //tBuffer只用于Debug计数，也不需要每帧重建
        EnsureTBuffer();

        if (nearArgsBuffer == null || farArgsBuffer == null || nearGrassMaterial == null || farGrassMaterial == null) return;

        SetupGrassMaterial(nearGrassMaterial, centerPos, 0);//LOD新增：近处材质读取Near位置Buffer
        SetupGrassMaterial(farGrassMaterial, centerPos, 1);//LOD新增：远处材质读取Far位置Buffer

        Graphics.DrawMeshInstancedIndirect(GetGrassMeshCache(nearGrassMeshSubdivision, ref cachedNearGrassMesh, ref oldNearSubdivision), 0, nearGrassMaterial, cameraBounds, nearArgsBuffer);
        Graphics.DrawMeshInstancedIndirect(GetGrassMeshCache(farGrassMeshSubdivision, ref cachedFarGrassMesh, ref oldFarSubdivision), 0, farGrassMaterial, cameraBounds, farArgsBuffer);
    }

    //LOD新增：设置近处和远处材质共用的参数
    private void SetupGrassMaterial(Material targetMaterial, Vector2 centerPos, int lodLevel)
    {
        targetMaterial.SetTexture("_DensityTexture", densityTexture);
        targetMaterial.SetTextureScale("_DensityTexture", new Vector2(1, 1));
        targetMaterial.SetVector("_CenterPos", centerPos);
        targetMaterial.SetFloat("_DrawDistance", drawDistance);//传数据RT的额外缓冲距离
        targetMaterial.SetFloat("_TextureUpdateThreshold", textureUpdateThreshold);
        targetMaterial.SetInt("_GrassLODLevel", lodLevel);//LOD新增：0读取Near Buffer，1读取Far Buffer
        targetMaterial.SetInt("_GrassDebugMode", (int)debugMode);//调试新增：把调试显示模式传给近处和远处草材质
    }

    //LOD新增：为两次Draw分别创建材质实例，避免同一个材质的LOD参数互相覆盖
    private void EnsureLODMaterials()
    {
        if (nearGrassMaterial != null && farGrassMaterial != null && cachedSourceGrassMaterial == grassMaterial) return;

        ReleaseLODMaterials();

        nearGrassMaterial = new Material(grassMaterial);
        farGrassMaterial = new Material(grassMaterial);
        nearGrassMaterial.name = grassMaterial.name + "_NearLOD";
        farGrassMaterial.name = grassMaterial.name + "_FarLOD";
        cachedSourceGrassMaterial = grassMaterial;
    }

    //LOD新增：释放运行时材质实例
    private void ReleaseLODMaterials()
    {
        if (Application.isPlaying)
        {
            if (nearGrassMaterial != null) Destroy(nearGrassMaterial);
            if (farGrassMaterial != null) Destroy(farGrassMaterial);
        }
        else
        {
            if (nearGrassMaterial != null) DestroyImmediate(nearGrassMaterial);
            if (farGrassMaterial != null) DestroyImmediate(farGrassMaterial);
        }

        nearGrassMaterial = null;
        farGrassMaterial = null;
        cachedSourceGrassMaterial = null;
    }

    //确保argsBuffer存在，并且只在草Mesh或maxBufferCount变化时重建
    private void EnsureArgsBuffer()
    {
        Mesh nearGrassMesh = GetGrassMeshCache(nearGrassMeshSubdivision, ref cachedNearGrassMesh, ref oldNearSubdivision);
        Mesh farGrassMesh = GetGrassMeshCache(farGrassMeshSubdivision, ref cachedFarGrassMesh, ref oldFarSubdivision);

        int nearIndexCount = (int)nearGrassMesh.GetIndexCount(0);
        int farIndexCount = (int)farGrassMesh.GetIndexCount(0);

        if (nearArgsBuffer != null && farArgsBuffer != null && cachedNearIndexCount == nearIndexCount && cachedFarIndexCount == farIndexCount && Mathf.Approximately(cachedMaxBufferCount, maxBufferCount)) return;

        nearArgsBuffer?.Release();
        farArgsBuffer?.Release();

        nearArgsBuffer = CreateArgsBuffer(nearGrassMesh);//LOD新增：近处草绘制参数
        farArgsBuffer = CreateArgsBuffer(farGrassMesh);//LOD新增：远处草绘制参数

        cachedNearIndexCount = nearIndexCount;
        cachedFarIndexCount = farIndexCount;
        cachedMaxBufferCount = maxBufferCount;
    }

    //LOD新增：根据指定Mesh创建对应的间接绘制参数Buffer
    private ComputeBuffer CreateArgsBuffer(Mesh grassMesh)
    {
        ComputeBuffer buffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);//ComputeBufferType.IndirectArguments用于GPU间接调用，将调用参数存于GPU缓冲区，实现完全由GPU驱动的绘制/计算

        uint[] args = new uint[5];
        args[0] = (uint)grassMesh.GetIndexCount(0);//每个实例要绘制多少个index
        args[1] = 0;//实例数量由RendererFeature从AppendBuffer计数器复制进来
        args[2] = (uint)grassMesh.GetIndexStart(0);//index起始位置
        args[3] = (uint)grassMesh.GetBaseVertex(0);//顶点编号整体偏移量
        args[4] = 0;//从第几个实例开始画
        buffer.SetData(args);

        return buffer;
    }

    //确保tBuffer存在，它只是Debug显示草数量用，不需要每帧重建
    private void EnsureTBuffer()
    {
        if (tBuffer == null) tBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);//ComputeBufferType.Raw表示原始字节Buffer，这里只是拿来装一个计数值
        if (farTBuffer == null) farTBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);//LOD新增：装远处草计数值
    }

    //调试新增：供编辑器窗口读取Near/Far可见草数量，调用时会产生一次GPU到CPU同步
    public bool TryGetVisibleGrassCounts(out uint nearCount, out uint farCount)
    {
        nearCount = 0;
        farCount = 0;

        if (tBuffer == null || farTBuffer == null) return false;

        tBuffer.GetData(nearDebugCountData);
        farTBuffer.GetData(farDebugCountData);

        nearCount = nearDebugCountData[0];
        farCount = farDebugCountData[0];

        return true;
    }

    private void OnGUI()
    {
        if (previewVisibleGrassCount)
        {
            if (Camera.main == null || tBuffer == null || farTBuffer == null) return;//防止编辑器状态下空引用

            GUI.contentColor = Color.black;
            GUIStyle style = new GUIStyle();
            style.fontSize = 25;

            uint[] nearCount = new uint[1];
            uint[] farCount = new uint[1];
            tBuffer.GetData(nearCount);
            farTBuffer.GetData(farCount);

            Bounds cameraBounds = CalculateCameraBounds(Camera.main);
            Vector2Int gridSize = new Vector2Int(Mathf.CeilToInt(cameraBounds.size.x / spacing), Mathf.CeilToInt(cameraBounds.size.z / spacing));

            GUI.Label(new Rect(50, 50, 500, 200), "Dispatch Size : " + gridSize.x + "x" + gridSize.y + " = " + (gridSize.x * gridSize.y), style);
            GUI.Label(new Rect(50, 80, 500, 200), "Near Grass Count : " + nearCount[0], style);
            GUI.Label(new Rect(50, 110, 500, 200), "Far Grass Count : " + farCount[0], style);
            GUI.Label(new Rect(50, 140, 500, 200), "Visible Grass Count : " + (nearCount[0] + farCount[0]), style);
        }
    }

    int oldSubdivision = -1;
    public Mesh GetGrassMeshCache()
    {
        return GetGrassMeshCache(grassMeshSubdivision, ref cachedGrassMesh, ref oldSubdivision);
    }

    //LOD新增：同一个建模逻辑根据不同细分数分别生成Near和Far草Mesh
    private Mesh GetGrassMeshCache(int subdivision, ref Mesh cachedMesh, ref int oldSubdivisionValue)
    {
        subdivision = Mathf.Max(0, subdivision);

        if (!cachedMesh || oldSubdivisionValue != subdivision)
        {
            cachedMesh = new Mesh();

            Vector3[] vertices = new Vector3[3 + 4 * subdivision];//顶点数组
            int[] triangles = new int[(1 + 2 * subdivision) * 3];//三角形索引数组
            //每循环一次，生成草叶的一段矩形
            for (int i = 0; i < subdivision; i++)
            {
                //算这一段的底部高度和顶部高度
                float y1 = (float)i / (subdivision + 1);
                float y2 = (float)(i + 1) / (subdivision + 1);
                //创建一段矩形的四个点
                Vector3 bottomLeft = new Vector3(-0.25f, y1);
                Vector3 bottomRight = new Vector3(0.25f, y1);
                Vector3 topLeft = new Vector3(-0.25f, y2);
                Vector3 topRight = new Vector3(0.25f, y2);
                //算这四个顶点在数组里的位置
                int bottomLeftIndex = i * 4;
                int bottomRightIndex = i * 4 + 1;
                int topLeftIndex = i * 4 + 2;
                int topRightIndex = i * 4 + 3;
                //把四个点写进顶点数组
                vertices[bottomLeftIndex] = bottomLeft;
                vertices[bottomRightIndex] = bottomRight;
                vertices[topLeftIndex] = topLeft;
                vertices[topRightIndex] = topRight;
                //生成两个三角形
                triangles[i * 6] = bottomLeftIndex;
                triangles[i * 6 + 1] = topRightIndex;
                triangles[i * 6 + 2] = bottomRightIndex;

                triangles[i * 6 + 3] = bottomLeftIndex;
                triangles[i * 6 + 4] = topLeftIndex;
                triangles[i * 6 + 5] = topRightIndex;
            }
            //补最后尖端三角形
            vertices[subdivision * 4] = new Vector3(-0.25f, (float)subdivision / (subdivision + 1));
            vertices[subdivision * 4 + 1] = new Vector3(0, 1);
            vertices[subdivision * 4 + 2] = new Vector3(0.25f, (float)subdivision / (subdivision + 1));
            //给顶部三角形写 index
            triangles[subdivision * 6] = subdivision * 4;
            triangles[subdivision * 6 + 1] = subdivision * 4 + 1;
            triangles[subdivision * 6 + 2] = subdivision * 4 + 2;

            cachedMesh.SetVertices(vertices);
            cachedMesh.SetTriangles(triangles, 0);

            oldSubdivisionValue = subdivision;
        }

        return cachedMesh;
    }

    Bounds CalculateCameraBounds(Camera camera)
    {
        Vector3 ntopLeft = camera.ViewportToWorldPoint(new Vector3(0, 1, camera.nearClipPlane));
        Vector3 ntopRight = camera.ViewportToWorldPoint(new Vector3(1, 1, camera.nearClipPlane));
        Vector3 nbottomLeft = camera.ViewportToWorldPoint(new Vector3(0, 0, camera.nearClipPlane));
        Vector3 nbottomRight = camera.ViewportToWorldPoint(new Vector3(1, 0, camera.nearClipPlane));

        Vector3 ftopLeft = camera.ViewportToWorldPoint(new Vector3(0, 1, drawDistance));
        Vector3 ftopRight = camera.ViewportToWorldPoint(new Vector3(1, 1, drawDistance));
        Vector3 fbottomLeft = camera.ViewportToWorldPoint(new Vector3(0, 0, drawDistance));
        Vector3 fbottomRight = camera.ViewportToWorldPoint(new Vector3(1, 0, drawDistance));

        float[] xValues = new float[] { ftopLeft.x, ftopRight.x, ntopLeft.x, ntopRight.x, fbottomLeft.x, fbottomRight.x, nbottomLeft.x, nbottomRight.x };
        float startX = xValues.Max();
        float endX = xValues.Min();

        float[] yValues = new float[] { ftopLeft.y, ftopRight.y, ntopLeft.y, ntopRight.y, fbottomLeft.y, fbottomRight.y, nbottomLeft.y, nbottomRight.y };
        float startY = yValues.Max();
        float endY = yValues.Min();

        float[] zValues = new float[] { ftopLeft.z, ftopRight.z, ntopLeft.z, ntopRight.z, fbottomLeft.z, fbottomRight.z, nbottomLeft.z, nbottomRight.z };
        float startZ = zValues.Max();
        float endZ = zValues.Min();

        Vector3 center = new Vector3((startX + endX) / 2, (startY + endY) / 2, (startZ + endZ) / 2);
        Vector3 size = new Vector3(Mathf.Abs(startX - endX), Mathf.Abs(startY - endY), Mathf.Abs(startZ - endZ));

        Bounds bounds = new Bounds(center, size);
        bounds.Expand(1);
        return bounds;
    }

    private void OnValidate()
    {
        spacing = Mathf.Max(0.01f, spacing);
        drawDistance = Mathf.Max(1f, drawDistance);
        fullDensityDistance = Mathf.Max(0.01f, fullDensityDistance);
        textureUpdateThreshold = Mathf.Max(0.01f, textureUpdateThreshold);
        maxBufferCount = Mathf.Max(0.01f, maxBufferCount);
        lodDistance = Mathf.Clamp(lodDistance, 0f, drawDistance);
        nearGrassMeshSubdivision = Mathf.Max(0, nearGrassMeshSubdivision);
        farGrassMeshSubdivision = Mathf.Max(0, farGrassMeshSubdivision);
    }
}
