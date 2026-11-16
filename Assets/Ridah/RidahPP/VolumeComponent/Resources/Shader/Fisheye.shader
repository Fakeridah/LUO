// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Ridah/Fisheye" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "" {}
	}
	
	// Shader code pasted into all further CGPROGRAM blocks
	HLSLINCLUDE
	
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
	struct appdata
	{
		uint vertexID : SV_VertexID;
	};

	struct v2f {
		float4 vertex : SV_POSITION;
		float2 uv : TEXCOORD0;
	};
	
	sampler2D _MainTex;
	half4 _MainTex_ST;

	float2 intensity;
	
	TEXTURE2D_X(_PostSource);
	SAMPLER(sampler_PostSource);
	half4 _PostSource_ST;

	inline float2 UnityStereoScreenSpaceUVAdjust(float2 uv, float4 scaleAndOffset)
	{
		 return uv.xy * scaleAndOffset.xy + scaleAndOffset.zw;
	}
	v2f vert( appdata v ) 
	{
		v2f o;
		o.vertex = GetFullScreenTriangleVertexPosition(v.vertexID);
		o.uv = GetFullScreenTriangleTexCoord(v.vertexID);
		return o;
	} 
	
	half4 frag(v2f i) : SV_Target 
	{
		half2 coords = i.uv;
		coords = (coords - 0.5) * 2.0;		
		
		half2 realCoordOffs;
		realCoordOffs.x = (1-coords.y * coords.y) * intensity.y * (coords.x); 
		realCoordOffs.y = (1-coords.x * coords.x) * intensity.x * (coords.y);
		
		half4 color = SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource,UnityStereoScreenSpaceUVAdjust(i.uv - realCoordOffs, _PostSource_ST));
		
		return color;
	}

	ENDHLSL 
	
Subshader {
 Pass {
	  ZTest Always Cull Off ZWrite Off

      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      ENDHLSL
  }
  
}

Fallback off
	
} // shader
