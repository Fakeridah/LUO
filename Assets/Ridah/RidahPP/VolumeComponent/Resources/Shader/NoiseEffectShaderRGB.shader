// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Ridah/Noise Shader RGB" {
Properties {
	_MainTex ("Base (RGB)", 2D) = "white" {}
	_GrainTex ("Base (RGB)", 2D) = "gray" {}
	_ScratchTex ("Base (RGB)", 2D) = "gray" {}
}

SubShader {
	Pass {
		ZTest Always Cull Off ZWrite Off

HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Ridah.hlsl"
struct appdata
{
	uint vertexID : SV_VertexID;
	half2 texcoord :TEXCOORD;
};
struct v2f { 
	float4 pos	: SV_POSITION;
	float2 uv	: TEXCOORD0;
	float2 uvg	: TEXCOORD1; // grain
	float2 uvs	: TEXCOORD2; // scratch
}; 
TEXTURE2D_X(_PostSource);
SAMPLER(sampler_PostSource);
half4 _PostSource_TexelSize;
half4 _PostSource_ST;

uniform TEXTURE2D_X( _GrainTex);
uniform TEXTURE2D_X( _ScratchTex);
SAMPLER(sampler_GrainTex);
SAMPLER(sampler_ScratchTex);

uniform float4 _GrainOffsetScale;
uniform float4 _ScratchOffsetScale;
uniform half4 _Intensity; // x=grain, y=scratch

half4 _MainTex_ST;

v2f vert (appdata v)
{
	v2f o;
	o.pos = GetFullScreenTriangleVertexPosition(v.vertexID);
	v.texcoord = GetFullScreenTriangleTexCoord(v.vertexID);
	o.uv = UnityStereoScreenSpaceUVAdjust(v.texcoord, _PostSource_ST);
	o.uvg = v.texcoord.xy * _GrainOffsetScale.zw + _GrainOffsetScale.xy;
	o.uvs = v.texcoord.xy * _ScratchOffsetScale.zw + _ScratchOffsetScale.xy;
	return o;
}

half4 frag (v2f i) : SV_Target
{
	half4 col = SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource, i.uv);
	
	// sample noise texture and do a signed add
	half3 grain = SAMPLE_TEXTURE2D_X(_GrainTex, sampler_GrainTex, i.uvg).r * 2.5 - 0.58;
	col.rgb +=grain * _Intensity.x;

	// sample scratch texture and do a signed add
	half3 scratch = SAMPLE_TEXTURE2D_X(_ScratchTex, sampler_ScratchTex, i.uvs).rgb *2.5 - 0.58;
	col.rgb += scratch<0?0:scratch * _Intensity.y;

	return col;
}
ENDHLSL
	}
}

Fallback off

}
