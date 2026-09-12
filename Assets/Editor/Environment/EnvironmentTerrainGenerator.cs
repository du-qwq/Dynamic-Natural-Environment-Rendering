using UnityEditor;
using UnityEngine;

public class EnvironmentTerrainGenerator : EditorWindow
{
    private Terrain targetTerrain;
    private GameObject lakePlaceholder;
    private Vector2 scroll;
    private int presetVersion;

    private int seed;
    private float baseHeight;
    private float waterHeight;
    private float lakeFloorHeight;

    private float lakeStartZ;
    private float lakeEndZ;
    private float lakeFrontHalfWidth;
    private float lakeBackHalfWidth;
    private float lakeCurveStrength;
    private float lakeWidthNoise;
    private float lakeBasinWidth;

    private float valleyHalfWidth;
    private float leftMountainHeight;
    private float rightMountainHeight;
    private float backgroundMountainHeight;
    private float ridgeDetailStrength;

    private float largeNoiseScale;
    private float largeNoiseStrength;
    private float detailNoiseScale;
    private float detailNoiseStrength;

    private const string LakeMeshPath = "Assets/Generated/Environment/Meshes/Lake_Blockout.asset";

    [MenuItem("Tools/Environment/高山湖谷地形生成器")]
    public static void OpenWindow()
    {
        EnvironmentTerrainGenerator window = GetWindow<EnvironmentTerrainGenerator>("高山湖谷地形");
        window.minSize = new Vector2(410f, 760f);
    }

    private void OnEnable()
    {
        if (targetTerrain == null) targetTerrain = Terrain.activeTerrain;
        if (lakePlaceholder == null) lakePlaceholder = GameObject.Find("Lake_Placeholder");
        if (presetVersion != 201) LoadStage02A1Parameters();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Stage 02A1 - 山谷湖地形", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("目标：前宽后窄的山谷湖、左侧缓坡、右侧主山、中央远景谷口。不会修改 Main Camera。", MessageType.Info);

        EditorGUILayout.Space(8);
        targetTerrain = (Terrain)EditorGUILayout.ObjectField("目标 Terrain", targetTerrain, typeof(Terrain), true);
        lakePlaceholder = (GameObject)EditorGUILayout.ObjectField("Lake Placeholder", lakePlaceholder, typeof(GameObject), true);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("基础", EditorStyles.boldLabel);
        seed = EditorGUILayout.IntField("随机种子", seed);
        baseHeight = EditorGUILayout.Slider("基础高度", baseHeight, 25f, 80f);
        waterHeight = EditorGUILayout.Slider("水面高度", waterHeight, 20f, 70f);
        lakeFloorHeight = EditorGUILayout.Slider("湖底高度", lakeFloorHeight, 5f, 45f);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("湖泊轮廓", EditorStyles.boldLabel);
        lakeStartZ = EditorGUILayout.Slider("湖泊前端 Z", lakeStartZ, 0.15f, 0.45f);
        lakeEndZ = EditorGUILayout.Slider("湖泊后端 Z", lakeEndZ, 0.55f, 0.85f);
        lakeFrontHalfWidth = EditorGUILayout.Slider("前端半宽", lakeFrontHalfWidth, 0.12f, 0.32f);
        lakeBackHalfWidth = EditorGUILayout.Slider("后端半宽", lakeBackHalfWidth, 0.04f, 0.18f);
        lakeCurveStrength = EditorGUILayout.Slider("湖泊弯曲", lakeCurveStrength, 0f, 0.08f);
        lakeWidthNoise = EditorGUILayout.Slider("湖岸变化", lakeWidthNoise, 0f, 0.15f);
        lakeBasinWidth = EditorGUILayout.Slider("湖盆过渡宽度", lakeBasinWidth, 0.15f, 0.8f);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("山谷与山体", EditorStyles.boldLabel);
        valleyHalfWidth = EditorGUILayout.Slider("山谷半宽", valleyHalfWidth, 0.12f, 0.3f);
        leftMountainHeight = EditorGUILayout.Slider("左侧山体高度", leftMountainHeight, 30f, 130f);
        rightMountainHeight = EditorGUILayout.Slider("右侧主山高度", rightMountainHeight, 60f, 200f);
        backgroundMountainHeight = EditorGUILayout.Slider("远景山高度", backgroundMountainHeight, 40f, 160f);
        ridgeDetailStrength = EditorGUILayout.Slider("山脊细节强度", ridgeDetailStrength, 0f, 30f);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("地形噪声", EditorStyles.boldLabel);
        largeNoiseScale = EditorGUILayout.Slider("大型噪声尺度", largeNoiseScale, 0.5f, 4f);
        largeNoiseStrength = EditorGUILayout.Slider("大型噪声强度", largeNoiseStrength, 0f, 15f);
        detailNoiseScale = EditorGUILayout.Slider("细节噪声尺度", detailNoiseScale, 3f, 18f);
        detailNoiseStrength = EditorGUILayout.Slider("细节噪声强度", detailNoiseStrength, 0f, 5f);

        EditorGUILayout.Space(12);

        GUI.backgroundColor = new Color(0.72f, 0.88f, 1f);
        if (GUILayout.Button("加载 Stage 02A1 推荐参数", GUILayout.Height(32))) LoadStage02A1Parameters();

        GUI.backgroundColor = Color.white;
        if (GUILayout.Button("应用 Terrain 尺寸 1000 × 300 × 1000", GUILayout.Height(30))) ApplyRecommendedSize();

        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
        if (GUILayout.Button("生成 Terrain + 湖面轮廓", GUILayout.Height(42))) GenerateAll();

        GUI.backgroundColor = Color.white;
        if (GUILayout.Button("只更新湖面 Mesh", GUILayout.Height(28))) GenerateLakeMesh();

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox("第一次请只用推荐参数 + Seed 128。生成后不要先摆资产，只看 Terrain、湖泊形状和现有主摄像机构图。", MessageType.Warning);

        EditorGUILayout.EndScrollView();
    }

    private void LoadStage02A1Parameters()
    {
        presetVersion = 201;
        seed = 128;

        baseHeight = 48f;
        waterHeight = 40f;
        lakeFloorHeight = 27f;

        lakeStartZ = 0.28f;
        lakeEndZ = 0.73f;
        lakeFrontHalfWidth = 0.245f;
        lakeBackHalfWidth = 0.085f;
        lakeCurveStrength = 0.032f;
        lakeWidthNoise = 0.055f;
        lakeBasinWidth = 0.5f;

        valleyHalfWidth = 0.19f;
        leftMountainHeight = 72f;
        rightMountainHeight = 145f;
        backgroundMountainHeight = 82f;
        ridgeDetailStrength = 15f;

        largeNoiseScale = 1.35f;
        largeNoiseStrength = 5f;
        detailNoiseScale = 6.5f;
        detailNoiseStrength = 1.2f;

        Repaint();
    }

    private void ApplyRecommendedSize()
    {
        if (!ValidateTerrain()) return;
        TerrainData data = targetTerrain.terrainData;
        Undo.RegisterCompleteObjectUndo(data, "修改 Terrain 尺寸");
        data.size = new Vector3(1000f, 300f, 1000f);
        EditorUtility.SetDirty(data);
    }

    private void GenerateAll()
    {
        GenerateTerrain();
        GenerateLakeMesh();
    }

    private void GenerateTerrain()
    {
        if (!ValidateTerrain()) return;

        TerrainData data = targetTerrain.terrainData;
        Undo.RegisterCompleteObjectUndo(data, "生成 Stage 02A1 Terrain");

        int resolution = data.heightmapResolution;
        float terrainHeight = data.size.y;
        float[,] heights = new float[resolution, resolution];

        float offsetX = seed * 0.173f + 41.37f;
        float offsetZ = seed * 0.317f + 87.13f;

        for (int z = 0; z < resolution; z++)
        {
            float nz = z / (resolution - 1f);

            for (int x = 0; x < resolution; x++)
            {
                float nx = x / (resolution - 1f);

                float warpX = Fbm(nx, nz, 1.1f, offsetX + 19f, offsetZ + 53f, 3) * 0.018f;
                float warpZ = Fbm(nx, nz, 1.15f, offsetX + 71f, offsetZ + 17f, 3) * 0.015f;
                float wx = nx + warpX;
                float wz = nz + warpZ;

                float valleyCenter = GetValleyCenter(wz, offsetX, offsetZ);
                float leftDistance = Mathf.Max(0f, valleyCenter - wx);
                float rightDistance = Mathf.Max(0f, wx - valleyCenter);

                float leftMask = Smooth01(Mathf.InverseLerp(valleyHalfWidth, 0.48f, leftDistance));
                float rightMask = Smooth01(Mathf.InverseLerp(valleyHalfWidth * 0.82f, 0.43f, rightDistance));

                float longitudinal = Smooth01(Mathf.InverseLerp(0.12f, 0.72f, wz));
                float leftScale = Mathf.Lerp(0.42f, 1f, longitudinal);
                float rightScale = Mathf.Lerp(0.48f, 1f, longitudinal);

                float height = baseHeight;

                float foregroundRoll = Fbm(wx, wz, 1.3f, offsetX + 113f, offsetZ + 137f, 3);
                float foregroundMask = 1f - Smooth01(Mathf.InverseLerp(0.05f, 0.32f, wz));
                height += foregroundRoll * 4f * foregroundMask;

                height += leftMask * leftMountainHeight * leftScale;
                height += rightMask * rightMountainHeight * rightScale;

                float rightPeakA = Mountain(wx, wz, 0.82f, 0.61f, 0.16f, 0.22f);
                float rightPeakB = Mountain(wx, wz, 0.88f, 0.80f, 0.13f, 0.16f);
                float leftPeak = Mountain(wx, wz, 0.13f, 0.63f, 0.18f, 0.23f);

                height += rightPeakA * 46f;
                height += rightPeakB * 28f;
                height += leftPeak * 18f;

                float backgroundMask = Smooth01(Mathf.InverseLerp(0.72f, 1f, wz));
                float opening = Mathf.Exp(-Mathf.Pow((wx - 0.52f) / 0.15f, 2f));
                float backgroundOpeningScale = Mathf.Lerp(1f, 0.48f, opening);

                float backgroundRidge = RidgedFbm(wx, wz, 2.2f, offsetX + 211f, offsetZ + 157f, 4);
                height += backgroundMask * backgroundMountainHeight * Mathf.Lerp(0.55f, 1f, backgroundRidge) * backgroundOpeningScale;

                float distantPeakA = Mountain(wx, wz, 0.38f, 0.965f, 0.13f, 0.09f);
                float distantPeakB = Mountain(wx, wz, 0.57f, 0.985f, 0.12f, 0.085f);
                float distantPeakC = Mountain(wx, wz, 0.72f, 0.96f, 0.13f, 0.10f);

                height += distantPeakA * 34f;
                height += distantPeakB * 48f;
                height += distantPeakC * 38f;

                float mountainMask = Mathf.Clamp01(leftMask + rightMask + backgroundMask * 0.7f + rightPeakA + rightPeakB + leftPeak);
                float ridgeNoise = RidgedFbm(wx, wz, 3f, offsetX + 307f, offsetZ + 263f, 4);
                height += (ridgeNoise - 0.46f) * ridgeDetailStrength * mountainMask;

                float largeNoise = Fbm(wx, wz, largeNoiseScale, offsetX + 401f, offsetZ + 349f, 3);
                float detailNoise = Fbm(wx, wz, detailNoiseScale, offsetX + 503f, offsetZ + 431f, 2);

                height += largeNoise * largeNoiseStrength * Mathf.Lerp(0.25f, 1f, mountainMask);
                height += detailNoise * detailNoiseStrength * mountainMask;

                float lakeDistance = GetLakeDistance(nx, nz, offsetX, offsetZ);

                if (lakeDistance < 1f + lakeBasinWidth)
                {
                    float targetHeight;

                    if (lakeDistance < 1f)
                    {
                        float shoreT = Smooth01(lakeDistance);
                        targetHeight = Mathf.Lerp(lakeFloorHeight, waterHeight - 0.5f, shoreT);
                    }
                    else
                    {
                        float outerT = Smooth01(Mathf.InverseLerp(1f, 1f + lakeBasinWidth, lakeDistance));
                        targetHeight = Mathf.Lerp(waterHeight + 0.5f, waterHeight + 11f, outerT);
                    }

                    float basinInfluence = 1f - Smooth01(Mathf.InverseLerp(1f, 1f + lakeBasinWidth, lakeDistance));
                    float lowered = Mathf.Min(height, targetHeight);
                    height = Mathf.Lerp(height, lowered, basinInfluence);
                }

                float edgeFade = Mathf.Min(EdgeFade(nx, 0.018f), EdgeFade(nz, 0.018f));
                height = Mathf.Lerp(baseHeight, height, edgeFade);

                height = Mathf.Clamp(height, 0f, terrainHeight * 0.985f);
                heights[z, x] = height / terrainHeight;
            }
        }

        data.SetHeights(0, 0, heights);
        EditorUtility.SetDirty(data);
        targetTerrain.Flush();
        SceneView.RepaintAll();
    }

    private void GenerateLakeMesh()
    {
        if (!ValidateTerrain()) return;

        if (lakePlaceholder == null)
        {
            GameObject waterParent = GameObject.Find("Water");
            lakePlaceholder = new GameObject("Lake_Placeholder");
            if (waterParent != null) lakePlaceholder.transform.SetParent(waterParent.transform);
            Undo.RegisterCreatedObjectUndo(lakePlaceholder, "创建 Lake Placeholder");
        }

        MeshFilter filter = lakePlaceholder.GetComponent<MeshFilter>();
        if (filter == null) filter = lakePlaceholder.AddComponent<MeshFilter>();
        if (lakePlaceholder.GetComponent<MeshRenderer>() == null) lakePlaceholder.AddComponent<MeshRenderer>();

        EnsureFolder("Assets/Generated");
        EnsureFolder("Assets/Generated/Environment");
        EnsureFolder("Assets/Generated/Environment/Meshes");

        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(LakeMeshPath);

        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "Lake_Blockout";
            AssetDatabase.CreateAsset(mesh, LakeMeshPath);
        }
        else
        {
            Undo.RecordObject(mesh, "更新 Lake Mesh");
            mesh.Clear();
        }

        const int segments = 64;
        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 6];

        TerrainData data = targetTerrain.terrainData;
        float offsetX = seed * 0.173f + 41.37f;
        float offsetZ = seed * 0.317f + 87.13f;

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float z = Mathf.Lerp(lakeStartZ, lakeEndZ, t);
            float center = GetLakeCenter(t, offsetX, offsetZ);
            float halfWidth = GetLakeHalfWidth(t, offsetX, offsetZ);

            vertices[i * 2] = new Vector3((center - halfWidth) * data.size.x, 0f, z * data.size.z);
            vertices[i * 2 + 1] = new Vector3((center + halfWidth) * data.size.x, 0f, z * data.size.z);

            uvs[i * 2] = new Vector2(0f, t);
            uvs[i * 2 + 1] = new Vector2(1f, t);

            if (i == segments) continue;

            int a = i * 2;
            int b = a + 1;
            int c = a + 2;
            int d = a + 3;
            int tri = i * 6;

            triangles[tri] = a;
            triangles[tri + 1] = c;
            triangles[tri + 2] = b;
            triangles[tri + 3] = b;
            triangles[tri + 4] = c;
            triangles[tri + 5] = d;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        filter.sharedMesh = mesh;

        Vector3 terrainPos = targetTerrain.transform.position;
        lakePlaceholder.transform.position = new Vector3(terrainPos.x, terrainPos.y + waterHeight, terrainPos.z);
        lakePlaceholder.transform.rotation = Quaternion.identity;
        lakePlaceholder.transform.localScale = Vector3.one;

        EditorUtility.SetDirty(mesh);
        EditorUtility.SetDirty(filter);
        EditorUtility.SetDirty(lakePlaceholder);
        AssetDatabase.SaveAssets();
    }

    private float GetLakeDistance(float x, float z, float offsetX, float offsetZ)
    {
        float frontCapDepth = 0.075f;
        float backCapDepth = 0.05f;

        if (z < lakeStartZ)
        {
            float t = 0f;
            float center = GetLakeCenter(t, offsetX, offsetZ);
            float halfWidth = GetLakeHalfWidth(t, offsetX, offsetZ);
            float dx = (x - center) / Mathf.Max(0.001f, halfWidth);
            float dz = (z - lakeStartZ) / frontCapDepth;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        if (z > lakeEndZ)
        {
            float t = 1f;
            float center = GetLakeCenter(t, offsetX, offsetZ);
            float halfWidth = GetLakeHalfWidth(t, offsetX, offsetZ);
            float dx = (x - center) / Mathf.Max(0.001f, halfWidth);
            float dz = (z - lakeEndZ) / backCapDepth;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        float lakeT = Mathf.InverseLerp(lakeStartZ, lakeEndZ, z);
        float lakeCenter = GetLakeCenter(lakeT, offsetX, offsetZ);
        float lakeWidth = GetLakeHalfWidth(lakeT, offsetX, offsetZ);

        return Mathf.Abs(x - lakeCenter) / Mathf.Max(0.001f, lakeWidth);
    }

    private float GetLakeCenter(float t, float offsetX, float offsetZ)
    {
        float curve = Mathf.Sin((t * 1.15f + 0.08f) * Mathf.PI) * lakeCurveStrength;
        float noise = (Mathf.PerlinNoise(t * 1.8f + offsetX + 17f, offsetZ + 23f) - 0.5f) * 0.018f;
        return 0.5f + curve + noise;
    }

    private float GetLakeHalfWidth(float t, float offsetX, float offsetZ)
    {
        float shapedT = Smooth01(t);
        float width = Mathf.Lerp(lakeFrontHalfWidth, lakeBackHalfWidth, shapedT);
        float noise = Mathf.PerlinNoise(t * 3.2f + offsetX + 61f, offsetZ + 97f) * 2f - 1f;
        return Mathf.Max(0.025f, width * (1f + noise * lakeWidthNoise));
    }

    private float GetValleyCenter(float z, float offsetX, float offsetZ)
    {
        float curve = Mathf.Sin(z * 3.3f + 0.35f) * 0.022f;
        float noise = Fbm(0.31f, z, 1.25f, offsetX + 131f, offsetZ + 181f, 2) * 0.018f;
        return 0.5f + curve + noise;
    }

    private bool ValidateTerrain()
    {
        if (targetTerrain != null && targetTerrain.terrainData != null) return true;
        EditorUtility.DisplayDialog("缺少 Terrain", "请先指定目标 Terrain。", "确定");
        return false;
    }

    private static float Mountain(float x, float z, float centerX, float centerZ, float radiusX, float radiusZ)
    {
        float dx = (x - centerX) / Mathf.Max(0.001f, radiusX);
        float dz = (z - centerZ) / Mathf.Max(0.001f, radiusZ);
        return Mathf.Exp(-(dx * dx + dz * dz) * 1.7f);
    }

    private static float Fbm(float x, float z, float scale, float offsetX, float offsetZ, int octaves)
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float totalAmplitude = 0f;

        for (int i = 0; i < octaves; i++)
        {
            float n = Mathf.PerlinNoise(x * scale * frequency + offsetX, z * scale * frequency + offsetZ) * 2f - 1f;
            value += n * amplitude;
            totalAmplitude += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return totalAmplitude > 0f ? value / totalAmplitude : 0f;
    }

    private static float RidgedFbm(float x, float z, float scale, float offsetX, float offsetZ, int octaves)
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float totalAmplitude = 0f;

        for (int i = 0; i < octaves; i++)
        {
            float n = Mathf.PerlinNoise(x * scale * frequency + offsetX, z * scale * frequency + offsetZ);
            n = 1f - Mathf.Abs(n * 2f - 1f);
            n *= n;

            value += n * amplitude;
            totalAmplitude += amplitude;

            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return totalAmplitude > 0f ? value / totalAmplitude : 0f;
    }

    private static float EdgeFade(float value, float width)
    {
        float left = Smooth01(Mathf.InverseLerp(0f, width, value));
        float right = 1f - Smooth01(Mathf.InverseLerp(1f - width, 1f, value));
        return Mathf.Min(left, right);
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
        string name = System.IO.Path.GetFileName(path);

        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        if (!string.IsNullOrEmpty(parent)) AssetDatabase.CreateFolder(parent, name);
    }
}