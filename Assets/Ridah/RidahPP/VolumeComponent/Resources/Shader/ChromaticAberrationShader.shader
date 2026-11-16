// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Ridah/ChromaticAberration" {
	Properties {
		_MainTex ("Base", 2D) = "" {}
	}
	
	HLSLINCLUDE
	
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
	#include "Ridah.hlsl"
	struct appdata
	{
		uint vertexID : SV_VertexID;
		half2 texcoord :TEXCOORD;
	};
	struct v2f {
		float4 pos : SV_POSITION;
		float2 uv : TEXCOORD0;
	};
	
	TEXTURE2D_X(_PostSource);
	SAMPLER(sampler_PostSource);
	half4 _PostSource_ST;
	half4 _PostSource_TexelSize;

	half _ChromaticAberration;
	half _AxialAberration;
	half _Luminance;
	half2 _BlurDistance;
	half _Intensity;
	half _Blur;
		
	v2f vert( appdata v ) 
	{
		v2f o;
		o.pos = GetFullScreenTriangleVertexPosition(v.vertexID);
		v.texcoord = GetFullScreenTriangleTexCoord(v.vertexID);
		o.uv = v.texcoord.xy;
		
		return o;
	} 
	
	half4 fragDs(v2f i) : SV_Target 
	{
		half2 coords = i.uv;
		half2 uv = i.uv;
		
		coords = (coords - 0.5) * 2.0;
		half coordDot = dot (coords,coords);
		float mask = smoothstep(0.0,_Blur,1-coordDot * _Intensity); 
		
		return mask;
	}

	half4 frag(v2f i) : SV_Target 
	{
		half2 coords = i.uv;
		half2 uv = i.uv;
		
		coords = (coords - 0.5) * 2.0;		
		half coordDot = dot (coords,coords);
		
		half2 uvG = uv - _PostSource_TexelSize.xy * _ChromaticAberration * coords * coordDot;
		half4 color = SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource,uv);
		#if SHADER_API_D3D9
			// Work around Cg's code generation bug for D3D9 pixel shaders :(
			color.g = color.g * 0.0001 + tex2D (_MainTex, UnityStereoScreenSpaceUVAdjust(uvG, _MainTex_ST)).g;
		#else
			color.g = SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource,uvG).g;
		#endif
		
		return color;
	}

	// squeezing into SM2.0 with 9 samples:
	static const int SmallDiscKernelSamples = 9;		
	static const half2 SmallDiscKernel[SmallDiscKernelSamples] =
	{
		half2(-0.926212,-0.40581),
		half2(-0.695914,0.457137),
		half2(-0.203345,0.820716),
		half2(0.96234,-0.194983),
		half2(0.473434,-0.480026),
		half2(0.519456,0.767022),
		half2(0.185461,-0.893124),
		half2(0.89642,0.412458),
		half2(-0.32194,-0.932615),
	};

	half4 fragComplex(v2f i) : SV_Target 
	{
		half2 coords = i.uv;
		half2 uv = i.uv;
		
		// corner heuristic
		coords = (coords - 0.5h) * 2.0h;		
		half coordDot = dot (coords,coords);

		half4 color = SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource,uv);
		half tangentialStrength = _ChromaticAberration * coordDot * coordDot;
		half maxOfs = clamp(max(_AxialAberration, tangentialStrength), _BlurDistance.x, _BlurDistance.y)*5;

		// we need a blurred sample tap for advanced aberration

		// NOTE: it's relatively important that input is HDR
		// and if you do have a proper HDR setup, lerping .rb might yield better results than .g
		// (see below)

		half4 blurredTap = color * 0.1h;
		for(int l=0; l < SmallDiscKernelSamples; l++)
		{
			half2 sampleUV = uv + SmallDiscKernel[l].xy * _PostSource_TexelSize * maxOfs;
			half3 tap = SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource,sampleUV);
			blurredTap.rgb += tap;
		}
		blurredTap.rgb /= (float)SmallDiscKernelSamples;

		// debug:
		//return blurredTap;

		half lumDiff = Luminance(abs(blurredTap.rgb-color.rgb));
		half isEdge = saturate(_Luminance * lumDiff);
		
		// debug #2:
		//return isEdge;

		color.rb = lerp(color.rb, blurredTap.rb, isEdge);
		
		return color;
	}

	ENDHLSL 
	
Subshader {

 // 0: box downsample
 Pass {
	  ZTest Always Cull Off ZWrite Off

      HLSLPROGRAM
      
      #pragma vertex vert
      #pragma fragment fragDs
      ENDHLSL
  }
// 1: simple chrom aberration
Pass {
	  ZTest Always Cull Off ZWrite Off

      HLSLPROGRAM
      
      #pragma vertex vert
      #pragma fragment frag
      ENDHLSL
  }
// 2: simulates more chromatic aberration effects
Pass {
	  ZTest Always Cull Off ZWrite Off

      HLSLPROGRAM
      
      #pragma vertex vert
      #pragma fragment fragComplex
      ENDHLSL
  }  
}

Fallback off
	
} // shader
