// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Ridah/Noise Shader YUV" {
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
	float2 uvOffsets : TEXCOORD3;
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
uniform float _DX11NoiseTime;
uniform float3 _NoisePerChannel;
uniform half _NoiseAmount;

half4 _MainTex_ST;

int ihash(int n)
{
    n = (n<<13)^n;
   	return (n*(n*n*15731+789221)+1376312589) & 2147483647;
}
float frand(int n)
{
	return ihash(n) / 2147483647.0;
}
float cellNoise1f(int3 p)
{
	return frand(p.z*65536 + p.y*256 + p.x);//*2.0-1.0;
}
float3 cellNoise3f(int3 p)
{
	int i = p.z*65536 + p.y*256 + p.x;
	return float3(frand(i), frand(i + 57), frand(i + 113));//*2.0-1.0;
}
inline float3 Overlay(float3 m, float3 color) {
	float3 check = step(0.5, color.rgb);
	float3 result = check * (float3(1,1,1) - ((float3(1,1,1) - 2*(color.rgb-0.5)) * (1-m.rgb))); 
	result += (1-check) * (2*color.rgb) * m.rgb;
	return result;
}

v2f vert (appdata v)
{
	v2f o;
	o.pos = GetFullScreenTriangleVertexPosition(v.vertexID);
	v.texcoord = GetFullScreenTriangleTexCoord(v.vertexID);
	o.uv = UnityStereoScreenSpaceUVAdjust(v.texcoord, _PostSource_ST);
	o.uvg = v.texcoord.xy * _GrainOffsetScale.zw + _GrainOffsetScale.xy;
	o.uvs = v.texcoord.xy * _ScratchOffsetScale.zw + _ScratchOffsetScale.xy;
	o.uvOffsets = v.texcoord.xy;
	return o;
}

half4 frag (v2f i) : SV_Target
{
	/*half4 col = SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource, i.uv);
	
	// convert to YUV
	half3 yuv;
	yuv.x = dot( col.rgb, half3(0.299,0.587,0.114) );
	yuv.y = (col.b-yuv.x)*0.492;
	yuv.z = (col.r-yuv.x)*0.877;
	
	// sample noise texture and do a signed add
	half3 grain = SAMPLE_TEXTURE2D_X(_GrainTex, sampler_GrainTex, i.uvg).rgb * 2.5 - 0.58;
	yuv.rgb += grain * _Intensity.x;

	// convert back to rgb
	col.r = yuv.z * 1.140 + yuv.x;
	col.g = yuv.z * (-0.581) + yuv.y * (-0.395) + yuv.x;
	col.b = yuv.y * 2.032 + yuv.x;

	// sample scratch texture and add
	half3 scratch = SAMPLE_TEXTURE2D_X(_ScratchTex, sampler_ScratchTex, i.uvs).rgb * 2.5 - 0.58;
	col.rgb += scratch <0?0:scratch* _Intensity.y;

	return col;*/
			float4 color = saturate(SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource, i.uv));
			half3 _MidGrey= half3(.5,2.0,-2.0);
			// black & white intensities
			float2 blackWhiteCurve = Luminance(color.rgb) - _MidGrey.x; // maybe tweak middle grey
			blackWhiteCurve.xy = saturate(blackWhiteCurve.xy * _MidGrey.yz); //float2(1.0/0.8, -1.0/0.2));

			float finalIntensity = _NoiseAmount + max(0.0f, dot(_NoiseAmount, blackWhiteCurve.xy));
			
			float3 m = cellNoise3f(float3( (i.uv.xy + i.uvOffsets) * _PostSource_TexelSize.zw, _DX11NoiseTime));
			m = saturate(lerp(float3(0.5,0.5,0.5), m, _NoisePerChannel.rgb*finalIntensity));	
			return float4(Overlay(float4(m,0), color.rgb), color.a);
}
ENDHLSL
	}
}

Fallback off

}
