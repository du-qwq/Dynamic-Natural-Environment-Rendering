using UnityEngine;

[ExecuteAlways]
public class CloudMacroDistributionController : MonoBehaviour
{
    [Header("宏观云分布")]
    [InspectorName("启用宏观分布")]
    [Tooltip("开启后使用宏观分布控制天空中哪些区域有云，哪些区域保持晴空。")]
    [SerializeField] private bool enableMacroDistribution = true;

    [InspectorName("使用分布贴图")]
    [Tooltip("开启后使用指定的黑白 Coverage Map；关闭时使用现有 Worley 噪声生成程序化宏观分布。")]
    [SerializeField] private bool useCoverageMap = true;

    [InspectorName("宏观分布贴图")]
    [Tooltip("白色区域容易生成云，黑色区域保持晴空。")]
    [SerializeField] private Texture2D coverageMap;

    [InspectorName("总体云量")]
    [Tooltip("控制天空中有多少区域存在云。数值越高，云量越多。")]
    [SerializeField, Range(0f, 1f)] private float coverage = 0.42f;

    [InspectorName("云团世界尺寸")]
    [Tooltip("Coverage Map 在世界空间中重复一次所对应的尺寸，单位为米。数值越大，主体云团越大。")]
    [SerializeField, Min(1000f)] private float macroScale = 30000f;

    [InspectorName("云团边缘柔和度")]
    [Tooltip("控制宏观云区和晴空区之间的过渡宽度。")]
    [SerializeField, Range(0.001f, 0.3f)] private float softness = 0.08f;

    [InspectorName("宏观分布偏移")]
    [Tooltip("移动整个宏观云分布，用于针对主镜头调整云团构图。")]
    [SerializeField] private Vector2 offset = Vector2.zero;

    [InspectorName("随风移动倍率")]
    [Tooltip("控制宏观云团跟随风场移动的速度比例。大尺度天气层建议移动得比较慢。")]
    [SerializeField, Range(0f, 1f)] private float windMultiplier = 0.05f;

    [Header("远处云控制")]
    [InspectorName("启用远处淡出")]
    [Tooltip("让远距离体积云逐渐消失，避免地平线出现大量细小重复云。")]
    [SerializeField] private bool enableFarFade = true;

    [InspectorName("开始淡出距离")]
    [Tooltip("从这个距离开始降低云密度，单位为米。")]
    [SerializeField, Min(0f)] private float farFadeStart = 20000f;

    [InspectorName("完全淡出距离")]
    [Tooltip("到达这个距离时云基本完成淡出，单位为米。")]
    [SerializeField, Min(1f)] private float farFadeEnd = 55000f;

    [InspectorName("远方云保留量")]
    [Tooltip("完全淡出距离处仍然保留多少云。0表示完全消失，0.1表示保留10%。")]
    [SerializeField, Range(0f, 1f)] private float farRetention = 0f;

    [InspectorName("最大云渲染距离")]
    [Tooltip("体积云 Raymarch 的硬距离上限。超过此距离完全停止计算。")]
    [SerializeField, Min(1000f)] private float maxRenderDistance = 60000f;

    private static readonly int MacroEnabledID = Shader.PropertyToID("_CloudMacroEnabled");
    private static readonly int MacroUseMapID = Shader.PropertyToID("_CloudMacroUseMap");
    private static readonly int MacroMapID = Shader.PropertyToID("_CloudMacroCoverageMap");
    private static readonly int MacroCoverageID = Shader.PropertyToID("_CloudMacroCoverage");
    private static readonly int MacroScaleID = Shader.PropertyToID("_CloudMacroScale");
    private static readonly int MacroSoftnessID = Shader.PropertyToID("_CloudMacroSoftness");
    private static readonly int MacroOffsetID = Shader.PropertyToID("_CloudMacroOffset");
    private static readonly int MacroWindMultiplierID = Shader.PropertyToID("_CloudMacroWindMultiplier");

    private static readonly int FarFadeEnabledID = Shader.PropertyToID("_CloudFarFadeEnabled");
    private static readonly int FarFadeStartID = Shader.PropertyToID("_CloudFarFadeStart");
    private static readonly int FarFadeEndID = Shader.PropertyToID("_CloudFarFadeEnd");
    private static readonly int FarRetentionID = Shader.PropertyToID("_CloudFarRetention");
    private static readonly int MaxRenderDistanceID = Shader.PropertyToID("_CloudMaxRenderDistance");

    private void OnEnable() => Apply();
    private void Update() => Apply();
    private void OnValidate() => Apply();

    private void OnDisable()
    {
        Shader.SetGlobalFloat(MacroEnabledID, 0f);
        Shader.SetGlobalFloat(FarFadeEnabledID, 0f);
    }

    private void Apply()
    {
        farFadeEnd = Mathf.Max(farFadeEnd, farFadeStart + 1f);
        maxRenderDistance = Mathf.Max(maxRenderDistance, farFadeEnd);

        Shader.SetGlobalFloat(MacroEnabledID, enableMacroDistribution ? 1f : 0f);
        Shader.SetGlobalFloat(MacroUseMapID, useCoverageMap && coverageMap != null ? 1f : 0f);
        Shader.SetGlobalTexture(MacroMapID, coverageMap != null ? coverageMap : Texture2D.whiteTexture);
        Shader.SetGlobalFloat(MacroCoverageID, coverage);
        Shader.SetGlobalFloat(MacroScaleID, Mathf.Max(macroScale, 1000f));
        Shader.SetGlobalFloat(MacroSoftnessID, Mathf.Max(softness, 0.001f));
        Shader.SetGlobalVector(MacroOffsetID, new Vector4(offset.x, offset.y, 0f, 0f));
        Shader.SetGlobalFloat(MacroWindMultiplierID, windMultiplier);

        Shader.SetGlobalFloat(FarFadeEnabledID, enableFarFade ? 1f : 0f);
        Shader.SetGlobalFloat(FarFadeStartID, farFadeStart);
        Shader.SetGlobalFloat(FarFadeEndID, farFadeEnd);
        Shader.SetGlobalFloat(FarRetentionID, farRetention);
        Shader.SetGlobalFloat(MaxRenderDistanceID, maxRenderDistance);
    }
}