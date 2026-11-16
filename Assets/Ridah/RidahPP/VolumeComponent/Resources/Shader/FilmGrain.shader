// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Ridah/FilmGrain" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "" {}
	}
	
	// Shader code pasted into all further CGPROGRAM blocks
	HLSLINCLUDE
	
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
	#include "Ridah.hlsl"
	struct appdata
	{
		uint vertexID : SV_VertexID;
	};

	struct v2f {
		float4 vertex : SV_POSITION;
		float2 uv : TEXCOORD0;
	};
	
	TEXTURE2D_X(_MainTex);
	SAMPLER(sampler_MainTex);
	half4 _MainTex_ST;

	int type;
	
	TEXTURE2D_X(_PostSource);
	SAMPLER(sampler_PostSource);
	half4 _PostSource_ST;

	v2f Vert( appdata v ) 
	{
		v2f o;
		o.vertex = GetFullScreenTriangleVertexPosition(v.vertexID);
		o.uv = GetFullScreenTriangleTexCoord(v.vertexID);
		return o;
	} 
	
	half4 SimpleGrainFrag(v2f i) : SV_Target 
	{
		half2 uv = i.uv ;	//change
		
		half4 color = SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource,UnityStereoScreenSpaceUVAdjust(i.uv, _PostSource_ST));
		 float strength = 16.0;
    
		float x = (uv.x + 4.0 ) * (uv.y + 4.0 ) * (iTime * 10.0);
		float4 grain = F4(mod((mod(x, 13.0) + 1.0) * (mod(x, 123.0) + 1.0), 0.01)-0.005) * strength;
		if(type== 0)
		{
			grain = 1.0 - grain;
			return color * grain;
		}
		else
		{
			return color + grain;
		}
	}


	int customScale;
	float2 spreadGrainScale;
	float SpreadGrainRand(float2 uv, float t) {
    return fract(sin(dot(uv, float2(1225.6548, 321.8942))) * 4251.4865 + t);}
	half4 SpreadGrainFrag(v2f i) : SV_Target 
	{
		half2 ps = F2(1.0) / iResolution.xy;
		half2 uv = i.uv ;	//change 
		
		float scale = 50.0;
		 if (customScale==0)
		     scale *= spreadGrainScale.x * ps.x * 2.0;
		 
		 float2 _offset = (SpreadGrainRand(uv, iTime) - 0.5) * 2.0 * ps * scale;
		 
		 float3 noise = SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource,UnityStereoScreenSpaceUVAdjust(uv+_offset, _PostSource_ST)).rgb;
		 float4 _color = SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource,UnityStereoScreenSpaceUVAdjust(uv, _PostSource_ST));
		 
		 float amount = 0.5;
		 if (customScale==0)
		     amount = spreadGrainScale.y * ps.y;
		 
		 _color.rgb = mix(_color.rgb, noise, amount);
		 return _color;
	}
	

	float3 TVParmas;
	float TVrand(float2 co){
	 return fract(sin(dot(co.xy ,float2(12.9898,78.233))) * 43758.5453);}
	half4 TVFrag(v2f i) : SV_Target 
	{
		float2 uv = i.uv;
		float2 fragCoord = uv * iResolution;
		float alpha = 1.;
		
		float isBar = mod(fragCoord.y, 2.);
		fragCoord.x -= isBar * (TVrand(uv * iTime) * 14. - 7.);
		fragCoord.y -= isBar * (TVrand(uv + iTime) * 4. - 2.);
		alpha *= 1. - isBar / 20.;
		                        
		float2 noisedUV = fragCoord / iResolution.xy;
		
		// lens distortion
		float distortAmount = distance(F2(.5), noisedUV)*TVParmas.y;
		//noisedUV -= F2(.5);
		//noisedUV *= 1. + ((.5 - distortAmount) / .5) * -.17;
		//noisedUV += F2(.5);
		
		float4 fragColor = SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource,UnityStereoScreenSpaceUVAdjust(noisedUV, _PostSource_ST));
		
		// desaturate
		
		float r = fragColor.r;
		float g = fragColor.g;
		float b = fragColor.b;
		
		float luma = .3 * r + .6 * g + .1 * b;
		
		fragColor.r = r + TVParmas.x * (luma - r);
		fragColor.g = g + TVParmas.x * (luma - g);
		fragColor.b = b + TVParmas.x * (luma - b);
		fragColor = clamp(fragColor, 0., 1.);
		
		// hdr
		fragColor += fragColor - TVParmas.z;
		fragColor = clamp(fragColor, 0., 1.);
		
		// noise
		fragColor -= round(TVrand(uv * iTime) / 1.5) / 15.;
		fragColor = clamp(fragColor, 0., 1.);
		fragColor *= alpha;
		
		// vignette
		fragColor *= 1. - smoothstep(.4, 1., distortAmount) * .2;
		fragColor.rb *= 1. - smoothstep(.4, 1., distortAmount) * .2;
   		fragColor.rg *= .95;

		return fragColor;
    }
	ENDHLSL 
	
Subshader {
 Pass {
	  ZTest Always Cull Off ZWrite Off

      HLSLPROGRAM
      #pragma vertex Vert
      #pragma fragment SimpleGrainFrag
      ENDHLSL
  }
 Pass {
	  ZTest Always Cull Off ZWrite Off

      HLSLPROGRAM
      #pragma vertex Vert
      #pragma fragment SpreadGrainFrag
      ENDHLSL
  }
  Pass {
	  ZTest Always Cull Off ZWrite Off

      HLSLPROGRAM
      #pragma vertex Vert
      #pragma fragment TVFrag
      ENDHLSL
  } 
}

Fallback off
	
} // shader
