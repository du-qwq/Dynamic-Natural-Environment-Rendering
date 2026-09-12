using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public class PlanarReflectionRenderer : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private Renderer waterRenderer;
    [SerializeField] private Camera targetCamera;

    [Header("反射质量")]
    [SerializeField, Range(256, 2048)] private int reflectionTextureHeight = 512;
    [SerializeField] private LayerMask reflectionMask = ~0;
    [SerializeField] private bool renderShadows = true;

    [Header("裁剪")]
    [SerializeField, Range(0.001f, 0.5f)] private float clipPlaneOffset = 0.05f;

    private Camera reflectionCamera;
    private RenderTexture reflectionTexture;
    private MaterialPropertyBlock propertyBlock;
    private bool requestUnsupportedLogged;

    private static readonly int PlanarReflectionTexID = Shader.PropertyToID("_PlanarReflectionTex");
    private static readonly int PlanarReflectionReadyID = Shader.PropertyToID("_PlanarReflectionReady");

    private void OnEnable()
    {
        AutoAssign();
        EnsureResources();
    }

    private void LateUpdate()
    {
        AutoAssign();
        if (waterRenderer == null || targetCamera == null || !waterRenderer.enabled) return;
        EnsureResources();
        RenderReflection();
    }

    private void OnDisable()
    {
        SetReflectionReady(false);
        ReleaseResources();
    }

    private void OnDestroy()
    {
        SetReflectionReady(false);
        ReleaseResources();
    }

    private void OnValidate()
    {
        reflectionTextureHeight = Mathf.Clamp(reflectionTextureHeight, 256, 2048);
        clipPlaneOffset = Mathf.Max(0.001f, clipPlaneOffset);
        AutoAssign();
        ReleaseReflectionTexture();
    }

    private void AutoAssign()
    {
        if (waterRenderer == null) waterRenderer = GetComponent<Renderer>();
        if (targetCamera == null) targetCamera = Camera.main;
    }

    private void EnsureResources()
    {
        if (targetCamera == null) return;
        EnsureReflectionCamera();
        EnsureReflectionTexture();
        ApplyReflectionTexture();
    }

    private void EnsureReflectionCamera()
    {
        if (reflectionCamera != null) return;

        GameObject cameraObject = new GameObject("Planar Reflection Camera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;

        reflectionCamera = cameraObject.AddComponent<Camera>();
        reflectionCamera.enabled = false;
        reflectionCamera.cameraType = CameraType.Reflection;

        UniversalAdditionalCameraData cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
        cameraData.renderType = CameraRenderType.Base;
        cameraData.renderPostProcessing = false;
        cameraData.requiresColorOption = CameraOverrideOption.Off;
        cameraData.requiresDepthOption = CameraOverrideOption.Off;
        cameraData.renderShadows = renderShadows;
    }

    private void EnsureReflectionTexture()
    {
        float aspect = Mathf.Max(0.1f, targetCamera.aspect);
        int height = reflectionTextureHeight;
        int width = Mathf.Max(256, Mathf.RoundToInt(height * aspect));

        if (reflectionTexture != null && reflectionTexture.width == width && reflectionTexture.height == height) return;

        ReleaseReflectionTexture();

        RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;

        reflectionTexture = new RenderTexture(width, height, 24, format)
        {
            name = "RT_PlanarReflection",
            hideFlags = HideFlags.DontSave,
            useMipMap = false,
            autoGenerateMips = false,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        reflectionTexture.Create();
    }

    private void ApplyReflectionTexture()
    {
        if (waterRenderer == null || reflectionTexture == null) return;
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

        waterRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetTexture(PlanarReflectionTexID, reflectionTexture);
        propertyBlock.SetFloat(PlanarReflectionReadyID, 1f);
        waterRenderer.SetPropertyBlock(propertyBlock);
    }

    private void SetReflectionReady(bool ready)
    {
        if (waterRenderer == null) return;
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

        waterRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(PlanarReflectionReadyID, ready ? 1f : 0f);
        waterRenderer.SetPropertyBlock(propertyBlock);
    }

    private void RenderReflection()
    {
        if (reflectionCamera == null || reflectionTexture == null) return;

        Vector3 planePosition = transform.position;
        Vector3 planeNormal = transform.up.normalized;

        reflectionCamera.CopyFrom(targetCamera);
        reflectionCamera.enabled = false;
        reflectionCamera.cameraType = CameraType.Reflection;
        reflectionCamera.cullingMask = reflectionMask;
        reflectionCamera.targetTexture = reflectionTexture;
        reflectionCamera.useOcclusionCulling = false;
        reflectionCamera.allowMSAA = false;
        reflectionCamera.stereoTargetEye = StereoTargetEyeMask.None;

        UniversalAdditionalCameraData cameraData = reflectionCamera.GetUniversalAdditionalCameraData();
        cameraData.renderType = CameraRenderType.Base;
        cameraData.renderPostProcessing = false;
        cameraData.requiresColorOption = CameraOverrideOption.Off;
        cameraData.requiresDepthOption = CameraOverrideOption.Off;
        cameraData.renderShadows = renderShadows;

        Vector3 reflectedPosition = ReflectPoint(targetCamera.transform.position, planePosition, planeNormal);
        Vector3 reflectedForward = Vector3.Reflect(targetCamera.transform.forward, planeNormal);
        Vector3 reflectedUp = Vector3.Reflect(targetCamera.transform.up, planeNormal);

        reflectionCamera.transform.position = reflectedPosition;
        reflectionCamera.transform.rotation = Quaternion.LookRotation(reflectedForward, reflectedUp);

        Vector4 reflectionPlane = new Vector4(planeNormal.x, planeNormal.y, planeNormal.z, -Vector3.Dot(planeNormal, planePosition));
        Matrix4x4 reflectionMatrix = CalculateReflectionMatrix(reflectionPlane);

        reflectionCamera.worldToCameraMatrix = targetCamera.worldToCameraMatrix * reflectionMatrix;

        Vector4 clipPlane = CameraSpacePlane(reflectionCamera, planePosition, planeNormal, 1f);
        reflectionCamera.projectionMatrix = reflectionCamera.CalculateObliqueMatrix(clipPlane);

        UniversalRenderPipeline.SingleCameraRequest request = new UniversalRenderPipeline.SingleCameraRequest();
        request.destination = reflectionTexture;

        if (!RenderPipeline.SupportsRenderRequest(reflectionCamera, request))
        {
            if (!requestUnsupportedLogged)
            {
                Debug.LogError("当前 URP Renderer 不支持 SingleCameraRequest，Planar Reflection 无法渲染。", this);
                requestUnsupportedLogged = true;
            }
            return;
        }

        bool rendererWasEnabled = waterRenderer.enabled;
        bool oldInvertCulling = GL.invertCulling;

        try
        {
            waterRenderer.enabled = false;
            GL.invertCulling = true;
            RenderPipeline.SubmitRenderRequest(reflectionCamera, request);
        }
        finally
        {
            GL.invertCulling = oldInvertCulling;
            waterRenderer.enabled = rendererWasEnabled;
        }

        ApplyReflectionTexture();
    }

    private Vector4 CameraSpacePlane(Camera camera, Vector3 position, Vector3 normal, float sideSign)
    {
        Vector3 offsetPosition = position + normal * clipPlaneOffset;
        Matrix4x4 worldToCamera = camera.worldToCameraMatrix;
        Vector3 cameraPosition = worldToCamera.MultiplyPoint(offsetPosition);
        Vector3 cameraNormal = worldToCamera.MultiplyVector(normal).normalized * sideSign;
        return new Vector4(cameraNormal.x, cameraNormal.y, cameraNormal.z, -Vector3.Dot(cameraPosition, cameraNormal));
    }

    private static Vector3 ReflectPoint(Vector3 point, Vector3 planePosition, Vector3 planeNormal)
    {
        float distance = Vector3.Dot(planeNormal, point - planePosition);
        return point - planeNormal * distance * 2f;
    }

    private static Matrix4x4 CalculateReflectionMatrix(Vector4 plane)
    {
        Matrix4x4 matrix = Matrix4x4.identity;

        matrix.m00 = 1f - 2f * plane.x * plane.x;
        matrix.m01 = -2f * plane.x * plane.y;
        matrix.m02 = -2f * plane.x * plane.z;
        matrix.m03 = -2f * plane.w * plane.x;

        matrix.m10 = -2f * plane.y * plane.x;
        matrix.m11 = 1f - 2f * plane.y * plane.y;
        matrix.m12 = -2f * plane.y * plane.z;
        matrix.m13 = -2f * plane.w * plane.y;

        matrix.m20 = -2f * plane.z * plane.x;
        matrix.m21 = -2f * plane.z * plane.y;
        matrix.m22 = 1f - 2f * plane.z * plane.z;
        matrix.m23 = -2f * plane.w * plane.z;

        return matrix;
    }

    private void ReleaseReflectionTexture()
    {
        if (reflectionTexture == null) return;

        reflectionTexture.Release();

        if (Application.isPlaying) Destroy(reflectionTexture);
        else DestroyImmediate(reflectionTexture);

        reflectionTexture = null;
    }

    private void ReleaseResources()
    {
        ReleaseReflectionTexture();

        if (reflectionCamera == null) return;

        GameObject cameraObject = reflectionCamera.gameObject;
        reflectionCamera = null;

        if (Application.isPlaying) Destroy(cameraObject);
        else DestroyImmediate(cameraObject);
    }
}