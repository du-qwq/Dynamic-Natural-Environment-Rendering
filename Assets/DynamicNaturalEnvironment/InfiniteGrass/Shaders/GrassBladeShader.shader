Shader "InfiniteGrass/GrassBladeShader"
{
    Properties
    {
        [MainTexture] _BaseColorTexture("BaseColor Texture", 2D) = "white" {}
        _ColorA("ColorA", Color) = (0,0,0,1)
        _ColorB("ColorB", Color) = (1,1,1,1)
        _AOColor("AO Color", Color) = (0.5,0.5,0.5)

        [Header(Grass Shape)][Space]
        _GrassWidth("Grass Width", Float) = 1
        _GrassHeight("Grass Height", Float) = 1
        _GrassWidthRandomness("Grass Width Randomness", Range(0, 1)) = 0.25
        _GrassHeightRandomness("Grass Height Randomness", Range(0, 1)) = 0.5

        _GrassCurving("Grass Curving", Float) = 0.1//草叶随机弯曲程度

        [Space]
        _ExpandDistantGrassWidth("Expand Distant Grass Width", Float) = 1
        _ExpandDistantGrassRange("Expand Distant Grass Range", Vector) = (50, 200, 0, 0)

        [Header(Wind)][Space]
        _WindTexture("Wind Texture", 2D) = "white" {}

        //保留原来的参数，兼容旧材质
        _WindScroll("Wind Scroll", Vector) = (1, 1, 0, 0)//风纹理随时间滚动速度
        _WindStrength("Wind Strength", Float) = 1

        //世界空间全局风
        _GlobalWindDirection("Global Wind Direction", Vector) = (1, 0, 0, 0)
        _GlobalWindStrength("Global Wind Strength", Range(0, 2)) = 0.15

        //阵风参数，用于形成成片移动的草浪
        _GustStrength("Gust Strength", Range(0, 2)) = 0.35
        _GustSpeed("Gust Speed", Float) = 0.5
        _GustScale("Gust Scale", Float) = 0.02

        //局部风场整体强度
        _LocalWindStrength("Local Wind Strength", Range(0, 2)) = 1

        [Header(Lighting)][Space]
        _RandomNormal("Random Normal", Range(0, 1)) = 0.1//给草的法线加一点随机，避免所有草受光完全一样

        _DensityTexture("Density Texture", 2D) = "white" {}
        _DensityTexture_ST("Density Texture ST", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Cull Back
            ZTest Less

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 color : COLOR;

                float2 uv : TEXCOORD0;
                float4 color2 : COLOR1;
            };

            CBUFFER_START(UnityPerMaterial)

                half3 _ColorA;
                half3 _ColorB;
                float4 _BaseColorTexture_ST;
                half3 _AOColor;

                float _GrassWidth;
                float _GrassHeight;
                float _GrassCurving;
                float _GrassWidthRandomness;
                float _GrassHeightRandomness;

                float _ExpandDistantGrassWidth;
                float2 _ExpandDistantGrassRange;

                float4 _WindTexture_ST;
                float _WindStrength;
                float2 _WindScroll;

                //新增：全局风
                float2 _GlobalWindDirection;
                float _GlobalWindStrength;

                //新增：阵风
                float _GustStrength;
                float _GustSpeed;
                float _GustScale;

                //新增：局部风场强度
                float _LocalWindStrength;

                half _RandomNormal;

                float2 _CenterPos;

                float _DrawDistance;
                float _TextureUpdateThreshold;
                int _GrassLODLevel;//LOD新增：0读取近处草Buffer，1读取远处草Buffer
                int _GrassDebugMode;//调试新增：0正常、1 LOD、2 Mask、3 Slope、4 Wind、5 Burn、6 Height、7 Color

            CBUFFER_END

            StructuredBuffer<float3> _GrassPositionsNear;//LOD新增：近处草位置Buffer
            StructuredBuffer<float3> _GrassPositionsFar;//LOD新增：远处草位置Buffer

            sampler2D _BaseColorTexture;
            sampler2D _WindTexture;

            sampler2D _GrassHeightDebugRT;//调试新增：高度RT
            sampler2D _GrassMaskDebugRT;//调试新增：Mask RT
            sampler2D _GrassColorRT;
            sampler2D _GrassSlopeRT;
            sampler2D _GrassSlopeHistoryRT;
            sampler2D _GrassBurnHistoryRT;//燃烧新增：历史燃烧RT
            sampler2D _GrassBurnRT;//燃烧修复：当前帧燃烧输入，只在正在燃烧时用于橙色

            //新增：局部风场RT
            sampler2D _GrassWindRT;

            sampler2D _DensityTexture;
            float4 _DensityTexture_ST;

            half3 ApplySingleDirectLight(Light light,half3 N,half3 V,half3 albedo,half mask,half positionY)
            {
                half3 H = normalize(light.direction + V);

                half directDiffuse = dot(N, light.direction) * 0.5 + 0.5;

                float directSpecular = saturate(dot(N, H));
                directSpecular *= directSpecular;
                directSpecular *= directSpecular;
                directSpecular *= directSpecular;
                directSpecular *= directSpecular;

                directSpecular *= positionY * 0.12;

                half3 lighting=light.color*(light.shadowAttenuation * light.distanceAttenuation);

                half3 result =(albedo * directDiffuse +directSpecular * (1 - mask)) *lighting;

                return result;
            }

            uint murmurHash3(float input)
            {
                uint h = abs(input);

                h ^= h >> 16;
                h *= 0x85ebca6b;
                h ^= h >> 13;
                h *= 0xc2b2ae3d;
                h ^= h >> 16;

                return h;
            }

            float random(float input)
            {
                return murmurHash3(input) / 4294967295.0;
            }

            float srandom(float input)
            {
                return
                    (murmurHash3(input) / 4294967295.0) * 2 - 1;
            }

            float Remap(float In,float2 InMinMax,float2 OutMinMax)
            {
                return
                    OutMinMax.x +
                    (In - InMinMax.x) *
                    (OutMinMax.y - OutMinMax.x) /
                    (InMinMax.y - InMinMax.x);
            }

            float3 CalculateLighting(float3 albedo,float3 positionWS,float3 N,float3 V,float mask,float positionY)
            {
                half3 result = SampleSH(0) * albedo;

                Light mainLight =GetMainLight(TransformWorldToShadowCoord(positionWS));

                result += ApplySingleDirectLight(mainLight,N,V,albedo,mask,positionY);

                int additionalLightsCount =GetAdditionalLightsCount();

                for (int i = 0; i < additionalLightsCount; ++i)
                {
                    Light light =GetAdditionalLight(i, positionWS);

                    result += ApplySingleDirectLight(light,N,V,albedo,mask,positionY);
                }

                return result;
            }

            //LOD新增：根据当前Draw的LOD等级读取对应位置Buffer
            float3 GetGrassPivot(uint instanceID)
            {
                if (_GrassLODLevel == 0) return _GrassPositionsNear[instanceID];
                return _GrassPositionsFar[instanceID];
            }

            Varyings vert(Attributes IN,uint instanceID : SV_InstanceID)
            {
                Varyings OUT;

                float3 pivot =GetGrassPivot(instanceID);//取当前草根位置

                float2 uv =(pivot.xz - _CenterPos)/(_DrawDistance + _TextureUpdateThreshold);

                uv = uv * 0.5 + 0.5;

                float grassWidth =_GrassWidth *(1 -random(pivot.x * 950 +pivot.z * 10) *_GrassWidthRandomness);

                //距离越远草越宽
                float distanceFromCamera =length(_WorldSpaceCameraPos - pivot);

                grassWidth +=saturate(Remap(distanceFromCamera,float2(_ExpandDistantGrassRange.x,_ExpandDistantGrassRange.y),float2(0, 1))) *_ExpandDistantGrassWidth;

                grassWidth *=(1 - IN.positionOS.y);//草尖变窄

                //随机草高
                float grassHeight =_GrassHeight *(1 -random(pivot.x * 230 +pivot.z * 10) *_GrassHeightRandomness);

                //燃烧新增：采样历史燃烧程度，0为正常，1为完全烧焦
                float burn = tex2Dlod(_GrassBurnHistoryRT,float4(uv, 0, 0)).r;
                float currentBurn = tex2Dlod(_GrassBurnRT,float4(uv, 0, 0)).r;//燃烧修复：只有当前帧仍在燃烧时才显示橙色
                grassHeight *= lerp(1.0, 0.08, smoothstep(0.35, 1.0, burn));//燃烧新增：草逐渐缩短，最后只留下很短的焦黑草根

                //Billboard相关方向
                float3 cameraTransformRightWS =UNITY_MATRIX_V[0].xyz;

                float3 cameraTransformUpWS =UNITY_MATRIX_V[1].xyz;

                float3 cameraTransformForwardWS =-UNITY_MATRIX_V[2].xyz;

                //读取历史压草RT
                float4 slope =tex2Dlod(_GrassSlopeHistoryRT,float4(uv, 0, 0));

                float xSlope = slope.r * 2 - 1;
                float zSlope = slope.g * 2 - 1;

                float3 slopeDirection =normalize(float3(xSlope,1 -(max(abs(xSlope),abs(zSlope)) *0.5),zSlope));//重建草倒伏方向

                float3 bladeDirection =normalize(lerp(float3(0, 1, 0),slopeDirection,slope.a));//用alpha控制倒伏强度

                /*
                 * 新风场结构：
                 *
                 * 1. 全局风：
                 *    由GlobalWindDirection和GlobalWindStrength控制
                 *
                 * 2. 阵风：
                 *    用WindTexture的R通道产生随时间移动的草浪
                 *
                 * 3. 局部风：
                 *    从GrassWindRT读取局部风向和强度
                 */

                //防止方向为(0,0)时normalize产生异常
                float2 globalWindDirection =_GlobalWindDirection;

                float globalWindLength =length(globalWindDirection);

                if (globalWindLength > 0.0001)
                {
                    globalWindDirection /=globalWindLength;
                }
                else
                {
                    globalWindDirection =float2(1, 0);
                }

                //沿全局风向滚动噪声，形成一片片经过的阵风
                float2 gustUV =pivot.xz * _GustScale +globalWindDirection *_Time.y *_GustSpeed;

                float gustNoise =tex2Dlod(_WindTexture,float4(gustUV, 0, 0)).r;

                //把普通噪声变成比较明显的风带
                float gust =smoothstep(0.35,0.75,gustNoise) *_GustStrength;

                //全局基础风 + 阵风
                float2 globalWind =globalWindDirection *(_GlobalWindStrength +gust);

                //采样局部风场
                float4 localWindData =tex2Dlod(_GrassWindRT,float4(uv, 0, 0));

                //局部风RT的RG从0~1还原到-1~1
                float2 localWindDirection =localWindData.rg * 2 - 1;

                float localWindDirectionLength =length(localWindDirection);

                if (localWindDirectionLength > 0.0001)
                {
                    localWindDirection /=localWindDirectionLength;
                }
                else
                {
                    localWindDirection =float2(0, 0);
                }

                /*
                 * WindRT通道含义：
                 *
                 * R：局部风X方向，0~1编码
                 * G：局部风Z方向，0~1编码
                 * B：局部风强度
                 * A：局部风影响权重
                 */
                float localWindAmount =localWindData.b *localWindData.a *_LocalWindStrength;

                float2 localWind =localWindDirection *localWindAmount;

                //叠加全局风和局部风
                float2 finalWind =globalWind +localWind;

                //保留旧WindStrength作为总风力倍率，兼容原材质
                finalWind *= _WindStrength;

                //被压倒的草减少风影响
                finalWind *=(1 - slope.a);

                //草根几乎不动，草尖风力最强
                float heightWeight =IN.positionOS.y *IN.positionOS.y;

                bladeDirection.xz +=finalWind *heightWeight;

                bladeDirection =normalize(bladeDirection);

                float3 rightTangent =cross(bladeDirection,cameraTransformForwardWS);

                //防止俯视或特殊角度下叉乘结果接近0
                float rightTangentLength =length(rightTangent);

                if (rightTangentLength > 0.0001)
                {
                    rightTangent /=rightTangentLength;
                }
                else
                {
                    rightTangent =float3(1, 0, 0);
                }

                float3 positionOS =bladeDirection *IN.positionOS.y *grassHeight+rightTangent *IN.positionOS.x *grassWidth;
                //用本地草叶Mesh的x/y坐标，重新生成一棵面向相机、可以倒伏、可以随机高宽的草

                //越靠近草尖，随机弯曲越明显
                positionOS.xz +=(IN.positionOS.y *IN.positionOS.y) *float2(srandom(pivot.x * 851 +pivot.z * 10),srandom(pivot.z * 647 +pivot.x * 10)) *_GrassCurving;

                float3 positionWS =positionOS +pivot;

                OUT.positionCS =TransformWorldToHClip(positionWS);

                half3 baseColor =lerp(_ColorA,_ColorB,tex2Dlod(_BaseColorTexture,float4(TRANSFORM_TEX(pivot.xz,_BaseColorTexture),0,0)).r);

                half3 albedo =lerp(_AOColor,baseColor,IN.positionOS.y);

                float4 color =tex2Dlod(_GrassColorRT,float4(uv, 0, 0));

                albedo =lerp(albedo,color.rgb,color.a);

                //燃烧修复：正在燃烧的草显示橙色，已经烧矮的部分最后覆盖成焦黑色
                half3 burningColor = half3(1.0, 0.28, 0.02);
                half3 charredColor = half3(0.035, 0.02, 0.012);
                float burningMask = saturate(currentBurn * 1.5);
                albedo = lerp(albedo, burningColor, burningMask);
                float charredHeight = saturate(burn);
                float charredMask = 1.0 - smoothstep(charredHeight - 0.08, charredHeight + 0.08, IN.positionOS.y);
                float charredStrength = charredMask * saturate(burn * 1.5);
                albedo = lerp(albedo, charredColor, charredStrength);

                //构造草法线
                half3 N =normalize(bladeDirection +cameraTransformForwardWS * -0.5 +_RandomNormal *half3(srandom(pivot.x * 314 +pivot.z * 10),0,srandom(pivot.z * 677 +pivot.x * 10)));

                half3 V =normalize(_WorldSpaceCameraPos -positionWS);

                float3 lighting =CalculateLighting(albedo,positionWS,N,V,color.a,IN.positionOS.y);

                float fogFactor =ComputeFogFactor(OUT.positionCS.z);

                half3 finalColor =MixFog(lighting,fogFactor);

                //调试新增：所有调试显示都在顶点阶段完成，不增加片元阶段采样
                if (_GrassDebugMode == 1)
                {
                    finalColor = _GrassLODLevel == 0 ? half3(1, 0, 0) : half3(0, 0.35, 1);//Near红色，Far蓝色
                }
                else if (_GrassDebugMode == 2)
                {
                    float maskDebug = tex2Dlod(_GrassMaskDebugRT,float4(uv, 0, 0)).r;
                    finalColor = maskDebug.xxx;//Mask黑白显示
                }
                else if (_GrassDebugMode == 3)
                {
                    finalColor = half3(slope.r,slope.g,slope.a);//RG显示方向，B显示倒伏强度
                }
                else if (_GrassDebugMode == 4)
                {
                    finalColor = half3(localWindData.r,localWindData.g,saturate(localWindAmount));//RG显示风向，B显示局部风强度
                }
                else if (_GrassDebugMode == 5)
                {
                    finalColor = half3(burn,currentBurn,0);//R显示历史烧焦，G显示当前燃烧
                }
                else if (_GrassDebugMode == 6)
                {
                    float heightDebug = tex2Dlod(_GrassHeightDebugRT,float4(uv, 0, 0)).r;
                    finalColor = heightDebug.xxx;//高度编码黑白显示
                }
                else if (_GrassDebugMode == 7)
                {
                    finalColor = lerp(half3(0, 0, 0),color.rgb,color.a);//显示Color Modifier RT
                }

                OUT.color.rgb =finalColor;

                OUT.uv = uv;
                OUT.color2 = color;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return half4(IN.color.rgb, 1);
            }

            ENDHLSL
        }
    }
}