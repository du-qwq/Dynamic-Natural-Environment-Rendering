using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class GrassMeshGeneratorWindow : EditorWindow
{
    [Serializable]
    public class Settings
    {
        [Header("General")]
        public int seed = 12345;
        [Min(1)] public int variantCount = 4;

        [Header("Cluster")]
        [Min(1)] public int bladeCountMin = 12;
        [Min(1)] public int bladeCountMax = 16;
        [Min(0.01f)] public float clusterRadius = 0.17f;

        [Header("Blade")]
        [Min(0.01f)] public float heightMin = 0.42f;
        [Min(0.01f)] public float heightMax = 0.82f;
        [Min(0.001f)] public float widthMin = 0.024f;
        [Min(0.001f)] public float widthMax = 0.050f;
        [Range(1, 8)] public int verticalSegments = 4;
        [Range(0f, 0.3f)] public float tipWidthFactor = 0.02f;
        [Min(0f)] public float leanMin = 0.01f;
        [Min(0f)] public float leanMax = 0.08f;
        [Range(0f, 1f)] public float curvature = 0.55f;
        [Range(0f, 1f)] public float directionRandomness = 0.45f;

        [Header("Shape Variation")]
        [Range(0.5f, 1.5f)] public float centerBladeHeightBoost = 1.08f;
        [Range(0.5f, 1.2f)] public float outerBladeHeightReduce = 0.88f;
        [Range(0f, 0.5f)] public float heightRandomness = 0.10f;
        [Range(0f, 0.5f)] public float widthRandomness = 0.10f;

        [Header("Bounds")]
        [Min(0f)] public float boundsPaddingXZ = 0.15f;
        [Min(0f)] public float boundsPaddingY = 0.08f;

        [Header("Output")]
        public string outputFolder = "Assets/Vegetation/Generated/GrassMeshes";
        public string meshNamePrefix = "GrassCluster_Arc";
    }

    private Settings settings = new Settings();

    private Vector2 scrollPosition;

    private PreviewRenderUtility previewUtility;
    private Mesh previewMesh;
    private Material previewMaterial;

    private int previewBladeCount;
    private int previewVertexCount;
    private int previewTriangleCount;

    private const float PreviewHeight = 320f;

    [MenuItem("Tools/Vegetation/Grass Mesh Generator")]
    public static void Open()
    {
        GrassMeshGeneratorWindow window = GetWindow<GrassMeshGeneratorWindow>();
        window.titleContent = new GUIContent("Grass Mesh Generator");
        window.minSize = new Vector2(420f, 700f);
        window.Show();
    }

    private void OnEnable()
    {
        CreatePreviewUtility();
    }

    private void OnDisable()
    {
        DestroyPreviewMesh();

        if (previewMaterial != null)
        {
            DestroyImmediate(previewMaterial);
            previewMaterial = null;
        }

        if (previewUtility != null)
        {
            previewUtility.Cleanup();
            previewUtility = null;
        }
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.Space(8);

        DrawPresetSettings();

        EditorGUILayout.Space(12);

        DrawGeneralSettings();

        EditorGUILayout.Space(8);

        DrawClusterSettings();

        EditorGUILayout.Space(8);

        DrawBladeSettings();

        EditorGUILayout.Space(8);

        DrawShapeSettings();

        EditorGUILayout.Space(8);

        DrawBoundsSettings();

        EditorGUILayout.Space(8);

        DrawOutputSettings();

        EditorGUILayout.Space(12);

        DrawButtons();

        EditorGUILayout.Space(12);

        DrawPreview();

        EditorGUILayout.EndScrollView();
    }

    private void DrawPresetSettings()
    {
        EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Dense Meadow：高覆盖自然草毯，适合作为主草地。\nSparse Wild Grass：较高、较散，适合树林边缘、坡地和点缀。\n当前生成的 Mesh 会在 TEXCOORD1 中保存每根 Blade 的 Root XZ，用于弧形风。",
            MessageType.Info
        );

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Dense Meadow", GUILayout.Height(34f)))
        {
            ApplyDenseMeadowPreset();
        }

        if (GUILayout.Button("Sparse Wild Grass", GUILayout.Height(34f)))
        {
            ApplySparseWildGrassPreset();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void ApplyDenseMeadowPreset()
    {
        settings.variantCount = 4;

        settings.bladeCountMin = 12;
        settings.bladeCountMax = 16;
        settings.clusterRadius = 0.17f;

        settings.heightMin = 0.42f;
        settings.heightMax = 0.82f;

        settings.widthMin = 0.024f;
        settings.widthMax = 0.050f;

        settings.verticalSegments = 4;
        settings.tipWidthFactor = 0.02f;

        settings.leanMin = 0.01f;
        settings.leanMax = 0.08f;

        settings.curvature = 0.55f;
        settings.directionRandomness = 0.45f;

        settings.centerBladeHeightBoost = 1.08f;
        settings.outerBladeHeightReduce = 0.88f;

        settings.heightRandomness = 0.10f;
        settings.widthRandomness = 0.10f;

        settings.boundsPaddingXZ = 0.15f;
        settings.boundsPaddingY = 0.08f;

        settings.meshNamePrefix = "GrassCluster_Arc";

        GeneratePreview();
    }

    private void ApplySparseWildGrassPreset()
    {
        settings.variantCount = 4;

        settings.bladeCountMin = 7;
        settings.bladeCountMax = 11;
        settings.clusterRadius = 0.22f;

        settings.heightMin = 0.55f;
        settings.heightMax = 1.05f;

        settings.widthMin = 0.020f;
        settings.widthMax = 0.042f;

        settings.verticalSegments = 4;
        settings.tipWidthFactor = 0.02f;

        settings.leanMin = 0.025f;
        settings.leanMax = 0.14f;

        settings.curvature = 0.68f;
        settings.directionRandomness = 0.62f;

        settings.centerBladeHeightBoost = 1.12f;
        settings.outerBladeHeightReduce = 0.80f;

        settings.heightRandomness = 0.16f;
        settings.widthRandomness = 0.14f;

        settings.boundsPaddingXZ = 0.18f;
        settings.boundsPaddingY = 0.10f;

        settings.meshNamePrefix = "GrassCluster_WildArc";

        GeneratePreview();
    }

    private void DrawGeneralSettings()
    {
        EditorGUILayout.LabelField("General", EditorStyles.boldLabel);

        settings.seed = EditorGUILayout.IntField("Seed", settings.seed);
        settings.variantCount = Mathf.Max(1, EditorGUILayout.IntField("Variant Count", settings.variantCount));
    }

    private void DrawClusterSettings()
    {
        EditorGUILayout.LabelField("Cluster", EditorStyles.boldLabel);

        settings.bladeCountMin = Mathf.Max(1, EditorGUILayout.IntField("Blade Count Min", settings.bladeCountMin));
        settings.bladeCountMax = Mathf.Max(settings.bladeCountMin, EditorGUILayout.IntField("Blade Count Max", settings.bladeCountMax));
        settings.clusterRadius = Mathf.Max(0.01f, EditorGUILayout.FloatField("Cluster Radius", settings.clusterRadius));
    }

    private void DrawBladeSettings()
    {
        EditorGUILayout.LabelField("Blade", EditorStyles.boldLabel);

        settings.heightMin = Mathf.Max(0.01f, EditorGUILayout.FloatField("Height Min", settings.heightMin));
        settings.heightMax = Mathf.Max(settings.heightMin, EditorGUILayout.FloatField("Height Max", settings.heightMax));

        settings.widthMin = Mathf.Max(0.001f, EditorGUILayout.FloatField("Width Min", settings.widthMin));
        settings.widthMax = Mathf.Max(settings.widthMin, EditorGUILayout.FloatField("Width Max", settings.widthMax));

        settings.verticalSegments = EditorGUILayout.IntSlider("Vertical Segments", settings.verticalSegments, 1, 8);
        settings.tipWidthFactor = EditorGUILayout.Slider("Tip Width Factor", settings.tipWidthFactor, 0f, 0.3f);

        settings.leanMin = Mathf.Max(0f, EditorGUILayout.FloatField("Lean Min", settings.leanMin));
        settings.leanMax = Mathf.Max(settings.leanMin, EditorGUILayout.FloatField("Lean Max", settings.leanMax));

        settings.curvature = EditorGUILayout.Slider("Curvature", settings.curvature, 0f, 1f);
        settings.directionRandomness = EditorGUILayout.Slider("Direction Randomness", settings.directionRandomness, 0f, 1f);
    }

    private void DrawShapeSettings()
    {
        EditorGUILayout.LabelField("Shape Variation", EditorStyles.boldLabel);

        settings.centerBladeHeightBoost = EditorGUILayout.Slider("Center Height Boost", settings.centerBladeHeightBoost, 0.5f, 1.5f);
        settings.outerBladeHeightReduce = EditorGUILayout.Slider("Outer Height Reduce", settings.outerBladeHeightReduce, 0.5f, 1.2f);

        settings.heightRandomness = EditorGUILayout.Slider("Height Randomness", settings.heightRandomness, 0f, 0.5f);
        settings.widthRandomness = EditorGUILayout.Slider("Width Randomness", settings.widthRandomness, 0f, 0.5f);
    }

    private void DrawBoundsSettings()
    {
        EditorGUILayout.LabelField("Bounds", EditorStyles.boldLabel);

        settings.boundsPaddingXZ = Mathf.Max(0f, EditorGUILayout.FloatField("Bounds Padding XZ", settings.boundsPaddingXZ));
        settings.boundsPaddingY = Mathf.Max(0f, EditorGUILayout.FloatField("Bounds Padding Y", settings.boundsPaddingY));
    }

    private void DrawOutputSettings()
    {
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        settings.outputFolder = EditorGUILayout.TextField("Output Folder", settings.outputFolder);

        if (GUILayout.Button("Select", GUILayout.Width(60f)))
        {
            SelectOutputFolder();
        }

        EditorGUILayout.EndHorizontal();

        settings.meshNamePrefix = EditorGUILayout.TextField("Mesh Name Prefix", settings.meshNamePrefix);
    }

    private void DrawButtons()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Generate Preview", GUILayout.Height(32f)))
        {
            GeneratePreview();
        }

        if (GUILayout.Button("Generate Grass Meshes", GUILayout.Height(32f)))
        {
            GenerateAssets();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawPreview()
    {
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        Rect rect = GUILayoutUtility.GetRect(10f, PreviewHeight, GUILayout.ExpandWidth(true));

        if (Event.current.type == EventType.Repaint)
        {
            RenderPreview(rect);
        }

        if (previewMesh == null)
        {
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));
            GUI.Label(rect, "Click Generate Preview", CenteredLabelStyle());
            return;
        }

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField($"Blades: {previewBladeCount}");
        EditorGUILayout.LabelField($"Vertices: {previewVertexCount}");
        EditorGUILayout.LabelField($"Triangles: {previewTriangleCount}");
        EditorGUILayout.LabelField($"Bounds: {previewMesh.bounds.size}");
        EditorGUILayout.LabelField("UV0: Blade UV");
        EditorGUILayout.LabelField("UV1: Blade Root XZ");
    }

    private void GeneratePreview()
    {
        ValidateSettings();

        DestroyPreviewMesh();

        previewMesh = GenerateGrassMesh(settings, settings.seed, out previewBladeCount);
        previewMesh.name = "GrassPreview";

        previewVertexCount = previewMesh.vertexCount;
        previewTriangleCount = previewMesh.triangles.Length / 3;

        Repaint();
    }

    private void GenerateAssets()
    {
        ValidateSettings();

        if (!settings.outputFolder.StartsWith("Assets", StringComparison.Ordinal))
        {
            EditorUtility.DisplayDialog(
                "Grass Mesh Generator",
                "Output Folder 必须位于 Assets 目录中。",
                "OK"
            );

            return;
        }

        EnsureAssetFolderExists(settings.outputFolder);

        int createdCount = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < settings.variantCount; i++)
            {
                int variantSeed = settings.seed + i * 7919;

                Mesh mesh = GenerateGrassMesh(
                    settings,
                    variantSeed,
                    out int bladeCount
                );

                string baseName = $"{settings.meshNamePrefix}_{i + 1:00}";

                mesh.name = baseName;

                string targetPath = $"{settings.outputFolder}/{baseName}.asset";
                targetPath = AssetDatabase.GenerateUniqueAssetPath(targetPath);

                AssetDatabase.CreateAsset(mesh, targetPath);

                Debug.Log(
                    $"Grass Mesh generated: {targetPath} | " +
                    $"Blades: {bladeCount}, " +
                    $"Vertices: {mesh.vertexCount}, " +
                    $"Triangles: {mesh.triangles.Length / 3}, " +
                    $"UV1 Root Data: Yes"
                );

                createdCount++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        EditorUtility.DisplayDialog(
            "Grass Mesh Generator",
            $"生成完成，共创建 {createdCount} 个 Grass Mesh。\n\n这些 Mesh 已包含 TEXCOORD1 Blade Root 数据。",
            "OK"
        );
    }

    public static Mesh GenerateGrassMesh(Settings settings, int seed)
    {
        return GenerateGrassMesh(settings, seed, out _);
    }

    public static Mesh GenerateGrassMesh(
        Settings settings,
        int seed,
        out int generatedBladeCount)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        System.Random random = new System.Random(seed);

        int bladeCountMin = Mathf.Max(1, settings.bladeCountMin);
        int bladeCountMax = Mathf.Max(bladeCountMin, settings.bladeCountMax);

        int bladeCount = random.Next(
            bladeCountMin,
            bladeCountMax + 1
        );

        int verticalSegments = Mathf.Clamp(
            settings.verticalSegments,
            1,
            8
        );

        int estimatedVertexCount = bladeCount * (verticalSegments + 1) * 2;

        List<Vector3> vertices = new List<Vector3>(estimatedVertexCount);
        List<Vector3> normals = new List<Vector3>(estimatedVertexCount);
        List<Vector2> uvs = new List<Vector2>(estimatedVertexCount);
        List<Vector2> bladeRoots = new List<Vector2>(estimatedVertexCount);

        List<int> triangles = new List<int>(
            bladeCount * verticalSegments * 6
        );

        for (int bladeIndex = 0; bladeIndex < bladeCount; bladeIndex++)
        {
            CreateBlade(
                settings,
                random,
                bladeIndex,
                bladeCount,
                verticalSegments,
                vertices,
                normals,
                uvs,
                bladeRoots,
                triangles
            );
        }

        Mesh mesh = new Mesh
        {
            name = $"GeneratedGrass_{seed}"
        };

        if (vertices.Count > 65535)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);

        // TEXCOORD0
        // x = Blade 横向 UV
        // y = Blade 从 Root 到 Tip 的高度 0~1
        mesh.SetUVs(0, uvs);

        // TEXCOORD1
        // x = 这根 Blade 的 Root Local X
        // y = 这根 Blade 的 Root Local Z
        mesh.SetUVs(1, bladeRoots);

        mesh.SetTriangles(
            triangles,
            0,
            true
        );

        mesh.RecalculateBounds();
        mesh.RecalculateTangents();

        Bounds bounds = mesh.bounds;

        bounds.Expand(
            new Vector3(
                settings.boundsPaddingXZ * 2f,
                settings.boundsPaddingY * 2f,
                settings.boundsPaddingXZ * 2f
            )
        );

        mesh.bounds = bounds;

        mesh.UploadMeshData(false);

        generatedBladeCount = bladeCount;

        return mesh;
    }

    private static void CreateBlade(
        Settings settings,
        System.Random random,
        int bladeIndex,
        int bladeCount,
        int verticalSegments,
        List<Vector3> vertices,
        List<Vector3> normals,
        List<Vector2> uvs,
        List<Vector2> bladeRoots,
        List<int> triangles)
    {
        bool forceCenterBlade = bladeIndex < Mathf.Min(2, bladeCount);

        float placementAngle = RandomRange(
            random,
            0f,
            Mathf.PI * 2f
        );

        float normalizedRadius = Mathf.Sqrt(
            Random01(random)
        );

        if (forceCenterBlade)
        {
            normalizedRadius *= 0.18f;
        }

        float placementRadius =
            normalizedRadius *
            settings.clusterRadius;

        Vector3 rootPosition = new Vector3(
            Mathf.Cos(placementAngle) * placementRadius,
            0f,
            Mathf.Sin(placementAngle) * placementRadius
        );

        float centerFactor =
            settings.clusterRadius > 0.0001f
                ? 1f - Mathf.Clamp01(
                    placementRadius /
                    settings.clusterRadius
                )
                : 1f;

        float baseHeight = RandomRange(
            random,
            settings.heightMin,
            settings.heightMax
        );

        float centerHeightFactor = Mathf.Lerp(
            settings.outerBladeHeightReduce,
            settings.centerBladeHeightBoost,
            centerFactor
        );

        float heightRandomFactor =
            1f +
            RandomSigned(random) *
            settings.heightRandomness;

        float height =
            baseHeight *
            centerHeightFactor *
            heightRandomFactor;

        if (forceCenterBlade)
        {
            height *= RandomRange(
                random,
                1.02f,
                1.08f
            );
        }

        float width = RandomRange(
            random,
            settings.widthMin,
            settings.widthMax
        );

        width *=
            1f +
            RandomSigned(random) *
            settings.widthRandomness;

        width = Mathf.Max(
            width,
            0.001f
        );

        float facingAngle = RandomRange(
            random,
            0f,
            Mathf.PI * 2f
        );

        Vector3 rightDirection = new Vector3(
            Mathf.Cos(facingAngle),
            0f,
            Mathf.Sin(facingAngle)
        ).normalized;

        Vector3 facingDirection = Vector3.Cross(
            rightDirection,
            Vector3.up
        ).normalized;

        float randomLeanAngle = RandomRange(
            random,
            0f,
            Mathf.PI * 2f
        );

        Vector3 randomLeanDirection = new Vector3(
            Mathf.Cos(randomLeanAngle),
            0f,
            Mathf.Sin(randomLeanAngle)
        ).normalized;

        Vector3 leanDirection = Vector3.Slerp(
            facingDirection,
            randomLeanDirection,
            settings.directionRandomness
        ).normalized;

        float leanAmount = RandomRange(
            random,
            settings.leanMin,
            settings.leanMax
        );

        if (forceCenterBlade)
        {
            leanAmount *= RandomRange(
                random,
                0.55f,
                0.85f
            );
        }

        float curveExponent = Mathf.Lerp(
            1.5f,
            3.2f,
            settings.curvature
        );

        int firstVertexIndex = vertices.Count;

        Vector2 bladeRootXZ = new Vector2(
            rootPosition.x,
            rootPosition.z
        );

        for (int segment = 0; segment <= verticalSegments; segment++)
        {
            float t =
                segment /
                (float)verticalSegments;

            float verticalPosition =
                height *
                t;

            float bendT = Mathf.Pow(
                t,
                curveExponent
            );

            float bendAmount =
                leanAmount *
                bendT;

            Vector3 center = rootPosition;

            center.y += verticalPosition;

            center +=
                leanDirection *
                bendAmount;

            float taper = Mathf.Pow(
                1f - t,
                0.65f
            );

            float widthFactor = Mathf.Lerp(
                settings.tipWidthFactor,
                1f,
                taper
            );

            float halfWidth =
                width *
                widthFactor *
                0.5f;

            Vector3 left =
                center -
                rightDirection *
                halfWidth;

            Vector3 right =
                center +
                rightDirection *
                halfWidth;

            Vector3 bladeNormal = CalculateBladeNormal(
                facingDirection,
                leanDirection,
                t,
                settings.curvature
            );

            vertices.Add(left);
            vertices.Add(right);

            normals.Add(bladeNormal);
            normals.Add(bladeNormal);

            uvs.Add(
                new Vector2(
                    0f,
                    t
                )
            );

            uvs.Add(
                new Vector2(
                    1f,
                    t
                )
            );

            // 保存每根 Blade 自己的 Root Local XZ。
            // Shader 中通过 TEXCOORD1 获取，
            // 从而可以围绕真正的 Blade 根部弯曲，
            // 而不是围绕整个 Grass Cluster Pivot 弯曲。
            bladeRoots.Add(bladeRootXZ);
            bladeRoots.Add(bladeRootXZ);
        }

        for (int segment = 0; segment < verticalSegments; segment++)
        {
            int row =
                firstVertexIndex +
                segment * 2;

            int bottomLeft = row;
            int bottomRight = row + 1;
            int topLeft = row + 2;
            int topRight = row + 3;

            AddTriangle(
                triangles,
                bottomLeft,
                topLeft,
                bottomRight
            );

            AddTriangle(
                triangles,
                bottomRight,
                topLeft,
                topRight
            );
        }
    }

    private static Vector3 CalculateBladeNormal(
        Vector3 facingDirection,
        Vector3 leanDirection,
        float heightT,
        float curvature)
    {
        Vector3 stylizedNormal =
            (
                facingDirection * 0.72f +
                Vector3.up * 0.28f
            ).normalized;

        float bendInfluence =
            heightT *
            curvature *
            0.18f;

        Vector3 bentNormal =
            (
                stylizedNormal -
                leanDirection * bendInfluence +
                Vector3.up * bendInfluence * 0.5f
            ).normalized;

        return bentNormal;
    }

    private static void AddTriangle(
        List<int> triangles,
        int a,
        int b,
        int c)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
    }

    private static float Random01(
        System.Random random)
    {
        return (float)random.NextDouble();
    }

    private static float RandomSigned(
        System.Random random)
    {
        return Random01(random) * 2f - 1f;
    }

    private static float RandomRange(
        System.Random random,
        float min,
        float max)
    {
        return Mathf.Lerp(
            min,
            max,
            Random01(random)
        );
    }

    private void ValidateSettings()
    {
        settings.variantCount = Mathf.Max(
            1,
            settings.variantCount
        );

        settings.bladeCountMin = Mathf.Max(
            1,
            settings.bladeCountMin
        );

        settings.bladeCountMax = Mathf.Max(
            settings.bladeCountMin,
            settings.bladeCountMax
        );

        settings.clusterRadius = Mathf.Max(
            0.01f,
            settings.clusterRadius
        );

        settings.heightMin = Mathf.Max(
            0.01f,
            settings.heightMin
        );

        settings.heightMax = Mathf.Max(
            settings.heightMin,
            settings.heightMax
        );

        settings.widthMin = Mathf.Max(
            0.001f,
            settings.widthMin
        );

        settings.widthMax = Mathf.Max(
            settings.widthMin,
            settings.widthMax
        );

        settings.verticalSegments = Mathf.Clamp(
            settings.verticalSegments,
            1,
            8
        );

        settings.leanMin = Mathf.Max(
            0f,
            settings.leanMin
        );

        settings.leanMax = Mathf.Max(
            settings.leanMin,
            settings.leanMax
        );

        if (string.IsNullOrWhiteSpace(settings.meshNamePrefix))
        {
            settings.meshNamePrefix = "GrassCluster_Arc";
        }
    }

    private void SelectOutputFolder()
    {
        string absolutePath = EditorUtility.OpenFolderPanel(
            "Select Grass Mesh Output Folder",
            Application.dataPath,
            string.Empty
        );

        if (string.IsNullOrEmpty(absolutePath))
        {
            return;
        }

        absolutePath =
            absolutePath.Replace(
                "\\",
                "/"
            );

        string projectPath =
            Application.dataPath.Replace(
                "\\",
                "/"
            );

        projectPath = projectPath.Substring(
            0,
            projectPath.Length -
            "Assets".Length
        );

        if (!absolutePath.StartsWith(
            projectPath,
            StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog(
                "Invalid Folder",
                "请选择当前 Unity 项目 Assets 目录中的文件夹。",
                "OK"
            );

            return;
        }

        settings.outputFolder =
            absolutePath.Substring(
                projectPath.Length
            );

        Repaint();
    }

    private static void EnsureAssetFolderExists(
        string folderPath)
    {
        folderPath = folderPath
            .Replace("\\", "/")
            .TrimEnd('/');

        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string[] parts =
            folderPath.Split('/');

        if (parts.Length == 0 || parts[0] != "Assets")
        {
            throw new InvalidOperationException(
                "Asset folder 必须从 Assets 开始。"
            );
        }

        string currentPath = "Assets";

        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath =
                $"{currentPath}/{parts[i]}";

            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(
                    currentPath,
                    parts[i]
                );
            }

            currentPath = nextPath;
        }
    }

    private void CreatePreviewUtility()
    {
        if (previewUtility != null)
        {
            return;
        }

        previewUtility =
            new PreviewRenderUtility();

        previewUtility.cameraFieldOfView = 30f;

        previewUtility.lights[0].intensity = 1.2f;

        previewUtility.lights[0]
            .transform.rotation =
            Quaternion.Euler(
                40f,
                35f,
                0f
            );

        previewUtility.lights[1].intensity = 0.7f;

        previewUtility.lights[1]
            .transform.rotation =
            Quaternion.Euler(
                340f,
                215f,
                0f
            );

        previewUtility.ambientColor =
            new Color(
                0.32f,
                0.32f,
                0.32f
            );

        Shader shader =
            Shader.Find(
                "Universal Render Pipeline/Unlit"
            );

        if (shader == null)
        {
            shader =
                Shader.Find(
                    "Standard"
                );
        }

        if (shader != null)
        {
            previewMaterial =
                new Material(shader)
                {
                    hideFlags =
                        HideFlags.HideAndDontSave
                };

            if (previewMaterial.HasProperty("_BaseColor"))
            {
                previewMaterial.SetColor(
                    "_BaseColor",
                    new Color(
                        0.28f,
                        0.60f,
                        0.18f,
                        1f
                    )
                );
            }

            if (previewMaterial.HasProperty("_Color"))
            {
                previewMaterial.SetColor(
                    "_Color",
                    new Color(
                        0.28f,
                        0.60f,
                        0.18f,
                        1f
                    )
                );
            }
        }
    }

    private void RenderPreview(Rect rect)
    {
        if (
            previewMesh == null ||
            previewUtility == null ||
            previewMaterial == null
        )
        {
            EditorGUI.DrawRect(
                rect,
                new Color(
                    0.12f,
                    0.12f,
                    0.12f
                )
            );

            return;
        }

        previewUtility.BeginPreview(
            rect,
            GUIStyle.none
        );

        Bounds bounds =
            previewMesh.bounds;

        float maxSize = Mathf.Max(
            bounds.size.x,
            bounds.size.y,
            bounds.size.z
        );

        float cameraDistance =
            Mathf.Max(
                maxSize * 2.3f,
                1.5f
            );

        Quaternion cameraRotation =
            Quaternion.Euler(
                12f,
                35f,
                0f
            );

        Vector3 target =
            bounds.center +
            Vector3.up *
            bounds.extents.y *
            0.08f;

        Vector3 cameraPosition =
            target -
            cameraRotation *
            Vector3.forward *
            cameraDistance;

        previewUtility.camera.transform.position =
            cameraPosition;

        previewUtility.camera.transform.rotation =
            cameraRotation;

        previewUtility.camera.nearClipPlane =
            0.01f;

        previewUtility.camera.farClipPlane =
            cameraDistance * 10f;

        previewUtility.DrawMesh(
            previewMesh,
            Matrix4x4.identity,
            previewMaterial,
            0
        );

        previewUtility.camera.Render();

        Texture previewTexture =
            previewUtility.EndPreview();

        GUI.DrawTexture(
            rect,
            previewTexture,
            ScaleMode.StretchToFill,
            false
        );
    }

    private void DestroyPreviewMesh()
    {
        if (previewMesh == null)
        {
            return;
        }

        DestroyImmediate(previewMesh);
        previewMesh = null;
    }

    private static GUIStyle CenteredLabelStyle()
    {
        GUIStyle style =
            new GUIStyle(
                EditorStyles.centeredGreyMiniLabel
            )
            {
                alignment =
                    TextAnchor.MiddleCenter,

                fontSize = 14
            };

        return style;
    }
}