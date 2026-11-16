// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Ridah/Vignetting" {
	Properties {
		_MainTex ("Base", 2D) = "white" {}
	}
	
	HLSLINCLUDE
	
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
	#include "Ridah.hlsl"
	struct appdata{
		uint vertexID : SV_VertexID;
		half2 texcoord :TEXCOORD;
	};
	struct v2f {
		float4 pos : SV_POSITION;
		float2 uv : TEXCOORD0;
		float2 uv2 : TEXCOORD1;
	};
	TEXTURE2D_X(_VignetteTex);
	SAMPLER(sampler_VignetteTex);
	half4  _VignetteTex_ST;
	
	TEXTURE2D_X(_PostSource);
	SAMPLER(sampler_PostSource);
	half4 _PostSource_ST;
	float4 _PostSource_TexelSize;

		
	v2f vert( appdata v ) {
		v2f o;
		o.pos = GetFullScreenTriangleVertexPosition(v.vertexID);
		v.texcoord = GetFullScreenTriangleTexCoord(v.vertexID);
		o.uv = v.texcoord.xy;
		o.uv2 = v.texcoord.xy;

		#if UNITY_UV_STARTS_AT_TOP
		if (_PostSource_TexelSize.y < 0)
			 o.uv2.y = 1.0 - o.uv2.y;
		#endif

		return o;
	} 
	
	half4 frag(v2f i) : SV_Target {
		half2 uv = i.uv;	
		half4 color = SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource,uv);	
		half4 colorBlur = SAMPLE_TEXTURE2D_X(_VignetteTex, sampler_VignetteTex,i.uv2);	
		return color*colorBlur ;
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
} 
