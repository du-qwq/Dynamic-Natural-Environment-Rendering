using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class WaterCausticsRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material causticsMaterial;

        [Header("Caustics Mesh")]
        public float meshSize = 1000f;

        [Header("Water")]
        public float waterLevel = 0f;

        [Header("Render Timing")]
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    [SerializeField] private Settings settings = new Settings();

    private WaterCausticsPass causticsPass;
    private Mesh causticsMesh;

    public override void Create()
    {
        causticsMesh = GenerateCausticsMesh(settings.meshSize);

        causticsPass = new WaterCausticsPass(settings, causticsMesh);
        causticsPass.renderPassEvent = settings.renderPassEvent;
        
        causticsPass.ConfigureInput(ScriptableRenderPassInput.Depth);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.causticsMaterial == null)
            return;

        if (causticsPass == null)
            return;

        renderer.EnqueuePass(causticsPass);
    }

    protected override void Dispose(bool disposing)
    {
        if (causticsMesh != null)
        {
            Object.DestroyImmediate(causticsMesh);
            causticsMesh = null;
        }
    }

    private static Mesh GenerateCausticsMesh(float size)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Generated Water Caustics Mesh";

        float halfSize = size * 0.5f;

        Vector3[] vertices =
        {
            new Vector3(-halfSize, 0f, -halfSize),
            new Vector3( halfSize, 0f, -halfSize),
            new Vector3(-halfSize, 0f,  halfSize),
            new Vector3( halfSize, 0f,  halfSize)
        };

        int[] triangles =
        {
            0, 2, 1,
            2, 3, 1
        };

        Vector2[] uvs =
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        return mesh;
    }

    private class WaterCausticsPass : ScriptableRenderPass
    {
        private static readonly string PassName = "Render Water Caustics";
        private static readonly int WaterLevelID = Shader.PropertyToID("_WaterLevel");
        private static readonly int MainLightMatrixID = Shader.PropertyToID("_MainLightMatrix");

        private readonly Settings settings;
        private readonly Mesh mesh;
        private readonly ProfilingSampler profilingSampler;

        public WaterCausticsPass(Settings settings, Mesh mesh)
        {
            this.settings = settings;
            this.mesh = mesh;
            profilingSampler = new ProfilingSampler(PassName);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;

            if (camera == null)
                return;

            if (camera.cameraType == CameraType.Preview)
                return;

            if (settings.causticsMaterial == null || mesh == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get(PassName);

            using (new ProfilingScope(cmd, profilingSampler))
            {
                settings.causticsMaterial.SetFloat(WaterLevelID, settings.waterLevel);
                
                Matrix4x4 lightMatrix;

                if (RenderSettings.sun != null)
                {
                    lightMatrix = RenderSettings.sun.transform.localToWorldMatrix;
                }
                else
                {
                    lightMatrix = Matrix4x4.TRS(
                        Vector3.zero,
                        Quaternion.Euler(45f, 45f, 0f),
                        Vector3.one
                    );
                }

                settings.causticsMaterial.SetMatrix(MainLightMatrixID, lightMatrix);
                
                Vector3 position = camera.transform.position;
                position.y = settings.waterLevel;

                Matrix4x4 matrix = Matrix4x4.TRS(
                    position,
                    Quaternion.identity,
                    Vector3.one
                );

                cmd.DrawMesh(mesh, matrix, settings.causticsMaterial, 0, 0);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}