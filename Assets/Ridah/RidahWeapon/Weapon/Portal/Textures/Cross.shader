Shader "Unlit/Cross"
{
    Properties
    {
		_MainTex("_MainTex", 2D) = "white" {}
        _Color ("Color", Color) = (1.0,1.0,1.0,1.0)
        _Width ("Width", Range(0.0,1.0)) = 0.2
		_Smooth ("Smooth", Range(0.0,0.2)) = 0.02
		_Tile_Offset ("Tile_Offset", vector)=(1.0,1.0,0.0,0.0)
		_Radius ("Radius", vector) = (1.0,1.0,0.0,0.0)
        _Exponent("Exponent", Float) = 1.0
        _Opacity("Opacity", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 _Tile_Offset;
            float4 _Color;
            float _Exponent;
            float _Opacity;
            float4 _Radius;
            float _Width;
			float _Smooth;
            void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
        {
            Out = UV * Tiling + Offset;
        }
            void Unity_Blend_Overlay_float4(float4 Base, float4 Blend, float Opacity, out float4 Out)
           {
               float4 result1 = 1.0 - 2.0 * (1.0 - Base) * (1.0 - Blend);
               float4 result2 = 2.0 * Base * Blend;
               float4 zeroOrOne = step(Base, 0.5);
               Out = result2 * zeroOrOne + (1 - zeroOrOne) * result1;
               Out = lerp(Base, Out, _Opacity);
           }
            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv;
                Unity_TilingAndOffset_float(i.uv,_Tile_Offset.xy,_Tile_Offset.zw,uv);
                float4 circle1 = smoothstep( length(uv)-_Radius.x,1,0)*_Radius.z;
                float4 circle2 = smoothstep( length(uv)-_Radius.y,0.93,0.42)*_Radius.w;
                float4 circle;
                Unity_Blend_Overlay_float4(circle1,circle2,  _Opacity, circle);
                clip(circle.x-0.1);
                circle.a = 1;

                return circle;
            }
            ENDCG
        }
    }
}
