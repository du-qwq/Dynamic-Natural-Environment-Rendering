Shader "InfiniteGrass/Modifiers/GrassExplosionBurnShader"
{
    Properties
    {
        _MainTex("Shape Texture", 2D) = "white" {}
        _Strength("Strength", Range(0, 1)) = 1
        _EdgeSoftness("Edge Softness", Range(0.001, 0.5)) = 0.12
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "LightMode" = "GrassBurn"
        }

        ZWrite Off
        Blend One One
        Cull Off
        ColorMask R

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Strength;
            float _EdgeSoftness;

            v2f vert(appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;

                return o;
            }

            float frag(v2f i) : SV_Target
            {
                float4 shape = tex2D(_MainTex, i.uv);

                float2 centerUV = i.uv - float2(0.5, 0.5);
                float distanceFromCenter = length(centerUV);
                float circleMask = 1 - smoothstep(0.5 - _EdgeSoftness, 0.5, distanceFromCenter);
                float burnStrength = shape.r * shape.a * i.color.a * _Strength * circleMask;

                return burnStrength;
            }

            ENDCG
        }
    }
}
