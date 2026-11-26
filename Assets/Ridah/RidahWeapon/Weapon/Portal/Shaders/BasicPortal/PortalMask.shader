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
			"Queue" = "Geometry" 
			"RenderPipeline" = "UniversalPipeline"
		}
		
		HLSLINCLUDE
		    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		ENDHLSL

        Pass
        {
		Name "Mask"
			// The aperture samples a rendered view directly. A fixed stencil
			// reference cannot represent Source's per-view masks and clobbers URP.
			// Recursive portal cameras do not run the main-camera aperture
			// composite pass, so this surface must win depth ties with its host
			// wall. Without the bias, grazing views reveal the coplanar wall as
			// long alternating bars across the portal image.
			Offset -1, -1
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
