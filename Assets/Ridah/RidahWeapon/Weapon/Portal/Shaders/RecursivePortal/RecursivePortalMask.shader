Shader "Portals/RecursivePortalMask"
{
    Properties
    {
		_MainTex("Main Texture", 2D) = "white" {}
    }
    SubShader
    {
		Tags 
		{ 
			"RenderType" = "Opaque"
		}

        Pass
        {
			// The aperture samples a rendered view directly. A fixed stencil
			// reference cannot represent Source's per-view masks and clobbers URP.
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag

				#include "UnityCG.cginc"

				struct appdata
				{
					float4 vertex : POSITION;
				};

				struct v2f
				{
					float4 vertex : SV_POSITION;
					float4 screenPos : TEXCOORD0;
				};

				v2f vert(appdata v)
				{
					v2f o;
					o.vertex = UnityObjectToClipPos(v.vertex);
					o.screenPos = ComputeScreenPos(o.vertex);
					return o;
				}

				uniform sampler2D _MainTex;
				uniform float4 _MainTex_TexelSize;

				fixed4 frag(v2f i) : SV_Target
				{
					float2 uv = i.screenPos.xy / i.screenPos.w;
					#if UNITY_UV_STARTS_AT_TOP
					if (_MainTex_TexelSize.y < 0.0)
						uv.y = 1.0 - uv.y;
					#endif
					fixed4 col = tex2D(_MainTex, uv);
					return col;
				}
			ENDCG
        }
    }
}
