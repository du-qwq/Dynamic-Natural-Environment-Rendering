using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class VegetationWindController : MonoBehaviour
{
    [Header("Global Wind")]
    [Tooltip("是否启用植被风")]
    public bool enableWind = true;

    [Tooltip("世界空间XZ风向")]
    public Vector2 windDirection = new Vector2(1f, 0.35f);

    [Range(0f, 1f)]
    [Tooltip("基础风摆幅度")]
    public float windStrength = 0.13f;

    [Range(0f, 5f)]
    [Tooltip("基础风速度")]
    public float windSpeed = 0.65f;

    [Range(0.01f, 3f)]
    [Tooltip("基础风空间频率")]
    public float windFrequency = 0.45f;

    [Header("Gust")]
    [Tooltip("是否启用大尺度阵风")]
    public bool enableGust = true;

    [Range(0f, 3f)]
    [Tooltip("阵风附加强度")]
    public float gustStrength = 1.0f;

    [Range(0.005f, 0.3f)]
    [Tooltip("阵风空间尺度，越小范围越大")]
    public float gustScale = 0.035f;

    [Range(0f, 5f)]
    [Tooltip("阵风传播速度")]
    public float gustSpeed = 0.65f;

    [Range(0.5f, 6f)]
    [Tooltip("阵风区域对比度")]
    public float gustContrast = 1.6f;

    [Header("Tip Flutter")]
    [Range(0f, 1f)]
    [Tooltip("草尖细碎摆动强度")]
    public float flutterStrength = 0.10f;

    [Range(0f, 10f)]
    [Tooltip("草尖细碎摆动速度")]
    public float flutterSpeed = 2.2f;

    [Range(0.1f, 8f)]
    [Tooltip("Flutter主要集中在草尖的程度")]
    public float flutterTipPower = 2.5f;

    [Header("Distance Optimization")]
    [Min(0f)]
    [Tooltip("该距离内保持完整风")]
    public float fullWindDistance = 60f;

    [Min(0f)]
    [Tooltip("超过该距离风逐渐关闭")]
    public float zeroWindDistance = 160f;

    [Header("Camera")]
    public Camera targetCamera;

    private static readonly int WindEnabledID = Shader.PropertyToID("_VegetationWindEnabled");
    private static readonly int WindDirectionStrengthID = Shader.PropertyToID("_VegetationWindDirectionStrength");
    private static readonly int WindParamsID = Shader.PropertyToID("_VegetationWindParams");
    private static readonly int WindCameraPositionID = Shader.PropertyToID("_VegetationWindCameraPosition");

    private static readonly int GustEnabledID = Shader.PropertyToID("_VegetationWindGustEnabled");
    private static readonly int GustParamsID = Shader.PropertyToID("_VegetationWindGustParams");

    private static readonly int FlutterParamsID = Shader.PropertyToID("_VegetationWindFlutterParams");

    private void OnEnable()
    {
        ApplyGlobalWind();
    }

    private void Update()
    {
        ApplyGlobalWind();
    }

    private void OnValidate()
    {
        fullWindDistance = Mathf.Max(0f, fullWindDistance);
        zeroWindDistance = Mathf.Max(fullWindDistance + 0.01f, zeroWindDistance);
        ApplyGlobalWind();
    }

    private void OnDisable()
    {
        Shader.SetGlobalFloat(WindEnabledID, 0f);
        Shader.SetGlobalFloat(GustEnabledID, 0f);
    }

    private void ApplyGlobalWind()
    {
        Vector2 direction = windDirection.sqrMagnitude > 0.0001f ? windDirection.normalized : Vector2.right;
        Camera cam = ResolveCamera();
        Vector3 cameraPosition = cam != null ? cam.transform.position : Vector3.zero;

        Shader.SetGlobalFloat(WindEnabledID, enableWind ? 1f : 0f);
        Shader.SetGlobalVector(WindDirectionStrengthID, new Vector4(direction.x, 0f, direction.y, windStrength));
        Shader.SetGlobalVector(WindParamsID, new Vector4(windSpeed, windFrequency, fullWindDistance, zeroWindDistance));
        Shader.SetGlobalVector(WindCameraPositionID, new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 1f));

        Shader.SetGlobalFloat(GustEnabledID, enableGust ? 1f : 0f);
        Shader.SetGlobalVector(GustParamsID, new Vector4(gustStrength, gustScale, gustSpeed, gustContrast));

        Shader.SetGlobalVector(FlutterParamsID, new Vector4(flutterStrength, flutterSpeed, flutterTipPower, 0f));
    }

    private Camera ResolveCamera()
    {
        if (targetCamera != null) return targetCamera;
        if (Application.isPlaying) return Camera.main;

#if UNITY_EDITOR
        if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null) return SceneView.lastActiveSceneView.camera;
#endif

        return Camera.main;
    }
}