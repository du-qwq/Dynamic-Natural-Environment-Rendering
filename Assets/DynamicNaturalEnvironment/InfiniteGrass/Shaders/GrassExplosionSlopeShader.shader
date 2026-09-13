Shader "InfiniteGrass/Modifiers/GrassExplosionSlopeShader"
{
    Properties
    {
        _MainTex("Shape Texture", 2D) = "white" {}
        _Strength("Strength", Range(0, 1)) = 1
        _RingWidth("Ring Width", Range(0.01, 0.5)) = 0.12
        _RingSoftness("Ring Softness", Range(0.001, 0.2)) = 0.03
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "LightMode" = "GrassSlope"
        }

        ZWrite Off
        Blend One Zero
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
            float _RingWidth;
            float _RingSoftness;

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

                float2 localDirection = i.uv - float2(0.5, 0.5);
                float distanceFromCenter = length(localDirection);

                if (distanceFromCenter > 0.0001)
                {
                    localDirection /= distanceFromCenter;
                }
                else
                {
                    localDirection = float2(0, 0);
                }

                float outerRadius = 0.5;
                float innerRadius = max(outerRadius - _RingWidth, 0);
                float innerMask = smoothstep(innerRadius - _RingSoftness, innerRadius + _RingSoftness, distanceFromCenter);
                float outerMask = 1 - smoothstep(outerRadius - _RingSoftness, outerRadius + _RingSoftness, distanceFromCenter);
                float ringMask = saturate(innerMask * outerMask);
                float strength = shape.r * shape.a * i.color.a * _Strength * ringMask;

                clip(strength - 0.001);

                float2 encodedDirection = localDirection * 0.5 + 0.5;

                return float4(encodedDirection, 0, strength);
            }

            ENDCG
        }
    }
}
