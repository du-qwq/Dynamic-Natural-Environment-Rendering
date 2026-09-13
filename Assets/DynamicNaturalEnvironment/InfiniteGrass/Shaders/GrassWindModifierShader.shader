Shader "InfiniteGrass/Modifiers/GrassWindModifierShader"
{
    Properties
    {
        _MainTex("Shape Texture", 2D) = "white" {}

        // RG编码风向，B控制强度，A控制影响范围
        _WindData("Wind Data", Color) = (1, 0.5, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "LightMode" = "GrassWind"
        }

        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

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
                half4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _WindData;

            v2f vert(appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 shape = tex2D(_MainTex, i.uv);

                float influence = shape.r * shape.a * i.color.a * _WindData.a;

                // RG：风向
                // B：风强
                // A：影响权重
                return float4(
                    _WindData.rg,
                    _WindData.b,
                    influence
                );
            }
            ENDCG
        }
    }
}