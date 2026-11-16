// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Ridah/BlendModesOverlay" {
	Properties {
		_Overlay ("Color", 2D) = "grey" {}
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
		float2 uv[2] : TEXCOORD0;
	};
	CBUFFER_START (Properties)
	TEXTURE2D_X(_Overlay);
	SAMPLER(sampler_Overlay);
	half4 _Overlay_ST;
	CBUFFER_END
	
	TEXTURE2D_X(_PostSource);
	SAMPLER(sampler_PostSource);
	half4 _PostSource_TexelSize;
	half4 _PostSource_ST;
	
	half _Intensity;
	half4 _UV_Transform = half4(1, 0, 0, 1);

	v2f vert( appdata v ) { 
		v2f o;
		o.pos = GetFullScreenTriangleVertexPosition(v.vertexID);
		v.texcoord = GetFullScreenTriangleTexCoord(v.vertexID);
		
		o.uv[0] = UnityStereoScreenSpaceUVAdjust(float2(
			dot(v.texcoord.xy, _UV_Transform.xy),
			dot(v.texcoord.xy, _UV_Transform.zw)
		), _PostSource_ST);
		
		#if UNITY_UV_STARTS_AT_TOP
		if(_PostSource_TexelSize.y<0.0)
			o.uv[0].y = 1.0-o.uv[0].y;
		#endif
		
		o.uv[1] = UnityStereoScreenSpaceUVAdjust(v.texcoord.xy, _PostSource_ST);
		return o;
	}
	
	half4 fragAddSub (v2f i) : SV_Target {
		half4 toAdd = SAMPLE_TEXTURE2D_X(_Overlay, sampler_Overlay,i.uv[0]) * _Intensity;
		return SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource, i.uv[1]) + toAdd;
	}

	half4 fragMultiply (v2f i) : SV_Target {
		half4 toBlend = SAMPLE_TEXTURE2D_X(_Overlay, sampler_Overlay,i.uv[0]) * _Intensity;
		return SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource, i.uv[1]) * toBlend;
	}	
			
	half4 fragScreen (v2f i) : SV_Target {
		half4 toBlend =  (SAMPLE_TEXTURE2D_X(_Overlay, sampler_Overlay,i.uv[0]) * _Intensity);
		return 1-(1-toBlend)*(1-(SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource, i.uv[1])));
	}

	half4 fragOverlay (v2f i) : SV_Target {
		half4 m = (SAMPLE_TEXTURE2D_X(_Overlay, sampler_Overlay,i.uv[0]));// * 255.0;
		half4 color = (SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource, i.uv[1]));//* 255.0;

		// overlay blend mode
		//color.rgb = (color.rgb/255.0) * (color.rgb + ((2*m.rgb)/( 255.0 )) * (255.0-color.rgb));
		//color.rgb /= 255.0; 
		 
		/*
if (Target > ½) R = 1 - (1-2x(Target-½)) x (1-Blend)
if (Target <= ½) R = (2xTarget) x Blend		
		*/
		
		float3 check = step(half3(0.5,0.5,0.5), color.rgb);
		float3 result = 0;
		
			result =  check * (half3(1,1,1) - ( (half3(1,1,1) - 2*(color.rgb-0.5)) *  (1-m.rgb))); 
			result += (1-check) * (2*color.rgb) * m.rgb;
		
		return half4(lerp(color.rgb, result.rgb, (_Intensity)), color.a);
	}
	
	half4 fragAlphaBlend (v2f i) : SV_Target {
		half4 toAdd =SAMPLE_TEXTURE2D_X(_Overlay, sampler_Overlay,i.uv[0]) ;
		return lerp(SAMPLE_TEXTURE2D_X(_PostSource, sampler_PostSource,  i.uv[1]), toAdd, toAdd.a * _Intensity);
	}	


	ENDHLSL
	
Subshader {
	  ZTest Always Cull Off ZWrite Off
      ColorMask RGB	  
  		  	
 Pass {    

      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment fragAddSub
      ENDHLSL
  }

 Pass {    

      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment fragScreen
      ENDHLSL
  }

 Pass {    

      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment fragMultiply
      ENDHLSL
  }  

 Pass {    

      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment fragOverlay
      ENDHLSL
  }  
  
 Pass {    

      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment fragAlphaBlend
      ENDHLSL
  }   
}

Fallback off
	
} // shader
