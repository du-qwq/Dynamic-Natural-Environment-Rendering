Shader "ANGRYMESH/Stylized Pack/Tree Leaf VegetationIndirect"
{
    Properties
    {
        [Header(Base)][Toggle(_ENABLEFLIPNORMALS_ON)] _EnableFlipNormals("Enable Flip Normals",Float)=0
        [Toggle(_ENABLEGLANCINGANGLECUT_ON)] _EnableGlancingAngleCut("Enable Glancing Angle Cut [ForwardOnly]",Float)=0
        _BaseGlancingAngleCut("Base Glancing Angle Cut",Range(0,1))=0.8
        _CutOff("Base Opacity Cutoff",Range(0,1))=0.3
        [HDR]_BaseAlbedoColor("Base Albedo Color",Color)=(0.5019608,0.5019608,0.5019608,1)
        _BaseAlbedoBrightness("Base Albedo Brightness",Range(0,5))=1
        _BaseAlbedoDesaturation("Base Albedo Desaturation",Range(0,1))=0
        _BaseNormalIntensity("Base Normal Intensity",Range(0,5))=1
        _BaseSmoothnessMin("Base Smoothness Min",Range(0,5))=0
        _BaseSmoothnessMax("Base Smoothness Max",Range(0,5))=1
        _BaseTreeAOIntensity("Base Tree AO Intensity",Range(0,1))=0.5
        _BaseLerpBetweenColorandTexture("Base Lerp Between Color and Texture",Range(0,1))=1
        [NoScaleOffset]_BaseAlbedo("Base Albedo",2D)="gray"{}
        [NoScaleOffset]_BaseNormal("Base Normal",2D)="bump"{}
        [NoScaleOffset]_BaseSSE("Base SSE",2D)="white"{}

        [HDR][Header(Base SSS)]_BaseSSSColor("Base SSS Color",Color)=(1,1,1,1)
        _BaseSSSIntensity("Base SSS Intensity",Range(0,50))=1
        _BaseSSSAOInfluence("Base SSS AO Influence",Range(0,1))=0.8
        _BaseSSSNormalDistortion("Base SSS Normal Distortion",Range(0,1))=0.5
        _BaseSSSScattering("Base SSS Scattering",Range(1,50))=2
        _BaseSSSDirect("Base SSS Direct",Range(0,1))=0.9
        _BaseSSSAmbiet("Base SSS Ambiet",Range(0,1))=0.1
        _BaseSSSShadow("Base SSS Shadow",Range(0,1))=0.9

        [HDR][Header(Base Emissive)]_BaseEmissiveColor("Base Emissive Color",Color)=(0,0,0,0)
        _BaseEmissiveIntensity("Base Emissive Intensity",Range(0,50))=1
        _BaseEmissiveMaskContrast("Base Emissive Mask Contrast",Range(0,50))=1
        _BaseEmissiveAOMask("Base Emissive AO Mask",Range(0,1))=0

        [Header(Base Gradient Color)][Toggle(_ENABLEGRADIENTCOLOR_ON)] _EnableGradientColor("Enable Gradient Color",Float)=0
        [HDR]_GradientColor("Gradient Color",Color)=(0.5019608,0.5019608,0.5019608,1)
        _GradientColorIntensity("Gradient Color Intensity",Range(0,1))=1
        _GradientColorOffset("Gradient Color Offset",Range(0,5))=1
        _GradientColorContrast("Gradient Color Contrast",Range(0,30))=1
        [IntRange]_GradientColorInvertMask("Gradient Color Invert Mask",Range(0,1))=0

        [Header(Base Second Color)][Toggle(_ENABLESECONDCOLOR_ON)] _EnableSecondColor("Enable Second Color",Float)=0
        [HDR]_SecondColor("Second Color",Color)=(0.5019608,0.5019608,0.5019608,1)
        _SecondColorIntensity("Second Color Intensity",Range(0,1))=1
        _SecondColorOffset("Second Color Offset",Range(0,3))=1
        _SecondColorContrast("Second Color Contrast",Range(0,30))=1
        [IntRange]_SecondColorInvertMask("Second Color Invert Mask",Range(0,1))=0

        [Header(Base Tint Color)][Toggle(_ENABLETINTCOLOR_ON)] _EnableTintColor("Enable Tint Color",Float)=0
        [HDR]_TintColor1("Tint Color 1",Color)=(0.5019608,0.5019608,0.5019608,0)
        [HDR]_TintColor2("Tint Color 2",Color)=(0.5019608,0.5019608,0.5019608,0)
        _TintNoiseIntensity("Tint Noise Intensity",Range(0,1))=1
        _TintNoiseOffset("Tint Noise Offset",Range(0,10))=1
        _TintNoiseContrast("Tint Noise Contrast",Range(0,10))=1

        [Header(Top Layer)][Toggle(_ENABLETOPLAYERBLEND_ON)] _EnableTopLayerBlend("Enable Top Layer Blend",Float)=0
        [HDR]_TopLayerColor("Top Layer Color",Color)=(0.7764706,0.8392157,0.9490196,1)
        [HDR]_TopLayerSSSColor("Top Layer SSS Color",Color)=(0.7843137,0.9215686,1,1)
        _TopLayerSSSIntensity("Top Layer SSS Intensity",Range(0,1))=0.5
        _TopLayerSmoothnessIntensity("Top Layer Smoothness Intensity",Range(0,5))=0
        _TopLayerIntensity("Top Layer Intensity",Range(0,1))=1
        _TopLayerOffset("Top Layer Offset",Range(0,1))=0.5
        _TopLayerContrast("Top Layer Contrast",Range(0,30))=10
        _TopLayerAOMask("Top Layer AO Mask",Range(0,10))=1
        [Toggle(_ENABLEWORLDPROJECTION_ON)] _EnableWorldProjection("Enable World Projection",Float)=1
        [Toggle(_ENABLEBACKFACEPROJECTION_ON)] _EnableBackfaceProjection("Enable Backface Projection",Float)=1

        [Header(Wind)][Toggle(_ENABLEWIND_ON)] _EnableWind("Enable Wind",Float)=1
        _WindLeafAmplitude("Wind Leaf Amplitude",Range(0,2))=1
        _WindLeafSpeed("Wind Leaf Speed",Range(0,2))=1
        _WindLeafScale("Wind Leaf Scale",Range(0,2))=1
        _WindLeafTurbulence("Wind Leaf Turbulence",Range(0,2))=1
        _WindLeafOffset("Wind Leaf Offset",Range(0,2))=1
        [Header(Wind (Vegetation Wind Controller))]_WindTreeFlexibility("Wind Tree Flexibility",Range(0,2))=1
        _WindTreeBaseRigidity("Wind Tree Base Rigidity",Range(0,5))=2.5
        _WindTreeSpeedMultiplier("Wind Tree Speed Multiplier",Range(0,2))=1
        _WindTreeFrequencyMultiplier("Wind Tree Frequency Multiplier",Range(0.05,3))=1
        [Toggle(_ENABLESTATICMESHSUPPORT_ON)] _EnableStaticMeshSupport("Enable Static Mesh Support",Float)=0
        [Enum(Normal,0,Base Color,1,Shadow Attenuation,2,Direct Lighting,3,Indirect Lighting,4,Final Lighting,5)] _LightingDebugMode("Lighting Debug Mode",Float)=0
        [HideInInspector]_texcoord("",2D)="white"{}
    }

    SubShader
    {
        Tags{"RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Lit"}
        LOD 300
        Cull Off
        ZWrite On
        ZTest LEqual

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
        #include "Assets/DynamicNaturalEnvironment/InfiniteGrass/Vegetation/Shaders/VegetationIndirectCommon.hlsl"
        #include "Assets/DynamicNaturalEnvironment/InfiniteGrass/Vegetation/Shaders/VegetationLOD.hlsl"
        #include "Assets/DynamicNaturalEnvironment/InfiniteGrass/Vegetation/Shaders/VegetationDepthNormals.hlsl"
        #include "Assets/DynamicNaturalEnvironment/InfiniteGrass/Vegetation/Shaders/VegetationReceiveShadows.hlsl"
        #include "Assets/DynamicNaturalEnvironment/InfiniteGrass/Vegetation/Shaders/VegetationAdditionalLights.hlsl"

        TEXTURE2D(_BaseAlbedo); SAMPLER(sampler_BaseAlbedo);
        TEXTURE2D(_BaseNormal); SAMPLER(sampler_BaseNormal);
        TEXTURE2D(_BaseSSE); SAMPLER(sampler_BaseSSE);

        CBUFFER_START(UnityPerMaterial)
        half4 _BaseAlbedoColor,_BaseSSSColor,_BaseEmissiveColor,_GradientColor,_SecondColor,_TintColor1,_TintColor2,_TopLayerColor,_TopLayerSSSColor;
        half _BaseGlancingAngleCut,_CutOff,_BaseAlbedoBrightness,_BaseAlbedoDesaturation,_BaseNormalIntensity,_BaseSmoothnessMin,_BaseSmoothnessMax,_BaseTreeAOIntensity,_BaseLerpBetweenColorandTexture;
        half _BaseSSSIntensity,_BaseSSSAOInfluence,_BaseSSSNormalDistortion,_BaseSSSScattering,_BaseSSSDirect,_BaseSSSAmbiet,_BaseSSSShadow;
        half _BaseEmissiveIntensity,_BaseEmissiveMaskContrast,_BaseEmissiveAOMask;
        half _GradientColorIntensity,_GradientColorOffset,_GradientColorContrast,_GradientColorInvertMask,_SecondColorIntensity,_SecondColorOffset,_SecondColorContrast,_SecondColorInvertMask,_TintNoiseIntensity,_TintNoiseOffset,_TintNoiseContrast;
        half _TopLayerSSSIntensity,_TopLayerSmoothnessIntensity,_TopLayerIntensity,_TopLayerOffset,_TopLayerContrast,_TopLayerAOMask;
        half _WindLeafAmplitude,_WindLeafSpeed,_WindLeafScale,_WindLeafTurbulence,_WindLeafOffset,_WindTreeFlexibility,_WindTreeBaseRigidity,_WindTreeSpeedMultiplier,_WindTreeFrequencyMultiplier,_LightingDebugMode;
        CBUFFER_END

        // Original Stylized Pack global controls. These are intentionally kept outside UnityPerMaterial:
        // the source package drives them through Shader.SetGlobal* / global settings.
        half ASP_GlobalTreeAO;
        half ASP_GlobalTreeSSSIntensity;
        half ASP_GlobalTreeSSSAOInfluence;
        half ASP_GlobalTreeSSSDistance;
        half ASP_GlobalTintNoiseUVScale;
        half ASP_GlobalTintNoiseContrast;
        half ASP_GlobalTintNoiseIntensity;
        half ASPT_TopLayerOffset;
        half ASPT_TopLayerContrast;
        half ASPT_TopLayerIntensity;
        half ASPT_TopLayerHeightStart;
        half ASPT_TopLayerHeightFade;

        half3 OverlayBlend(half3 src,half3 dst){return saturate(lerp(2.0h*dst*src,1.0h-2.0h*(1.0h-dst)*(1.0h-src),step(0.5h,dst)));}
        float3 TransformVegetationTangentToWorld(float3 tangentOS,uint instanceID){return normalize(mul((float3x3)GetVegetationObjectToWorld(instanceID),tangentOS));}
        float GetVegetationTransformSign(uint instanceID){float3x3 m=(float3x3)GetVegetationObjectToWorld(instanceID);return determinant(m)<0.0?-1.0:1.0;}
        float GetTreeWindMask(float2 uv2){return pow(saturate(abs(uv2.y)),max((float)_WindTreeBaseRigidity,0.0001));}

        float3 ApplyLeafWindWS(float3 positionWS,float3 pivotWS,float2 uv2,half4 color)
        {
        #ifndef _ENABLEWIND_ON
            return positionWS;
        #else
            float treeMask=GetTreeWindMask(uv2);
            VegetationWindSample treeWind=EvaluateVegetationWind(positionWS,pivotWS,treeMask,max((float)_WindTreeFlexibility,0.0),max((float)_WindTreeSpeedMultiplier,0.01),max((float)_WindTreeFrequencyMultiplier,0.01),0.0,0.0,0.0);
            positionWS=ApplyVegetationWindSample(positionWS,treeWind);

            float leafHeightMask=saturate(abs(uv2.y));
            float leafMovementMask=saturate(max((float)color.b,(float)color.g)*leafHeightMask);
            float phaseOffset=positionWS.y*max((float)_WindLeafScale,0.01)*3.0;
            float flutterPhase=dot(positionWS.xz,float2(0.73,1.17))*max((float)_WindLeafScale,0.01);
            VegetationWindSample leafWind=EvaluateVegetationWind(positionWS,pivotWS,leafMovementMask,max((float)_WindLeafAmplitude,0.0),max((float)_WindLeafSpeed,0.01),max((float)_WindLeafScale,0.01),phaseOffset,flutterPhase,max((float)_WindLeafTurbulence,0.0));

            positionWS.xz+=leafWind.direction*leafWind.bend*saturate(color.g+color.b*0.35h);
            positionWS.xz+=leafWind.sideDirection*leafWind.flutter*color.b;
            positionWS.y+=leafWind.flutter*0.45*color.b;

            float directionalPush=(float)_WindLeafOffset*_VegetationWindDirectionStrength.w*leafWind.distanceFade*color.g*leafHeightMask*0.5;
            positionWS.xz+=leafWind.direction*directionalPush;
            return positionWS;
        #endif
        }

        float3 ApplyLeafForwardWindWS(float3 positionWS,float3 pivotWS,float2 uv2,half4 color)
        {
        #ifndef _ENABLEWIND_ON
            return positionWS;
        #else
            float quality=GetVegetationForwardWindQuality(pivotWS);
            if(quality<0.5)return positionWS;
            if(quality>2.5)return ApplyLeafWindWS(positionWS,pivotWS,uv2,color);
            float treeMask=GetTreeWindMask(uv2);
            float treeStrength=max((float)_WindTreeFlexibility,0.0);
            float treeSpeed=max((float)_WindTreeSpeedMultiplier,0.01);
            float treeFrequency=max((float)_WindTreeFrequencyMultiplier,0.01);
            if(quality>1.5)
            {
                positionWS=ApplyVegetationShadowWind(positionWS,pivotWS,treeMask,treeStrength,treeSpeed,treeFrequency);
                float leafHeightMask=saturate(abs(uv2.y));
                float leafMovementMask=saturate(max((float)color.b,(float)color.g)*leafHeightMask);
                float leafStrength=max((float)_WindLeafAmplitude,0.0)*saturate((float)color.g+(float)color.b*0.35);
                positionWS=ApplyVegetationShadowWind(positionWS,pivotWS,leafMovementMask,leafStrength,max((float)_WindLeafSpeed,0.01),max((float)_WindLeafScale,0.01));
                return positionWS;
            }
            return ApplyVegetationShadowMainBend(positionWS,pivotWS,treeMask,treeStrength,treeSpeed,treeFrequency);
        #endif
        }

        float3 ApplyLeafShadowWindWS(float3 positionWS,float3 pivotWS,float2 uv2,half4 color)
        {
        #ifndef _ENABLEWIND_ON
            return positionWS;
        #else
            float quality=GetVegetationShadowWindQuality(pivotWS);
            if(quality<0.5)return positionWS;
            if(quality>2.5)return ApplyLeafWindWS(positionWS,pivotWS,uv2,color);
            float treeMask=GetTreeWindMask(uv2);
            float treeStrength=max((float)_WindTreeFlexibility,0.0);
            float treeSpeed=max((float)_WindTreeSpeedMultiplier,0.01);
            float treeFrequency=max((float)_WindTreeFrequencyMultiplier,0.01);
            if(quality>1.5)
            {
                positionWS=ApplyVegetationShadowWind(positionWS,pivotWS,treeMask,treeStrength,treeSpeed,treeFrequency);
                float leafHeightMask=saturate(abs(uv2.y));
                float leafMovementMask=saturate(max((float)color.b,(float)color.g)*leafHeightMask);
                float leafStrength=max((float)_WindLeafAmplitude,0.0)*saturate((float)color.g+(float)color.b*0.35);
                positionWS=ApplyVegetationShadowWind(positionWS,pivotWS,leafMovementMask,leafStrength,max((float)_WindLeafSpeed,0.01),max((float)_WindLeafScale,0.01));
                return positionWS;
            }
            return ApplyVegetationShadowMainBend(positionWS,pivotWS,treeMask,treeStrength,treeSpeed,treeFrequency);
        #endif
        }

        struct Attributes
        {
            float4 positionOS:POSITION;
            half3 normalOS:NORMAL;
            half4 tangentOS:TANGENT;
            float2 uv:TEXCOORD0;
            float2 staticLightmapUV:TEXCOORD1;
            float2 uv2:TEXCOORD2;
            half4 color:COLOR;
            uint instanceID:SV_InstanceID;
        };

        struct Varyings
        {
            float4 positionCS:SV_POSITION;
            nointerpolation float lodDistance:TEXCOORD15;
            float3 positionWS:TEXCOORD0;
            half3 normalWS:TEXCOORD1;
            half4 tangentWS:TEXCOORD2;
            float2 uv:TEXCOORD3;
            float2 uv2:TEXCOORD4;
            half4 color:COLOR;
            half fogFactor:TEXCOORD5;
            half tintRandom:TEXCOORD6;
            DECLARE_LIGHTMAP_OR_SH(staticLightmapUV,vertexSH,7);
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings Vert(Attributes IN)
        {
            Varyings O=(Varyings)0;
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(O);
            float3 pivotWS=GetVegetationPivotWS(IN.instanceID);
            O.lodDistance=GetVegetationLODDistance(pivotWS);
            float3 positionWS=TransformVegetationPositionToWorld(IN.positionOS.xyz,IN.instanceID);
            half3 normalWS=TransformVegetationNormalToWorld(IN.normalOS,IN.instanceID);
            half3 tangentWS=TransformVegetationTangentToWorld(IN.tangentOS.xyz,IN.instanceID);
            positionWS=ApplyLeafForwardWindWS(positionWS,pivotWS,IN.uv2,IN.color);
            O.positionWS=positionWS;
            O.positionCS=TransformWorldToHClip(positionWS);
            O.normalWS=normalWS;
            O.tangentWS=half4(tangentWS,IN.tangentOS.w*GetVegetationTransformSign(IN.instanceID));
            O.uv=IN.uv;
            O.uv2=IN.uv2;
            O.color=IN.color;
            O.fogFactor=ComputeFogFactor(O.positionCS.z);
            // Original shader used GetObjectToWorldMatrix translation here. For indirect instances pivotWS is the equivalent object pivot.
            O.tintRandom=(half)frac(((pivotWS.x+pivotWS.y+pivotWS.z)*1.23h)*(_TintNoiseOffset*ASP_GlobalTintNoiseUVScale));
            OUTPUT_LIGHTMAP_UV(IN.staticLightmapUV,unity_LightmapST,O.staticLightmapUV);
            OUTPUT_SH(O.normalWS,O.vertexSH);
            return O;
        }

        void EvaluateLeaf(Varyings IN,half faceSign,out half3 albedo,out half3 normalTS,out half3 emission,out half smoothness,out half occlusion,out half alpha,out half3 sss)
        {
            half4 aTex=SAMPLE_TEXTURE2D(_BaseAlbedo,sampler_BaseAlbedo,IN.uv);
            half3 tex=aTex.rgb;
            half lum=dot(tex,half3(0.299h,0.587h,0.114h));
            tex=lerp(tex,lum.xxx,_BaseAlbedoDesaturation)*_BaseAlbedoBrightness;
            half3 base=lerp(_BaseAlbedoColor.rgb,OverlayBlend(tex,_BaseAlbedoColor.rgb),_BaseLerpBetweenColorandTexture);
            occlusion=lerp(1.0h,IN.color.a,saturate(_BaseTreeAOIntensity*ASP_GlobalTreeAO));
            base*=occlusion;
            half3 stepAlb=base;

        #ifdef _ENABLEGRADIENTCOLOR_ON
            half gm=saturate(pow(abs((1.0h-IN.uv2.y)*_GradientColorOffset),_GradientColorContrast));
            gm=lerp(gm,1.0h-gm,_GradientColorInvertMask)*_GradientColorIntensity;
            stepAlb=lerp(stepAlb,OverlayBlend(base,_GradientColor.rgb),gm);
        #endif

        #ifdef _ENABLESECONDCOLOR_ON
            half sm=saturate(pow(abs((1.0h-IN.color.a)*_SecondColorOffset),_SecondColorContrast+2.0h));
            sm=lerp(sm,1.0h-sm,_SecondColorInvertMask)*_SecondColorIntensity;
            stepAlb=lerp(stepAlb,OverlayBlend(base,_SecondColor.rgb),sm);
        #endif

        #ifdef _ENABLETINTCOLOR_ON
            half tm=saturate(round(IN.tintRandom*(_TintNoiseContrast*ASP_GlobalTintNoiseContrast)));
            half3 tc=lerp(_TintColor1.rgb,_TintColor2.rgb,tm);
            stepAlb=lerp(stepAlb,OverlayBlend(stepAlb,tc),_TintNoiseIntensity*ASP_GlobalTintNoiseIntensity);
        #endif

            normalTS=UnpackNormalScale(SAMPLE_TEXTURE2D(_BaseNormal,sampler_BaseNormal,IN.uv),_BaseNormalIntensity);
        #ifdef _ENABLEFLIPNORMALS_ON
            normalTS.z*=faceSign;
        #endif

            half3 b=cross(IN.normalWS,IN.tangentWS.xyz)*IN.tangentWS.w;
            half3 wN=normalize(IN.tangentWS.xyz*normalTS.x+b*normalTS.y+IN.normalWS*normalTS.z);
            half proj=wN.y;
        #ifndef _ENABLEWORLDPROJECTION_ON
            proj=dot(normalTS,half3(0.299h,0.587h,0.114h));
        #endif

            half top=saturate(pow(abs(saturate(proj)+_TopLayerOffset*ASPT_TopLayerOffset),max(_TopLayerContrast*ASPT_TopLayerContrast,1e-4h))*(_TopLayerIntensity*ASPT_TopLayerIntensity))*saturate((IN.positionWS.y-ASPT_TopLayerHeightStart)/max(abs(ASPT_TopLayerHeightFade),1e-4h));
            top=lerp(top,0.0h,saturate(pow(abs(1.0h-IN.color.a),max(_TopLayerContrast*ASPT_TopLayerContrast,1e-4h))*_TopLayerAOMask));
        #ifndef _ENABLEBACKFACEPROJECTION_ON
            top*=saturate(faceSign);
        #endif

            albedo=stepAlb;
        #ifdef _ENABLETOPLAYERBLEND_ON
            albedo=lerp(stepAlb,_TopLayerColor.rgb,top);
        #endif

            half4 sse=SAMPLE_TEXTURE2D(_BaseSSE,sampler_BaseSSE,IN.uv);
            half3 baseEm=_BaseEmissiveColor.rgb*pow(abs(sse.b),_BaseEmissiveMaskContrast)*_BaseEmissiveIntensity;
            baseEm=lerp(baseEm,baseEm*IN.color.a,_BaseEmissiveAOMask);
            emission=baseEm;
        #ifdef _ENABLETOPLAYERBLEND_ON
            emission=lerp(baseEm,0.0h,top);
        #endif

            smoothness=saturate(lerp(_BaseSmoothnessMin,_BaseSmoothnessMax,sse.r));
        #ifdef _ENABLETOPLAYERBLEND_ON
            smoothness=saturate(lerp(smoothness,_TopLayerSmoothnessIntensity,top));
        #endif

            alpha=aTex.a;
        #ifdef _ENABLEGLANCINGANGLECUT_ON
            half3 geomN=normalize(cross(ddy(IN.positionWS),ddx(IN.positionWS)));
            half v=abs(dot(geomN,GetWorldSpaceNormalizeViewDir(IN.positionWS)));
            alpha*=lerp(1.0h,v,_BaseGlancingAngleCut);
        #endif

            half sssAO=lerp(1.0h,IN.color.a,saturate(_BaseSSSAOInfluence*ASP_GlobalTreeSSSAOInfluence));
            half distFade=saturate(1.0-distance(IN.positionWS,GetCameraPositionWS())/max(ASP_GlobalTreeSSSDistance,1e-4h));
            half3 baseSSS=(_BaseSSSIntensity*ASP_GlobalTreeSSSIntensity*2.0h)*sse.g*sssAO*distFade*_BaseSSSColor.rgb;
            sss=albedo*baseSSS;
        #ifdef _ENABLETOPLAYERBLEND_ON
            sss=lerp(sss,_TopLayerColor.rgb*_TopLayerSSSIntensity*ASP_GlobalTreeSSSIntensity*_TopLayerSSSColor.rgb,top);
        #endif
        }

        half4 Frag(Varyings IN,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
        {
            half fs=IS_FRONT_VFACE(face,1.0h,-1.0h);
            half3 albedo,nTS,em,sss;
            half smooth,occ,alpha;
            EvaluateLeaf(IN,fs,albedo,nTS,em,smooth,occ,alpha,sss);
            clip(alpha-_CutOff);
            ApplyVegetationLODCrossFade(IN.lodDistance,IN.positionCS.xy);
            half3 b=cross(IN.normalWS,IN.tangentWS.xyz)*IN.tangentWS.w;
            half3 n=normalize(IN.tangentWS.xyz*nTS.x+b*nTS.y+IN.normalWS*nTS.z);
            //half3 n=normalize(IN.normalWS);
            InputData d=(InputData)0;
            d.positionWS=IN.positionWS;
            d.normalWS=n;
            d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(IN.positionWS);
            d.shadowCoord=TransformWorldToShadowCoord(IN.positionWS);
            d.fogCoord=IN.fogFactor;
            d.vertexLighting=VertexLighting(IN.positionWS,n);
            d.bakedGI=SAMPLE_GI(IN.staticLightmapUV,IN.vertexSH,n);
            d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(IN.positionCS);
            d.shadowMask=SAMPLE_SHADOWMASK(IN.staticLightmapUV);
            half4 c=UniversalFragmentPBR(d,albedo,0.0h,0.0h,smooth,occ,em,alpha);
            c.rgb+=VegetationUnshadowedMainLightPBRDelta(d,albedo,0.0h,
                half3(0.0h,0.0h,0.0h),smooth,occ,alpha);
            c.rgb+=EvaluateVegetationAdditionalLightsPBR(d,albedo,0.0h,half3(0.0h,0.0h,0.0h),smooth,occ,alpha);

            Light ml=GetMainLight(d.shadowCoord);
            ml.shadowAttenuation=VegetationMainLightShadowAttenuation(ml.shadowAttenuation);
            half3 ld=normalize(ml.direction+n*_BaseSSSNormalDistortion);
            half trans=pow(saturate(dot(d.viewDirectionWS,-ld)),_BaseSSSScattering);
            half shadow=lerp(1.0h,ml.shadowAttenuation,_BaseSSSShadow);
            half3 ambient=SampleSH(n);
            c.rgb+=albedo*(ml.color*shadow*(trans*_BaseSSSDirect)+ambient*_BaseSSSAmbiet)*sss*_BaseSSSIntensity;

            // Receiver-lighting diagnostics for the indirect vegetation draw path.
            if (_LightingDebugMode>0.5h)
            {
                half3 bakedGI=d.bakedGI;
                Light debugMainLight=GetMainLight(d.shadowCoord);
                debugMainLight.shadowAttenuation=VegetationMainLightShadowAttenuation(debugMainLight.shadowAttenuation);
                MixRealtimeAndBakedGI(debugMainLight,n,bakedGI);
                half debugAlpha=alpha;
                BRDFData debugBRDF;
                InitializeBRDFData(albedo,0.0h,half3(0.0h,0.0h,0.0h),smooth,debugAlpha,debugBRDF);
                half3 directLighting=LightingPhysicallyBased(debugBRDF,debugMainLight,n,d.viewDirectionWS);
                half3 indirectLighting=GlobalIllumination(debugBRDF,bakedGI,occ,d.positionWS,n,d.viewDirectionWS);

                if (_LightingDebugMode<1.5h) return half4(albedo,1.0h);
                if (_LightingDebugMode<2.5h) return half4(debugMainLight.shadowAttenuation.xxx,1.0h);
                if (_LightingDebugMode<3.5h) return half4(directLighting,1.0h);
                if (_LightingDebugMode<4.5h) return half4(indirectLighting,1.0h);
                return half4(c.rgb,1.0h);
            }

            c.rgb=MixFog(c.rgb,IN.fogFactor);
            return c;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags{"LightMode"="UniversalForwardOnly"}
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma shader_feature_local _ENABLEWIND_ON
            #pragma shader_feature_local _ENABLESTATICMESHSUPPORT_ON
            #pragma shader_feature_local _ENABLETOPLAYERBLEND_ON
            #pragma shader_feature_local _ENABLETINTCOLOR_ON
            #pragma shader_feature_local _ENABLESECONDCOLOR_ON
            #pragma shader_feature_local _ENABLEGRADIENTCOLOR_ON
            #pragma shader_feature_local _ENABLEBACKFACEPROJECTION_ON
            #pragma shader_feature_local _ENABLEWORLDPROJECTION_ON
            #pragma shader_feature_local _ENABLEFLIPNORMALS_ON
            #pragma shader_feature_local _ENABLEGLANCINGANGLECUT_ON
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode"="ShadowCaster"}
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local _ENABLEWIND_ON
            #pragma shader_feature_local _ENABLESTATICMESHSUPPORT_ON

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS:POSITION;
                half3 normalOS:NORMAL;
                float2 uv:TEXCOORD0;
                float2 uv2:TEXCOORD2;
                half4 color:COLOR;
                uint instanceID:SV_InstanceID;
            };

            struct ShadowVaryings
            {
                float4 positionCS:SV_POSITION;
                nointerpolation float lodDistance:TEXCOORD15;
                float2 uv:TEXCOORD0;
                nointerpolation float shadowCutoff:TEXCOORD1;
            };

            ShadowVaryings ShadowVert(ShadowAttributes IN)
            {
                ShadowVaryings O=(ShadowVaryings)0;

                float3 pivotWS=GetVegetationPivotWS(IN.instanceID);
                O.lodDistance=GetVegetationLODDistance(pivotWS);
                float3 positionWS=TransformVegetationPositionToWorld(IN.positionOS.xyz,IN.instanceID);
                half3 normalWS=TransformVegetationNormalToWorld(IN.normalOS,IN.instanceID);

                positionWS=ApplyLeafShadowWindWS(positionWS,pivotWS,IN.uv2,IN.color);

                float3 lightDirectionWS=_LightDirection;

            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                lightDirectionWS=normalize(_LightPosition-positionWS);
            #endif

                positionWS=ApplyShadowBias(positionWS,normalWS,lightDirectionWS);
                O.positionCS=TransformWorldToHClip(positionWS);

            #if !defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                #if UNITY_REVERSED_Z
                    O.positionCS.z=min(O.positionCS.z,O.positionCS.w*UNITY_NEAR_CLIP_VALUE);
                #else
                    O.positionCS.z=max(O.positionCS.z,O.positionCS.w*UNITY_NEAR_CLIP_VALUE);
                #endif
            #endif

                O.uv=IN.uv;
                O.shadowCutoff=GetVegetationShadowAlphaCutoff(_CutOff,pivotWS);
                return O;
            }

            half4 ShadowFrag(ShadowVaryings IN):SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseAlbedo,sampler_BaseAlbedo,IN.uv).a-IN.shadowCutoff);
                ApplyVegetationLODCrossFade(IN.lodDistance,IN.positionCS.xy);
                return 0;
            }

            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode"="DepthOnly"}
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local _ENABLEWIND_ON
            #pragma shader_feature_local _ENABLESTATICMESHSUPPORT_ON

            struct DepthAttributes{float4 positionOS:POSITION;float2 uv:TEXCOORD0;float2 uv2:TEXCOORD2;half4 color:COLOR;uint instanceID:SV_InstanceID;};
            struct DepthVaryings{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;nointerpolation float lodDistance:TEXCOORD15;};

            DepthVaryings DepthVert(DepthAttributes IN)
            {
                DepthVaryings O=(DepthVaryings)0;
                float3 pivotWS=GetVegetationPivotWS(IN.instanceID);
                O.lodDistance=GetVegetationLODDistance(pivotWS);
                float3 positionWS=TransformVegetationPositionToWorld(IN.positionOS.xyz,IN.instanceID);
                positionWS=ApplyLeafWindWS(positionWS,pivotWS,IN.uv2,IN.color);
                O.positionCS=TransformWorldToHClip(positionWS);
                O.uv=IN.uv;
                return O;
            }

            half4 DepthFrag(DepthVaryings IN):SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseAlbedo,sampler_BaseAlbedo,IN.uv).a-_CutOff);
                ApplyVegetationLODCrossFade(IN.lodDistance,IN.positionCS.xy);
                return 0;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma shader_feature_local _ENABLEWIND_ON
            #pragma shader_feature_local _ENABLESTATICMESHSUPPORT_ON
            #pragma shader_feature_local _ENABLETOPLAYERBLEND_ON
            #pragma shader_feature_local _ENABLETINTCOLOR_ON
            #pragma shader_feature_local _ENABLESECONDCOLOR_ON
            #pragma shader_feature_local _ENABLEGRADIENTCOLOR_ON
            #pragma shader_feature_local _ENABLEBACKFACEPROJECTION_ON
            #pragma shader_feature_local _ENABLEWORLDPROJECTION_ON
            #pragma shader_feature_local _ENABLEFLIPNORMALS_ON
            #pragma shader_feature_local _ENABLEGLANCINGANGLECUT_ON

            half4 DepthNormalsFrag(Varyings IN, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC) : SV_Target
            {
                half faceSign = IS_FRONT_VFACE(face,1.0h,-1.0h);
                half3 albedo, normalTS, emission, sss;
                half smoothness, occlusion, alpha;
                EvaluateLeaf(IN,faceSign,albedo,normalTS,emission,smoothness,occlusion,alpha,sss);
                clip(alpha-_CutOff);
                ApplyVegetationLODCrossFade(IN.lodDistance,IN.positionCS.xy);
                half3 bitangentWS=cross(IN.normalWS,IN.tangentWS.xyz)*IN.tangentWS.w;
                half3 normalWS=normalize(IN.tangentWS.xyz*normalTS.x+
                    bitangentWS*normalTS.y+IN.normalWS*normalTS.z);
                return EncodeVegetationDepthNormal(normalWS);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
