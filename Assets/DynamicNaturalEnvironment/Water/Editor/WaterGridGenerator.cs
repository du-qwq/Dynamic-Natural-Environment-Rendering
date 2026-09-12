using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.IO;

public class WaterGridGenerator : EditorWindow
{
    private MeshFilter targetMeshFilter;
    private int subdivisions = 256;
    private const float LocalSize = 10f;
    private const string MeshFolder = "Assets/Generated/Environment/Water";
    private const string MeshPath = MeshFolder + "/Lake_HighResGrid.asset";

    [MenuItem("Tools/Environment/Water/高细分湖面生成器")]
    public static void Open()
    {
        WaterGridGenerator window = GetWindow<WaterGridGenerator>("高细分湖面");
        window.minSize = new Vector2(390f, 300f);
    }

    private void OnEnable()
    {
        if (targetMeshFilter != null) return;
        GameObject lake = GameObject.Find("Lake_Placeholder");
        if (lake != null) targetMeshFilter = lake.GetComponent<MeshFilter>();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Lake High Resolution Grid", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("生成与 Unity 默认 Plane 相同的 10×10 本地尺寸高细分网格，并保留 Lake_Placeholder 当前 Transform，因此不会改变现有湖面覆盖范围。", MessageType.Info);
        EditorGUILayout.Space(8);
        targetMeshFilter = (MeshFilter)EditorGUILayout.ObjectField("目标 MeshFilter", targetMeshFilter, typeof(MeshFilter), true);
        subdivisions = EditorGUILayout.IntSlider("细分数量", subdivisions, 32, 512);

        if (targetMeshFilter != null)
        {
            Renderer renderer = targetMeshFilter.GetComponent<Renderer>();
            if (renderer != null)
            {
                Vector3 size = renderer.bounds.size;
                float spacingX = size.x / Mathf.Max(1, subdivisions);
                float spacingZ = size.z / Mathf.Max(1, subdivisions);
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("当前世界尺寸", $"{size.x:F1} × {size.z:F1} m");
                EditorGUILayout.LabelField("预计顶点间距", $"X {spacingX:F2} m / Z {spacingZ:F2} m");
            }
        }

        EditorGUILayout.Space(12);
        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
        if (GUILayout.Button("生成并替换湖面 Mesh", GUILayout.Height(40))) Generate();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox("当前项目推荐先用 256。若湖面仍能看到明显的大三角波形，再考虑 384；不要直接上 512。", MessageType.Warning);
    }

    private void Generate()
    {
        if (targetMeshFilter == null)
        {
            EditorUtility.DisplayDialog("缺少目标", "请把 Lake_Placeholder 的 MeshFilter 拖进来。", "确定");
            return;
        }

        EnsureFolder(MeshFolder);
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);

        if (mesh == null)
        {
            mesh = new Mesh { name = "Lake_HighResGrid" };
            AssetDatabase.CreateAsset(mesh, MeshPath);
        }
        else
        {
            Undo.RecordObject(mesh, "更新高细分湖面 Mesh");
            mesh.Clear();
        }

        int vertexPerSide = subdivisions + 1;
        int vertexCount = vertexPerSide * vertexPerSide;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        Vector4[] tangents = new Vector4[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        int[] triangles = new int[subdivisions * subdivisions * 6];

        for (int z = 0; z <= subdivisions; z++)
        {
            float v = z / (float)subdivisions;
            float localZ = Mathf.Lerp(-LocalSize * 0.5f, LocalSize * 0.5f, v);

            for (int x = 0; x <= subdivisions; x++)
            {
                float u = x / (float)subdivisions;
                float localX = Mathf.Lerp(-LocalSize * 0.5f, LocalSize * 0.5f, u);
                int index = z * vertexPerSide + x;
                vertices[index] = new Vector3(localX, 0f, localZ);
                normals[index] = Vector3.up;
                tangents[index] = new Vector4(1f, 0f, 0f, 1f);
                uvs[index] = new Vector2(u, v);
            }
        }

        int triangleIndex = 0;
        for (int z = 0; z < subdivisions; z++)
        {
            for (int x = 0; x < subdivisions; x++)
            {
                int a = z * vertexPerSide + x;
                int b = a + 1;
                int c = a + vertexPerSide;
                int d = c + 1;
                triangles[triangleIndex++] = a;
                triangles[triangleIndex++] = c;
                triangles[triangleIndex++] = b;
                triangles[triangleIndex++] = b;
                triangles[triangleIndex++] = c;
                triangles[triangleIndex++] = d;
            }
        }

        mesh.indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.tangents = tangents;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        Undo.RecordObject(targetMeshFilter, "替换湖面 Mesh");
        targetMeshFilter.sharedMesh = mesh;
        EditorUtility.SetDirty(mesh);
        EditorUtility.SetDirty(targetMeshFilter);
        AssetDatabase.SaveAssets();
        SceneView.RepaintAll();

        EditorUtility.DisplayDialog("完成", $"已生成 {subdivisions}×{subdivisions} 网格。\n顶点：{vertexCount:N0}\n三角形：{subdivisions * subdivisions * 2:N0}", "确定");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        if (!string.IsNullOrEmpty(parent)) AssetDatabase.CreateFolder(parent, name);
    }
}
