using System;
using UnityEditor;
using UnityEngine;

public class EnvironmentTerrainGenerator : EditorWindow
{
    private Terrain targetTerrain;
    private Camera targetCamera;
    private Vector2 scroll;

    private int seed;

    private float baseHeight;
    private float mountainHeight;
    private float foregroundRise;

    private Vector2 lakeCenter;
    private Vector2 lakeRadius;
    private float lakeFloorHeight;
    private float waterHeight;
    private float lakeIrregularity;
    private float lakeBasinWidth;

    private float valleyWidth;
    private float valleyDepth;

    private float largeNoiseScale;
    private float largeNoiseStrength;
    private float detailNoiseScale;
    private float detailNoiseStrength;
    private float ridgeStrength;

    private float cameraHeight;
    private float cameraFov;

    [MenuItem("Tools/Environment/高山湖谷地形生成器")]
    public static void OpenWindow()
    {
        EnvironmentTerrainGenerator window = GetWindow<EnvironmentTerrainGenerator>("高山湖谷地形");
        window.minSize = new Vector2(400f, 720f);
    }

    private void OnEnable()
    {
        if (targetTerrain == null) targetTerrain = Terrain.activeTerrain;
        if (targetCamera == null) targetCamera = Camera.main;
        if (mountainHeight <= 0f) LoadA5Parameters();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("高山湖谷基础地形", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Stage 01-A5：使用弯曲山谷中心线生成整体 U 型山谷，再叠加非对称侧山、远景山层和湖泊低地。", MessageType.Info);

        EditorGUILayout.Space(8);
        targetTerrain = (Terrain)EditorGUILayout.ObjectField("目标 Terrain", targetTerrain, typeof(Terrain), true);
        targetCamera = (Camera)EditorGUILayout.ObjectField("主摄像机", targetCamera, typeof(Camera), true);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("基础", EditorStyles.boldLabel);
        seed = EditorGUILayout.IntField("随机种子", seed);
        baseHeight = EditorGUILayout.Slider("基础高度", baseHeight, 20f, 90f);
        mountainHeight = EditorGUILayout.Slider("山体高度", mountainHeight, 80f, 220f);
        foregroundRise = EditorGUILayout.Slider("前景坡地抬升", foregroundRise, 0f, 50f);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("湖泊", EditorStyles.boldLabel);
        lakeCenter = EditorGUILayout.Vector2Field("湖泊中心", lakeCenter);
        lakeRadius = EditorGUILayout.Vector2Field("湖泊尺寸", lakeRadius);
        lakeFloorHeight = EditorGUILayout.Slider("湖底高度", lakeFloorHeight, 5f, 50f);
        waterHeight = EditorGUILayout.Slider("水面参考高度", waterHeight, 10f, 70f);
        lakeIrregularity = EditorGUILayout.Slider("湖岸不规则度", lakeIrregularity, 0f, 0.10f);
        lakeBasinWidth = EditorGUILayout.Slider("湖盆过渡宽度", lakeBasinWidth, 0.15f, 0.80f);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("山谷", EditorStyles.boldLabel);
        valleyWidth = EditorGUILayout.Slider("山谷宽度", valleyWidth, 0.12f, 0.35f);
        valleyDepth = EditorGUILayout.Slider("谷底压低", valleyDepth, 0f, 35f);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("山体细节", EditorStyles.boldLabel);
        largeNoiseScale = EditorGUILayout.Slider("大型噪声尺度", largeNoiseScale, 0.5f, 4f);
        largeNoiseStrength = EditorGUILayout.Slider("大型噪声强度", largeNoiseStrength, 0f, 20f);
        detailNoiseScale = EditorGUILayout.Slider("细节噪声尺度", detailNoiseScale, 3f, 20f);
        detailNoiseStrength = EditorGUILayout.Slider("细节噪声强度", detailNoiseStrength, 0f, 8f);
        ridgeStrength = EditorGUILayout.Slider("山脊细节强度", ridgeStrength, 0f, 30f);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("摄像机", EditorStyles.boldLabel);
        cameraHeight = EditorGUILayout.Slider("离地高度", cameraHeight, 5f, 40f);
        cameraFov = EditorGUILayout.Slider("FOV", cameraFov, 35f, 65f);

        EditorGUILayout.Space(12);

        GUI.backgroundColor = new Color(0.72f, 0.88f, 1f);
        if (GUILayout.Button("加载 A5 推荐参数", GUILayout.Height(32))) LoadA5Parameters();

        GUI.backgroundColor = Color.white;
        if (GUILayout.Button("应用 Terrain 尺寸 1000 × 300 × 1000", GUILayout.Height(30))) ApplyRecommendedSize();

        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
        if (GUILayout.Button("生成 A5 地形", GUILayout.Height(40))) GenerateTerrain();

        GUI.backgroundColor = Color.white;
        if (GUILayout.Button("重新定位主摄像机", GUILayout.Height(30))) PositionCamera();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("随机 Seed", GUILayout.Height(28))) RandomizeSeed();
        if (GUILayout.Button("恢复平地", GUILayout.Height(28))) ResetTerrain();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox("第一次只使用 A5 推荐参数和 Seed 128。先判断大型结构，不调整 Noise。", MessageType.Warning);

        EditorGUILayout.EndScrollView();
    }

    private void LoadA5Parameters()
    {
        seed = 128;

        baseHeight = 48f;
        mountainHeight = 170f;
        foregroundRise = 16f;

        lakeCenter = new Vector2(0.53f, 0.52f);
        lakeRadius = new Vector2(0.25f, 0.17f);
        lakeFloorHeight = 29f;
        waterHeight = 40f;
        lakeIrregularity = 0.035f;
        lakeBasinWidth = 0.48f;

        valleyWidth = 0.21f;
        valleyDepth = 11f;

        largeNoiseScale = 1.35f;
        largeNoiseStrength = 6f;
        detailNoiseScale = 6.5f;
        detailNoiseStrength = 1.5f;
        ridgeStrength = 16f;

        cameraHeight = 22f;
        cameraFov = 47f;

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

    private void GenerateTerrain()
    {
        if (!ValidateTerrain()) return;

        TerrainData data = targetTerrain.terrainData;
        Undo.RegisterCompleteObjectUndo(data, "生成 A5 高山湖谷");

        int resolution = data.heightmapResolution;
        float terrainHeight = data.size.y;
        float[,] heights = new float[resolution, resolution];

        float offsetX = seed * 0.173f + 43.71f;
        float offsetZ = seed * 0.317f + 91.37f;

        for (int z = 0; z < resolution; z++)
        {
            float nz = z / (resolution - 1f);

            for (int x = 0; x < resolution; x++)
            {
                float nx = x / (resolution - 1f);

                float warpX = Fbm(nx, nz, 1.15f, offsetX + 31f, offsetZ + 79f, 3) * 0.025f;
                float warpZ = Fbm(nx, nz, 1.20f, offsetX + 113f, offsetZ + 17f, 3) * 0.018f;

                float wx = nx + warpX;
                float wz = nz + warpZ;

                float valleyCurveNoise = Fbm(0.37f, wz, 1.35f, offsetX + 173f, offsetZ + 211f, 3);
                float valleyCenter = 0.50f + Mathf.Sin(wz * 4.2f) * 0.018f + valleyCurveNoise * 0.025f;

                float distanceFromValley = Mathf.Abs(wx - valleyCenter);

                float sideMask = Smooth01(Mathf.InverseLerp(valleyWidth, 0.50f, distanceFromValley));
                float northMask = Smooth01(Mathf.InverseLerp(0.20f, 0.92f, wz));

                float leftRightScale = wx < valleyCenter ? 0.84f : 1.08f;
                float longitudinalVariation = Fbm(wx * 0.35f, wz, 1.55f, offsetX + 227f, offsetZ + 317f, 3) * 0.5f + 0.5f;
                float mountainScale = Mathf.Lerp(0.86f, 1.10f, longitudinalVariation);

                float height = baseHeight;
                height += sideMask * mountainHeight * 0.67f * Mathf.Lerp(0.48f, 1f, northMask) * leftRightScale * mountainScale;

                float backgroundMask = Smooth01(Mathf.InverseLerp(0.70f, 0.995f, wz));
                float backgroundRidge = RidgedFbm(wx, wz, 2.15f, offsetX + 401f, offsetZ + 127f, 4);
                float centerBackground = Mathf.Exp(-Mathf.Pow((wx - 0.54f) / 0.24f, 2f));
                float backgroundCenterScale = Mathf.Lerp(1f, 0.70f, centerBackground);

                height += backgroundMask * mountainHeight * 0.29f * Mathf.Lerp(0.45f, 1f, backgroundRidge) * backgroundCenterScale;

                float leftPeakA = Mountain(wx, wz, 0.15f, 0.67f, 0.12f, 0.15f);
                float leftPeakB = Mountain(wx, wz, 0.25f, 0.86f, 0.11f, 0.13f);

                float rightPeakA = Mountain(wx, wz, 0.84f, 0.64f, 0.12f, 0.17f);
                float rightPeakB = Mountain(wx, wz, 0.73f, 0.84f, 0.13f, 0.15f);

                float distantPeakA = Mountain(wx, wz, 0.39f, 0.975f, 0.14f, 0.10f);
                float distantPeakB = Mountain(wx, wz, 0.61f, 0.982f, 0.13f, 0.10f);

                height += leftPeakA * mountainHeight * 0.16f;
                height += leftPeakB * mountainHeight * 0.13f;
                height += rightPeakA * mountainHeight * 0.25f;
                height += rightPeakB * mountainHeight * 0.20f;
                height += distantPeakA * mountainHeight * 0.13f;
                height += distantPeakB * mountainHeight * 0.17f;

                float mountainMask = Mathf.Clamp01(sideMask + backgroundMask * 0.65f + leftPeakA + leftPeakB + rightPeakA + rightPeakB);

                float ridged = RidgedFbm(wx, wz, 3.1f, offsetX + 503f, offsetZ + 349f, 4);
                float ridgeCentered = ridged - 0.48f;
                height += ridgeCentered * ridgeStrength * mountainMask;

                float largeNoise = Fbm(wx, wz, largeNoiseScale, offsetX + 607f, offsetZ + 421f, 3);
                float detailNoise = Fbm(wx, wz, detailNoiseScale, offsetX + 701f, offsetZ + 557f, 2);

                height += largeNoise * largeNoiseStrength * Mathf.Lerp(0.25f, 1f, mountainMask);
                height += detailNoise * detailNoiseStrength * mountainMask;

                float valleyFloorMask = 1f - Smooth01(Mathf.InverseLerp(0f, valleyWidth + 0.08f, distanceFromValley));
                float valleyLengthMask = Smooth01(Mathf.InverseLerp(0.28f, 0.48f, wz)) * (1f - Smooth01(Mathf.InverseLerp(0.86f, 0.96f, wz)));

                height -= valleyFloorMask * valleyLengthMask * valleyDepth;

                float foregroundMask = 1f - Smooth01(Mathf.InverseLerp(0.06f, 0.28f, nz));
                float foregroundNoise = Fbm(nx, nz, 1.2f, offsetX + 809f, offsetZ + 643f, 2);
                height += foregroundMask * foregroundRise * (1f + foregroundNoise * 0.12f);

                float lakeWarpX = Fbm(nx, nz, 1.40f, offsetX + 911f, offsetZ + 733f, 3) * lakeIrregularity;
                float lakeWarpZ = Fbm(nx, nz, 1.55f, offsetX + 977f, offsetZ + 811f, 3) * lakeIrregularity;

                float lx = nx + lakeWarpX;
                float lz = nz + lakeWarpZ;

                float mainLake = EllipseDistance(lx, lz, lakeCenter.x, lakeCenter.y, lakeRadius.x, lakeRadius.y);
                float northLobe = EllipseDistance(lx, lz, lakeCenter.x - 0.045f, lakeCenter.y + 0.075f, lakeRadius.x * 0.72f, lakeRadius.y * 0.62f);
                float eastLobe = EllipseDistance(lx, lz, lakeCenter.x + 0.105f, lakeCenter.y - 0.015f, lakeRadius.x * 0.50f, lakeRadius.y * 0.58f);

                float lakeField = SmoothMin(mainLake, northLobe, 0.14f);
                lakeField = SmoothMin(lakeField, eastLobe, 0.12f);

                float lakeCore = 0.70f;
                float lakeOuter = 1f + lakeBasinWidth;

                if (lakeField < lakeOuter)
                {
                    float shoreT = Smooth01(Mathf.InverseLerp(lakeCore, lakeOuter, lakeField));
                    float targetHeight = Mathf.Lerp(lakeFloorHeight, waterHeight + 8f, shoreT);

                    float floorMask = 1f - Smooth01(Mathf.InverseLerp(0.35f, 0.90f, lakeField));
                    float floorNoise = Fbm(nx, nz, 4.5f, offsetX + 1061f, offsetZ + 953f, 2) * 0.9f * floorMask;

                    targetHeight += floorNoise;

                    float basinInfluence = 1f - Smooth01(Mathf.InverseLerp(0.88f, lakeOuter, lakeField));
                    float loweredHeight = Mathf.Min(height, targetHeight);

                    height = Mathf.Lerp(height, loweredHeight, basinInfluence);
                }

                float edgeFade = Mathf.Min(EdgeFade(nx, 0.02f), EdgeFade(nz, 0.02f));
                height = Mathf.Lerp(baseHeight, height, edgeFade);

                height = Mathf.Clamp(height, 0f, terrainHeight * 0.985f);
                heights[z, x] = height / terrainHeight;
            }
        }

        data.SetHeights(0, 0, heights);
        EditorUtility.SetDirty(data);
        targetTerrain.Flush();

        PositionCamera();
        SceneView.RepaintAll();
    }

    private void PositionCamera()
    {
        if (!ValidateTerrain()) return;

        if (targetCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            Undo.RegisterCreatedObjectUndo(cameraObject, "创建主摄像机");
            targetCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        TerrainData data = targetTerrain.terrainData;
        Vector3 terrainPosition = targetTerrain.transform.position;

        float cameraX = 0.33f;
        float cameraZ = 0.15f;

        float groundHeight = data.GetInterpolatedHeight(cameraX, cameraZ) + terrainPosition.y;

        Vector3 cameraPosition = new Vector3(
            terrainPosition.x + data.size.x * cameraX,
            groundHeight + cameraHeight,
            terrainPosition.z + data.size.z * cameraZ
        );

        Vector3 targetPosition = new Vector3(
            terrainPosition.x + data.size.x * 0.54f,
            terrainPosition.y + waterHeight + 10f,
            terrainPosition.z + data.size.z * 0.62f
        );

        Undo.RecordObject(targetCamera.transform, "定位主摄像机");
        Undo.RecordObject(targetCamera, "设置主摄像机");

        targetCamera.transform.position = cameraPosition;
        targetCamera.transform.rotation = Quaternion.LookRotation((targetPosition - cameraPosition).normalized, Vector3.up);
        targetCamera.fieldOfView = cameraFov;
        targetCamera.nearClipPlane = 0.3f;
        targetCamera.farClipPlane = 2000f;

        EditorUtility.SetDirty(targetCamera);
        EditorUtility.SetDirty(targetCamera.transform);
    }

    private void ResetTerrain()
    {
        if (!ValidateTerrain()) return;

        TerrainData data = targetTerrain.terrainData;
        Undo.RegisterCompleteObjectUndo(data, "恢复 Terrain 平地");

        int resolution = data.heightmapResolution;
        float normalizedHeight = Mathf.Clamp01(baseHeight / data.size.y);
        float[,] heights = new float[resolution, resolution];

        for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
                heights[z, x] = normalizedHeight;

        data.SetHeights(0, 0, heights);
        EditorUtility.SetDirty(data);
        targetTerrain.Flush();
        SceneView.RepaintAll();
    }

    private void RandomizeSeed()
    {
        seed = new System.Random().Next(0, 999999);
        Repaint();
    }

    private bool ValidateTerrain()
    {
        if (targetTerrain != null && targetTerrain.terrainData != null) return true;

        EditorUtility.DisplayDialog("缺少 Terrain", "请先把场景中的 Terrain 拖到“目标 Terrain”。", "确定");
        return false;
    }

    private static float Mountain(float x, float z, float centerX, float centerZ, float radiusX, float radiusZ)
    {
        float dx = (x - centerX) / Mathf.Max(0.001f, radiusX);
        float dz = (z - centerZ) / Mathf.Max(0.001f, radiusZ);
        return Mathf.Exp(-(dx * dx + dz * dz) * 1.8f);
    }

    private static float EllipseDistance(float x, float z, float centerX, float centerZ, float radiusX, float radiusZ)
    {
        float dx = (x - centerX) / Mathf.Max(0.001f, radiusX);
        float dz = (z - centerZ) / Mathf.Max(0.001f, radiusZ);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static float SmoothMin(float a, float b, float k)
    {
        if (k <= 0f) return Mathf.Min(a, b);

        float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
        return Mathf.Lerp(b, a, h) - k * h * (1f - h);
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
}