// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Ridah/SeparableBlur" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "" {}
	}
	
	HLSLINCLUDE
	
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
	#include "Assets/Ridah/RidahPP/VolumeComponent/Resources/Shader/Ridah.hlsl"
	struct appdata
	{
		uint vertexID : SV_VertexID;
		half2 texcoord :TEXCOORD;
	};
	struct v2f {
		float4 pos : SV_POSITION;
		float2 uv : TEXCOORD0;

		float4 uv01 : TEXCOORD1;
		float4 uv23 : TEXCOORD2;
		float4 uv45 : TEXCOORD3;
	};
	
	float4 offsets;
	
	TEXTURE2D_X(_PostSource);
	SAMPLER(sampler_PostSource);
	half4 _PostSource_ST;

	sampler2D _MainTex;
	half4 _MainTex_ST;
		
	v2f vert (appdata v) {
		v2f o;
		o.pos = GetFullScreenTriangleVertexPosition(v.vertexID);
		v.texcoord = GetFullScreenTriangleTexCoord(v.vertexID);

		o.uv.xy = UnityStereoScreenSpaceUVAdjust(v.texcoord.xy, _PostSource_ST);

		o.uv01 = UnityStereoScreenSpaceUVAdjust(v.texcoord.xyxy + offsets.xyxy * float4(1,1, -1,-1), _PostSource_ST);
		o.uv23 = UnityStereoScreenSpaceUVAdjust(v.texcoord.xyxy + offsets.xyxy * float4(1,1, -1,-1) * 2.0, _PostSource_ST);
		o.uv45 = UnityStereoScreenSpaceUVAdjust(v.texcoord.xyxy + offsets.xyxy * float4(1,1, -1,-1) * 3.0, _PostSource_ST);

		return o;  
	}	
	half4 frag (v2f i) : SV_Target {
		half4 color = float4 (0,0,0,0);
		color += 0.40 * SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource, i.uv);
		color += 0.15 * SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource, i.uv01.xy);
		color += 0.15 * SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource, i.uv01.zw);
		color += 0.10 * SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource, i.uv23.xy);
		color += 0.10 * SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource, i.uv23.zw);
		color += 0.05 * SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource, i.uv45.xy);
		color += 0.05 * SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource, i.uv45.zw);
		
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
