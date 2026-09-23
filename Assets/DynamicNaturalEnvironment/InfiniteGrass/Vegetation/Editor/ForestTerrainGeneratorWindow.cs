using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class ForestTerrainGeneratorWindow : EditorWindow
{
    [SerializeField] private Terrain targetTerrain;
    [SerializeField] private ForestTerrainProfile profile;

    [SerializeField] private bool duplicateTerrainDataBeforeGenerate = true;
    [SerializeField] private TerrainData previousTerrainData;

    [SerializeField] private string outputFolder = "Assets/Vegetation/Generated/Terrain";

    [SerializeField] private Vector3 newTerrainSize = new Vector3(1000f, 250f, 1000f);
    [SerializeField] private int newTerrainResolutionIndex = 4;
    [SerializeField] private Vector3 newTerrainPosition = Vector3.zero;

    private static readonly int[] HeightmapResolutions =
    {
        33,
        65,
        129,
        257,
        513,
        1025,
        2049,
        4097
    };

    private static readonly string[] HeightmapResolutionNames =
    {
        "33",
        "65",
        "129",
        "257",
        "513",
        "1025",
        "2049",
        "4097"
    };

    [MenuItem("Tools/Vegetation/Forest Terrain Generator")]
    public static void Open()
    {
        ForestTerrainGeneratorWindow window = GetWindow<ForestTerrainGeneratorWindow>();
        window.titleContent = new GUIContent("Forest Terrain Generator");
        window.minSize = new Vector2(430f, 620f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);

        EditorGUILayout.LabelField("Forest Terrain Generator", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "用于生成适合森林、草甸和开放世界场景的自然丘陵Terrain。\n\n推荐先使用 Duplicate TerrainData，这样不会破坏原TerrainData。",
            MessageType.Info
        );

        EditorGUILayout.Space(8f);

        DrawTargetSection();

        EditorGUILayout.Space(10f);

        DrawProfileSection();

        EditorGUILayout.Space(10f);

        DrawGenerateSection();

        EditorGUILayout.Space(14f);

        DrawCreateTerrainSection();
    }

    private void DrawTargetSection()
    {
        EditorGUILayout.LabelField("Target Terrain", EditorStyles.boldLabel);

        targetTerrain = (Terrain)EditorGUILayout.ObjectField(
            "Terrain",
            targetTerrain,
            typeof(Terrain),
            true
        );

        if (targetTerrain != null && targetTerrain.terrainData != null)
        {
            TerrainData data = targetTerrain.terrainData;

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("TerrainData", data.name);
            EditorGUILayout.LabelField("Height Resolution", data.heightmapResolution.ToString());
            EditorGUILayout.LabelField(
                "Size",
                $"{data.size.x:F0} × {data.size.y:F0} × {data.size.z:F0}"
            );
            EditorGUI.indentLevel--;
        }
    }

    private void DrawProfileSection()
    {
        EditorGUILayout.LabelField("Profile", EditorStyles.boldLabel);

        profile = (ForestTerrainProfile)EditorGUILayout.ObjectField(
            "Forest Terrain Profile",
            profile,
            typeof(ForestTerrainProfile),
            false
        );

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Create Profile"))
        {
            CreateProfile();
        }

        using (new EditorGUI.DisabledScope(profile == null))
        {
            if (GUILayout.Button("Randomize Seed"))
            {
                Undo.RecordObject(profile, "Randomize Forest Terrain Seed");

                profile.seed = Guid.NewGuid().GetHashCode();

                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }
        }

        EditorGUILayout.EndHorizontal();

        if (profile != null)
        {
            EditorGUILayout.Space(5f);

            EditorGUILayout.HelpBox(
                $"Seed: {profile.seed}\n" +
                $"Macro Scale: {profile.macroScale:F4}\n" +
                $"Hill Scale: {profile.hillScale:F4}\n" +
                $"Mountain Strength: {profile.mountainStrength:F2}\n" +
                $"Valley Strength: {profile.valleyStrength:F2}",
                MessageType.None
            );
        }
    }

    private void DrawGenerateSection()
    {
        EditorGUILayout.LabelField("Generate", EditorStyles.boldLabel);

        duplicateTerrainDataBeforeGenerate = EditorGUILayout.Toggle(
            new GUIContent(
                "Duplicate TerrainData",
                "开启后会先复制TerrainData，再把生成结果写到副本中。推荐开启。"
            ),
            duplicateTerrainDataBeforeGenerate
        );

        outputFolder = EditorGUILayout.TextField(
            "Output Folder",
            outputFolder
        );

        using (new EditorGUI.DisabledScope(targetTerrain == null || targetTerrain.terrainData == null || profile == null))
        {
            if (GUILayout.Button("Generate Forest Terrain", GUILayout.Height(38f)))
            {
                GenerateTerrain();
            }
        }

        if (previousTerrainData != null)
        {
            EditorGUILayout.Space(5f);

            GUI.backgroundColor = new Color(0.75f, 0.9f, 1f);

            if (GUILayout.Button("Restore Previous TerrainData", GUILayout.Height(30f)))
            {
                RestorePreviousTerrainData();
            }

            GUI.backgroundColor = Color.white;
        }

        if (!duplicateTerrainDataBeforeGenerate)
        {
            EditorGUILayout.HelpBox(
                "当前关闭了Duplicate TerrainData。Generate会直接修改现有TerrainData的Heightmap。",
                MessageType.Warning
            );
        }
    }

    private void DrawCreateTerrainSection()
    {
        EditorGUILayout.LabelField("Create New Terrain", EditorStyles.boldLabel);

        newTerrainSize = EditorGUILayout.Vector3Field(
            "Terrain Size",
            newTerrainSize
        );

        newTerrainSize.x = Mathf.Max(1f, newTerrainSize.x);
        newTerrainSize.y = Mathf.Max(1f, newTerrainSize.y);
        newTerrainSize.z = Mathf.Max(1f, newTerrainSize.z);

        newTerrainResolutionIndex = EditorGUILayout.Popup(
            "Height Resolution",
            newTerrainResolutionIndex,
            HeightmapResolutionNames
        );

        newTerrainPosition = EditorGUILayout.Vector3Field(
            "World Position",
            newTerrainPosition
        );

        outputFolder = EditorGUILayout.TextField(
            "Asset Folder",
            outputFolder
        );

        if (GUILayout.Button("Create New Terrain", GUILayout.Height(32f)))
        {
            CreateNewTerrain(false);
        }

        using (new EditorGUI.DisabledScope(profile == null))
        {
            if (GUILayout.Button("Create + Generate", GUILayout.Height(38f)))
            {
                CreateNewTerrain(true);
            }
        }
    }

    private void GenerateTerrain()
    {
        if (targetTerrain == null || targetTerrain.terrainData == null || profile == null)
        {
            return;
        }

        try
        {
            if (duplicateTerrainDataBeforeGenerate)
            {
                DuplicateTargetTerrainData();
            }
            else
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "Modify TerrainData",
                    "这会直接修改当前TerrainData的Heightmap。\n\n确定继续？",
                    "Generate",
                    "Cancel"
                );

                if (!confirmed)
                {
                    return;
                }
            }

            Undo.RecordObject(targetTerrain.terrainData, "Generate Forest Terrain");

            ForestTerrainGenerator.GenerateAndApply(
                targetTerrain,
                profile,
                (progress, message) =>
                {
                    EditorUtility.DisplayProgressBar(
                        "Forest Terrain Generator",
                        message,
                        Mathf.Clamp01(progress)
                    );
                }
            );

            EditorUtility.SetDirty(targetTerrain.terrainData);
            EditorUtility.SetDirty(targetTerrain);

            TerrainCollider collider = targetTerrain.GetComponent<TerrainCollider>();

            if (collider != null)
            {
                collider.terrainData = targetTerrain.terrainData;
                EditorUtility.SetDirty(collider);
            }

            if (targetTerrain.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(targetTerrain.gameObject.scene);
            }

            AssetDatabase.SaveAssets();

            SceneView.RepaintAll();

            Debug.Log(
                $"Forest terrain generated: {targetTerrain.name} | Seed={profile.seed}",
                targetTerrain
            );
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private void DuplicateTargetTerrainData()
    {
        TerrainData source = targetTerrain.terrainData;

        if (source == null)
        {
            return;
        }

        previousTerrainData = source;

        EnsureAssetFolder(outputFolder);

        TerrainData duplicate = Instantiate(source);
        duplicate.name = $"{source.name}_Forest_{profile.seed}";

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(
            $"{NormalizeFolder(outputFolder)}/{duplicate.name}.asset"
        );

        AssetDatabase.CreateAsset(
            duplicate,
            assetPath
        );

        Undo.RecordObject(
            targetTerrain,
            "Assign Generated TerrainData"
        );

        targetTerrain.terrainData = duplicate;

        TerrainCollider collider = targetTerrain.GetComponent<TerrainCollider>();

        if (collider != null)
        {
            Undo.RecordObject(
                collider,
                "Assign Generated TerrainData"
            );

            collider.terrainData = duplicate;
        }

        EditorUtility.SetDirty(targetTerrain);

        AssetDatabase.SaveAssets();
    }

    private void RestorePreviousTerrainData()
    {
        if (targetTerrain == null || previousTerrainData == null)
        {
            return;
        }

        Undo.RecordObject(
            targetTerrain,
            "Restore TerrainData"
        );

        targetTerrain.terrainData =
            previousTerrainData;

        TerrainCollider collider =
            targetTerrain
                .GetComponent<TerrainCollider>();

        if (collider != null)
        {
            Undo.RecordObject(
                collider,
                "Restore TerrainData"
            );

            collider.terrainData =
                previousTerrainData;

            EditorUtility.SetDirty(
                collider
            );
        }

        EditorUtility.SetDirty(
            targetTerrain
        );

        if (
            targetTerrain
                .gameObject
                .scene
                .IsValid()
        )
        {
            EditorSceneManager.MarkSceneDirty(
                targetTerrain
                    .gameObject
                    .scene
            );
        }

        previousTerrainData = null;

        SceneView.RepaintAll();
    }

    private void CreateNewTerrain(bool generateImmediately)
    {
        EnsureAssetFolder(outputFolder);

        int resolution =
            HeightmapResolutions[
                Mathf.Clamp(
                    newTerrainResolutionIndex,
                    0,
                    HeightmapResolutions.Length - 1
                )
            ];

        TerrainData terrainData =
            new TerrainData();

        terrainData.heightmapResolution =
            resolution;

        terrainData.size =
            newTerrainSize;

        terrainData.name =
            $"ForestTerrain_{resolution}";

        string assetPath =
            AssetDatabase
                .GenerateUniqueAssetPath(
                    $"{NormalizeFolder(outputFolder)}/{terrainData.name}.asset"
                );

        AssetDatabase.CreateAsset(
            terrainData,
            assetPath
        );

        GameObject terrainObject =
            Terrain.CreateTerrainGameObject(
                terrainData
            );

        terrainObject.name =
            "Forest Terrain";

        terrainObject.transform.position =
            newTerrainPosition;

        Undo.RegisterCreatedObjectUndo(
            terrainObject,
            "Create Forest Terrain"
        );

        targetTerrain =
            terrainObject
                .GetComponent<Terrain>();

        Selection.activeGameObject =
            terrainObject;

        EditorGUIUtility.PingObject(
            terrainObject
        );

        AssetDatabase.SaveAssets();

        if (
            terrainObject
                .scene
                .IsValid()
        )
        {
            EditorSceneManager.MarkSceneDirty(
                terrainObject.scene
            );
        }

        if (
            generateImmediately &&
            profile != null
        )
        {
            bool oldDuplicateSetting =
                duplicateTerrainDataBeforeGenerate;

            duplicateTerrainDataBeforeGenerate =
                false;

            GenerateTerrainWithoutConfirmation();

            duplicateTerrainDataBeforeGenerate =
                oldDuplicateSetting;
        }

        SceneView.RepaintAll();
    }

    private void GenerateTerrainWithoutConfirmation()
    {
        if (
            targetTerrain == null ||
            targetTerrain.terrainData == null ||
            profile == null
        )
        {
            return;
        }

        try
        {
            ForestTerrainGenerator.GenerateAndApply(
                targetTerrain,
                profile,
                (progress, message) =>
                {
                    EditorUtility.DisplayProgressBar(
                        "Forest Terrain Generator",
                        message,
                        Mathf.Clamp01(progress)
                    );
                }
            );

            EditorUtility.SetDirty(
                targetTerrain.terrainData
            );

            EditorUtility.SetDirty(
                targetTerrain
            );

            TerrainCollider collider =
                targetTerrain
                    .GetComponent<TerrainCollider>();

            if (collider != null)
            {
                collider.terrainData =
                    targetTerrain.terrainData;

                EditorUtility.SetDirty(
                    collider
                );
            }

            AssetDatabase.SaveAssets();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private void CreateProfile()
    {
        string path =
            EditorUtility.SaveFilePanelInProject(
                "Create Forest Terrain Profile",
                "FTP_ForestTerrain",
                "asset",
                "选择Forest Terrain Profile保存位置"
            );

        if (
            string.IsNullOrEmpty(
                path
            )
        )
        {
            return;
        }

        ForestTerrainProfile newProfile =
            CreateInstance<
                ForestTerrainProfile
            >();

        AssetDatabase.CreateAsset(
            newProfile,
            path
        );

        AssetDatabase.SaveAssets();

        profile =
            newProfile;

        Selection.activeObject =
            newProfile;

        EditorGUIUtility.PingObject(
            newProfile
        );
    }

    private static string NormalizeFolder(
        string folder)
    {
        if (
            string.IsNullOrWhiteSpace(
                folder
            )
        )
        {
            return
                "Assets/Vegetation/Generated/Terrain";
        }

        folder =
            folder
                .Replace(
                    "\\",
                    "/"
                )
                .TrimEnd('/');

        if (
            !folder.StartsWith(
                "Assets",
                StringComparison.Ordinal
            )
        )
        {
            folder =
                "Assets/Vegetation/Generated/Terrain";
        }

        return folder;
    }

    private static void EnsureAssetFolder(
        string folder)
    {
        folder =
            NormalizeFolder(
                folder
            );

        if (
            AssetDatabase.IsValidFolder(
                folder
            )
        )
        {
            return;
        }

        string[] parts =
            folder.Split('/');

        string current =
            parts[0];

        for (
            int i = 1;
            i < parts.Length;
            i++
        )
        {
            string next =
                $"{current}/{parts[i]}";

            if (
                !AssetDatabase
                    .IsValidFolder(
                        next
                    )
            )
            {
                AssetDatabase
                    .CreateFolder(
                        current,
                        parts[i]
                    );
            }

            current =
                next;
        }
    }
}