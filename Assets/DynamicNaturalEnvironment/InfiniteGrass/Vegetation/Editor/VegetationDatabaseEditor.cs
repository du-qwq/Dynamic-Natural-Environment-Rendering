using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VegetationDatabase))]
public class VegetationDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        VegetationDatabase database = (VegetationDatabase)target;
        SerializedProperty targetSize = serializedObject.FindProperty("rechunkTargetSize");

        EditorGUILayout.LabelField("Spatial Partition", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.FloatField("Current Chunk Size", database.chunkSize);
        EditorGUILayout.PropertyField(targetSize, new GUIContent("Target Chunk Size"));
        EditorGUILayout.LabelField("Instance Count", database.TotalInstanceCount.ToString("N0"));
        EditorGUILayout.LabelField("Chunk Count", database.ChunkCount.ToString("N0"));

        float requestedSize = targetSize.floatValue;
        bool validSize = VegetationDatabase.IsValidChunkSize(requestedSize);
        if (!validSize)
            EditorGUILayout.HelpBox("Target Chunk Size must be finite and greater than zero.", MessageType.Error);

        EditorGUILayout.Space();
        Editor.DrawPropertiesExcluding(serializedObject, "m_Script", "committedChunkSize", "rechunkTargetSize");
        serializedObject.ApplyModifiedProperties();

        using (new EditorGUI.DisabledScope(!validSize || Mathf.Approximately(requestedSize, database.chunkSize)))
        {
            if (!GUILayout.Button("Rechunk Database")) return;
        }

        if (database.TotalInstanceCount >= 100000 &&
            !EditorUtility.DisplayDialog("Rechunk Vegetation Database",
                $"Rebuild spatial chunks for {database.TotalInstanceCount:N0} instances?",
                "Rechunk", "Cancel")) return;

        if (!database.Rechunk(requestedSize)) return;
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        serializedObject.Update();
    }
}
