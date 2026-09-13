using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_2023_1_OR_NEWER
[Serializable, VolumeComponentMenu("Sky/Volumetric Clouds (URP)"), SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
#else
[Serializable, VolumeComponentMenuForRenderPipeline("Sky/Volumetric Clouds (URP)", typeof(UniversalRenderPipeline))]
#endif
public class VolumetricClouds : VolumeComponent, IPostProcessComponent
{
    [Header("General")]
    [Tooltip("启用或关闭体积云效果。")]
    public BoolParameter state = new(false, BoolParameter.DisplayType.EnumPopup, overrideState: true);

    [Tooltip("决定云是场景中的局部云，还是作为天空的一部分进行渲染。")]
    public BoolParameter localClouds = new(false, BoolParameter.DisplayType.Checkbox, overrideState: false);

    public CloudPresets cloudPreset
    {
        get { return m_CloudPreset.value; }
        set
        {
            m_CloudPreset.value = value;
            ApplyCurrentCloudPreset();
        }
    }

    [Header("Shape")]
    [InspectorName("Cloud Preset")]
    [SerializeField]
    [Tooltip("选择一套预设的云层形状参数。")]
    private CloudPresetsParameter m_CloudPreset = new(CloudPresets.Cloudy, overrideState: false);

    [Tooltip("控制整个云层体积的总体密度。")]
    public ClampedFloatParameter densityMultiplier = new(0.4f, 0.0f, 1.0f);

    [Tooltip("根据云层内部的高度控制云密度。曲线横轴0代表云底，1代表云顶；纵轴代表密度。")]
    public AnimationCurveParameter densityCurve = new(new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 1.0f), new Keyframe(1.0f, 0.1f)), false);

    [Tooltip("控制大尺度噪声对云层形状的侵蚀程度。数值越高，云层覆盖越少，云块通常也越小。")]
    public ClampedFloatParameter shapeFactor = new(0.9f, 0.0f, 1.0f);

    [Tooltip("控制大尺度云形状噪声的尺寸。")]
    public MinFloatParameter shapeScale = new(5.0f, 0.1f);

    [Tooltip("控制小尺度噪声对云层边缘的侵蚀程度。数值越高，云层边缘越破碎。")]
    public ClampedFloatParameter erosionFactor = new(0.8f, 0.0f, 1.0f);

    [Tooltip("控制小尺度侵蚀噪声的尺寸。")]
    public MinFloatParameter erosionScale = new(107.0f, 1.0f);

    [Tooltip("根据云层内部的高度控制侵蚀程度。曲线横轴0代表云底，1代表云顶；纵轴代表侵蚀强度。")]
    public AnimationCurveParameter erosionCurve = new(new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.1f, 0.9f), new Keyframe(1.0f, 1.0f)), false);

    [Tooltip("根据云层内部的高度控制环境光遮蔽。曲线横轴0代表云底，1代表云顶；纵轴代表遮蔽强度。")]
    public AnimationCurveParameter ambientOcclusionCurve = new(new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 0.4f), new Keyframe(1.0f, 0.0f)), false);

    [Tooltip("启用额外的高频微小侵蚀噪声，为云层边缘增加更细小的结构，但会明显增加性能开销。")]
    public BoolParameter microErosion = new(false, BoolParameter.DisplayType.Checkbox, overrideState: false);

    [Tooltip("控制微小侵蚀噪声对云层边缘的侵蚀强度。")]
    public ClampedFloatParameter microErosionFactor = new(0.5f, 0.0f, 1.0f);

    [Tooltip("控制微小侵蚀噪声的尺寸。")]
    public MinFloatParameter microErosionScale = new(200.0f, 0.1f);

    [Tooltip("控制体积云层底部的海拔高度，单位为米。")]
    public MinFloatParameter bottomAltitude = new(1200.0f, 0.01f);

    [Tooltip("控制体积云层的垂直厚度，单位为米。云顶高度等于底部高度加上该数值。")]
    public MinFloatParameter altitudeRange = new(2000.0f, 100.0f);

    [Tooltip("控制大尺度云形状噪声在世界空间中的偏移。")]
    public Vector3Parameter shapeOffset = new(Vector3.zero);

    [Tooltip("控制云层的曲率，从而影响远处云层与地平线相交的位置。")]
    public ClampedFloatParameter earthCurvature = new(0.0f, 0.0f, 1.0f);

    [Header("Wind")]
    [Tooltip("设置云层整体的水平风速，单位为千米每小时。")]
    public FloatParameter globalSpeed = new(0.0f);

    [Tooltip("控制风向相对于世界空间X轴的旋转角度。")]
    public ClampedFloatParameter globalOrientation = new(0.0f, 0.0f, 360.0f);

    [AdditionalProperty]
    [Tooltip("控制大尺度云形状噪声的风速倍率。")]
    public ClampedFloatParameter shapeSpeedMultiplier = new(1.0f, 0.0f, 1.0f);

    [AdditionalProperty]
    [Tooltip("控制侵蚀噪声的风速倍率。")]
    public ClampedFloatParameter erosionSpeedMultiplier = new(0.25f, 0.0f, 1.0f);

    [AdditionalProperty]
    [Tooltip("控制风对云层高度产生的扭曲强度。")]
    public ClampedFloatParameter altitudeDistortion = new(0.25f, -1.0f, 1.0f);

    [AdditionalProperty]
    [Tooltip("控制大尺度云形状噪声在垂直方向上的移动速度。")]
    public FloatParameter verticalShapeWindSpeed = new(0.0f);

    [AdditionalProperty]
    [Tooltip("控制侵蚀噪声在垂直方向上的移动速度。")]
    public FloatParameter verticalErosionWindSpeed = new(0.0f);

    [Header("Lighting")]
    [Tooltip("控制环境光探针对云层亮度的影响。数值越低，云层整体越暗。")]
    public ClampedFloatParameter ambientLightProbeDimmer = new(1.0f, 0.0f, 2.0f);

    [Tooltip("控制太阳主方向光对云层亮度的影响。数值越低，云层整体越暗。")]
    public ClampedFloatParameter sunLightDimmer = new(1.0f, 0.0f, 2.0f);

    [AdditionalProperty]
    [Tooltip("控制侵蚀参数在计算云层环境光遮蔽时所占的影响程度。")]
    public ClampedFloatParameter erosionOcclusion = new(0.1f, 0.0f, 1.0f);

    [Tooltip("设置云层散射光的颜色偏移。")]
    public ColorParameter scatteringTint = new(new Color(0.0f, 0.0f, 0.0f, 1.0f));

    [AdditionalProperty]
    [Tooltip("控制云层局部散射的强度。数值越高，云层可能表现得更加柔软和具有粉末感。")]
    public ClampedFloatParameter powderEffectIntensity = new(0.25f, 0.0f, 1.0f);

    [AdditionalProperty]
    [Tooltip("控制云层内部近似多重散射的强度。")]
    public ClampedFloatParameter multiScattering = new(0.5f, 0.0f, 1.0f);

    [Header("Shadows")]
    [Tooltip("启用体积云阴影。启用后会覆盖主方向光原本使用的Cookie纹理。")]
    public BoolParameter shadows = new(false);

    [Tooltip("设置体积云阴影纹理的分辨率。")]
    public CloudShadowResolutionParameter shadowResolution = new(CloudShadowResolution.Medium256);

    [AdditionalProperty]
    [Tooltip("设置云影围绕相机覆盖的世界空间范围。")]
    public MinFloatParameter shadowDistance = new(8000.0f, 1000.0f);

    [AdditionalProperty]
    [Tooltip("控制体积云阴影的不透明度。")]
    public ClampedFloatParameter shadowOpacity = new(1.0f, 0.0f, 1.0f);

    [AdditionalProperty]
    [Tooltip("控制处于云影纹理覆盖范围之外时使用的备用阴影不透明度。")]
    public ClampedFloatParameter shadowOpacityFallback = new(0.0f, 0.0f, 1.0f);

    [Header("Quality")]
    [Tooltip("控制历史帧累积强度。数值越高噪点越少，但可能产生更明显的拖影。")]
    public ClampedFloatParameter temporalAccumulationFactor = new(0.95f, 0.0f, 1.0f);

    [Tooltip("控制体积云的感知混合强度。该参数通常应当作为开关使用，只设置为0或1。")]
    public ClampedFloatParameter perceptualBlending = new(1.0f, 0.0f, 1.0f);

    [Tooltip("控制计算云层透射率时使用的主要射线步进次数。数值越高噪点越少、观察距离越远，但性能开销也越大。")]
    public ClampedIntParameter numPrimarySteps = new(32, 24, 256);

    [Tooltip("控制计算云层光照和自阴影时使用的步进次数。数值越高光照越平滑，但性能开销也越大。")]
    public ClampedIntParameter numLightSteps = new(2, 1, 16);

    [Tooltip("控制云层靠近相机近裁剪面时的淡入模式。")]
    public CloudFadeInParameter fadeInMode = new(CloudFadeInMode.Automatic);

    [Tooltip("在手动淡入模式下，控制云层开始出现的最小距离。")]
    public MinFloatParameter fadeInStart = new(0.0f, 0.0f);

    [Tooltip("在手动淡入模式下，控制云层从开始出现到达到完整密度所需要的距离。")]
    public MinFloatParameter fadeInDistance = new(5000.0f, 0.01f);

    public bool IsActive() => state.value;

    public bool IsTileCompatible() => false;

    public enum CloudPresets
    {
        Sparse,
        Cloudy,
        Overcast,
        Stormy,
        Custom
    }

    private static readonly AnimationCurve s_SparseDensityCurve = new(new Keyframe(0f, 0f), new Keyframe(0.05f, 1.0f), new Keyframe(0.75f, 1.0f), new Keyframe(1.0f, 0.0f));
    private static readonly AnimationCurve s_SparseErosionCurve = new(new Keyframe(0f, 1f), new Keyframe(0.1f, 0.9f), new Keyframe(1.0f, 1.0f));
    private static readonly AnimationCurve s_SparseAmbientOcclusionCurve = new(new Keyframe(0f, 0f), new Keyframe(0.25f, 0.5f), new Keyframe(1.0f, 0.0f));

    private static readonly AnimationCurve s_CloudyDensityCurve = new(new Keyframe(0f, 0f), new Keyframe(0.15f, 1.0f), new Keyframe(1.0f, 0.1f));
    private static readonly AnimationCurve s_CloudyErosionCurve = new(new Keyframe(0f, 1f), new Keyframe(0.1f, 0.9f), new Keyframe(1.0f, 1.0f));
    private static readonly AnimationCurve s_CloudyAmbientOcclusionCurve = new(new Keyframe(0f, 0f), new Keyframe(0.25f, 0.4f), new Keyframe(1.0f, 0.0f));

    private static readonly AnimationCurve s_OvercastDensityCurve = new(new Keyframe(0f, 0f), new Keyframe(0.05f, 1.0f), new Keyframe(0.9f, 0.0f), new Keyframe(1.0f, 0.0f));
    private static readonly AnimationCurve s_OvercastErosionCurve = new(new Keyframe(0f, 1f), new Keyframe(0.1f, 0.9f), new Keyframe(1.0f, 1.0f));
    private static readonly AnimationCurve s_OvercastAmbientOcclusionCurve = new(new Keyframe(0f, 0f), new Keyframe(1.0f, 0.0f));

    private static readonly AnimationCurve s_StormyDensityCurve = new(new Keyframe(0f, 0f), new Keyframe(0.037f, 1.0f), new Keyframe(0.6f, 1.0f), new Keyframe(1.0f, 0.0f));
    private static readonly AnimationCurve s_StormyErosionCurve = new(new Keyframe(0f, 1f), new Keyframe(0.05f, 0.8f), new Keyframe(0.2438f, 0.9498f), new Keyframe(0.5f, 1.0f), new Keyframe(0.93f, 0.9268f), new Keyframe(1.0f, 1.0f));
    private static readonly AnimationCurve s_StormyAmbientOcclusionCurve = new(new Keyframe(0f, 0f), new Keyframe(0.1f, 0.4f), new Keyframe(1.0f, 0.0f));

    private void ApplyCurrentCloudPreset()
    {
        bool microDetails = microErosion.value;

        switch (cloudPreset)
        {
            case CloudPresets.Sparse:
            {
                densityMultiplier.value = 0.4f;

                if (microDetails)
                {
                    shapeFactor.value = 0.925f;
                    shapeScale.value = 5.0f;
                    erosionFactor.value = 0.85f;
                    erosionScale.value = 75.0f;
                    microErosionFactor.value = 0.65f;
                    microErosionScale.value = 300.0f;
                }
                else
                {
                    shapeFactor.value = 0.95f;
                    shapeScale.value = 5.0f;
                    erosionFactor.value = 0.8f;
                    erosionScale.value = 107.0f;
                }

                densityCurve.value = s_SparseDensityCurve;
                erosionCurve.value = s_SparseErosionCurve;
                ambientOcclusionCurve.value = s_SparseAmbientOcclusionCurve;
                bottomAltitude.value = 3000.0f;
                altitudeRange.value = 1000.0f;
                break;
            }

            case CloudPresets.Cloudy:
            {
                densityMultiplier.value = 0.4f;

                if (microDetails)
                {
                    shapeFactor.value = 0.875f;
                    shapeScale.value = 5.0f;
                    erosionFactor.value = 0.9f;
                    erosionScale.value = 75.0f;
                    microErosionFactor.value = 0.65f;
                    microErosionScale.value = 300.0f;
                }
                else
                {
                    shapeFactor.value = 0.9f;
                    shapeScale.value = 5.0f;
                    erosionFactor.value = 0.8f;
                    erosionScale.value = 107.0f;
                }

                densityCurve.value = s_CloudyDensityCurve;
                erosionCurve.value = s_CloudyErosionCurve;
                ambientOcclusionCurve.value = s_CloudyAmbientOcclusionCurve;
                bottomAltitude.value = 1200.0f;
                altitudeRange.value = 2000.0f;
                break;
            }

            case CloudPresets.Overcast:
            {
                densityMultiplier.value = 0.3f;

                if (microDetails)
                {
                    shapeFactor.value = 0.45f;
                    shapeScale.value = 5.0f;
                    erosionFactor.value = 0.7f;
                    erosionScale.value = 75.0f;
                    microErosionFactor.value = 0.5f;
                    microErosionScale.value = 300.0f;
                }
                else
                {
                    shapeFactor.value = 0.5f;
                    shapeScale.value = 5.0f;
                    erosionFactor.value = 0.5f;
                    erosionScale.value = 107.0f;
                }

                densityCurve.value = s_OvercastDensityCurve;
                erosionCurve.value = s_OvercastErosionCurve;
                ambientOcclusionCurve.value = s_OvercastAmbientOcclusionCurve;
                bottomAltitude.value = 1500.0f;
                altitudeRange.value = 2500.0f;
                break;
            }

            case CloudPresets.Stormy:
            {
                densityMultiplier.value = 0.35f;

                if (microDetails)
                {
                    shapeFactor.value = 0.825f;
                    shapeScale.value = 5.0f;
                    erosionFactor.value = 0.9f;
                    erosionScale.value = 75.0f;
                    microErosionFactor.value = 0.6f;
                    microErosionScale.value = 300.0f;
                }
                else
                {
                    shapeFactor.value = 0.85f;
                    shapeScale.value = 5.0f;
                    erosionFactor.value = 0.75f;
                    erosionScale.value = 107.0f;
                }

                densityCurve.value = s_StormyDensityCurve;
                erosionCurve.value = s_StormyErosionCurve;
                ambientOcclusionCurve.value = s_StormyAmbientOcclusionCurve;
                bottomAltitude.value = 1000.0f;
                altitudeRange.value = 5000.0f;
                break;
            }

            case CloudPresets.Custom:
                break;
        }
    }

    [Serializable]
    public sealed class CloudPresetsParameter : VolumeParameter<CloudPresets>
    {
        public CloudPresetsParameter(CloudPresets value, bool overrideState = false) : base(value, overrideState) { }
    }

    public enum CloudFadeInMode
    {
        Automatic,
        Manual
    }

    [Serializable]
    public sealed class CloudFadeInParameter : VolumeParameter<CloudFadeInMode>
    {
        public CloudFadeInParameter(CloudFadeInMode value, bool overrideState = false) : base(value, overrideState) { }
    }

    public enum CloudShadowResolution
    {
        VeryLow64 = 64,
        Low128 = 128,
        Medium256 = 256,
        High512 = 512,
        Ultra1024 = 1024
    }

    [Serializable]
    public sealed class CloudShadowResolutionParameter : VolumeParameter<CloudShadowResolution>
    {
        public CloudShadowResolutionParameter(CloudShadowResolution value, bool overrideState = false) : base(value, overrideState) { }
    }
}