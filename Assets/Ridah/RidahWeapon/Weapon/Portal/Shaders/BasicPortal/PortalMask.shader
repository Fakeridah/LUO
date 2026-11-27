Shader "Portals/PortalMask"
{
    Properties
    {
		_MainTex("Main Texture", 2D) = "white"{}
    }
    SubShader
    {
        Tags
		{ 
			"RenderType" = "Opaque" 
			"Queue" = "Geometry+1"
			"RenderPipeline" = "UniversalPipeline"
		}
		
		HLSLINCLUDE
		    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		ENDHLSL

        Pass
        {
		Name "Mask"
			Cull Back
			ZTest LEqual
			ZWrite Off
			// Resolve the aperture against its coplanar host wall without
			// writing biased depth that could hide PortalWhite/PortalBlack.
			Offset -1, -1
			// The aperture samples a rendered view directly. A fixed stencil
			// reference cannot represent Source's per-view masks and clobbers URP.
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			uniform sampler2D _MainTex;
			uniform float4 _MainTex_TexelSize;
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
				o.vertex = TransformObjectToHClip(v.vertex.xyz);
				o.screenPos = ComputeScreenPos(o.vertex);
				return o;
			}

			half4 frag(v2f i) : SV_Target
			{
			float2 uv = i.screenPos.xy / i.screenPos.w;
			#if UNITY_UV_STARTS_AT_TOP
			if (_MainTex_TexelSize.y < 0.0)
				uv.y = 1.0 - uv.y;
			#endif
			float4 col = tex2D(_MainTex,uv);
				return col;
			}
		ENDHLSL
        }
    }
}
