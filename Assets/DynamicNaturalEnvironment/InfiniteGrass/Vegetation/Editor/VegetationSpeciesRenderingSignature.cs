using System.Text;
using UnityEngine;

// Editor-only resource identity. Never changes Species IDs or runtime/GPU data.
internal static class VegetationSpeciesRenderingSignature
{
    internal static bool LODMatches(VegetationLODAsset speciesLOD, Mesh mesh, Material[] materials,
        VegetationShaderAdapterProfile profile = null)
    {
        if (mesh == null)
            return speciesLOD == null || (speciesLOD.mesh == null &&
                (speciesLOD.materials == null || speciesLOD.materials.Length == 0));

        if (speciesLOD == null || speciesLOD.mesh != mesh || materials == null ||
            speciesLOD.materials == null || materials.Length != mesh.subMeshCount ||
            speciesLOD.materials.Length != materials.Length) return false;

        // The same Mesh asset guarantees identical submesh topology/order/layout.
        for (int i = 0; i < materials.Length; i++)
            if (!VegetationMaterialAdapter.MaterialMatchesSource(speciesLOD.materials[i], materials[i], profile))
                return false;
        return true;
    }

    internal static string BuildLODKey(Mesh mesh, Material[] materials)
    {
        if (mesh == null) return "None";
        var key = new StringBuilder();
        key.Append(mesh.GetInstanceID()).Append(':').Append(mesh.subMeshCount)
            .Append(':').Append(materials == null ? -1 : materials.Length);
        if (materials != null)
            foreach (Material material in materials)
                key.Append(':').Append(material != null ? material.GetInstanceID() : 0);
        return key.ToString();
    }
}
