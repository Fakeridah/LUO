Shader "Ridah/GrayScale" {
Properties {
	_MainTex ("Base (RGB)", 2D) = "white" {}
	_RampTex ("Base (RGB)", 2D) = "grayscaleRamp" {}
}

SubShader {
	Pass {
		ZTest Always Cull Off ZWrite Off
				
HLSLPROGRAM
#pragma vertex vert_img
#pragma fragment frag

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Ridah.hlsl"
uniform TEXTURE2D_X(_RampTex);
SAMPLER(sampler_RampTex);
uniform half _RampOffset;

TEXTURE2D_X(_PostSource);
SAMPLER(sampler_PostSource);
half4 _PostSource_TexelSize;
half4 _PostSource_ST;
		struct appdata
		{
			uint vertexID : SV_VertexID;
			half2 texcoord :TEXCOORD;
		};
		struct v2f
		{
			float4 pos : SV_POSITION;
			half2 uv : TEXCOORD0;
		};
		v2f vert_img ( appdata v )
		{
			v2f o;

			o.pos = GetFullScreenTriangleVertexPosition(v.vertexID);
			v.texcoord = GetFullScreenTriangleTexCoord(v.vertexID);
        	o.uv = UnityStereoScreenSpaceUVAdjust(v.texcoord + _PostSource_TexelSize.xy, _PostSource_ST);

			return o; 
		}		
		half4 frag (v2f i) : SV_Target
		{
			half4 original = SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource, i.uv);
			half grayscale = Luminance(original.rgb);
			half2 remap = half2 (grayscale + _RampOffset, .5);
			half4 output = SAMPLE_TEXTURE2D_X(_RampTex, sampler_RampTex, remap);
			output.a = original.a;
			return output;
		}
		ENDHLSL

	}
}

Fallback off

}
